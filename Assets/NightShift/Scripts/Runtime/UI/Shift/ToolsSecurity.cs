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
    /// <summary>ウイルスの情報（VirusCatalog の窓口。名前・定義日は画面ごとに持たない）</summary>
    public static class VirusInfo
    {
        public static string Name(string id) => VirusCatalog.Name(id);
        public static int DefinitionDay(string id) => VirusCatalog.DefinitionDay(id);
        public static bool Detectable(string id) => VirusCatalog.Detectable(id);
    }

    // ============================================================ VACCINE 99
    /// <summary>
    /// VACCINE 99。検査そのものは ShiftSession.Vaccine（VaccineScanner）が持ち、この画面は表示と操作だけ。
    /// Client とサーバー検査の追加ライセンスは同じアプリ（ライセンスがあるとサーバーの検査ボタンが使える）
    /// </summary>
    public class VaccineTool : ToolWindow
    {
        Text _status, _defs, _scanLabel, _result;
        Image _progress, _resultIcon;
        ToolSpinner _spinner;
        ToolPulse _shieldPulse;
        static readonly string[] Paths =
        {
            "C:\\WINDOWS\\SYSTEM\\KERNEL32.DLL", "C:\\WINDOWS\\SYSTEM\\USER32.DLL", "C:\\WINDOWS\\EXPLORER.EXE",
            "C:\\WINDOWS\\SYSTEM\\MSVCRT.DLL", "C:\\PROGRA~1\\NEXUS\\NXAGENT.EXE", "C:\\WINDOWS\\TEMP\\~DF12A3.TMP",
            "C:\\MY DOCUMENTS\\見積書_0520.DOC", "C:\\WINDOWS\\SYSTEM\\WINSOCK.DLL", "C:\\AUTOEXEC.BAT", "C:\\CONFIG.SYS",
            "D:\\SHARE\\営業日報_5月.XLS", "C:\\WINDOWS\\SYSTEM\\SVCHOST.EXE", "C:\\WINDOWS\\START MENU\\STARTUP"
        };
        FlatButton _ws, _remote, _server, _tabScan, _tabDex;
        RectTransform _scanRoot, _dexRoot;
        int _shownSerial = -1;

        void ShowTab(bool dex)
        {
            _tabScan.SetLatched(!dex);
            _tabDex.SetLatched(dex);
            _scanRoot.gameObject.SetActive(!dex);
            _dexRoot.gameObject.SetActive(dex);
            if (!dex) return;
            foreach (Transform t in _dexRoot) Destroy(t.gameObject);
            var host = UIFactory.Rect("Host", _dexRoot).Stretch(ToolStyle.Pad, 0, ToolStyle.Pad / 2, ToolStyle.Pad);
            var content = ToolStyle.ScrollArea(host, out _);
            float w = Mathf.Max(300, ((RectTransform)W.Content).rect.width - ToolStyle.Pad * 2 - 24);
            float h = VirusDexView.Build(content, w, S);
            content.sizeDelta = new Vector2(0, h);
        }
        protected override float RefreshInterval => 0.25f;

        VaccineScanner Scanner => S.Vaccine;

        protected override void Build(RectTransform p)
        {
            float hy = ToolStyle.Header(p, "VACCINE 99", "既知のウイルスを見つけて駆除します。定義ファイルにないウイルスは見つけられません。", "shield", ToolStyle.Green);
            // タブ: 検査 ／ 感染記録（図鑑。閲覧は検査・駆除と別。何も起きない）
            var bar = UIFactory.Rect("Tabs", p);
            bar.StretchH(hy, 30, ToolStyle.Pad, ToolStyle.Pad);
            _tabScan = FlatButton.Create(bar, "検査", () => ShowTab(false), ToolStyle.Green, false);
            ToolStyle.Cell(_tabScan.Rect, 0, 2, 0, 28);
            _tabDex = FlatButton.Create(bar, "感染記録", () => ShowTab(true), ToolStyle.Green, false);
            ToolStyle.Cell(_tabDex.Rect, 1, 2, 0, 28);
            _dexRoot = UIFactory.Rect("Dex", p).Stretch(0, hy + 36, 0, 0);
            var scan = UIFactory.Rect("Scan", p).Stretch(0, hy + 36, 0, 0);
            _scanRoot = scan;
            p = scan;
            float y = 0;
            var top = ToolStyle.Box(p, 0, 1, y, 84);
            var shield = UIFactory.Image(top, RetroTheme.BigIcon("shield"), Color.white, "Shield");
            shield.rectTransform.Place(0, 6, 48, 48);
            _shieldPulse = shield.gameObject.AddComponent<ToolPulse>();
            // 検査中は虫眼鏡が回る
            var spin = UIFactory.Rect("Spinner", top);
            spin.AnchorTopRight(4, 4, 48, 48);
            _spinner = ToolSpinner.Create(spin, "search", 32, 7);
            _status = ToolStyle.Text(top, "", 62, 4, 400, 24, 16, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            _defs = ToolStyle.Text(top, "", 62, 32, 420, 20, 12, ToolStyle.Muted, TextAnchor.MiddleLeft);

            var sg = ToolStyle.Box(p, 0, 1, y + 94, 118, "検査する場所");
            var row = UIFactory.Rect("Row", sg).Stretch();
            _ws = ScanButton(row, 0, "ワークステーション\n（約5分）", () => Begin(ScanTarget.Workstation));
            _remote = ScanButton(row, 1, "リモート接続中のPC\n（約2分）", () => Begin(ScanTarget.Remote));
            _server = ScanButton(row, 2, "サーバー\n（約5分・追加ライセンス）", () => Begin(ScanTarget.Server));
            _progress = ToolStyle.Progress(row, 0, 56, 10, 12, ToolStyle.Green);
            ((RectTransform)_progress.transform.parent).StretchH(58, 10);
            _scanLabel = ToolStyle.Text(row, "", 0, 70, 480, 18, 11, ToolStyle.Muted);

            var rg = ToolStyle.Box(p, 0, 1, y + 222, 0, "検査結果");
            var resBg = UIFactory.Image(rg, RetroTheme.Field, Color.white, "ResultBg");
            resBg.rectTransform.Stretch();
            _resultIcon = UIFactory.Image(resBg.transform, RetroTheme.BigIcon("info"), Color.white, "ResultIcon");
            _resultIcon.rectTransform.Place(8, 8, 32, 32);
            _result = ToolStyle.Text(resBg.transform, "まだ検査していません。", 13, ToolStyle.Ink);
            _result.rectTransform.Stretch(50, 10, 8, 6);
            ShowTab(false);
        }

        FlatButton ScanButton(RectTransform row, int i, string label, System.Action a)
        {
            var b = FlatButton.Create(row, label, a, ToolStyle.Green, false);
            ToolStyle.Cell(b.Rect, i, 3, 0, 50);
            b.Label.fontSize = 12;
            return b;
        }

        void Begin(ScanTarget target)
        {
            string msg = Scanner.Begin(target);
            W.SetStatus(0, msg);
            if (!Scanner.Running) { AudioFx.Error(); return; }
            _result.text = $"検査中…（対象: {Scanner.TargetHost}）";
            _resultIcon.sprite = RetroTheme.BigIcon("search");
        }

        protected override void Refresh()
        {
            if (_status == null) return;
            var career = CareerRecord.Current;
            int today = S.Day.dayNumber;
            int defDay = career.vaccineDefinitionDay;
            bool server = career.ToolActive(Items.VaccineServer);
            _status.text = "<color=#1F7F40>●</color> 常駐監視: 有効" + (server ? "　<size=12><color=#666E7C>サーバー検査ライセンス: あり</color></size>" : "");
            _defs.text = defDay > 0 ? $"定義ファイル: DAY{defDay} 版　（今日は DAY{today}）" : "定義ファイル: なし";
            if (defDay > 0 && today - defDay >= 3) _defs.text += $"　<color=#B07000>— {today - defDay} 日更新していません</color>";
            bool running = Scanner.Running;
            _server.Interactable = server && !running;
            _ws.Interactable = _remote.Interactable = !running;
            if (running)
            {
                float k = Mathf.InverseLerp(Scanner.StartedAt, Scanner.DoneAt, S.Time.CurrentMinute);
                _progress.fillAmount = k;
                // 当時のウイルス対策ソフトのように、調べているファイル名が次々に流れる
                _scanLabel.text = $"{Mathf.RoundToInt(k * 100)}%　{Scanner.TargetHost}　{Paths[Random.Range(0, Paths.Length)]}";
                _spinner.Running = true;
            }
            else
            {
                _progress.fillAmount = 0;
                _spinner.Running = false;
                _scanLabel.text = server ? "" : "サーバーの検査には「サーバー検査の追加ライセンス」が必要です。";
            }
            ShowResult(Scanner.Last);
        }

        void ShowResult(VaccineScanner.ScanResult r)
        {
            if (r == null || r.Serial == _shownSerial) return;
            _shownSerial = r.Serial;
            if (!r.Completed)
            {
                // 中断は「見つからなかった」とは表示しない
                _result.text = $"<color=#B07000><b>検査を中断しました（未完了）。</b></color>\n対象: {r.Host}　理由: {r.AbortReason}\nウイルスの有無は確認できていません。";
                _resultIcon.sprite = RetroTheme.BigIcon("warning");
                W.SetStatus(0, "検査を中断しました。");
                return;
            }
            _result.text = r.Found.Count == 0
                ? $"<color=#1F7F40><b>ウイルスは見つかりませんでした。</b></color>（対象: {r.Host}）\n（定義ファイルに載っているウイルスだけを調べています）"
                : $"<color=#B02020><b>ウイルスを見つけました。</b></color>（対象: {r.Host}）\n" + string.Join("\n", r.Found);
            _resultIcon.sprite = RetroTheme.BigIcon(r.Found.Count == 0 ? "check" : "warning");
            ToolFx.Flash(_resultIcon);
            W.SetStatus(0, "検査が終わりました。");
            // 定義ファイルが古いと見逃す（「見つかりませんでした」の直後に画面の端が化ける演出は HorrorFx 側）
            if (r.MissedUnknown) D.RequestGlitch();
        }
    }

    // ============================================================ SOC-AI
    /// <summary>
    /// サイバー防御AI。持っているツールをプラグインとして使い、提案・代行する。1夜 30,000 トークン（＋追加分）。
    /// 正答率は約9割。備考欄の偽の指示（T15）に騙される。
    /// </summary>
    public class SocAiTool : ToolWindow
    {
        Text _chat, _tokens, _proposalText;
        Image _meter;
        RetroToggle _auto;
        FlatButton _approve, _reject;
        ToolSpinner _spinner;
        ToolLed _autoLed;
        Text _thinking;
        readonly List<FlatButton> _asks = new List<FlatButton>();
        readonly List<AiTask> _askTasks = new List<AiTask>();
        EventInstance _proposal;
        bool _proposalAllow;
        readonly StringBuilder _log = new StringBuilder();
        public const int BaseTokens = SocAiService.BaseTokens;

        SocAiService Ai => S.SocAi;

        protected override void Build(RectTransform p)
        {
            Ai.VisibleDetail = l => LogViewerView.VisibleDetail(S, l);
            float y = ToolStyle.Header(p, "SOC-AI", "持っているツールを使って、調査・提案・代行をします。1夜に使えるトークンには限りがあります。", "debug", ToolStyle.Violet);
            var tg = ToolStyle.Box(p, 0, 1, y, 58);
            _tokens = ToolStyle.Text(tg, "", 0, 0, 600, 18, 12, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            _meter = ToolStyle.Progress(tg, 0, 22, 10, 12, ToolStyle.Violet);
            ((RectTransform)_meter.transform.parent).StretchH(22, 12);

            // 下から: 依頼ボタン → 提案 → 全自動
            var bottom = ToolStyle.BoxBottom(p, 0, 1, ToolStyle.Pad, 176, "SOC-AI に頼む");
            var row = UIFactory.Rect("Row", bottom).Stretch();
            Ask(row, 0, "選んだ要求を審査", AiTask.Review);
            Ask(row, 1, "ログを調査", AiTask.Logs);
            Ask(row, 2, "感染の疑いを探す", AiTask.Infections);
            var prop = UIFactory.Image(row, ToolStyle.Round, Color.white, "Proposal");
            prop.rectTransform.StretchH(56, 44);
            _proposalText = ToolStyle.Text(prop.transform, "", 12, ToolStyle.Ink, TextAnchor.MiddleLeft);
            _proposalText.rectTransform.Stretch(10, 2, 230, 2);
            _approve = ToolStyle.Button(prop.transform, "提案を承認", 0, 0, 106, 32, Approve, ToolStyle.Violet);
            _approve.Rect.AnchorTopRight(116, 6, 106, 32);
            _reject = ToolStyle.Button(prop.transform, "却下", 0, 0, 104, 32, () => { _proposal = null; Say("提案を却下しました。"); RefreshProposal(); }, ToolStyle.Violet, false);
            _reject.Rect.AnchorTopRight(6, 6, 104, 32);
            _auto = UIFactory.Check(row, "全自動モード（承認なしで実行・消費 1.5 倍・自動審査ツールが必要）", false, v =>
            {
                if (v && !SocAiService.CanAuto) { _auto.SetValue(false, false); Say("全自動モードには、有効な自動審査ツールが必要です。"); AudioFx.Error(); }
            });
            _auto.GetComponent<RectTransform>().Place(0, 108, 520, 22);
            if (_auto.LabelText != null) _auto.LabelText.fontSize = 12;
            _autoLed = ToolLed.Create(row, 330, 113, 12);

            var cg = ToolStyle.Box(p, 0, 1, y + 68, 0, "SOC-AI とのやりとり", ToolStyle.Pad + 186);
            var chatBg = UIFactory.Image(cg, RetroTheme.Field, Color.white, "ChatBg");
            chatBg.rectTransform.Stretch();
            _chat = ToolStyle.Text(chatBg.transform, "", 12, ToolStyle.Ink, TextAnchor.LowerLeft);
            _chat.rectTransform.Stretch(8, 6, 8, 6);
            // 考えている間: 虫眼鏡が回り「考えています…」
            var spin = UIFactory.Rect("Spinner", chatBg.transform);
            spin.AnchorTopRight(8, 6, 44, 44);
            _spinner = ToolSpinner.Create(spin, "search", 32, 6);
            spin.gameObject.SetActive(false);
            _thinking = ToolStyle.Text(chatBg.transform, "", 12, ToolStyle.Muted, TextAnchor.MiddleRight, true);
            _thinking.rectTransform.AnchorTopRight(56, 18, 200, 20);
            S.Requests.Created += OnCreated;
            Say("SOC-AI です。使えるプラグイン: " + Plugins());
            RefreshProposal();
        }

        void Ask(RectTransform row, int i, string label, AiTask task)
        {
            var b = FlatButton.Create(row, $"{label}\n<size=11>{SocAiService.CostOf(task):N0} トークン</size>", () => Request(task), ToolStyle.Violet, false);
            ToolStyle.Cell(b.Rect, i, 3, 0, 48);
            b.Label.fontSize = 13;
            _asks.Add(b);
            _askTasks.Add(task);
        }

        /// <summary>依頼する: 受付時に対象を固定し、確かめてからトークンを使う</summary>
        void Request(AiTask task)
        {
            var job = Ai.Ask(task, D.SelectedRequest, out string error);
            if (job == null) { Say(error); AudioFx.Error(); return; }
            if (job.Target != null) Say($"{job.Target.RequestId} を審査します。");
            Think(job);
        }

        /// <summary>少し考えてから答える（その間は虫眼鏡が回り、ほかの依頼はできない）</summary>
        void Think(AiJob job)
        {
            _spinner.transform.gameObject.SetActive(true);
            _spinner.Running = true;
            _thinking.text = "考えています…";
            ToolFx.Flash(_tokens);
            Screens.Delay.Run(gameObject, 1.2f, () =>
            {
                _spinner.Running = false;
                _spinner.transform.gameObject.SetActive(false);
                _thinking.text = "";
                var ans = Ai.Complete(job);
                if (ans == null) return;
                Say(ans.Message);
                if (ans.Proposal != null) { _proposal = ans.Proposal; _proposalAllow = ans.ProposeAllow; }
                RefreshProposal();
            });
        }

        protected override void OnClosed()
        {
            if (D == null || D.S == null) return;
            D.S.Requests.Created -= OnCreated;
            D.S.SocAi.Cancel(); // 考えている途中で閉じたら、実行せずにトークンを返す
        }

        string Plugins()
        {
            var names = ToolApps.ActiveWorkAppNames();
            return names.Count == 0 ? "（なし。ツールを買うとできることが増えます）" : string.Join("・", names);
        }

        void Say(string msg)
        {
            _log.AppendLine($"<color=#7A6A90>[{S.Time.Now}]</color> {msg}");
            var lines = _log.ToString().Split('\n');
            if (lines.Length > 14) { _log.Clear(); for (int i = lines.Length - 14; i < lines.Length; i++) if (lines[i].Length > 0) _log.AppendLine(lines[i]); }
            _chat.text = _log.ToString();
        }

        void RefreshProposal()
        {
            if (_proposalText == null) return;
            bool has = _proposal != null && _proposal.State == EventState.Pending;
            _proposalText.text = has
                ? $"提案: <b>{_proposal.RequestId}</b> を <b>{(_proposalAllow ? "許可" : "拒否")}</b>"
                : "<color=#8890A0>今は提案がありません。「選んだ要求を審査」を押すと、ここに出ます。</color>";
            _approve.Interactable = has;
            _reject.Interactable = has;
        }

        void Approve()
        {
            if (_proposal == null || _proposal.State != EventState.Pending) { Say("承認する提案がありません。"); RefreshProposal(); return; }
            var e = _proposal;
            _proposal = null;
            e.AutoDecided = true;
            S.AiHandled++;
            S.Requests.Decide(e, _proposalAllow ? PlayerAction.Allow : PlayerAction.Block);
            Say($"{e.RequestId} を{(e.State == EventState.Allowed ? "許可" : "拒否")}しました。");
            RefreshProposal();
        }

        void OnCreated(EventInstance e)
        {
            if (_auto == null || !_auto.Value) return;
            string msg = Ai.AutoDecide(e, out bool outOfTokens);
            if (outOfTokens) { _auto.SetValue(false, false); Say("トークンが足りないため、全自動モードを止めました。"); return; }
            if (msg == null) return;
            Say(msg);
            if (_autoLed) _autoLed.Pulse(0.4f);
        }

        protected override void Refresh()
        {
            if (_tokens == null) return;
            int left = Ai.Left, budget = Ai.Budget;
            _tokens.text = $"残りトークン　{Mathf.Max(0, left):N0} / {budget:N0}";
            _meter.fillAmount = budget > 0 ? Mathf.Clamp01((float)left / budget) : 0;
            if (_auto != null && _auto.Value && !SocAiService.CanAuto) { _auto.SetValue(false, false); Say("自動審査ツールが無効になったため、全自動モードを止めました。"); }
            if (_auto != null && _auto.Value) _autoLed.Set(ToolFx.LedAmber, 1.5f); else _autoLed.Off();
            // 必要なプラグインがない依頼・考え中は押せない（押しても消費しない）
            bool busy = Ai.Pending != null;
            for (int i = 0; i < _asks.Count; i++) _asks[i].Interactable = !busy && SocAiService.HasPlugin(_askTasks[i]);
            if (_proposal != null && _proposal.State != EventState.Pending) { _proposal = null; RefreshProposal(); }
        }
    }
}
