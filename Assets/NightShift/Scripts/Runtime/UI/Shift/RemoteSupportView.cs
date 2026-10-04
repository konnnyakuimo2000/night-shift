using System.Collections.Generic;
using NightShift.Controls;
using NightShift.Systems;
using NightShift.UI.Remote;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// Remote Support（社員PCのリモート操作）。ゲームは停止しない。
    /// 最小化・他Windowへの切替で中断、HelpDesk から再接続で再開（PC状態はチケットに保持）。
    /// このWindowがアクティブな間、キーボード入力はリモートPCへ送られる。
    /// </summary>
    public class RemoteSupportView : MonoBehaviour
    {
        public const string Id = "remote";
        ShiftDesktop _d;
        RetroWindow _w;
        RemoteSession _session;
        RemoteDesktop _desktop;
        RectTransform _viewport;
        GameObject _endedOverlay;
        RetroButton _report, _disconnect;
        Text _kbd;
        float _sessionStart;

        public bool HasKeyboard => _session != null && _session.Connected && _desktop != null && _endedOverlay == null;

        public static void Open(ShiftDesktop d, RemoteSession session)
        {
            if (!d.Require(Data.Features.RemoteSupport)) return;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "リモートサポート", Icon = "remote", Width = 880, Height = 640, Maximizable = true,
                StatusCells = 3, StatusWidths = new float[] { 220, 120 }, Position = new Vector2(200, 4)
            }, out bool created);
            var v = created ? w.gameObject.AddComponent<RemoteSupportView>() : w.GetComponent<RemoteSupportView>();
            if (created) { v._d = d; v._w = w; v.BuildFrame(); }
            v.Attach(session);
        }

        void BuildFrame()
        {
            var c = _w.Content;
            _disconnect = UIFactory.Button(c, "切断(X)", 0, 0, 100, 24, Disconnect, "exit");
            _report = UIFactory.Button(c, "対応完了を報告(P)", 104, 0, 150, 24, Report, "check");
            UIFactory.Button(c, "問い合わせを表示", 258, 0, 130, 24, () => { if (_session != null) HelpDeskView.Open(_d, _session.Ticket); }, "helpdesk");
            float kx = 400;
            if (_d.Unlocked(Data.Features.ServerManagement))
            {
                // PC に問題がないときは、サーバー側の障害として引き継ぐ（R9）
                UIFactory.Button(c, "サーバー側の障害として報告", 392, 0, 190, 24, ReportServerSide, "server");
                kx = 586;
            }
            _kbd = UIFactory.Label(c, "", kx, 0, 860 - kx, 24, 11, TextAnchor.MiddleRight);
            _kbd.color = RetroTheme.Shadow;

            var frame = UIFactory.Image(c, RetroTheme.Field, Color.black, "Viewport");
            frame.rectTransform.Stretch(0, 28, 0, 0);
            _viewport = UIFactory.Rect("Screen", frame.transform).Stretch(2, 2, 2, 2);

            _w.CanClose = () => { Disconnect(); return false; };
            _d.S.HelpDesk.SessionChanged += OnSessionChanged;
            _d.S.HelpDesk.Changed += OnTicketChanged;
            _w.ActiveChanged += _ => UpdateKeyboardLabel();
        }

        void OnDestroy()
        {
            if (_d == null || _d.S == null) return;
            _d.S.HelpDesk.SessionChanged -= OnSessionChanged;
            _d.S.HelpDesk.Changed -= OnTicketChanged;
        }

        void Attach(RemoteSession session)
        {
            if (session == null) return;
            if (_session == session && _desktop != null && _endedOverlay == null) return;
            _session = session;
            if (_desktop != null) Destroy(_desktop.gameObject);
            if (_endedOverlay != null) Destroy(_endedOverlay);
            _endedOverlay = null;
            _desktop = RemoteDesktop.Build(_viewport, _d.S, session.PC);
            _sessionStart = _d.S.Time.CurrentMinute;
            var t = session.Ticket;
            _w.SetTitle($"リモートサポート - {t.Employee.deviceId} ({t.Employee.fullName})");
            _w.SetStatus(0, $"{t.TicketId}  {t.Subject}");
            _w.SetStatus(1, $"接続先: {t.Employee.deviceId} ({_d.S.Employees.GetIp(t.Employee)})");
            _report.Interactable = true;
            _disconnect.Interactable = true;
            UpdateKeyboardLabel();
        }

        void OnSessionChanged(RemoteSession s)
        {
            if (s == null && _session != null && !_session.Connected) ShowEnded();
        }

        void OnTicketChanged(Ticket t)
        {
            if (_session != null && _session.Ticket == t && !t.IsOpen) ShowEnded();
        }

        void ShowEnded()
        {
            if (_endedOverlay != null) return;
            PopupMenu.CloseAll();
            var t = _session != null ? _session.Ticket : null;
            var dim = UIFactory.Solid(_viewport, new Color(0, 0, 0, 0.78f), "Ended");
            dim.rectTransform.Stretch();
            _endedOverlay = dim.gameObject;
            string msg = t == null ? "リモート セッションは終了しました。" :
                t.State == TicketState.Resolved ? $"リモート セッションは終了しました。\n\n{t.TicketId}: 完了" :
                t.State == TicketState.Failed ? $"リモート セッションは終了しました。\n\n{t.TicketId}: 未解決" :
                "リモート セッションは切断されました。\n\n「ITサポート受付管理」から再接続できます（PCの状態は保持されています）。";
            UIFactory.Label(dim.transform, msg, 14, TextAnchor.MiddleCenter, Color.white).rectTransform.Stretch(40, 40, 40, 40);
            _report.Interactable = false;
            _disconnect.Interactable = false;
            _w.SetStatus(2, "切断");
            UpdateKeyboardLabel();
        }

        void Disconnect()
        {
            if (_session != null && _session.Connected && _d.S.HelpDesk.ActiveSession == _session)
                _d.S.HelpDesk.Disconnect();
            _w.Closable = true;
            _w.CanClose = null;
            _w.Close();
        }

        void ReportServerSide()
        {
            if (_session == null || !_session.Ticket.IsOpen) return;
            _session.Ticket.PC.ReportKind = "SERVER";
            Report();
            if (_session != null && _session.Ticket.IsOpen) _session.Ticket.PC.ReportKind = "PC";
        }

        void Report()
        {
            if (_session == null || !_session.Ticket.IsOpen) return;
            var t = _session.Ticket;
            bool ok = _d.S.HelpDesk.Report(t, _d.Game.OperatorName);
            _w.SetStatus(0, ok ? "対応完了を報告しました。" : $"対応完了を報告しました。（{t.Employee.fullName} さんから返信あり）");
            if (!ok)
            {
                AudioFx.Ticket();
                _d.ShowStatus($"{t.TicketId}: {t.Employee.fullName} さんから返信がありました。");
            }
        }

        void UpdateKeyboardLabel()
        {
            if (_kbd == null) return;
            bool active = _d.Windows.Active == _w;
            _kbd.text = HasKeyboard
                ? (active ? $"キーボード: リモートPCへ送信中  ({Core.GameSettings.KeyLabel(Core.GameSettings.RemoteStart)}=スタート / {Core.GameSettings.KeyLabel(Core.GameSettings.RemoteRun)}=実行 / {Core.GameSettings.KeyLabel(Core.GameSettings.RemoteTaskManager)}=タスクマネージャー)" : "キーボード: ローカル（この画面をクリックでリモートへ）")
                : "";
        }

        public void HandleShortcuts(List<Shortcut> keys)
        {
            if (_desktop == null) return;
            _desktop.HandleShortcuts(keys);
        }

        void Update()
        {
            if (_session != null && _session.Connected && _endedOverlay == null)
            {
                int m = Mathf.FloorToInt(_d.S.Time.CurrentMinute - _sessionStart);
                _w.SetStatus(2, $"接続中 {m} 分");
            }
            if (Time.frameCount % 20 == 0) UpdateKeyboardLabel();
        }
    }
}
