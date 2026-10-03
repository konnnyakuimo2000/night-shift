using System.Collections.Generic;
using System.Text;
using NightShift.Core;
using NightShift.Data;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// ITサポート受付管理。
    /// TYPE A（相談）: 二択で回答。結果は「対応内容を送信しました」のみ。
    /// TYPE B（障害）: 症状のみ提示。リモート接続して自分で調査・操作する。
    /// </summary>
    public class HelpDeskView : MonoBehaviour
    {
        public const string Id = "helpdesk";
        ShiftDesktop _d;
        RetroWindow _w;
        TableView _table;
        Text _conversation;
        RectTransform _convContent;
        ScrollRect _convScroll;
        RectTransform _binaryPanel, _remotePanel;
        RetroToggle[] _options = new RetroToggle[2];
        RetroButton _send, _connect, _report;
        Text _remoteInfo;
        bool _dirty = true;
        Ticket _shown;
        int _shownMessages;

        public static void Open(ShiftDesktop d, Ticket focus)
        {
            if (!d.Require(Data.Features.HelpDesk)) return;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "ITサポート受付管理", Icon = "helpdesk", Width = 820, Height = 560,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 2, StatusWidths = new float[] { 140 }
            }, out bool created);
            var v = created ? w.gameObject.AddComponent<HelpDeskView>() : w.GetComponent<HelpDeskView>();
            if (created) { v._d = d; v._w = w; v.Build(); }
            if (focus != null) { v.Refresh(); v._table.SelectData(focus); }
        }

        void Build()
        {
            var c = _w.Content;
            var lg = UIFactory.Group(c, "受付一覧", 0, 0, 800, 170);
            _table = TableView.CreateStretch(lg, 0, 0, 0, 0,
                new TableColumn("受付番号", 72), new TableColumn("受付", 48), new TableColumn("依頼者", 96), new TableColumn("所属", 140),
                new TableColumn("件名", 250), new TableColumn("区分", 60), new TableColumn("状態", 70));
            _table.SelectionChanged += _ => ShowSelected();

            var mg = UIFactory.Group(c, "問い合わせ内容", 0, 176, 480, 274);
            _convScroll = UIFactory.Scroll(mg, out _convContent, true);
            ((RectTransform)_convScroll.transform).Stretch();
            _conversation = UIFactory.Label(_convContent, "", 12, TextAnchor.UpperLeft);
            _conversation.rectTransform.Stretch(6, 4, 6, 0);
            _conversation.verticalOverflow = VerticalWrapMode.Overflow;
            _conversation.font = RetroTheme.ReadingFont;
            _conversation.fontSize = 13;
            _conversation.lineSpacing = 1.1f;

            var ag = UIFactory.Group(c, "対応", 486, 176, 314, 274);
            // 二択
            _binaryPanel = UIFactory.Rect("Binary", ag).Stretch();
            UIFactory.Label(_binaryPanel, "回答を選択してください:", 0, 0, 290, 18);
            for (int i = 0; i < 2; i++)
            {
                var t = UIFactory.Check(_binaryPanel, "", false, null, true);
                t.GetComponent<RectTransform>().Place(0, 24 + i * 84, 294, 80);
                t.LabelText.alignment = TextAnchor.UpperLeft;
                t.LabelText.rectTransform.Stretch(18, 0, 0, 0);
                ((RectTransform)t.Mark.transform.parent).anchorMin = ((RectTransform)t.Mark.transform.parent).anchorMax = new Vector2(0, 1);
                ((RectTransform)t.Mark.transform.parent).pivot = new Vector2(0, 1);
                ((RectTransform)t.Mark.transform.parent).anchoredPosition = new Vector2(0, -1);
                _options[i] = t;
            }
            _options[0].Group = _options;
            _options[1].Group = _options;
            _send = UIFactory.Button(_binaryPanel, "回答を送信(S)", 0, 200, 294, 30, Send, "mail");

            // リモート
            _remotePanel = UIFactory.Rect("Remote", ag).Stretch();
            _remoteInfo = UIFactory.Label(_remotePanel, "", 12, TextAnchor.UpperLeft);
            _remoteInfo.rectTransform.Place(0, 0, 294, 80);
            _connect = UIFactory.Button(_remotePanel, "リモート接続(R)", 0, 92, 294, 32, Connect, "remote");
            _report = UIFactory.Button(_remotePanel, "対応完了を報告(P)", 0, 132, 294, 30, Report, "check");
            var note = UIFactory.Label(_remotePanel, "リモート接続中も業務時間は進みます。\n接続は中断（最小化・切断）しても再開できます。", 0, 172, 294, 40, 11, TextAnchor.UpperLeft);
            note.color = RetroTheme.Shadow;

            UIFactory.Button(c, "閉じる(C)", 690, 456, 110, 26, _w.RequestClose, "exit");

            _w.Accelerators[KeyCode.S] = Send;
            _w.Accelerators[KeyCode.R] = Connect;
            _w.Accelerators[KeyCode.P] = Report;
            _w.Accelerators[KeyCode.C] = _w.RequestClose;
            _w.Accelerators[KeyCode.UpArrow] = () => _table.MoveSelection(-1);
            _w.Accelerators[KeyCode.DownArrow] = () => _table.MoveSelection(1);
            _w.Accelerators[KeyCode.Alpha1] = () => { if (_binaryPanel.gameObject.activeSelf) Pick(0); };
            _w.Accelerators[KeyCode.Alpha2] = () => { if (_binaryPanel.gameObject.activeSelf) Pick(1); };

            _d.S.HelpDesk.Changed += OnChanged;
            _d.S.HelpDesk.SessionChanged += OnSession;
            Refresh();
            if (_table.Count > 0) _table.Select(0); else ShowSelected();
        }

        void Pick(int i)
        {
            _options[i].SetValue(true);
            _options[1 - i].SetValue(false);
        }

        void OnDestroy()
        {
            if (_d == null || _d.S == null) return;
            _d.S.HelpDesk.Changed -= OnChanged;
            _d.S.HelpDesk.SessionChanged -= OnSession;
        }

        void OnChanged(Ticket t) => _dirty = true;
        void OnSession(RemoteSession s) => _dirty = true;

        void Refresh()
        {
            var list = new List<Ticket>(_d.S.HelpDesk.Tickets);
            list.Reverse();
            _table.SetRows(list, t => new[]
            {
                t.TicketId, TimeManager.Format(t.CreatedAt), t.Employee.fullName, t.Employee.department, t.Subject,
                t.Type == HelpDeskType.Binary ? "相談" : "障害", t.StateLabel
            });
            _w.SetStatus(1, $"未対応 {_d.S.HelpDesk.OpenCount} 件");
            _dirty = false;
            if (_shown != null && (_shown.Messages.Count != _shownMessages)) ShowSelected();
            else UpdateButtons();
        }

        void ShowSelected()
        {
            var t = _table.Selected as Ticket;
            _shown = t;
            if (t == null)
            {
                _conversation.text = "問い合わせを選択してください。";
                _binaryPanel.gameObject.SetActive(false);
                _remotePanel.gameObject.SetActive(false);
                _shownMessages = 0;
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{t.TicketId}  {t.Subject}</b>");
            sb.AppendLine($"依頼者: {t.Employee.fullName} ({t.Employee.employeeId} / {t.Employee.department})");
            sb.AppendLine($"端末: {t.Employee.deviceId}");
            sb.AppendLine("──────────────────────────────");
            foreach (var m in t.Messages)
            {
                sb.AppendLine($"[{TimeManager.Format(m.Time)}] {m.From}:");
                sb.AppendLine(m.Text);
                sb.AppendLine();
            }
            _conversation.text = sb.ToString();
            _shownMessages = t.Messages.Count;
            _convContent.sizeDelta = new Vector2(0, _conversation.preferredHeight + 16);
            _convScroll.verticalNormalizedPosition = 0;

            bool binary = t.Type == HelpDeskType.Binary;
            _binaryPanel.gameObject.SetActive(binary);
            _remotePanel.gameObject.SetActive(!binary);
            if (binary)
            {
                for (int i = 0; i < 2; i++)
                {
                    _options[i].LabelText.text = $"{(char)('A' + i)}: {t.Options[i]}";
                    _options[i].SetValue(t.AnsweredIndex == i, false);
                    _options[i].Interactable = t.IsOpen;
                }
            }
            UpdateButtons();
        }

        void UpdateButtons()
        {
            var t = _shown;
            if (t == null) return;
            if (t.Type == HelpDeskType.Binary)
            {
                _send.Interactable = t.IsOpen;
            }
            else
            {
                var session = _d.S.HelpDesk.ActiveSession;
                bool connectedHere = session != null && session.Ticket == t && session.Connected;
                _remoteInfo.text = $"接続先端末: {t.Employee.deviceId}\nIPアドレス: {_d.S.Employees.GetIp(t.Employee)}\n状態: {(connectedHere ? "リモート接続中" : t.IsOpen ? "未接続" : t.StateLabel)}\n報告回数: {t.Reports} / {t.Definition.maxReports}";
                _connect.Interactable = t.IsOpen && _d.Unlocked(Data.Features.RemoteSupport);
                _connect.SetText(connectedHere ? "リモート画面を表示(R)" : "リモート接続(R)");
                _report.Interactable = t.IsOpen && t.State == TicketState.InProgress;
            }
        }

        void Send()
        {
            var t = _shown;
            if (t == null || t.Type != HelpDeskType.Binary || !t.IsOpen) return;
            int idx = _options[0].Value ? 0 : _options[1].Value ? 1 : -1;
            if (idx < 0) { AudioFx.Error(); _w.SetStatus(0, "回答を選択してください。"); return; }
            _d.S.HelpDesk.Answer(t, idx, _d.Game.OperatorName);
            _w.SetStatus(0, "対応内容を送信しました。");
            Refresh();
            ShowSelected();
        }

        void Connect()
        {
            var t = _shown;
            if (t == null || t.Type != HelpDeskType.Remote || !t.IsOpen) return;
            var session = _d.S.HelpDesk.Connect(t);
            if (session == null) return;
            AudioFx.Connect();
            RemoteSupportView.Open(_d, session);
            _w.SetStatus(0, $"{t.Employee.deviceId} にリモート接続しました。");
        }

        void Report()
        {
            var t = _shown;
            if (t == null || t.Type != HelpDeskType.Remote || t.State != TicketState.InProgress) return;
            _d.S.HelpDesk.Report(t, _d.Game.OperatorName);
            _w.SetStatus(0, "対応完了を報告しました。");
            Refresh();
            ShowSelected();
        }

        void Update()
        {
            if (_dirty && Time.frameCount % 5 == 0) Refresh();
        }
    }
}
