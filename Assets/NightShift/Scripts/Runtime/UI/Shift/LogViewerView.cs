using System.Collections.Generic;
using NightShift.Core;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>システムログの検索・参照。自動相関はしない（同じ接続元に気付くのはプレイヤー）。</summary>
    public class LogViewerView : MonoBehaviour
    {
        public const string Id = "logs";
        ShiftDesktop _d;
        RetroWindow _w;
        TableView _table;
        InputField _keyword;
        RetroDropdown _type;
        RetroToggle _follow;
        bool _dirty = true;
        List<LogEntry> _filtered = new List<LogEntry>();
        Text _detail;

        public static void Open(ShiftDesktop d, string keyword)
        {
            if (!d.Require(Data.Features.SystemLog)) return;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "システムログの検索・参照", Icon = "log", Width = 820, Height = 520,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 2, StatusWidths = new float[] { 180 }
            }, out bool created);
            var v = created ? w.gameObject.AddComponent<LogViewerView>() : w.GetComponent<LogViewerView>();
            if (created) { v._d = d; v._w = w; v.Build(); }
            if (keyword != null) { v._keyword.text = keyword; v.Apply(); }
        }

        void Build()
        {
            var c = _w.Content;
            var g = UIFactory.Group(c, "検索条件", 0, 0, 800, 66);
            UIFactory.Label(g, "キーワード(K):", 0, 4, 86, 20);
            _keyword = UIFactory.Input(g, 88, 4, 200);
            UIFactory.Label(g, "種別(T):", 300, 4, 56, 20);
            _type = RetroDropdown.Create(g, 356, 4, 110, new List<string> { "(すべて)" }, 0, _ => Apply());
            UIFactory.Button(g, "検索開始(F)", 480, 2, 100, 24, Apply, "search");
            UIFactory.Button(g, "条件クリア(R)", 584, 2, 100, 24, Clear);
            _follow = UIFactory.Check(g, "最新を表示", true, v => { if (v) _table.ScrollToBottom(); });
            _follow.GetComponent<RectTransform>().Place(694, 4, 100, 20);

            var lg = UIFactory.Group(c, "ログ", 0, 72, 800, 300);
            _table = TableView.CreateStretch(lg, 0, 0, 0, 0,
                new TableColumn("時刻", 64), new TableColumn("接続元", 112), new TableColumn("接続先", 100), new TableColumn("利用者", 150),
                new TableColumn("種別", 76), new TableColumn("結果", 70), new TableColumn("詳細", 170));
            _table.SelectionChanged += o => ShowDetail(o as LogEntry);
            _table.DoubleClicked += o => { if (o is LogEntry le) { _keyword.text = le.Source; Apply(); } };

            var dg = UIFactory.Group(c, "選択行", 0, 378, 800, 52);
            _detail = UIFactory.Label(dg, "", 12, TextAnchor.MiddleLeft);
            _detail.rectTransform.Stretch(0, 0, 330, 0);
            float bx = 470;
            if (_d.Unlocked(Data.Features.FirewallBlock)) { UIFactory.Button(dg, "接続元を遮断", bx, 4, 104, 24, BlockSelected, "lock"); bx += 108; }
            if (_d.Unlocked(Data.Features.Isolation)) { UIFactory.Button(dg, "端末を隔離", bx, 4, 104, 24, IsolateSelected, "pc"); bx += 108; }
            if (_d.Unlocked(Data.Features.Report)) UIFactory.Button(dg, "報告", bx, 4, 104, 24, ReportSelected, "warning");

            _w.Accelerators[KeyCode.F] = Apply;
            _w.Accelerators[KeyCode.Return] = Apply;
            _w.Accelerators[KeyCode.KeypadEnter] = Apply;
            _w.Accelerators[KeyCode.R] = Clear;
            _w.Accelerators[KeyCode.UpArrow] = () => _table.MoveSelection(-1);
            _w.Accelerators[KeyCode.DownArrow] = () => _table.MoveSelection(1);
            _w.Accelerators[KeyCode.F5] = Apply;
            _w.SetStatus(0, "行をダブルクリックすると、その接続元で絞り込みます。");
            _d.S.Logs.Added += OnAdded;
            Apply();
        }

        void OnDestroy()
        {
            if (_d != null && _d.S != null) _d.S.Logs.Added -= OnAdded;
        }

        void OnAdded(LogEntry e) => _dirty = true;

        void Clear()
        {
            _keyword.text = "";
            _type.Set(0);
            Apply();
        }

        bool Match(LogEntry e, string kw, string type)
        {
            if (type != null && e.EventType != type) return false;
            if (string.IsNullOrEmpty(kw)) return true;
            kw = kw.Trim().ToUpperInvariant();
            return (e.Source ?? "").ToUpperInvariant().Contains(kw) || (e.Destination ?? "").ToUpperInvariant().Contains(kw)
                || (e.User ?? "").ToUpperInvariant().Contains(kw) || (e.EventType ?? "").ToUpperInvariant().Contains(kw)
                || (e.Result ?? "").ToUpperInvariant().Contains(kw) || (e.Detail ?? "").ToUpperInvariant().Contains(kw);
        }

        void Apply()
        {
            // 種別の候補を更新
            var types = new List<string> { "(すべて)" };
            types.AddRange(_d.S.Logs.EventTypes());
            string current = _type.Index > 0 ? _type.Value : null;
            int idx = current != null ? Mathf.Max(0, types.IndexOf(current)) : 0;
            _type.SetOptions(types, idx);

            string type = _type.Index > 0 ? _type.Value : null;
            string kw = _keyword.text;
            _filtered.Clear();
            foreach (var e in _d.S.Logs.Entries) if (Match(e, kw, type)) _filtered.Add(e);
            bool atBottom = _table.IsAtBottom;
            _table.SetRows(_filtered, e => new[] { TimeManager.FormatWithSeconds(e.Time), e.Source, e.Destination, e.User, e.EventType, e.Result, DetailText(e) });
            if (_follow.Value && atBottom) _table.ScrollToBottom();
            _w.SetStatus(1, $"表示 {_filtered.Count} / 全 {_d.S.Logs.Entries.Count} 件");
            _dirty = false;
        }

        LogEntry SelectedEntry => _table.Selected as LogEntry;

        /// <summary>書き換えられた（V2 GARBLE）サーバーのログは、意味のない文字に化けて読めない</summary>
        string DetailText(LogEntry e)
        {
            var st = _d.S.Servers.Find(e.Destination) ?? _d.S.Servers.Find(e.Source);
            return st != null && st.Garbled ? HorrorFx.Garble(e.Detail, e.Sequence) : e.Detail;
        }

        void BlockSelected()
        {
            var e = SelectedEntry;
            if (e == null) { AudioFx.Error(); return; }
            _w.SetStatus(0, _d.S.Security.Block(e.Source));
        }

        /// <summary>選択行の接続元IPから、社員の登録端末を探して隔離する</summary>
        void IsolateSelected()
        {
            var e = SelectedEntry;
            Data.EmployeeData emp = null;
            if (e != null) foreach (var x in _d.S.Employees.All) if (_d.S.Employees.GetIp(x) == e.Source) emp = x;
            if (emp == null) { AudioFx.Error(); _w.SetStatus(0, "選択行の接続元は社員の端末ではありません。"); return; }
            _w.SetStatus(0, _d.S.Security.Isolate(emp.deviceId));
        }

        void ReportSelected()
        {
            var e = SelectedEntry;
            if (e == null) { AudioFx.Error(); return; }
            bool suspicious = _d.S.Security.IsHostile(e.Source) || IsInfectedSource(e.Source) || e.Result == "SUSPICIOUS";
            _w.SetStatus(0, _d.S.Security.Report($"不審なログ（{e.Source} / {e.EventType} {e.Result}）", suspicious, e.Source));
        }

        bool IsInfectedSource(string ip)
        {
            foreach (var x in _d.S.Employees.All) if (_d.S.Employees.GetIp(x) == ip && _d.S.Security.IsInfected(x.deviceId)) return true;
            return false;
        }

        void ShowDetail(LogEntry e)
        {
            _d.SelectLog(e);
            _detail.text = e == null ? "" : $"[{TimeManager.FormatWithSeconds(e.Time)}] {e.Source} → {e.Destination}  利用者: {e.User}  種別: {e.EventType}  結果: {e.Result}\n{e.Detail}";
        }

        void Update()
        {
            if (_dirty && Time.frameCount % 20 == 0) Apply();
        }
    }
}
