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
        RetroButton _tabPending, _tabProcessed, _allow, _block, _fwBtn, _reportBtn;
        Text _hint;
        readonly Dictionary<string, Text> _fields = new Dictionary<string, Text>();
        bool _dirty = true;

        public static void Open(ShiftDesktop d, EventInstance focus)
        {
            if (!d.Require(Data.Features.ConnectionMonitor)) return;
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
            var mapBtn = UIFactory.Button(c, "ネットワークマップ(M)", 400, 0, 150, 24, () => NetworkMonitorView.Open(_d), "globe");
            var logBtn = UIFactory.Button(c, "ログを表示(L)", 556, 0, 120, 24, () => LogViewerView.Open(_d, null), "log");
            mapBtn.Interactable = _d.Unlocked(Data.Features.NetworkMap);
            logBtn.Interactable = _d.Unlocked(Data.Features.SystemLog);

            var lg = UIFactory.Group(c, "接続要求一覧", 0, 30, 780, 200);
            _table = TableView.CreateStretch(lg, 0, 0, 0, 0,
                new TableColumn("受付", 48), new TableColumn("要求番号", 74), new TableColumn("利用者", 110), new TableColumn("端末", 104),
                new TableColumn("接続元", 140), new TableColumn("接続先", 96), new TableColumn("利用システム", 122), new TableColumn("状態", 56));
            _table.SelectionChanged += _ => RefreshDetail();

            var dg = UIFactory.Group(c, "要求内容", 0, 236, 560, 210);
            float y = 0;
            void F(string key, string label, float x, float w)
            {
                UIFactory.Label(dg, label, x, y, 74, 20);
                _fields[key] = UIFactory.ValueBox(dg, x + 76, y, w, 20, "");
            }
            F("id", "要求番号:", 0, 160); F("time", "受付時刻:", 270, 180); y += 26;
            F("user", "利用者:", 0, 160); F("emp", "社員番号:", 270, 180); y += 26;
            F("device", "端末:", 0, 160); F("src", "接続元:", 270, 180); y += 26;
            F("dest", "接続先:", 0, 160); F("access", "利用システム:", 270, 180); y += 26;
            F("perm", "必要な権限:", 0, 160); F("state", "状態:", 270, 180); y += 26;
            F("decided", "処理時刻:", 0, 160); y += 30;
            _hint = UIFactory.Label(dg, DefaultHint, 0, y, 540, 36, 11, TextAnchor.UpperLeft);
            _hint.color = RetroTheme.Shadow;

            var ag = UIFactory.Group(c, "処理", 566, 236, 214, 196);
            _allow = UIFactory.Button(ag, $"接続を許可 [{Core.GameSettings.KeyLabel(Core.GameSettings.Allow)}]", 0, 0, 196, 32, () => Decide(PlayerAction.Allow), "check");
            _block = UIFactory.Button(ag, $"接続を拒否 [{Core.GameSettings.KeyLabel(Core.GameSettings.Block)}]", 0, 38, 196, 32, () => Decide(PlayerAction.Block), "lock");
            UIFactory.Separator(ag, 0, 80, 196);
            UIFactory.Button(ag, $"社員情報を参照 [{Core.GameSettings.KeyLabel(Core.GameSettings.Lookup)}]", 0, 90, 196, 26, LookupEmployee, "people");
            UIFactory.Button(ag, "閉じる(C)", 0, 122, 196, 26, _w.RequestClose, "exit");
            if (_d.Unlocked(Data.Features.FirewallBlock))
                _fwBtn = UIFactory.Button(ag, "接続元を遮断", 0, 154, 96, 24, BlockSource, "lock");
            if (_d.Unlocked(Data.Features.Report))
                _reportBtn = UIFactory.Button(ag, "報告", 100, 154, 96, 24, ReportSelected, "warning");

            if (_d.Unlocked(Data.Features.NetworkMap)) _w.Accelerators[KeyCode.M] = () => NetworkMonitorView.Open(_d);
            if (_d.Unlocked(Data.Features.SystemLog)) _w.Accelerators[KeyCode.L] = () => LogViewerView.Open(_d, null);
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
                case EventState.Allowed: return e.AutoDecided ? "自動許可" : "許可";
                case EventState.Blocked: return e.AutoDecided ? "自動拒否" : "拒否";
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
                TimeManager.Format(e.GeneratedTime), e.RequestId, e.DisplayUser, DeviceText(e), e.SourceLabel, e.DestinationLabel, e.AccessType, StateLabel(e)
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
            if (e != _d.SelectedRequest) _d.SelectRequest(e);
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
            Set("device", DeviceText(e));
            Set("src", e.SourceLabel);
            Set("dest", e.Destination != null ? $"{e.Destination.nodeId} ({e.Destination.ipAddress})" : "-");
            Set("access", e.AccessType);
            Set("perm", RequiredPermissionText(e));
            Set("state", StateLabel(e));
            Set("decided", e.State == EventState.Pending ? "-" : TimeManager.Format(e.DecidedTime > 0 ? e.DecidedTime : e.GeneratedTime + e.ResponseTime));
            bool pending = e.State == EventState.Pending;
            _allow.Interactable = _block.Interactable = pending;
            _hint.text = string.IsNullOrEmpty(e.Note) ? (e.AutoDecided ? "※ この要求は自動的に処理されました。" : DefaultHint) : "備考: " + e.Note;
            _hint.color = string.IsNullOrEmpty(e.Note) ? RetroTheme.Shadow : RetroTheme.Text;
            if (_fwBtn != null) _fwBtn.Interactable = !_d.S.Security.IsBlocked(e.SourceIp);
            if (_reportBtn != null) _reportBtn.Interactable = true;
            CheckClues(e);
        }

        /// <summary>前任者の手がかり（E-0004 の接続元・攻撃AIの通信元）</summary>
        void CheckClues(EventInstance e)
        {
            if (e.DisplayEmployeeId == "E-0004" && _d.S.Day.dayNumber >= 12)
                _d.OfferClue("C2", "退職者アカウント E-0004 の接続元",
                    $"無効にされないまま残っていた退職者アカウント E-0004 からの接続。\n接続元は {e.SourceIp}。この範囲は覚えておいたほうがよさそうだ。");
            if (e.Owner is ThreatScenarioRun run && run.Definition.scenarioId.StartsWith("T14"))
                _d.OfferClue("C4", "攻撃AIの通信元",
                    $"拒否するたびに経路を変えてくる攻撃の接続元 {e.SourceIp}。\nE-0004 が使っていた範囲と、同じ並びだ。");
        }

        const string DefaultHint ="※ 利用者・端末・権限は「社員情報の検索・参照」で確認してください。";

        void BlockSource()
        {
            if (!(_table.Selected is EventInstance e)) { AudioFx.Error(); return; }
            _w.SetStatus(0, _d.S.Security.Block(e.SourceIp));
            _dirty = true;
        }

        void ReportSelected()
        {
            if (!(_table.Selected is EventInstance e)) { AudioFx.Error(); return; }
            _w.SetStatus(0, _d.S.Security.Report($"不審な接続要求 {e.RequestId}（{e.SourceIp} / {e.DisplayUser}）", e.IsMalicious, e.SourceIp));
            if (_reportBtn != null) _reportBtn.Interactable = false;
        }

        /// <summary>
        /// 「必要な権限」欄。DAY3 だけ表示し、DAY4 からは「社内サーバー構成資料」で調べさせる。
        /// </summary>
        string RequiredPermissionText(EventInstance e)
        {
            if (_d.Unlocked(Data.Features.PermissionHint))
            {
                var acc = e.Destination != null ? e.Destination.FindAccess(e.AccessType) : null;
                if (acc == null || string.IsNullOrEmpty(acc.requiredPermission)) return "不要";
                foreach (var p in _d.Game.Database.permissions)
                    if (p != null && p.permissionId == acc.requiredPermission) return p.displayName;
                return acc.requiredPermission;
            }
            if (_d.Unlocked(Data.Features.ServerGuide)) return "（資料で確認）";
            return "—";
        }

        /// <summary>端末の確認は DAY2 から。それまでは端末名を表示しない（端末で判断させない）。</summary>
        string DeviceText(EventInstance e) => _d.Unlocked(Data.Features.DeviceCheck) ? e.Device : "—";

        void Decide(PlayerAction action)
        {
            if (!(_table.Selected is EventInstance e) || e.State != EventState.Pending) { AudioFx.Error(); return; }
            int idx = _table.SelectedIndex;
            _d.S.Requests.Decide(e, action);
            _w.SetStatus(0, $"{e.RequestId} の要求を処理しました。");
            Refresh();
            if (_table.Count > 0) _table.Select(Mathf.Min(idx, _table.Count - 1));
        }

        /// <summary>社員情報の画面を開くだけ（検索はプレイヤー自身が行う。答えの行をいきなり出さない）</summary>
        void LookupEmployee() => EmployeeDirectoryView.Open(_d);

        void Update()
        {
            // キー設定で割り当てた 許可／拒否／社員参照（このWindowがアクティブで、メインモニターを見ていて、
            // 一時停止・確認ダイアログ・ブルースクリーン等の演出中でない時のみ）
            if (_d.Windows.Active == _w && !_d.InputBlocked && !Controls.ShortcutInput.Capturing && !Controls.ShortcutInput.TextFieldFocused && !PopupMenu.IsOpen)
            {
                if (Core.GameSettings.PressedButton(Core.GameSettings.Allow)) Decide(PlayerAction.Allow);
                else if (Core.GameSettings.PressedButton(Core.GameSettings.Block)) Decide(PlayerAction.Block);
                else if (Core.GameSettings.PressedButton(Core.GameSettings.Lookup)) LookupEmployee();
            }
            if (_dirty && Time.frameCount % 5 == 0) Refresh();
            if (_d.S.Phase == ShiftPhase.Grace)
                _w.SetStatus(0, _d.S.Day.requestsExpire
                    ? $"業務終了 — 保留中の要求を処理してください（残り {Mathf.CeilToInt(_d.S.GraceRemaining)} 秒）"
                    : "業務終了 — 保留中の要求をすべて処理すると、本日の業務が終わります。");
        }
    }
}
