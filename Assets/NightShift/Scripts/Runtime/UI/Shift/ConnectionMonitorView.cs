using System.Collections.Generic;
using NightShift.Core;
using NightShift.Events;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// ネットワーク接続監視（Connection Request の審査）。
    /// 判断結果に「正解/不正解」は出さず、「要求を処理しました」のみ表示する。危険項目の自動強調もしない。
    /// </summary>
    public class ConnectionMonitorView : MonoBehaviour
    {
        public const string Id = "connections";
        ShiftDesktop _d;
        RetroWindow _w;
        TableView _table;
        bool _showProcessed;
        RetroButton _tabPending, _tabProcessed, _allow, _block;
        readonly Dictionary<string, Text> _fields = new Dictionary<string, Text>();
        bool _dirty = true;

        public static void Open(ShiftDesktop d, EventInstance focus)
        {
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "ネットワーク接続監視 - 接続要求", Icon = "network", Width = 800, Height = 540,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 3, StatusWidths = new float[] { 110, 110 }
            }, out bool created);
            var v = created ? w.gameObject.AddComponent<ConnectionMonitorView>() : w.GetComponent<ConnectionMonitorView>();
            if (created)
            {
                v._d = d; v._w = w;
                v.Build();
            }
            if (focus != null) v.FocusOn(focus);
        }

        void Build()
        {
            var c = _w.Content;
            // ツールバー
            _tabPending = UIFactory.Button(c, "保留中の要求", 0, 0, 130, 24, () => SetTab(false), "network");
            _tabProcessed = UIFactory.Button(c, "処理済みの要求", 132, 0, 130, 24, () => SetTab(true), "doc");
            UIFactory.Button(c, "ネットワークマップ(M)", 400, 0, 150, 24, () => NetworkMonitorView.Open(_d), "globe");
            UIFactory.Button(c, "ログを表示(L)", 556, 0, 120, 24, () => LogViewerView.Open(_d, null), "log");

            var lg = UIFactory.Group(c, "接続要求一覧", 0, 30, 780, 200);
            _table = TableView.CreateStretch(lg, 0, 0, 0, 0,
                new TableColumn("受付", 48), new TableColumn("要求番号", 74), new TableColumn("利用者", 110), new TableColumn("端末", 104),
                new TableColumn("接続元", 160), new TableColumn("接続先", 96), new TableColumn("接続種別", 102), new TableColumn("状態", 56));
            _table.SelectionChanged += _ => RefreshDetail();

            var dg = UIFactory.Group(c, "要求内容", 0, 236, 560, 196);
            float y = 0;
            void F(string key, string label, float x, float w)
            {
                UIFactory.Label(dg, label, x, y, 74, 20);
                _fields[key] = UIFactory.ValueBox(dg, x + 76, y, w, 20, "");
            }
            F("id", "要求番号:", 0, 160); F("time", "受付時刻:", 270, 180); y += 26;
            F("user", "利用者:", 0, 160); F("emp", "社員番号:", 270, 180); y += 26;
            F("device", "端末:", 0, 160); F("src", "接続元:", 270, 180); y += 26;
            F("dest", "接続先:", 0, 160); F("access", "接続種別:", 270, 180); y += 26;
            F("state", "状態:", 0, 160); F("decided", "処理時刻:", 270, 180); y += 30;
            var hint = UIFactory.Label(dg, "※ 利用者・端末・権限は「社員情報の検索・参照」で確認してください。", 0, y, 540, 18, 11);
            hint.color = RetroTheme.Shadow;

            var ag = UIFactory.Group(c, "処理", 566, 236, 214, 196);
            _allow = UIFactory.Button(ag, "接続を許可(A)", 0, 0, 196, 32, () => Decide(PlayerAction.Allow), "check");
            _block = UIFactory.Button(ag, "接続を拒否(B)", 0, 38, 196, 32, () => Decide(PlayerAction.Block), "lock");
            UIFactory.Separator(ag, 0, 80, 196);
            UIFactory.Button(ag, "社員情報を参照(R)", 0, 90, 196, 26, LookupEmployee, "people");
            UIFactory.Button(ag, "閉じる(C)", 0, 122, 196, 26, _w.RequestClose, "exit");

            _w.Accelerators[KeyCode.A] = () => Decide(PlayerAction.Allow);
            _w.Accelerators[KeyCode.B] = () => Decide(PlayerAction.Block);
            _w.Accelerators[KeyCode.R] = LookupEmployee;
            _w.Accelerators[KeyCode.M] = () => NetworkMonitorView.Open(_d);
            _w.Accelerators[KeyCode.L] = () => LogViewerView.Open(_d, null);
            _w.Accelerators[KeyCode.C] = _w.RequestClose;
            _w.Accelerators[KeyCode.UpArrow] = () => _table.MoveSelection(-1);
            _w.Accelerators[KeyCode.DownArrow] = () => _table.MoveSelection(1);

            _d.S.Requests.Created += OnChanged;
            _d.S.Requests.Resolved += OnChanged;
            SetTab(false);
        }

        void OnDestroy()
        {
            if (_d == null || _d.S == null) return;
            _d.S.Requests.Created -= OnChanged;
            _d.S.Requests.Resolved -= OnChanged;
        }

        void OnChanged(EventInstance e) => _dirty = true;

        void SetTab(bool processed)
        {
            _showProcessed = processed;
            _tabPending.SetLatched(!processed);
            _tabProcessed.SetLatched(processed);
            _table.ClearSelection();
            _dirty = true;
            Refresh();
            if (_table.Count > 0) _table.Select(0);
        }

        void FocusOn(EventInstance e)
        {
            bool processed = e.State != EventState.Pending;
            if (processed != _showProcessed) SetTab(processed);
            Refresh();
            _table.SelectData(e);
        }

        static string StateLabel(EventInstance e)
        {
            switch (e.State)
            {
                case EventState.Pending: return "保留";
                case EventState.Allowed: return "許可";
                case EventState.Blocked: return "拒否";
                case EventState.Expired: return "期限切れ";
                default: return "-";
            }
        }

        void Refresh()
        {
            var s = _d.S.Requests;
            var list = new List<EventInstance>(_showProcessed ? s.Processed : s.Pending);
            if (_showProcessed) list.Reverse();
            _table.SetRows(list, e => new[]
            {
                TimeManager.Format(e.GeneratedTime), e.RequestId, e.DisplayUser, e.Device, e.SourceLabel, e.DestinationLabel, e.AccessType, StateLabel(e)
            });
            if (_table.Selected == null && list.Count > 0 && !_showProcessed) _table.Select(0);
            _tabPending.SetText($"保留中の要求 ({s.Pending.Count})");
            _w.SetStatus(1, $"保留: {s.Pending.Count} 件");
            _w.SetStatus(2, $"処理済: {s.Processed.Count} 件");
            RefreshDetail();
            _dirty = false;
        }

        void RefreshDetail()
        {
            var e = _table.Selected as EventInstance;
            void Set(string k, string v) => _fields[k].text = v;
            if (e == null)
            {
                foreach (var f in _fields.Values) f.text = "";
                _allow.Interactable = _block.Interactable = false;
                return;
            }
            Set("id", e.RequestId);
            Set("time", TimeManager.Format(e.GeneratedTime));
            Set("user", e.DisplayUser);
            Set("emp", string.IsNullOrEmpty(e.DisplayEmployeeId) ? "-" : e.DisplayEmployeeId);
            Set("device", e.Device);
            Set("src", e.SourceLabel);
            Set("dest", e.Destination != null ? $"{e.Destination.nodeId} ({e.Destination.ipAddress})" : "-");
            Set("access", e.AccessType);
            Set("state", StateLabel(e));
            Set("decided", e.State == EventState.Pending ? "-" : TimeManager.Format(e.DecidedTime > 0 ? e.DecidedTime : e.GeneratedTime + e.ResponseTime));
            bool pending = e.State == EventState.Pending;
            _allow.Interactable = _block.Interactable = pending;
        }

        void Decide(PlayerAction action)
        {
            if (!(_table.Selected is EventInstance e) || e.State != EventState.Pending) { AudioFx.Error(); return; }
            int idx = _table.SelectedIndex;
            _d.S.Requests.Decide(e, action);
            _w.SetStatus(0, $"{e.RequestId} の要求を処理しました。");
            Refresh();
            if (_table.Count > 0) _table.Select(Mathf.Min(idx, _table.Count - 1));
        }

        void LookupEmployee()
        {
            var e = _table.Selected as EventInstance;
            if (e == null) { EmployeeDirectoryView.Open(_d); return; }
            EmployeeDirectoryView.OpenWith(_d, e.DisplayEmployeeId, e.Device);
        }

        void Update()
        {
            if (_dirty && Time.frameCount % 5 == 0) Refresh();
            if (_d.S.Phase == ShiftPhase.Grace)
                _w.SetStatus(0, $"業務終了 — 保留中の要求を処理してください（残り {Mathf.CeilToInt(_d.S.GraceRemaining)} 秒）");
        }
    }
}
