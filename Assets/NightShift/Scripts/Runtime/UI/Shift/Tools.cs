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
    /// サブモニターで動くツール（TOOL_CATALOG.md）。原則「手間を減らすだけ・判断はしない」。
    /// 例外は 自動審査ツール（条件をすべて満たす簡単な要求だけ自動で許可）、VACCINE 99（既知のウイルスだけ検出）、SOC-AI（提案・代行）。
    /// </summary>
    public static class Tools
    {
        public static void Launch(WorkstationScreen ws, string id)
        {
            var d = ws.Desktop;
            if (!d.HasTool(id)) { AudioFx.Error(); return; }
            switch (id)
            {
                case Items.ProfilePanel: Open<ProfilePanelTool>(ws, id, "社員情報パネル", "people", 420, 380); break;
                case Items.AutoReview: Open<AutoReviewTool>(ws, id, "自動審査ツール", "check", 520, 490); break;
                case Items.LogAnalyzer: Open<LogAnalyzerTool>(ws, id, "ログ解析", "log", 520, 360); break;
                case Items.FwConsole: Open<FirewallConsoleTool>(ws, id, "遮断コンソール", "firewall", 380, 330); break;
                case Items.MailInspector: Open<MailInspectorTool>(ws, id, "メール差出人チェッカー", "mail", 460, 220); break;
                case Items.RemoteKit: Open<RemoteKitTool>(ws, id, "リモート診断キット", "remote", 400, 380); break;
                case Items.Isolator: Open<IsolatorTool>(ws, id, "端末隔離ツール", "lock", 440, 380); break;
                case Items.ServerMonitor: Open<ServerMonitorTool>(ws, id, "サーバー監視ダッシュボード", "server", 560, 420); break;
                case Items.Correlator: Open<CorrelatorTool>(ws, id, "相関タイムライン", "clock", 600, 420); break;
                case Items.Vaccine: Open<VaccineTool>(ws, id, "VACCINE 99", "shield", 440, 330); break;
                case Items.VaccineServer: Open<VaccineTool>(ws, Items.Vaccine, "VACCINE 99", "shield", 440, 330); break;
                case Items.SocAi: Open<SocAiTool>(ws, id, "SOC-AI", "debug", 560, 470); break;
            }
        }

        static void Open<T>(WorkstationScreen ws, string id, string title, string icon, float w, float h) where T : ToolWindow
        {
            var win = ws.Windows.Open(new WindowSpec { Id = "tool:" + id, Title = title, Icon = icon, Width = w, Height = h, StatusCells = 1 }, out bool created);
            if (!created) return;
            var t = win.gameObject.AddComponent<T>();
            t.Init(ws.Desktop, win, id);
        }
    }

    /// <summary>ツールの共通部分（メインモニターの選択が変わると更新）</summary>
    public abstract class ToolWindow : MonoBehaviour
    {
        protected ShiftDesktop D;
        protected RetroWindow W;
        protected string ToolId;
        protected ShiftSession S => D.S;
        float _timer;
        protected virtual float RefreshInterval => 1f;

        public void Init(ShiftDesktop d, RetroWindow w, string id)
        {
            D = d; W = w; ToolId = id;
            Build(w.Content);
            d.SelectionChanged += Refresh;
            w.Closed += () => { if (D != null) D.SelectionChanged -= Refresh; OnClosed(); };
            Refresh();
        }

        protected abstract void Build(RectTransform c);
        protected abstract void Refresh();
        protected virtual void OnClosed() { }

        protected void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer >= RefreshInterval) { _timer = 0; Refresh(); }
        }

        protected static string Ok(bool v) => v ? "○" : "×";
    }

    // ============================================================ 社員情報パネル
    public class ProfilePanelTool : ToolWindow
    {
        Text _text;
        protected override void Build(RectTransform c)
        {
            _text = UIFactory.Label(c, "", 12, TextAnchor.UpperLeft);
            _text.rectTransform.Stretch(6, 4, 6, 4);
            W.SetStatus(0, "メインモニターで接続要求を選ぶと表示します。見比べるのはあなたです。");
        }

        protected override void Refresh()
        {
            var e = D.SelectedRequest;
            if (e == null) { _text.text = "接続要求を選んでください。"; return; }
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{e.RequestId}</b>　利用者: {e.DisplayUser}（{e.DisplayEmployeeId}）");
            sb.AppendLine($"要求の端末: {e.Device}　接続元: {e.SourceLabel}");
            sb.AppendLine("――――――――――――――――――");
            var emp = e.Employee;
            if (emp == null) { sb.AppendLine("社員名簿に該当する社員はいません。"); _text.text = sb.ToString(); return; }
            sb.AppendLine($"<b>{emp.DisplayName}</b>（{emp.employeeId}）　{emp.department} / {emp.position}");
            sb.AppendLine($"登録端末: {emp.deviceId}");
            if (D.Unlocked(Features.IpCheck)) sb.AppendLine($"端末IP: {S.Employees.GetIp(emp)}");
            if (D.Unlocked(Features.WorkStatus)) sb.AppendLine($"勤務状態: {(emp.onShift ? "勤務中" : "退勤")}　勤務時間: {emp.workHours}");
            if (D.Unlocked(Features.EmploymentStatus)) sb.AppendLine($"在籍状態: {EmployeeData.EmploymentLabel(emp.employment)}");
            sb.AppendLine($"VPN利用: {(emp.vpnEnabled ? "許可" : "不可")}　VPN端末: {(string.IsNullOrEmpty(emp.laptopDeviceId) ? "-" : emp.laptopDeviceId)}");
            if (D.Unlocked(Features.EmployeePermissions))
            {
                var perms = new List<string>();
                foreach (var p in emp.permissions) if (p != null) perms.Add(p.displayName);
                sb.AppendLine("権限: " + string.Join("・", perms));
            }
            _text.text = sb.ToString();
        }
    }

    // ============================================================ 自動審査ツール
    public class AutoReviewTool : ToolWindow
    {
        public static readonly string[] Conditions =
        {
            "社員として登録されている", "自分の登録端末からの接続", "必要な権限が ○", "普段使うシステムへの接続",
            "勤務中の社員", "接続元IPが登録端末のIPと一致", "在籍中（退職・休職でない）"
        };
        TableView _queue;
        RetroToggle _enabled;
        Text _count;

        protected override float RefreshInterval => 0.5f;

        protected override void Build(RectTransform c)
        {
            var career = CareerRecord.Current;
            var g = UIFactory.Group(c, "自動で許可する条件（すべて満たした要求だけ）", 0, 0, 500, 214);
            UIFactory.Label(g, "☑ 社内からの接続（常に必須）　※社外・管理系・VPN・セキュリティ警告は常に手動", 0, 0, 490, 18, 11);
            for (int i = 0; i < Conditions.Length; i++)
            {
                string cond = Conditions[i];
                bool available = i < 6 || D.Unlocked(Features.EmploymentStatus);
                var t = UIFactory.Check(g, cond + (available ? "" : "（DAY12〜）") + (i == IpMatchIndex ? "（推奨）" : ""), career.autoReviewConditions.Contains(cond), v =>
                {
                    if (v) { if (!career.autoReviewConditions.Contains(cond)) career.autoReviewConditions.Add(cond); }
                    else career.autoReviewConditions.Remove(cond);
                    // 条件を全部外したら、自動許可も止める
                    if (career.autoReviewConditions.Count == 0 && _enabled != null && _enabled.Value) SetEnabled(false, "条件がないため、自動許可を停止しました。");
                    UpdateWarning();
                });
                t.GetComponent<RectTransform>().Place(0, 22 + i * 22, 300, 20);
                if (!available) t.Interactable = false;
            }
            _enabled = UIFactory.Check(g, "自動許可を有効にする", career.autoReviewEnabled && career.autoReviewConditions.Count > 0, v =>
            {
                if (v && career.autoReviewConditions.Count == 0) { AudioFx.Error(); SetEnabled(false, "条件を1つ以上選んでから有効にしてください。"); return; }
                SetEnabled(v, v ? "自動許可を開始しました。許可した要求の責任は、あなたにあります。" : "自動許可を停止しました。");
            });
            _enabled.GetComponent<RectTransform>().Place(320, 22, 170, 20);
            _count = UIFactory.Label(g, "", 320, 50, 170, 40, 11, TextAnchor.UpperLeft);
            // IP の照合を外すと、IP を偽ったなりすまし接続も通してしまう
            _warning = UIFactory.Label(g, "", 320, 94, 170, 80, 11, TextAnchor.UpperLeft, new Color(0.7f, 0, 0));
            UpdateWarning();

            var q = UIFactory.Group(c, "要求キュー（期限の近い順）", 0, 220, 500, 200);
            _queue = TableView.CreateStretch(q, 0, 0, 0, 0,
                new TableColumn("要求", 70), new TableColumn("利用者", 110), new TableColumn("接続元", 104), new TableColumn("接続先", 100), new TableColumn("残り", 60));
            S.Requests.Created += OnCreated;
            W.SetStatus(0, _enabled.Value ? "自動許可した要求の責任は、あなたにあります。" : "条件を選んでから「自動許可を有効にする」を押してください。");
            ProcessPending();
        }

        protected override void OnClosed() { if (D != null && D.S != null) D.S.Requests.Created -= OnCreated; }

        void OnCreated(EventInstance e) { if (e.State == EventState.Pending) TryAuto(e); }

        public const int IpMatchIndex = 5;
        Text _warning;

        void UpdateWarning()
        {
            if (_warning == null) return;
            bool ipOff = !CareerRecord.Current.autoReviewConditions.Contains(Conditions[IpMatchIndex]);
            _warning.text = ipOff ? "⚠IPの照合がオフです。接続元を偽ったなりすまし接続も、自動で許可されます。" : "";
        }

        void SetEnabled(bool v, string message)
        {
            CareerRecord.Current.autoReviewEnabled = v;
            if (_enabled.Value != v) _enabled.SetValue(v, false);
            W.SetStatus(0, message);
            if (v) ProcessPending();
        }

        void ProcessPending()
        {
            foreach (var e in new List<EventInstance>(S.Requests.Pending)) TryAuto(e);
        }

        void TryAuto(EventInstance e)
        {
            if (_enabled == null || !_enabled.Value || CareerRecord.Current.autoReviewConditions.Count == 0) return;
            if (!Eligible(S, e, CareerRecord.Current.autoReviewConditions)) return;
            e.AutoDecided = true;
            S.AutoAllowed++;
            S.Requests.Decide(e, PlayerAction.Allow);
            S.Logs.Add(S.Time.CurrentMinute, e.SourceIp, e.DestinationLabel, RequestManager.UserLabel(e), "CONN-REQ", "AUTO-ALLOWED", e.RequestId + " 自動審査ツール");
        }

        /// <summary>自動許可の対象か（社内・非管理系・警告なし ＋ 有効にした条件をすべて満たす）</summary>
        public static bool Eligible(ShiftSession s, EventInstance e, List<string> conds)
        {
            if (e.Zone != SourceZone.External && e.Destination != null && !e.Destination.isAdministrative && !e.NotifyAsSecurityAlert)
            {
                var acc = e.Destination.FindAccess(e.AccessType);
                if (acc != null && acc.requiresVpnEnabled) return false;
                var emp = e.Employee;
                foreach (var c in conds)
                {
                    switch (System.Array.IndexOf(Conditions, c))
                    {
                        case 0: if (emp == null) return false; break;
                        case 1: if (emp == null || e.Device != emp.deviceId) return false; break;
                        case 2: if (emp == null || (acc != null && !string.IsNullOrEmpty(acc.requiredPermission) && !emp.HasPermission(acc.requiredPermission))) return false; break;
                        case 3: if (emp == null || !emp.UsesNormally(e.ServiceId)) return false; break;
                        case 4: if (emp == null || !emp.onShift) return false; break;
                        case 5: if (emp == null || e.SourceIp != s.Employees.GetIp(emp)) return false; break;
                        case 6: if (emp == null || !emp.IsActive) return false; break;
                    }
                }
                return true;
            }
            return false;
        }

        protected override void Refresh()
        {
            if (_queue == null) return;
            var list = new List<EventInstance>(S.Requests.Pending);
            list.Sort((a, b) => a.TimeoutAt.CompareTo(b.TimeoutAt));
            float now = S.Time.CurrentMinute;
            _queue.SetRows(list, e => new[] { e.RequestId, e.DisplayUser, e.SourceLabel, e.DestinationLabel, S.Day.requestsExpire ? $"{Mathf.Max(0, Mathf.CeilToInt(e.TimeoutAt - now))} 分" : "-" });
            _count.text = $"今夜の自動許可: {S.AutoAllowed} 件";
        }
    }

    // ============================================================ ログ解析
    public class LogAnalyzerTool : ToolWindow
    {
        Text _summary;
        TableView _rows;
        protected override void Build(RectTransform c)
        {
            _summary = UIFactory.Label(c, "", 0, 0, 500, 48, 12, TextAnchor.UpperLeft);
            var g = UIFactory.Group(c, "同じ接続元・同じ利用者の記録", 0, 52, 500, 250);
            _rows = TableView.CreateStretch(g, 0, 0, 0, 0, new TableColumn("時刻", 70), new TableColumn("接続元", 110), new TableColumn("利用者", 110), new TableColumn("結果", 60), new TableColumn("詳細", 140));
            W.SetStatus(0, "メインモニターのシステムログで行を選ぶと表示します。");
        }

        protected override void Refresh()
        {
            var sel = D.SelectedLog;
            if (sel == null) { _summary.text = "ログの行を選んでください。"; _rows.SetRows(new List<LogEntry>(), x => new string[0]); return; }
            var list = new List<LogEntry>();
            int recent = 0, fails = 0;
            float now = S.Time.CurrentMinute;
            foreach (var e in S.Logs.Entries)
            {
                bool same = e.Source == sel.Source || (sel.User != "-" && e.User == sel.User);
                if (!same) continue;
                list.Add(e);
                if (now - e.Time <= 10f) recent++;
                if (e.Result == "FAIL") fails++;
            }
            list.Reverse();
            _summary.text = $"接続元 <b>{sel.Source}</b> / 利用者 {sel.User}\n該当 {list.Count} 件（直近10分 <b>{recent}</b> 件・失敗 {fails} 件）";
            _rows.SetRows(list, e => new[] { TimeManager.Format(e.Time), e.Source, e.User, e.Result, e.Detail });
        }
    }

    // ============================================================ 遮断コンソール
    public class FirewallConsoleTool : ToolWindow
    {
        InputField _range;
        TableView _list;
        protected override void Build(RectTransform c)
        {
            UIFactory.Label(c, "範囲（例: 198.51.100.*）:", 0, 0, 160, 22);
            _range = UIFactory.Input(c, 160, 0, 120);
            UIFactory.Button(c, "遮断", 286, 0, 74, 22, () =>
            {
                string t = (_range.text ?? "").Trim();
                if (t.Length < 4) { AudioFx.Error(); return; }
                W.SetStatus(0, S.Security.Block(t));
                Refresh();
            }, "firewall");
            var g = UIFactory.Group(c, "遮断リスト", 0, 30, 360, 230);
            _list = TableView.CreateStretch(g, 0, 0, 0, 0, new TableColumn("接続元・範囲", 300));
            W.SetStatus(0, "選んだ要求の接続元の範囲は自動で入力されます。");
            D.SelectionChanged += Prefill;
            Prefill();
        }

        protected override void OnClosed() { if (D != null) D.SelectionChanged -= Prefill; }

        void Prefill()
        {
            var e = D.SelectedRequest;
            string ip = e != null ? e.SourceIp : D.SelectedLog?.Source;
            if (string.IsNullOrEmpty(ip) || _range == null) return;
            int dot = ip.LastIndexOf('.');
            if (dot > 0) _range.text = ip.Substring(0, dot + 1) + "*";
        }

        protected override void Refresh()
        {
            if (_list == null) return;
            _list.SetRows(new List<string>(S.Security.Blocked), x => new[] { x });
        }
    }

    // ============================================================ メール差出人チェッカー
    public class MailInspectorTool : ToolWindow
    {
        Text _text;
        protected override void Build(RectTransform c)
        {
            _text = UIFactory.Label(c, "", 14, TextAnchor.UpperLeft);
            _text.rectTransform.Stretch(6, 4, 6, 4);
        }

        protected override void Refresh()
        {
            var m = D.SelectedMail;
            if (m == null) { _text.text = "メールを選んでください。"; return; }
            string server = string.IsNullOrEmpty(m.FromAddress) ? "-" : m.IsPhish ? "mail." + m.FromAddress.Substring(m.FromAddress.IndexOf('@') + 1) : "MAIL-SRV01 (10.10.0.20)";
            _text.text = $"件名: {m.Subject}\n\n差出人アドレス:\n<size=18><b>{(string.IsNullOrEmpty(m.FromAddress) ? "(なし)" : m.FromAddress)}</b></size>\n\n送信元サーバー: <b>{server}</b>\n社内のアドレス: @{MailManager.CompanyDomain}";
        }
    }

    // ============================================================ リモート診断キット
    public class RemoteKitTool : ToolWindow
    {
        Text _text;
        protected override void Build(RectTransform c)
        {
            _text = UIFactory.Label(c, "", 12, TextAnchor.UpperLeft, null, true);
            _text.rectTransform.Stretch(6, 4, 6, 4);
        }

        protected override void Refresh()
        {
            var session = S.HelpDesk.ActiveSession;
            if (session == null || !session.Connected) { _text.text = "リモート接続中のPCはありません。"; return; }
            var pc = session.PC;
            var sb = new StringBuilder();
            sb.AppendLine($"{pc.Hostname} ({pc.UserDisplay})");
            sb.AppendLine($"Wi-Fi        : {(pc.WifiConnected ? "接続" : "切断")}");
            sb.AppendLine($"IP アドレス  : {(string.IsNullOrEmpty(pc.IPAddress) ? "-" : pc.IPAddress)} ({(pc.DHCPEnabled ? "自動" : "手動")})");
            sb.AppendLine($"DNS          : {(string.IsNullOrEmpty(pc.DNS) ? "-" : pc.DNS)} ({(pc.DnsAuto ? "自動" : "手動")})");
            sb.AppendLine($"インターネット: {(pc.InternetConnectivity ? "OK" : "NG")}");
            sb.AppendLine($"プリンター   : {(pc.PrinterOnline ? "オンライン" : "オフライン")}{(pc.PrintQueuePaused ? " / 一時停止" : "")}");
            sb.AppendLine($"修正プログラム: 未適用 {pc.PendingPatches} 件");
            sb.AppendLine($"画面ロック   : {(pc.ScreenLockEnabled ? "あり" : "なし")}");
            sb.AppendLine("プロセス:");
            foreach (var p in pc.RunningProcesses) if (!p.System) sb.AppendLine($"  {p.Name,-16} {p.MemoryKb,7:N0} KB");
            _text.text = sb.ToString();
        }
    }

    // ============================================================ 端末隔離ツール
    public class IsolatorTool : ToolWindow
    {
        TableView _table;
        protected override void Build(RectTransform c)
        {
            _table = TableView.CreateStretch(c, 0, 0, 0, 34, new TableColumn("端末", 100), new TableColumn("社員", 110), new TableColumn("IP", 100), new TableColumn("状態", 70));
            var b = UIFactory.Button(c, "選んだ端末を隔離", 0, 0, 160, 26, () =>
            {
                if (!(_table.Selected is EmployeeData e)) { AudioFx.Error(); return; }
                W.SetStatus(0, S.Security.Isolate(e.deviceId));
                Refresh();
            }, "lock");
            b.Rect.anchorMin = b.Rect.anchorMax = new Vector2(0, 0); b.Rect.pivot = new Vector2(0, 0); b.Rect.anchoredPosition = new Vector2(0, 4);
        }

        protected override void Refresh()
        {
            var sel = _table.Selected;
            var list = new List<EmployeeData>();
            foreach (var e in S.Employees.All) if (e.IsActive) list.Add(e);
            _table.SetRows(list, e => new[] { e.deviceId, e.DisplayName, S.Employees.GetIp(e), S.Security.IsIsolated(e.deviceId) ? "隔離中" : "接続" });
            if (sel != null) _table.SelectData(sel);
        }
    }

    // ============================================================ サーバー監視ダッシュボード
    public class ServerMonitorTool : ToolWindow
    {
        Text _text;
        protected override float RefreshInterval => 2f;
        protected override void Build(RectTransform c)
        {
            _text = UIFactory.ScrollText(c, "", out var scroll);
            ((RectTransform)scroll.transform).Stretch();
        }

        protected override void Refresh()
        {
            if (_text == null) return;
            var sb = new StringBuilder();
            foreach (var st in S.Servers.Servers)
            {
                sb.AppendLine($"<b>{st.Id}</b>  [{st.StatusLabel}]");
                foreach (var svc in st.Services) sb.AppendLine($"   サービス: {svc.Name}  {(svc.Running ? "実行中" : "<color=#B00000>停止</color>")}");
                foreach (var p in st.Processes) sb.AppendLine($"   {p.Name}  （発行元: {p.Publisher}）");
            }
            _text.text = sb.ToString();
        }
    }

    // ============================================================ 相関タイムライン
    public class CorrelatorTool : ToolWindow
    {
        Text _text;
        protected override float RefreshInterval => 2f;
        protected override void Build(RectTransform c)
        {
            _text = UIFactory.ScrollText(c, "", out var scroll);
            ((RectTransform)scroll.transform).Stretch();
        }

        static string Range(string ip)
        {
            int dot = ip.LastIndexOf('.');
            return dot > 0 ? ip.Substring(0, dot) + ".*" : ip;
        }

        protected override void Refresh()
        {
            if (_text == null) return;
            var e = D.SelectedRequest;
            var sb = new StringBuilder();
            if (e != null && e.Employee != null)
            {
                sb.AppendLine($"<b>{e.Employee.DisplayName}</b> の普段: 勤務時間 {e.Employee.workHours}／普段使うシステム {string.Join("・", e.Employee.normalAccessTargets.ConvertAll(EmployeeDetailView.ServiceLabel))}");
                sb.AppendLine();
            }
            // 社外の接続元を範囲ごとに時系列で並べる（関連があるかの判断はプレイヤー）
            var groups = new Dictionary<string, List<LogEntry>>();
            foreach (var l in S.Logs.Entries)
            {
                if (l.Source == null || l.Source.StartsWith("10.") || !char.IsDigit(l.Source[0])) continue;
                string r = Range(l.Source);
                if (!groups.TryGetValue(r, out var list)) groups[r] = list = new List<LogEntry>();
                list.Add(l);
            }
            foreach (var kv in groups)
            {
                if (kv.Value.Count < 2) continue;
                sb.AppendLine($"<b>{kv.Key}</b>  {kv.Value.Count} 件");
                int from = Mathf.Max(0, kv.Value.Count - 6);
                for (int i = from; i < kv.Value.Count; i++)
                {
                    var l = kv.Value[i];
                    sb.AppendLine($"   {TimeManager.Format(l.Time)}  {l.Source} → {EmployeeDetailView.ServiceLabel(l.Destination)}  {l.EventType} {l.Result}  {l.Detail}");
                }
            }
            _text.text = sb.Length == 0 ? "まだ記録がありません。" : sb.ToString();
        }
    }
}
