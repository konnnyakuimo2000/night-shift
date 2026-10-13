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
    /// アプリと購入の権利の対応は ToolApps（ログ解析＋相関、端末管理＝診断＋隔離 は1つのアプリのタブ）。
    /// 例外は 自動審査ツール（条件をすべて満たす簡単な要求だけ自動で許可）、VACCINE 99（既知のウイルスだけ検出）、SOC-AI（提案・代行）。
    /// 見た目は業務システムと違う「市販ソフト」風（ToolStyle）: 何をするツールかを見出しに書き、選んでいないときは手順を案内する。
    /// </summary>
    public static class Tools
    {
        public static void Launch(WorkstationScreen ws, string id)
        {
            var d = ws.Desktop;
            // 販売を終えて業務システムに統合したもの: その業務画面を開く
            if (id == Items.FwConsole) { d.ShowStatus("接続元の遮断（範囲指定）は、接続監視・システムログの「接続元を遮断」に統合されました。"); d.OpenLogViewer(); return; }
            if (id == Items.MailInspector) { d.ShowStatus("差出人の詳細は、メールの「差出人の詳細」に統合されました。"); d.OpenMailbox(null); return; }
            var app = ToolApps.ForItem(id);
            if (id == Items.VaccineServer) app = ToolApps.ForItem(Items.Vaccine); // 追加ライセンスは VACCINE 99 のボタンが増えるだけ
            if (app == null || !ToolApps.Usable(d, app)) { AudioFx.Error(); return; }
            switch (app.Id)
            {
                case Items.ProfilePanel: Open<ProfilePanelTool>(ws, app, 680, 470); break;
                case Items.AutoReview: Open<AutoReviewTool>(ws, app, 640, 620); break;
                case Items.LogAnalyzer:
                {
                    var t = Open<LogAnalyzerApp>(ws, app, 680, 600);
                    if (t != null && id == Items.Correlator) t.Select(1); // 相関タイムラインのアイコンから開いたら相関タブ
                    break;
                }
                case Items.RemoteKit:
                {
                    var t = Open<DeviceManagerApp>(ws, app, 620, 600);
                    if (t != null && id == Items.Isolator) t.Select(0);
                    break;
                }
                case Items.ServerMonitor: Open<ServerMonitorTool>(ws, app, 700, 580); break;
                case Items.Vaccine: Open<VaccineTool>(ws, app, 700, 600); break;
                case Items.SocAi: Open<SocAiTool>(ws, app, 660, 640); break;
                case Items.GambleTerminal: Open<NightClubTool>(ws, app, 600, 700); break;
                case Items.FxTerminal: Open<MidnightFxTool>(ws, app, 700, 760); break;
                case Items.SignalDesk: Open<SignalDeskTool>(ws, app, 700, 620); break;
            }
        }

        /// <summary>アプリのウィンドウを開く（すでに開いていれば、そのウィンドウ）</summary>
        static T Open<T>(WorkstationScreen ws, ToolApps.App app, float w, float h) where T : ToolWindow
        {
            var win = ws.Windows.Open(new WindowSpec { Id = "tool:" + app.Id, Title = app.Name, Icon = app.Icon, Width = w, Height = h, StatusCells = 1 }, out bool created);
            if (!created) return win.GetComponent<T>();
            var t = win.gameObject.AddComponent<T>();
            t.Init(ws.Desktop, win, app.Id);
            return t;
        }
    }

    /// <summary>ツールの共通部分（メインモニターの選択が変わると更新）</summary>
    public abstract class ToolWindow : MonoBehaviour
    {
        protected ShiftDesktop D;
        protected RetroWindow W;
        protected string ToolId;
        public ShiftDesktop Desk => D;
        public RetroWindow Win => W;
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
                // 負荷・通信量は測っていないので、メーターは出さない（状態だけを言葉で出す）
                int stopped = st.Services.FindAll(x => !x.Running).Count;
                ToolStyle.Text(c, st.Restoring ? "復元中（サービス停止）" : stopped > 0 ? $"停止中のサービス {stopped}" : "サービスはすべて実行中", 11, ToolStyle.Muted, TextAnchor.MiddleRight)
                    .rectTransform.AnchorTopRight(80, 11, 160, 18);
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
}
