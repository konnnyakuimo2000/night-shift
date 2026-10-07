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
    /// 見た目は業務システムと違う「市販ソフト」風（ToolStyle）: 何をするツールかを見出しに書き、選んでいないときは手順を案内する。
    /// </summary>
    public static class Tools
    {
        public static void Launch(WorkstationScreen ws, string id)
        {
            var d = ws.Desktop;
            if (!d.HasTool(id)) { AudioFx.Error(); return; }
            switch (id)
            {
                case Items.ProfilePanel: Open<ProfilePanelTool>(ws, id, "社員情報パネル", "people", 680, 470); break;
                case Items.AutoReview: Open<AutoReviewTool>(ws, id, "自動審査ツール", "check", 640, 620); break;
                case Items.LogAnalyzer: Open<LogAnalyzerTool>(ws, id, "ログ解析", "log", 620, 540); break;
                case Items.FwConsole: Open<FirewallConsoleTool>(ws, id, "遮断コンソール", "firewall", 480, 470); break;
                case Items.MailInspector: Open<MailInspectorTool>(ws, id, "メール差出人チェッカー", "mail", 540, 360); break;
                case Items.RemoteKit: Open<RemoteKitTool>(ws, id, "リモート診断キット", "remote", 600, 540); break;
                case Items.Isolator: Open<IsolatorTool>(ws, id, "端末隔離ツール", "lock", 520, 540); break;
                case Items.ServerMonitor: Open<ServerMonitorTool>(ws, id, "サーバー監視ダッシュボード", "server", 700, 580); break;
                case Items.Correlator: Open<CorrelatorTool>(ws, id, "相関タイムライン", "clock", 700, 560); break;
                case Items.Vaccine: Open<VaccineTool>(ws, id, "VACCINE 99", "shield", 540, 500); break;
                case Items.VaccineServer: Open<VaccineTool>(ws, Items.Vaccine, "VACCINE 99", "shield", 540, 500); break;
                case Items.SocAi: Open<SocAiTool>(ws, id, "SOC-AI", "debug", 660, 640); break;
                case Items.GambleTerminal: Open<NightClubTool>(ws, id, "NIGHT CLUB", "globe", 600, 700); break;
                case Items.FxTerminal: Open<MidnightFxTool>(ws, id, "MIDNIGHT FX", "network", 700, 760); break;
                case Items.SignalDesk: Open<SignalDeskTool>(ws, id, "SIGNAL DESK", "network", 700, 620); break;
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
            Build(ToolStyle.Page(w));
            d.SelectionChanged += Refresh;
            w.Closed += () => { if (D != null) D.SelectionChanged -= Refresh; OnClosed(); };
            Refresh();
        }

        protected abstract void Build(RectTransform page);
        protected abstract void Refresh();
        protected virtual void OnClosed() { }

        protected void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer >= RefreshInterval) { _timer = 0; Refresh(); }
        }

        /// <summary>何も選ばれていないときの案内と、選ばれたときの中身を切り替える</summary>
        protected static void Show(GameObject empty, GameObject body, bool hasData)
        {
            if (empty) empty.SetActive(!hasData);
            if (body) body.SetActive(hasData);
        }

        protected static string Locked => "<color=#8890A0>（まだ使えません）</color>";
    }

    // ============================================================ 社員情報パネル
    public class ProfilePanelTool : ToolWindow
    {
        GameObject _empty, _body;
        Text _rUser, _rId, _rDevice, _rIp, _rDest, _rTime;
        Text _eName, _eId, _eDevice, _eIp, _eDept, _eWork, _eEmployment, _eVpn, _ePerms;
        GameObject _notFound, _empFields;
        Image _photo;
        RectTransform _left, _right;
        object _last;

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, "社員情報パネル", "選んだ接続要求の内容と、社員名簿の情報を左右に並べます。見比べて判断するのはあなたです。", "people", ToolStyle.Teal);
            _empty = ToolStyle.EmptyState(UIFactory.Rect("EmptyHost", p).Stretch(0, y, 0, 0), "接続要求が選ばれていません",
                "メインモニターの「ネットワーク接続監視」で要求を選ぶ", "ここに、要求の内容と社員名簿の情報が並びます");
            _body = UIFactory.Rect("Body", p).Stretch(0, y, 0, 0).gameObject;
            var left = ToolStyle.Box(_body.transform, 0, 0.5f, 0, 0, "接続要求の内容");
            var right = ToolStyle.Box(_body.transform, 0.5f, 1, 0, 0, "社員名簿");
            _left = left; _right = right;
            // 見比べる項目は同じ高さに並べる（利用者↔氏名、端末↔登録端末、接続元↔端末IP）
            _rUser = ToolStyle.Field(left, "利用者", 0, 0, 300);
            _rId = ToolStyle.Field(left, "社員番号", 0, 28, 300, 96, true);
            _rDevice = ToolStyle.Field(left, "端末", 0, 56, 300, 96, true);
            _rIp = ToolStyle.Field(left, "接続元", 0, 84, 300, 96, true);
            _rDest = ToolStyle.Field(left, "接続先", 0, 124, 300);
            _rTime = ToolStyle.Field(left, "受付時刻", 0, 152, 300, 96, true);

            _empFields = UIFactory.Rect("Fields", right).Stretch().gameObject;
            var f = _empFields.transform;
            _eName = ToolStyle.Field(f, "氏名", 0, 0, 220);
            _eId = ToolStyle.Field(f, "社員番号", 0, 28, 220, 96, true);
            _eDevice = ToolStyle.Field(f, "登録端末", 0, 56, 220, 96, true);
            _eIp = ToolStyle.Field(f, "端末IP", 0, 84, 220, 96, true);
            // 社員名簿の顔写真
            var frame = UIFactory.Image(f, RetroTheme.Field, Color.white, "PhotoFrame");
            frame.rectTransform.AnchorTopRight(0, 0, 68, 84);
            _photo = UIFactory.Image(frame.transform, RetroTheme.White, Color.white, "Photo");
            _photo.rectTransform.Stretch(2, 2, 2, 2);
            _eDept = ToolStyle.Field(f, "部署・役職", 0, 124, 300);
            _eWork = ToolStyle.Field(f, "勤務", 0, 152, 300);
            _eEmployment = ToolStyle.Field(f, "在籍", 0, 180, 300);
            _eVpn = ToolStyle.Field(f, "VPN", 0, 208, 300);
            ToolStyle.Text(f, "権限", 0, 238, 96, 20, 12, ToolStyle.Muted, TextAnchor.MiddleLeft);
            _ePerms = ToolStyle.Text(f, "", 96, 238, 210, 80, 12, ToolStyle.Ink);
            _notFound = UIFactory.Rect("NotFound", right).Stretch().gameObject;
            ToolStyle.Badge(_notFound.transform, "該当なし", Tone.Neutral, 0, 0);
            ToolStyle.Text(_notFound.transform, "社員名簿に、この利用者は見つかりません。", 0, 28, 300, 40, 13, ToolStyle.Ink);
            W.SetStatus(0, "メインモニターで接続要求を選ぶと表示します。");
        }

        protected override void Refresh()
        {
            var e = D.SelectedRequest;
            Show(_empty, _body, e != null);
            if (e == null) return;
            if (!ReferenceEquals(e, _last))
            {
                // 選び直したら、名簿を照合しているように光が走る
                _last = e;
                ToolFx.Sweep(_left, 0.5f);
                ToolFx.Sweep(_right, 0.7f);
            }
            _rUser.text = e.DisplayUser;
            _rId.text = e.DisplayEmployeeId;
            _rDevice.text = e.Device;
            _rIp.text = $"{e.SourceIp}（{(e.Zone == SourceZone.External ? "社外" : "社内")}）";
            _rDest.text = $"{e.DestinationLabel}　{e.AccessType}";
            _rTime.text = TimeManager.Format(e.GeneratedTime);
            var emp = e.Employee;
            _notFound.SetActive(emp == null);
            _empFields.SetActive(emp != null);
            if (emp == null) return;
            _eName.text = emp.DisplayName;
            _eId.text = emp.employeeId;
            _eDevice.text = emp.deviceId;
            _photo.sprite = emp.nameRedacted ? RetroTheme.White : PortraitPainter.Get(emp);
            _eIp.text = D.Unlocked(Features.IpCheck) ? S.Employees.GetIp(emp) : Locked;
            _eDept.text = $"{emp.department} / {emp.position}";
            _eWork.text = D.Unlocked(Features.WorkStatus) ? $"{(emp.onShift ? "勤務中" : "退勤")}（{emp.workHours}）" : Locked;
            _eEmployment.text = D.Unlocked(Features.EmploymentStatus) ? EmployeeData.EmploymentLabel(emp.employment) : Locked;
            _eVpn.text = (emp.vpnEnabled ? "利用できる" : "利用できない") + (string.IsNullOrEmpty(emp.laptopDeviceId) ? "" : $"（VPN端末 {emp.laptopDeviceId}）");
            if (D.Unlocked(Features.EmployeePermissions))
            {
                var perms = new List<string>();
                foreach (var pm in emp.permissions) if (pm != null) perms.Add(pm.displayName);
                _ePerms.text = string.Join("・", perms);
            }
            else _ePerms.text = Locked;
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
        public const int IpMatchIndex = 5;

        ToolList _queue;
        FlatButton _toggle;
        Text _count, _state;
        GameObject _warning;
        ToolLed _led;
        protected override float RefreshInterval => 0.5f;

        static CareerRecord Career => CareerRecord.Current;
        static bool On => Career.autoReviewEnabled && Career.autoReviewConditions.Count > 0;

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, "自動審査ツール", "選んだ条件をすべて満たす簡単な要求だけを、自動で許可します。許可した要求の責任はあなたにあります。", "check", ToolStyle.Green);
            var cg = ToolStyle.Box(p, 0, 0.58f, y, 268, "自動で許可する条件（すべて満たした要求だけ）");
            ToolStyle.Text(cg, "社内からの接続・管理系以外・警告なし は、いつも必須です", 0, 0, 340, 16, 11, ToolStyle.Muted);
            for (int i = 0; i < Conditions.Length; i++)
            {
                string cond = Conditions[i];
                bool available = i < 6 || D.Unlocked(Features.EmploymentStatus);
                var t = UIFactory.Check(cg, cond + (available ? "" : "（DAY12〜）"), Career.autoReviewConditions.Contains(cond), v =>
                {
                    if (v) { if (!Career.autoReviewConditions.Contains(cond)) Career.autoReviewConditions.Add(cond); }
                    else Career.autoReviewConditions.Remove(cond);
                    // 条件を全部外したら、自動許可も止める
                    if (Career.autoReviewConditions.Count == 0 && Career.autoReviewEnabled) SetEnabled(false, "条件がないため、自動許可を停止しました。");
                    RefreshState();
                });
                t.GetComponent<RectTransform>().Place(0, 22 + i * 28, 280, 22);
                if (t.LabelText != null) t.LabelText.fontSize = 13;
                if (!available) t.Interactable = false;
                if (i == IpMatchIndex) ToolStyle.BadgeRight(cg, "推奨", Tone.Info, 0, 23 + i * 28);
            }

            var sg = ToolStyle.Box(p, 0.58f, 1, y, 268, "自動許可");
            _led = ToolLed.Create(sg, 0, 5, 12);
            _state = ToolStyle.Text(sg, "", 18, 0, 200, 22, 15, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            _count = ToolStyle.Text(sg, "", 0, 28, 220, 20, 12, ToolStyle.Muted);
            _toggle = ToolStyle.Button(sg, "", 0, 58, 10, 38, Toggle, ToolStyle.Green);
            _toggle.Rect.StretchH(58, 38);
            var warn = UIFactory.Rect("Warn", sg);
            warn.StretchH(108, 92);
            _warning = warn.gameObject;
            var banner = ToolStyle.Banner(warn, "IP の照合がオフです。\n接続元を偽ったなりすまし接続も、自動で許可されます。", Tone.Warn, 0, 0, 10, 92);
            ((RectTransform)banner.transform.parent).Stretch();

            var qg = ToolStyle.Box(p, 0, 1, y + 278, 0, "待っている要求（期限の近い順）");
            _queue = ToolList.Create(qg, ToolStyle.Green, "待っている要求はありません。");
            S.Requests.Created += OnCreated;
            RefreshState();
            W.SetStatus(0, On ? "自動許可した要求の責任は、あなたにあります。" : "条件を選んでから「自動許可を始める」を押してください。");
            ProcessPending();
        }

        protected override void OnClosed() { if (D != null && D.S != null) D.S.Requests.Created -= OnCreated; }

        void Toggle()
        {
            if (On) { SetEnabled(false, "自動許可を停止しました。"); return; }
            if (Career.autoReviewConditions.Count == 0) { AudioFx.Error(); W.SetStatus(0, "条件を1つ以上選んでから始めてください。"); return; }
            SetEnabled(true, "自動許可を始めました。許可した要求の責任は、あなたにあります。");
        }

        void SetEnabled(bool v, string message)
        {
            Career.autoReviewEnabled = v;
            W.SetStatus(0, message);
            RefreshState();
            if (v) ProcessPending();
        }

        void RefreshState()
        {
            if (_state == null) return;
            bool on = On;
            _state.text = on ? "<color=#1F7F40>動作中</color>" : "<color=#667080>停止中</color>";
            if (on) _led.Set(ToolFx.LedGreen, 0); else _led.Off();
            _toggle.SetText(on ? "自動許可を止める" : "自動許可を始める");
            _toggle.SetAccent(on ? ToolStyle.Red : ToolStyle.Green, true);
            _warning.SetActive(!Career.autoReviewConditions.Contains(Conditions[IpMatchIndex]));
        }

        void OnCreated(EventInstance e) { if (e.State == EventState.Pending) TryAuto(e); }

        void ProcessPending()
        {
            foreach (var e in new List<EventInstance>(S.Requests.Pending)) TryAuto(e);
        }

        void TryAuto(EventInstance e)
        {
            if (!On) return;
            if (!Eligible(S, e, Career.autoReviewConditions)) return;
            e.AutoDecided = true;
            S.AutoAllowed++;
            S.Requests.Decide(e, PlayerAction.Allow);
            S.Logs.Add(S.Time.CurrentMinute, e.SourceIp, e.DestinationLabel, RequestManager.UserLabel(e), "CONN-REQ", "AUTO-ALLOWED", e.RequestId + " 自動審査ツール");
            if (_led) _led.Pulse(0.4f);
            if (_count) ToolFx.Flash(_count);
        }

        /// <summary>自動許可の対象か（社内・非管理系・警告なし ＋ 有効にした条件をすべて満たす）</summary>
        public static bool Eligible(ShiftSession s, EventInstance e, List<string> conds)
        {
            if (e.QuestTag != null) return false; // サブクエストの対象要求は、自動で処理しない
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
            bool expire = S.Day.requestsExpire;
            _queue.SetItems(list, e =>
            {
                int left = Mathf.Max(0, Mathf.CeilToInt(e.TimeoutAt - now));
                bool auto = Eligible(S, e, Career.autoReviewConditions);
                var row = new ToolList.Row($"{e.RequestId}　{e.DisplayUser}", $"{e.SourceLabel} → {e.DestinationLabel}　・{(auto ? "自動の対象" : "手動で審査")}",
                    expire ? $"残り {left} 分" : null, left <= 5 ? Tone.Bad : left <= 15 ? Tone.Warn : Tone.Info, e.NotifyAsSecurityAlert ? "warning" : "network");
                row.Blink = expire && left <= 5;
                return row;
            });
            _count.text = $"今夜の自動許可: {S.AutoAllowed} 件";
        }
    }

    // ============================================================ ログ解析
    public class LogAnalyzerTool : ToolWindow
    {
        GameObject _empty, _body;
        Text _who, _total, _recent, _fails;
        ToolList _rows;
        int _lastRecent = -1, _lastFails = -1;

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, "ログ解析", "選んだログの行と、同じ接続元・同じ利用者の記録を集めて数えます。", "log", ToolStyle.Indigo);
            _empty = ToolStyle.EmptyState(UIFactory.Rect("EmptyHost", p).Stretch(0, y, 0, 0), "ログの行が選ばれていません",
                "メインモニターの「システムログの検索・参照」で行を選ぶ", "同じ接続元・同じ利用者の記録が、ここに新しい順に集まります");
            _body = UIFactory.Rect("Body", p).Stretch(0, y, 0, 0).gameObject;
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

        protected override void Refresh()
        {
            var sel = D.SelectedLog;
            Show(_empty, _body, sel != null);
            if (sel == null) return;
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
            _who.text = $"接続元 <b>{sel.Source}</b>　　利用者 <b>{sel.User}</b>";
            _total.text = $"{list.Count} 件";
            _recent.text = $"{recent} 件";
            _fails.text = $"{fails} 件";
            if (_lastRecent >= 0 && recent > _lastRecent) ToolFx.Flash(_recent);
            if (_lastFails >= 0 && fails > _lastFails) ToolFx.Flash(_fails);
            _lastRecent = recent; _lastFails = fails;
            _rows.SetItems(list, e => new ToolList.Row($"{TimeManager.Format(e.Time)}　{e.EventType}", $"{e.Source} → {e.Destination}　{e.User}　{e.Detail}",
                e.Result, e.Result == "FAIL" ? Tone.Warn : Tone.Neutral, e.Result == "FAIL" ? "warning" : "log"));
        }
    }

    // ============================================================ 遮断コンソール
    public class FirewallConsoleTool : ToolWindow
    {
        InputField _range;
        ToolList _list;
        ToolLed _led;
        Text _auto;
        int _lastAuto = -1;

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, "遮断コンソール", "接続元のIPや範囲を、まとめて遮断します。遮断した接続元からの要求は自動で拒否されます。", "firewall", ToolStyle.Red);
            var ig = ToolStyle.Box(p, 0, 1, y, 146, "遮断する接続元");
            _range = ToolStyle.Input(ig, 0, 0, 280, 34);
            ToolStyle.Button(ig, "遮断する", 290, 0, 140, 34, BlockNow, ToolStyle.Red);
            ToolStyle.Text(ig, "選んだ接続要求・ログの接続元が自動で入ります。\n末尾を「*」にすると範囲で遮断します（例: 198.51.100.*）。", 0, 42, 440, 36, 11, ToolStyle.Muted);
            _led = ToolLed.Create(ig, 0, 88, 12);
            _auto = ToolStyle.Text(ig, "", 18, 84, 420, 20, 12, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            var lg = ToolStyle.Box(p, 0, 1, y + 156, 0, "遮断中の接続元");
            _list = ToolList.Create(lg, ToolStyle.Red, "まだ何も遮断していません。");
            W.SetStatus(0, "選んだ要求の接続元の範囲は自動で入力されます。");
            D.SelectionChanged += Prefill;
            Prefill();
        }

        void BlockNow()
        {
            string t = (_range.text ?? "").Trim();
            if (t.Length < 4) { AudioFx.Error(); W.SetStatus(0, "遮断する接続元を入力してください。"); return; }
            W.SetStatus(0, S.Security.Block(t));
            Refresh();
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
            _list.SetItems(new List<string>(S.Security.Blocked), x => new ToolList.Row(x, x.EndsWith("*") ? "範囲で遮断" : "1つの接続元を遮断", "遮断中", Tone.Bad, "lock"));
            // 遮断した接続元から来て、自動で拒否された要求の数（増えたらランプが光る）
            int n = 0;
            foreach (var e in S.Requests.Processed)
                if (e.AutoDecided && e.State == EventState.Blocked && S.Security.IsBlocked(e.SourceIp)) n++;
            _auto.text = $"遮断で自動拒否した要求: {n} 件";
            if (n > 0) _led.Set(ToolFx.LedRed, 0); else _led.Off();
            if (_lastAuto >= 0 && n > _lastAuto) { _led.Pulse(0.6f); ToolFx.Flash(_auto); }
            _lastAuto = n;
        }
    }

    // ============================================================ メール差出人チェッカー
    public class MailInspectorTool : ToolWindow
    {
        GameObject _empty, _body;
        Text _subject, _addr, _server, _domain;
        RectTransform _box;
        object _last;

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, "メール差出人チェッカー", "表示名ではなく、本当の差出人アドレスと送信元サーバーを大きく表示します。", "mail", ToolStyle.Orange);
            _empty = ToolStyle.EmptyState(UIFactory.Rect("EmptyHost", p).Stretch(0, y, 0, 0), "メールが選ばれていません",
                "メインモニターでメールを開く", "差出人の本当のアドレスが、ここに大きく出ます");
            _body = UIFactory.Rect("Body", p).Stretch(0, y, 0, 0).gameObject;
            var b = ToolStyle.Box(_body.transform, 0, 1, 0, 0);
            _box = b;
            var env = UIFactory.Image(b, RetroTheme.BigIcon("mail"), Color.white, "Envelope");
            env.rectTransform.AnchorTopRight(0, 30, 32, 32);
            _subject = ToolStyle.Field(b, "件名", 0, 0, 480, 70);
            ToolStyle.Text(b, "差出人アドレス", 0, 34, 300, 18, 12, ToolStyle.Muted, TextAnchor.MiddleLeft, true);
            _addr = ToolStyle.Text(b, "", 0, 54, 500, 34, 22, ToolStyle.Ink, TextAnchor.MiddleLeft, true, true);
            _addr.horizontalOverflow = HorizontalWrapMode.Overflow;
            _domain = ToolStyle.Text(b, "", 0, 92, 500, 20, 12, ToolStyle.Muted);
            _server = ToolStyle.Field(b, "送信元サーバー", 0, 124, 480, 110, true);
        }

        protected override void Refresh()
        {
            var m = D.SelectedMail;
            Show(_empty, _body, m != null);
            if (m == null) return;
            if (!ReferenceEquals(m, _last)) { _last = m; ToolFx.Sweep(_box, 0.6f); }
            _subject.text = m.Subject;
            string addr = string.IsNullOrEmpty(m.FromAddress) ? "(なし)" : m.FromAddress;
            int at = addr.IndexOf('@');
            // ドメイン（@ の後ろ）を目立たせる。社内のドメインと見比べやすいように
            _addr.text = at > 0 ? $"{addr.Substring(0, at)}<color=#B85A00>{addr.Substring(at)}</color>" : addr;
            _domain.text = $"社内のアドレスは  @{MailManager.CompanyDomain}";
            _server.text = string.IsNullOrEmpty(m.FromAddress) ? "-" : m.IsPhish ? "mail." + m.FromAddress.Substring(m.FromAddress.IndexOf('@') + 1) : "MAIL-SRV01 (10.10.0.20)";
        }
    }

    // ============================================================ リモート診断キット
    public class RemoteKitTool : ToolWindow
    {
        GameObject _empty, _body;
        Text _host;
        Text[] _vals;
        RectTransform[] _tiles;
        readonly List<GameObject> _badges = new List<GameObject>();
        ToolList _procs;
        ToolLed _link;
        static readonly string[] Labels = { "Wi-Fi", "IP アドレス", "DNS", "インターネット", "プリンター", "修正プログラム", "画面ロック", "接続" };
        static readonly string[] Icons = { "wifi", "network", "globe", "internet", "printer", "shield", "lock", "remote" };

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, "リモート診断キット", "リモート接続中のPCの状態を、1つの画面にまとめて表示します。", "remote", ToolStyle.Purple);
            _empty = ToolStyle.EmptyState(UIFactory.Rect("EmptyHost", p).Stretch(0, y, 0, 0), "リモート接続中のPCはありません",
                "メインモニターの「ITサポート受付管理」で問い合わせを開く", "「リモート接続」を押す", "PCの状態がここにまとめて出ます");
            _body = UIFactory.Rect("Body", p).Stretch(0, y, 0, 0).gameObject;
            var top = ToolStyle.Box(_body.transform, 0, 1, 0, 180);
            _link = ToolLed.Create(top, 0, 5, 12);
            _link.Set(ToolFx.LedGreen, 3f);
            _host = ToolStyle.Text(top, "", 18, 0, 400, 22, 15, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            var traffic = ToolMeter.Create(top, 14);
            traffic.Rect.AnchorTopRight(0, 0, 130, 22);
            traffic.Level = 0.35f; traffic.Jitter = 0.3f;
            ToolStyle.Text(top, "通信量", 11, ToolStyle.Muted, TextAnchor.MiddleRight).rectTransform.AnchorTopRight(136, 2, 60, 18);
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

        protected override void Refresh()
        {
            var session = S.HelpDesk.ActiveSession;
            bool on = session != null && session.Connected;
            Show(_empty, _body, on);
            if (!on) return;
            var pc = session.PC;
            _host.text = $"{pc.Hostname}　<size=12><color=#666E7C>{pc.UserDisplay}</color></size>";
            foreach (var b in _badges) if (b) Destroy(b);
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
            SetTile(7, "リモート接続中", "接続中", Tone.Info);
            var procs = new List<RemotePC.VirtualProcess>();
            foreach (var pr in pc.RunningProcesses) if (!pr.System) procs.Add(pr);
            _procs.SetItems(procs, pr => new ToolList.Row(pr.Name, $"{pr.MemoryKb:N0} KB", null, Tone.Neutral, "run"));
        }
    }

    // ============================================================ 端末隔離ツール
    public class IsolatorTool : ToolWindow
    {
        ToolList _list;
        Text _sel;
        FlatButton _btn;

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, "端末隔離ツール", "選んだ端末をネットワークから切り離します。その端末の仕事も止まるので、慎重に。", "lock", ToolStyle.Red);
            var top = ToolStyle.Box(p, 0, 1, y, 74, "選んだ端末");
            _sel = ToolStyle.Text(top, "", 0, 0, 300, 32, 15, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            _btn = ToolStyle.Button(top, "この端末を隔離する", 0, 0, 170, 32, IsolateSelected, ToolStyle.Red);
            _btn.Rect.AnchorTopRight(0, 0, 170, 32);
            var lg = ToolStyle.Box(p, 0, 1, y + 84, 0, "社内の端末（選んだ接続要求の端末は自動で選ばれます）");
            _list = ToolList.Create(lg, ToolStyle.Red, "端末がありません。");
            _list.SelectionChanged += _ => RefreshSelection();
            D.SelectionChanged += FollowSelection;
        }

        protected override void OnClosed() { if (D != null) D.SelectionChanged -= FollowSelection; }

        /// <summary>メインモニターで選んだ要求・ログの端末を選んでおく（隔離するかはプレイヤーが決める）</summary>
        void FollowSelection()
        {
            string device = D.SelectedRequest?.Device;
            string ip = D.SelectedLog?.Source;
            foreach (var e in S.Employees.All)
                if (e.deviceId == device || (ip != null && S.Employees.GetIp(e) == ip)) { _list.Select(e); return; }
        }

        void IsolateSelected()
        {
            if (!(_list.Selected is EmployeeData e)) { AudioFx.Error(); W.SetStatus(0, "隔離する端末を選んでください。"); return; }
            D.ShowModal("端末の隔離", $"{e.deviceId}（{e.DisplayName}）をネットワークから切り離します。\nよろしいですか？", "lock", "隔離する", () =>
            {
                W.SetStatus(0, S.Security.Isolate(e.deviceId));
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
                _sel.text = "<color=#8890A0>一覧から端末を選んでください</color>";
                _btn.Interactable = false;
                _btn.SetText("この端末を隔離する");
            }
        }

        protected override void Refresh()
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

    // ============================================================ サーバー監視ダッシュボード
    public class ServerMonitorTool : ToolWindow
    {
        RectTransform _content;
        string _signature;
        protected override float RefreshInterval => 2f;

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, "サーバー監視ダッシュボード", "全サーバーの状態・サービス・動いているプログラム（発行元）を一覧にします。", "server", ToolStyle.Slate);
            var host = UIFactory.Rect("Host", p).Stretch(ToolStyle.Pad, y, ToolStyle.Pad / 2, ToolStyle.Pad);
            _content = ToolStyle.ScrollArea(host, out _);
        }

        static Tone StatusTone(string s) => s == "稼働中" ? Tone.Ok : s == "改ざん" ? Tone.Bad : s == "復元中" ? Tone.Info : Tone.Warn;

        protected override void Refresh()
        {
            if (_content == null) return;
            var sig = new StringBuilder();
            foreach (var st in S.Servers.Servers)
            {
                sig.Append(st.Id).Append(st.StatusLabel);
                foreach (var svc in st.Services) sig.Append(svc.Name).Append(svc.Running);
                foreach (var pr in st.Processes) sig.Append(pr.Name);
            }
            if (sig.ToString() == _signature) return;
            _signature = sig.ToString();
            foreach (Transform t in _content) Destroy(t.gameObject);

            // 2列に並べる（各列の高さを積み上げる）
            float[] colY = { 0, 0 };
            foreach (var st in S.Servers.Servers)
            {
                int col = colY[0] <= colY[1] ? 0 : 1;
                float h = 64 + st.Services.Count * 24 + (st.Processes.Count > 0 ? 22 + st.Processes.Count * 20 : 0);
                var card = UIFactory.Image(_content, ToolStyle.Outline, Color.white, "Server");
                ToolStyle.Cell(card.rectTransform, col, 2, colY[col], h, 10);
                var c = card.transform;
                var sic = UIFactory.Image(c, RetroTheme.Icon("server"), Color.white, "Icon");
                sic.rectTransform.Place(12, 12, 16, 16);
                ToolStyle.Text(c, st.Id, 32, 10, 200, 20, 14, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
                ToolStyle.BadgeRight(c, st.StatusLabel, StatusTone(st.StatusLabel), 10, 10);
                var load = ToolMeter.Create(c, 10);
                load.Rect.AnchorTopRight(80, 10, 64, 18);
                load.Level = st.StatusLabel == "復元中" ? 0.75f : 0.2f;
                float y = 40;
                foreach (var svc in st.Services)
                {
                    var led = ToolLed.Create(c, 12, y + 4, 12);
                    if (svc.Running) led.Set(ToolFx.LedGreen, 0); else led.Set(ToolFx.LedRed, 1f);
                    ToolStyle.Text(c, svc.Name, 30, y, 200, 20, 12, ToolStyle.Ink, TextAnchor.MiddleLeft);
                    ToolStyle.BadgeRight(c, svc.Running ? "実行中" : "停止", svc.Running ? Tone.Ok : Tone.Bad, 10, y);
                    y += 24;
                }
                if (st.Processes.Count > 0)
                {
                    ToolStyle.Text(c, "動いているプログラム（発行元）", 12, y + 2, 300, 18, 11, ToolStyle.Muted, TextAnchor.MiddleLeft, true);
                    y += 22;
                    foreach (var pr in st.Processes)
                    {
                        ToolStyle.Text(c, $"{pr.Name}　<color=#666E7C>{pr.Publisher}</color>", 12, y, 300, 18, 12, ToolStyle.Ink, TextAnchor.MiddleLeft, false, true);
                        y += 20;
                    }
                }
                colY[col] += h + 10;
            }
            _content.sizeDelta = new Vector2(0, Mathf.Max(colY[0], colY[1]));
        }
    }

    // ============================================================ 相関タイムライン
    public class CorrelatorTool : ToolWindow
    {
        Text _normal, _text;
        protected override float RefreshInterval => 2f;

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, "相関タイムライン", "社外からの接続を、IPの範囲ごとに時間順に並べます。関係があるかを判断するのはあなたです。", "clock", ToolStyle.Teal);
            var top = ToolStyle.Box(p, 0, 1, y, 66, "選んだ要求の利用者の「ふだん」");
            _normal = ToolStyle.Text(top, "", 0, 0, 640, 22, 13, ToolStyle.Ink, TextAnchor.MiddleLeft);
            var lg = ToolStyle.Box(p, 0, 1, y + 76, 0, "社外の接続元（範囲ごと・2件以上）");
            _text = ToolStyle.ScrollText(lg, 13, true);
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
            _normal.text = e != null && e.Employee != null
                ? $"<b>{e.Employee.DisplayName}</b>　勤務時間 {e.Employee.workHours}　ふだん使うシステム {string.Join("・", e.Employee.normalAccessTargets.ConvertAll(EmployeeDetailView.ServiceLabel))}"
                : "<color=#8890A0>メインモニターで接続要求を選ぶと、その利用者のふだんの使い方が出ます。</color>";
            // 社外の接続元を範囲ごとに時系列で並べる（関連があるかの判断はプレイヤー）
            var groups = new Dictionary<string, List<LogEntry>>();
            foreach (var l in S.Logs.Entries)
            {
                if (l.Source == null || l.Source.StartsWith("10.") || l.Source.Length == 0 || !char.IsDigit(l.Source[0])) continue;
                string r = Range(l.Source);
                if (!groups.TryGetValue(r, out var list)) groups[r] = list = new List<LogEntry>();
                list.Add(l);
            }
            var sb = new StringBuilder();
            foreach (var kv in groups)
            {
                if (kv.Value.Count < 2) continue;
                sb.AppendLine($"<b><color=#0D7888>{kv.Key}</color></b>　{kv.Value.Count} 件");
                int from = Mathf.Max(0, kv.Value.Count - 6);
                for (int i = from; i < kv.Value.Count; i++)
                {
                    var l = kv.Value[i];
                    sb.AppendLine($"  {TimeManager.Format(l.Time)}  {l.Source} → {EmployeeDetailView.ServiceLabel(l.Destination)}  {l.EventType} {l.Result}  <color=#666E7C>{l.Detail}</color>");
                }
                sb.AppendLine();
            }
            _text.text = sb.Length == 0 ? "<color=#8890A0>社外からの接続の記録は、まだありません。</color>" : sb.ToString();
        }
    }
}
