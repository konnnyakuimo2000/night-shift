using System.Collections.Generic;
using System.Text;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// サブモニターのアプリと、それを使えるようにする商品（購入の権利）の対応。
    /// 1つのアプリに複数の権利がある場合（ログ解析＋相関、端末管理の診断＋隔離）、持っている権利のタブだけが使える。
    /// 販売を終えた商品（遮断コンソール・メール差出人チェッカー）は業務システムに統合済みで、アプリはない。所有の記録は残す。
    /// </summary>
    public static class ToolApps
    {
        public class App
        {
            public string Id;        // 起動に使う ID（代表の商品 ID）
            public string Name;      // デスクトップ・スタートメニュー・AI のプラグイン一覧で使う名前
            public string Icon;
            public string[] Rights;  // この中のどれかを持っていれば、アプリが出る
        }

        public static readonly App[] All =
        {
            new App { Id = Items.ProfilePanel, Name = "社員情報パネル", Icon = "people", Rights = new[] { Items.ProfilePanel } },
            new App { Id = Items.AutoReview, Name = "自動審査ツール", Icon = "check", Rights = new[] { Items.AutoReview } },
            new App { Id = Items.LogAnalyzer, Name = "ログ解析", Icon = "log", Rights = new[] { Items.LogAnalyzer, Items.Correlator } },
            new App { Id = Items.RemoteKit, Name = "端末管理", Icon = "remote", Rights = new[] { Items.RemoteKit, Items.Isolator } },
            new App { Id = Items.ServerMonitor, Name = "サーバー監視ダッシュボード", Icon = "server", Rights = new[] { Items.ServerMonitor } },
            new App { Id = Items.Vaccine, Name = "VACCINE 99", Icon = "shield", Rights = new[] { Items.Vaccine } },
            new App { Id = Items.SocAi, Name = "SOC-AI", Icon = "debug", Rights = new[] { Items.SocAi } },
            new App { Id = Items.GambleTerminal, Name = "NIGHT CLUB", Icon = "globe", Rights = new[] { Items.GambleTerminal } },
            new App { Id = Items.FxTerminal, Name = "MIDNIGHT FX", Icon = "network", Rights = new[] { Items.FxTerminal } },
            new App { Id = Items.SignalDesk, Name = "SIGNAL DESK", Icon = "network", Rights = new[] { Items.SignalDesk } },
        };

        /// <summary>販売を終えて、業務システムに統合した商品（アプリはない。所有の記録は残す）</summary>
        public static readonly string[] Retired = { Items.FwConsole, Items.MailInspector };

        public static bool IsRetired(string itemId) => System.Array.IndexOf(Retired, itemId) >= 0;

        /// <summary>商品 ID（古いデスクトップのアイコンなど）から、そのアプリ</summary>
        public static App ForItem(string itemId)
        {
            foreach (var a in All) if (System.Array.IndexOf(a.Rights, itemId) >= 0) return a;
            return null;
        }

        /// <summary>アプリの権利をどれか持っている（デスクトップに出す）</summary>
        public static bool Owns(App a)
        {
            foreach (var r in a.Rights) if (CareerRecord.Current.Owns(r)) return true;
            return false;
        }

        /// <summary>アプリを開ける（権利が有効で、サブモニターがある）</summary>
        public static bool Usable(ShiftDesktop d, App a)
        {
            foreach (var r in a.Rights) if (d.HasTool(r)) return true;
            return false;
        }

        /// <summary>有効なアプリの名前（AI のプラグイン一覧。遊びのアプリは含めない）</summary>
        public static List<string> ActiveWorkAppNames()
        {
            var list = new List<string>();
            foreach (var a in All)
            {
                if (a.Id == Items.GambleTerminal || a.Id == Items.FxTerminal || a.Id == Items.SignalDesk || a.Id == Items.SocAi) continue;
                foreach (var r in a.Rights) if (CareerRecord.Current.ToolActive(r)) { list.Add(a.Name); break; }
            }
            if (CareerRecord.Current.ToolActive(Items.VaccineServer)) list.Add("VACCINE 99 サーバー検査ライセンス");
            return list;
        }
    }

    /// <summary>タブの中身（ToolWindow の部品）。ホストのウィンドウ・デスクトップを使う</summary>
    public abstract class ToolPanel
    {
        public ToolWindow Host;
        protected ShiftDesktop D => Host.Desk;
        protected RetroWindow W => Host.Win;
        protected ShiftSession S => D.S;
        public abstract void Build(RectTransform p);
        public abstract void Refresh();
        public virtual void OnClosed() { }
        protected static void Show(GameObject empty, GameObject body, bool hasData)
        {
            if (empty) empty.SetActive(!hasData);
            if (body) body.SetActive(hasData);
        }
        protected static string Locked => "<color=#8890A0>（まだ使えません）</color>";
    }

    /// <summary>タブで機能を切り替えるアプリ。タブごとに権利を確かめ、ないタブは理由を出す</summary>
    public abstract class TabbedTool : ToolWindow
    {
        protected class Tab
        {
            public string Label;
            public string Right;       // このタブに必要な商品
            public ToolPanel Panel;
            public RectTransform Root, Locked;
            public FlatButton Button;
            public bool Built;
        }

        protected readonly List<Tab> Tabs = new List<Tab>();
        protected int Current = -1;
        RectTransform _area;

        protected abstract string Title { get; }
        protected abstract string Lead { get; }
        protected abstract string IconName { get; }
        protected abstract Color Accent { get; }
        protected abstract void DefineTabs();

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, Title, Lead, IconName, Accent);
            DefineTabs();
            var bar = UIFactory.Rect("Tabs", p);
            bar.StretchH(y, 30, ToolStyle.Pad, ToolStyle.Pad);
            for (int i = 0; i < Tabs.Count; i++)
            {
                int idx = i;
                var t = Tabs[i];
                t.Button = FlatButton.Create(bar, t.Label, () => Select(idx), Accent, false);
                ToolStyle.Cell(t.Button.Rect, i, Tabs.Count, 0, 28);
                t.Button.Label.fontSize = 13;
            }
            _area = UIFactory.Rect("TabArea", p).Stretch(0, y + 36, 0, 0);
            int first = Tabs.FindIndex(t => D.HasTool(t.Right));
            Select(first < 0 ? 0 : first);
        }

        /// <summary>タブを開く（起動時に指定する: 例 相関タイムラインのアイコンから開いたら相関タブ）</summary>
        public void Select(int index)
        {
            if (index < 0 || index >= Tabs.Count) return;
            Current = index;
            for (int i = 0; i < Tabs.Count; i++)
            {
                var t = Tabs[i];
                bool on = i == index;
                t.Button.SetLatched(on);
                bool usable = D.HasTool(t.Right);
                if (on && usable && !t.Built)
                {
                    t.Root = UIFactory.Rect("Tab" + i, _area).Stretch();
                    t.Panel.Host = this;
                    t.Panel.Build(t.Root);
                    t.Built = true;
                }
                if (on && !usable && t.Locked == null)
                {
                    t.Locked = UIFactory.Rect("Locked" + i, _area).Stretch();
                    var it = D.Game.Database.FindItem(t.Right);
                    string name = it != null ? it.displayName : t.Right;
                    string when = it != null && it.unlockDay > D.S.Day.dayNumber ? $"（DAY{it.unlockDay} から社内購買システムで購入できます）" : "（社内購買システムで購入できます）";
                    string reason = CareerRecord.Current.Owns(t.Right) ? "このツールは無効になっています（システム設定で有効にできます）" : $"この機能には「{name}」が必要です{when}";
                    ToolStyle.EmptyState(t.Locked, "この機能はまだ使えません", reason);
                }
                if (t.Root) t.Root.gameObject.SetActive(on && usable);
                if (t.Locked) t.Locked.gameObject.SetActive(on && !usable);
            }
            Refresh();
        }

        protected override void Refresh()
        {
            if (Current < 0 || Current >= Tabs.Count) return;
            var t = Tabs[Current];
            if (t.Built && D.HasTool(t.Right)) t.Panel.Refresh();
        }

        protected override void OnClosed()
        {
            foreach (var t in Tabs) if (t.Built) t.Panel.OnClosed();
        }
    }

    // ============================================================ ログ解析（基本 ＋ 相関）
    public class LogAnalyzerApp : TabbedTool
    {
        protected override string Title => "ログ解析";
        protected override string Lead => "選んだログと同じ接続元・利用者の記録を集めます。相関タブでは範囲・利用者ごとに時系列に並べます。関係があるかを判断するのはあなたです。";
        protected override string IconName => "log";
        protected override Color Accent => ToolStyle.Indigo;

        protected override void DefineTabs()
        {
            Tabs.Add(new Tab { Label = "選んだログ", Right = Items.LogAnalyzer, Panel = new LogBasicPanel() });
            Tabs.Add(new Tab { Label = "相関（範囲・利用者）", Right = Items.Correlator, Panel = new LogCorrelationPanel() });
        }
    }

    public class LogBasicPanel : ToolPanel
    {
        GameObject _empty, _body;
        Text _who, _total, _recent, _fails;
        ToolList _rows;
        int _lastRecent = -1, _lastFails = -1;

        public override void Build(RectTransform p)
        {
            _empty = ToolStyle.EmptyState(UIFactory.Rect("EmptyHost", p).Stretch(), "ログの行が選ばれていません",
                "メインモニターの「システムログの検索・参照」で行を選ぶ", "同じ接続元・同じ利用者の記録が、ここに新しい順に集まります");
            _body = UIFactory.Rect("Body", p).Stretch().gameObject;
            var top = ToolStyle.Box(_body.transform, 0, 1, 0, 104);
            _who = ToolStyle.Text(top, "", 0, 0, 560, 20, 14, ToolStyle.Ink, TextAnchor.MiddleLeft);
            var tiles = UIFactory.Rect("Tiles", top).Stretch();
            _total = StatCell(tiles, 0, "該当する記録");
            _recent = StatCell(tiles, 1, "直近10分");
            _fails = StatCell(tiles, 2, "失敗");
            var lg = ToolStyle.Box(_body.transform, 0, 1, 114, 0, "同じ接続元・同じ利用者の記録（新しい順）");
            _rows = ToolList.Create(lg, ToolStyle.Indigo, "記録はありません。");
            W.SetStatus(0, "メインモニターのシステムログで行を選ぶと表示します。");
        }

        Text StatCell(RectTransform parent, int i, string label)
        {
            var bg = UIFactory.Image(parent, ToolStyle.Round, Color.white, "Stat");
            ToolStyle.Cell(bg.rectTransform, i, 3, 28, 52);
            ToolStyle.Text(bg.transform, label, 10, 4, 200, 16, 11, ToolStyle.Muted, TextAnchor.MiddleLeft, true);
            return ToolStyle.Text(bg.transform, "-", 10, 20, 200, 28, 20, ToolStyle.Indigo, TextAnchor.MiddleLeft, true);
        }

        public override void Refresh()
        {
            var sel = D.SelectedLog;
            Show(_empty, _body, sel != null);
            if (sel == null) return;
            var list = new List<LogEntry>();
            int recent = 0, fails = 0;
            float now = S.Time.CurrentMinute;
            bool knownUser = LogCorrelation.IsKnownUser(sel.User);
            foreach (var e in S.Logs.Entries)
            {
                bool same = e.Source == sel.Source || (knownUser && e.User == sel.User);
                if (!same) continue;
                list.Add(e);
                if (now - e.Time <= 10f) recent++;
                if (e.Result == "FAIL") fails++;
            }
            list.Reverse();
            _who.text = $"接続元 <b>{sel.Source}</b>　　利用者 <b>{sel.User}</b>";
            _total.text = $"{list.Count} 件";
            _recent.text = $"{recent} 件";
            _fails.text = $"{fails} 件";
            if (_lastRecent >= 0 && recent > _lastRecent) ToolFx.Flash(_recent);
            if (_lastFails >= 0 && fails > _lastFails) ToolFx.Flash(_fails);
            _lastRecent = recent; _lastFails = fails;
            _rows.SetItems(list, e => new ToolList.Row($"{TimeManager.Format(e.Time)}　{e.EventType}", $"{e.Source} → {e.Destination}　{e.User}　{LogViewerView.VisibleDetail(S, e)}",
                e.Result, e.Result == "FAIL" ? Tone.Warn : Tone.Neutral, e.Result == "FAIL" ? "warning" : "log"));
        }
    }

    public class LogCorrelationPanel : ToolPanel
    {
        Text _normal, _text;
        float _timer;

        public override void Build(RectTransform p)
        {
            var top = ToolStyle.Box(p, 0, 1, 0, 66, "選んだ要求の利用者の「ふだん」");
            _normal = ToolStyle.Text(top, "", 0, 0, 640, 22, 13, ToolStyle.Ink, TextAnchor.MiddleLeft);
            var lg = ToolStyle.Box(p, 0, 1, 76, 0, "社外の接続元（範囲ごと）・同じ利用者の複数の範囲");
            _text = ToolStyle.ScrollText(lg, 13, true);
        }

        public override void Refresh()
        {
            if (_text == null) return;
            var e = D.SelectedRequest;
            _normal.text = e != null && e.Employee != null
                ? $"<b>{e.Employee.DisplayName}</b>　勤務時間 {e.Employee.workHours}　ふだん使うシステム {string.Join("・", e.Employee.normalAccessTargets.ConvertAll(EmployeeDetailView.ServiceLabel))}"
                : "<color=#8890A0>メインモニターで接続要求を選ぶと、その利用者のふだんの使い方が出ます。</color>";
            if (Time.unscaledTime < _timer) return; // 集計は2秒ごと
            _timer = Time.unscaledTime + 2f;
            var sb = new StringBuilder();
            foreach (var kv in LogCorrelation.ByRange(S.Logs.Entries))
            {
                sb.AppendLine($"<b><color=#0D7888>{kv.Key}</color></b>　{kv.Value.Count} 件");
                Lines(sb, kv.Value);
            }
            var users = LogCorrelation.ByUserAcrossRanges(S.Logs.Entries);
            if (users.Count > 0)
            {
                sb.AppendLine("<b>同じ利用者が、複数の範囲から</b>");
                foreach (var kv in users)
                {
                    sb.AppendLine($"<b><color=#7A3D9A>{kv.Key}</color></b>　{kv.Value.Count} 件");
                    Lines(sb, kv.Value);
                }
            }
            _text.text = sb.Length == 0 ? "<color=#8890A0>まとまった記録は、まだありません。</color>" : sb.ToString();
        }

        void Lines(StringBuilder sb, List<LogEntry> list)
        {
            int from = Mathf.Max(0, list.Count - 6);
            for (int i = from; i < list.Count; i++)
            {
                var l = list[i];
                sb.AppendLine($"  {TimeManager.Format(l.Time)}  {l.Source} → {EmployeeDetailView.ServiceLabel(l.Destination)}  {l.EventType} {l.Result}  <color=#666E7C>{LogViewerView.VisibleDetail(S, l)}</color>");
            }
            sb.AppendLine();
        }
    }

    // ============================================================ 端末管理（端末一覧・隔離 ＋ 接続中PCの診断）
    public class DeviceManagerApp : TabbedTool
    {
        protected override string Title => "端末管理";
        protected override string Lead => "社内の端末の一覧・隔離と、リモート接続中のPCの診断をまとめた画面です。隔離するかを決めるのはあなたです。";
        protected override string IconName => "remote";
        protected override Color Accent => ToolStyle.Purple;

        protected override void DefineTabs()
        {
            Tabs.Add(new Tab { Label = "端末一覧・隔離", Right = Items.Isolator, Panel = new DeviceListPanel() });
            Tabs.Add(new Tab { Label = "接続中のPCの診断", Right = Items.RemoteKit, Panel = new DiagnosisPanel() });
        }
    }

    public class DeviceListPanel : ToolPanel
    {
        ToolList _list;
        Text _sel;
        FlatButton _btn;

        public override void Build(RectTransform p)
        {
            var top = ToolStyle.Box(p, 0, 1, 0, 74, "隔離する端末");
            _sel = ToolStyle.Text(top, "", 0, 0, 300, 32, 15, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            _btn = ToolStyle.Button(top, "この端末を隔離する", 0, 0, 170, 32, IsolateSelected, ToolStyle.Red);
            _btn.Rect.AnchorTopRight(0, 0, 170, 32);
            var lg = ToolStyle.Box(p, 0, 1, 84, 0, "社内の端末（最後に選んだ接続要求・ログの端末が選ばれます）");
            _list = ToolList.Create(lg, ToolStyle.Red, "端末がありません。");
            _list.SelectionChanged += _ => RefreshSelection();
            D.SelectionKindChanged += FollowSelection;
            Refresh();
            FollowSelection(SelectionKind.Request);
        }

        public override void OnClosed() { if (D != null) D.SelectionKindChanged -= FollowSelection; }

        /// <summary>
        /// メインモニターで最後に選んだ要求・ログの端末を選んでおく（隔離するかはプレイヤーが決める）。
        /// 端末が分からない行を選んだら、前の端末を残さず選択を外す。メールの選択では変えない
        /// </summary>
        void FollowSelection(SelectionKind kind)
        {
            if (_list == null || kind == SelectionKind.Mail) return;
            string device = D.TargetDevice;
            EmployeeData match = null;
            if (device != null) foreach (var e in S.Employees.All) if (e.IsActive && e.deviceId == device) { match = e; break; }
            _list.Select(match);
        }

        void IsolateSelected()
        {
            if (!(_list.Selected is EmployeeData e)) { AudioFx.Error(); W.SetStatus(0, "隔離する端末を選んでください。"); return; }
            string device = e.deviceId; // 確認画面と実行で同じ端末
            D.ShowModal("端末の隔離", $"{device}（{e.DisplayName}）をネットワークから切り離します。\nよろしいですか？", "lock", "隔離する", () =>
            {
                W.SetStatus(0, S.Security.Isolate(device));
                Refresh();
            }, "やめる", null);
        }

        void RefreshSelection()
        {
            if (_list.Selected is EmployeeData e)
            {
                bool done = S.Security.IsIsolated(e.deviceId);
                _sel.text = $"{e.deviceId}　<size=12><color=#666E7C>{e.DisplayName}</color></size>";
                _btn.Interactable = !done;
                _btn.SetText(done ? "隔離中です" : "この端末を隔離する");
            }
            else
            {
                _sel.text = "<color=#8890A0>対象なし（一覧から端末を選んでください）</color>";
                _btn.Interactable = false;
                _btn.SetText("この端末を隔離する");
            }
        }

        public override void Refresh()
        {
            if (_list == null) return;
            var list = new List<EmployeeData>();
            foreach (var e in S.Employees.All) if (e.IsActive) list.Add(e);
            _list.SetItems(list, e =>
            {
                bool iso = S.Security.IsIsolated(e.deviceId);
                return new ToolList.Row($"{e.deviceId}　{e.DisplayName}", $"{S.Employees.GetIp(e)}　{e.department}", iso ? "隔離中" : "接続中", iso ? Tone.Warn : Tone.Neutral, iso ? "lock" : "pc");
            });
            RefreshSelection();
        }
    }

    public class DiagnosisPanel : ToolPanel
    {
        GameObject _empty, _body;
        Text _host, _link;
        Text[] _vals;
        RectTransform[] _tiles;
        readonly List<GameObject> _badges = new List<GameObject>();
        ToolList _procs;
        static readonly string[] Labels = { "Wi-Fi", "IP アドレス", "DNS", "インターネット", "プリンター", "修正プログラム", "画面ロック", "隔離" };
        static readonly string[] Icons = { "wifi", "network", "globe", "internet", "printer", "shield", "lock", "remote" };

        public override void Build(RectTransform p)
        {
            _empty = ToolStyle.EmptyState(UIFactory.Rect("EmptyHost", p).Stretch(), "リモート接続中のPCはありません",
                "メインモニターの「ITサポート受付管理」で問い合わせを開く", "「リモート接続」を押す", "PCの状態がここにまとめて出ます");
            _body = UIFactory.Rect("Body", p).Stretch().gameObject;
            var top = ToolStyle.Box(_body.transform, 0, 1, 0, 180);
            _host = ToolStyle.Text(top, "", 0, 0, 400, 22, 15, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            // 通信量は測っていないので、メーターではなく「通信できるか」だけを出す
            _link = ToolStyle.Text(top, "", 12, ToolStyle.Muted, TextAnchor.MiddleRight);
            _link.rectTransform.AnchorTopRight(0, 2, 220, 18);
            var grid = UIFactory.Rect("Grid", top).Stretch(0, 28, 0, 0);
            _vals = new Text[Labels.Length];
            _tiles = new RectTransform[Labels.Length];
            for (int i = 0; i < Labels.Length; i++)
                _vals[i] = ToolStyle.Tile(grid, i % 4, 4, (i / 4) * 64, 56, Labels[i], out _tiles[i], Icons[i]);
            var pg = ToolStyle.Box(_body.transform, 0, 1, 190, 0, "動いているプログラム");
            _procs = ToolList.Create(pg, ToolStyle.Purple, "プログラムの情報はありません。");
        }

        void SetTile(int i, string value, string badge, Tone tone)
        {
            _vals[i].text = value;
            _badges.Add(ToolStyle.BadgeRight(_tiles[i], badge, tone, 6, 5).gameObject);
        }

        public override void Refresh()
        {
            var session = S.HelpDesk.ActiveSession;
            bool on = session != null && session.Connected;
            Show(_empty, _body, on);
            if (!on) return;
            var pc = session.PC;
            _host.text = $"{pc.Hostname}　<size=12><color=#666E7C>{pc.UserDisplay}</color></size>";
            _link.text = pc.InternetConnectivity ? "社内ネットワーク: 通信できる" : pc.Quarantined ? "社内ネットワーク: 隔離中" : "社内ネットワーク: 通信できない";
            foreach (var b in _badges) if (b) Object.Destroy(b);
            _badges.Clear();
            var wifiIcon = _tiles[0].Find("Icon")?.GetComponent<Image>();
            if (wifiIcon) wifiIcon.sprite = RetroTheme.Icon(pc.WifiConnected ? "wifi" : "wifi_off");
            SetTile(0, pc.WifiConnected ? "接続" : "切断", pc.WifiConnected ? "OK" : "NG", pc.WifiConnected ? Tone.Ok : Tone.Bad);
            SetTile(1, string.IsNullOrEmpty(pc.IPAddress) ? "-" : pc.IPAddress, pc.DHCPEnabled ? "自動" : "手動", Tone.Neutral);
            SetTile(2, string.IsNullOrEmpty(pc.DNS) ? "-" : pc.DNS, pc.DnsAuto ? "自動" : "手動", Tone.Neutral);
            SetTile(3, pc.InternetConnectivity ? "つながる" : "つながらない", pc.InternetConnectivity ? "OK" : "NG", pc.InternetConnectivity ? Tone.Ok : Tone.Bad);
            SetTile(4, pc.PrinterOnline ? "オンライン" : "オフライン", pc.PrintQueuePaused ? "一時停止" : pc.PrinterOnline ? "OK" : "NG",
                pc.PrintQueuePaused || !pc.PrinterOnline ? Tone.Warn : Tone.Ok);
            SetTile(5, $"未適用 {pc.PendingPatches} 件", pc.PendingPatches > 0 ? "あり" : "なし", pc.PendingPatches > 0 ? Tone.Warn : Tone.Ok);
            SetTile(6, pc.ScreenLockEnabled ? "あり" : "なし", pc.ScreenLockEnabled ? "OK" : "なし", pc.ScreenLockEnabled ? Tone.Ok : Tone.Warn);
            SetTile(7, pc.Quarantined ? "隔離中" : "していない", pc.Quarantined ? "隔離" : "-", pc.Quarantined ? Tone.Warn : Tone.Neutral);
            var procs = new List<RemotePC.VirtualProcess>();
            foreach (var pr in pc.RunningProcesses) if (!pr.System) procs.Add(pr);
            _procs.SetItems(procs, pr => new ToolList.Row(pr.Name, $"{pr.MemoryKb:N0} KB", null, Tone.Neutral, "run"));
        }
    }

    // ============================================================ 接続元の遮断（範囲指定つき）
    /// <summary>
    /// 業務システム（接続監視・システムログ）の「接続元を遮断」。1つの接続元か、同じ範囲（末尾 *）かを選ぶ。
    /// 旧「遮断コンソール」の範囲指定をここに統合した（解禁日は FirewallBlock のまま）
    /// </summary>
    public static class BlockDialog
    {
        public static void Open(ShiftDesktop d, string ip, System.Action after = null)
        {
            if (string.IsNullOrEmpty(ip) || ip == "-") { AudioFx.Error(); return; }
            var w = d.OpenWindow(new WindowSpec { Id = "block", Title = "接続元の遮断", Icon = "firewall", Width = 440, Height = 250, Maximizable = false, Minimizable = false, Centered = true }, out bool created);
            if (!created) { w.Close(); w = d.OpenWindow(new WindowSpec { Id = "block", Title = "接続元の遮断", Icon = "firewall", Width = 440, Height = 250, Maximizable = false, Minimizable = false, Centered = true }, out _); }
            var c = w.Content;
            int dot = ip.LastIndexOf('.');
            string range = dot > 0 && char.IsDigit(ip[0]) ? ip.Substring(0, dot + 1) + "*" : null;
            UIFactory.Label(c, "遮断する接続元を選んでください。遮断した接続元からの要求は自動で拒否されます。", 8, 6, 420, 32, 12).verticalOverflow = VerticalWrapMode.Overflow;
            var input = UIFactory.Input(c, 8, 88, 270, 24, ip);
            var target = UIFactory.Label(c, "", 8, 118, 420, 20, 12);
            target.fontStyle = FontStyle.Bold;
            RetroToggle single = null, wide = null;
            single = UIFactory.Check(c, $"この接続元だけ（{ip}）", true, v => { if (v) { input.text = ip; wide?.SetValue(false, false); } }, true);
            single.GetComponent<RectTransform>().Place(8, 42, 410, 20);
            wide = UIFactory.Check(c, range != null ? $"同じ範囲をまとめて（{range}）" : "同じ範囲をまとめて（この接続元では使えません）", false, v => { if (v && range != null) { input.text = range; single.SetValue(false, false); } }, true);
            wide.GetComponent<RectTransform>().Place(8, 64, 410, 20);
            if (range == null) wide.Interactable = false;
            void ShowTarget()
            {
                string t = (input.text ?? "").Trim();
                target.text = t.Length >= 4 ? $"対象: {t}{(t.EndsWith("*") ? "（範囲）" : "")}" : "対象: なし";
            }
            input.onValueChanged.AddListener(_ => ShowTarget());
            ShowTarget();
            UIFactory.Button(c, "遮断する", 8, 150, 120, 28, () =>
            {
                string t = (input.text ?? "").Trim(); // 確認画面と実行で同じ対象
                if (t.Length < 4) { AudioFx.Error(); return; }
                d.ShowModal("接続元の遮断", $"{t} からの通信を遮断します。\nよろしいですか？", "lock", "遮断する", () =>
                {
                    d.ShowStatus(d.S.Security.Block(t));
                    w.Close();
                    after?.Invoke();
                }, "やめる", null);
            }, "lock");
            UIFactory.Button(c, "やめる", 136, 150, 100, 28, w.Close);
        }
    }
}
