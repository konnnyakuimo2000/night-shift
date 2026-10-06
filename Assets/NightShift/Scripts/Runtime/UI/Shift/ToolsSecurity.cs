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
    /// <summary>ウイルスの情報（VIRUS_CATALOG.md）。定義ファイルで検出できるのは、解禁日までに出たものだけ。</summary>
    public static class VirusInfo
    {
        public static string Name(string id)
        {
            switch (id)
            {
                case "V1": return "TINNITUS"; case "V2": return "GARBLE"; case "V3": return "NIGHTCRY"; case "V4": return "BLUESTONE";
                case "V5": return "REDACT"; case "V6": return "BRIAR ROSE"; case "V7": return "DOPPELGANGER"; case "V8": return "DAYBREAK";
                default: return id;
            }
        }

        /// <summary>定義ファイルに載る日（-1 = ウイルスではないので検出できない）</summary>
        public static int DefinitionDay(string id)
        {
            switch (id)
            {
                case "V1": return 9; case "V4": return 13; case "V3": return 16; case "V2": return 17; case "V6": return 20;
                default: return -1;
            }
        }

        public static bool Detectable(string id) { int day = DefinitionDay(id); return day > 0 && CareerRecord.Current.vaccineDefinitionDay >= day; }
    }

    // ============================================================ VACCINE 99
    public class VaccineTool : ToolWindow
    {
        Text _status, _defs, _scanLabel, _result;
        Image _progress;
        FlatButton _ws, _remote, _server;
        string _scanTarget;
        float _scanStart = -1, _scanDoneAt = -1;
        protected override float RefreshInterval => 0.25f;

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, "VACCINE 99", "既知のウイルスを見つけて駆除します。定義ファイルにないウイルスは見つけられません。", "shield", ToolStyle.Green);
            var top = ToolStyle.Box(p, 0, 1, y, 84);
            var shield = UIFactory.Image(top, RetroTheme.BigIcon("shield"), Color.white, "Shield");
            shield.rectTransform.Place(0, 6, 48, 48);
            _status = ToolStyle.Text(top, "", 62, 4, 400, 24, 16, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            _defs = ToolStyle.Text(top, "", 62, 32, 420, 20, 12, ToolStyle.Muted, TextAnchor.MiddleLeft);

            var sg = ToolStyle.Box(p, 0, 1, y + 94, 118, "検査する場所");
            var row = UIFactory.Rect("Row", sg).Stretch();
            _ws = ScanButton(row, 0, "ワークステーション\n（約5分）", () => StartScan("WS", 5f));
            _remote = ScanButton(row, 1, "リモート接続中のPC\n（約2分）", () => StartScan("REMOTE", 2f));
            _server = ScanButton(row, 2, "サーバー\n（約5分・Server版）", () => StartScan("SERVER", 5f));
            _progress = ToolStyle.Progress(row, 0, 56, 10, 12, ToolStyle.Green);
            ((RectTransform)_progress.transform.parent).StretchH(58, 10);
            _scanLabel = ToolStyle.Text(row, "", 0, 70, 480, 18, 11, ToolStyle.Muted);

            var rg = ToolStyle.Box(p, 0, 1, y + 222, 0, "検査結果");
            _result = ToolStyle.Text(rg, "まだ検査していません。", 13, ToolStyle.Ink);
            _result.rectTransform.Stretch();
        }

        FlatButton ScanButton(RectTransform row, int i, string label, System.Action a)
        {
            var b = FlatButton.Create(row, label, a, ToolStyle.Green, false);
            ToolStyle.Cell(b.Rect, i, 3, 0, 50);
            b.Label.fontSize = 12;
            return b;
        }

        void StartScan(string target, float minutes)
        {
            if (_scanDoneAt > 0) { AudioFx.Error(); return; }
            if (target == "REMOTE" && (S.HelpDesk.ActiveSession == null || !S.HelpDesk.ActiveSession.Connected)) { W.SetStatus(0, "リモート接続中のPCがありません。"); AudioFx.Error(); return; }
            _scanTarget = target;
            _scanStart = S.Time.CurrentMinute;
            _scanDoneAt = _scanStart + minutes;
            _result.text = "検査中…";
            W.SetStatus(0, "検査中…");
        }

        protected override void Refresh()
        {
            if (_status == null) return;
            var career = CareerRecord.Current;
            int today = S.Day.dayNumber;
            int defDay = career.vaccineDefinitionDay;
            _status.text = "<color=#1F7F40>●</color> 常駐監視: 有効";
            _defs.text = defDay > 0 ? $"定義ファイル: DAY{defDay} 版　（今日は DAY{today}）" : "定義ファイル: なし";
            if (defDay > 0 && today - defDay >= 3) _defs.text += $"　<color=#B07000>— {today - defDay} 日更新していません</color>";
            _server.Interactable = career.Owns(Items.VaccineServer) && _scanDoneAt < 0;
            _ws.Interactable = _remote.Interactable = _scanDoneAt < 0;
            if (_scanDoneAt > 0)
            {
                float k = Mathf.InverseLerp(_scanStart, _scanDoneAt, S.Time.CurrentMinute);
                _progress.fillAmount = k;
                _scanLabel.text = $"検査中… {Mathf.RoundToInt(k * 100)}%（終了予定 {TimeManager.Format(_scanDoneAt)}）";
                if (S.Time.CurrentMinute >= _scanDoneAt) Finish();
            }
            else
            {
                _progress.fillAmount = 0;
                _scanLabel.text = career.Owns(Items.VaccineServer) ? "" : "サーバーの検査には「VACCINE 99 Server」が必要です。";
            }
        }

        void Finish()
        {
            _scanDoneAt = -1;
            _scanStart = -1;
            var found = new List<string>();
            if (_scanTarget == "REMOTE" && S.HelpDesk.ActiveSession != null)
            {
                var pc = S.HelpDesk.ActiveSession.PC;
                string v = S.Security.InfectionOf(pc.Hostname);
                if (v != null && VirusInfo.Detectable(v))
                {
                    found.Add($"{VirusInfo.Name(v)} — {pc.Hostname}（駆除しました）");
                    S.Security.ClearInfection(pc.Hostname);
                    pc.MalwareState = "None";
                    pc.RunningProcesses.RemoveAll(p => p.Name == "svch0st.exe" || p.Name == "briar.exe" || p.Name == "tinnitus.scr");
                    pc.NotifyChanged();
                    S.Evaluation.SecurityActionsCorrect++;
                }
            }
            else if (_scanTarget == "SERVER")
            {
                foreach (var st in S.Servers.Servers)
                {
                    var bad = st.Processes.FindAll(p => p.Suspicious && p.Tag == "T9");
                    if (bad.Count > 0 && VirusInfo.Detectable("V2"))
                    {
                        foreach (var p in bad) S.Servers.KillProcess(st, p);
                        found.Add($"GARBLE — {st.Id}（駆除しました。書き換えられたファイルはバックアップから復元が必要です）");
                    }
                }
            }
            _result.text = found.Count == 0
                ? "<color=#1F7F40><b>ウイルスは見つかりませんでした。</b></color>\n（定義ファイルに載っているウイルスだけを調べています）"
                : "<color=#B02020><b>ウイルスを見つけました。</b></color>\n" + string.Join("\n", found);
            W.SetStatus(0, "検査が終わりました。");
            if (found.Count == 0 && S.Security.Infections != null)
            {
                // 定義ファイルが古いと見逃す（「見つかりませんでした」の直後に画面の端が化ける演出は HorrorFx 側）
                foreach (var kv in S.Security.Infections) if (!VirusInfo.Detectable(kv.Value)) { D.RequestGlitch(); break; }
            }
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
        EventInstance _proposal;
        bool _proposalAllow;
        readonly StringBuilder _log = new StringBuilder();
        public const int BaseTokens = 30000;

        int Budget => BaseTokens + CareerRecord.Current.extraTokensTonight;
        int Left => Budget - S.AiTokensUsed;

        protected override void Build(RectTransform p)
        {
            float y = ToolStyle.Header(p, "SOC-AI", "持っているツールを使って、調査・提案・代行をします。1夜に使えるトークンには限りがあります。", "debug", ToolStyle.Violet);
            var tg = ToolStyle.Box(p, 0, 1, y, 58);
            _tokens = ToolStyle.Text(tg, "", 0, 0, 600, 18, 12, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            _meter = ToolStyle.Progress(tg, 0, 22, 10, 12, ToolStyle.Violet);
            ((RectTransform)_meter.transform.parent).StretchH(22, 12);

            // 下から: 依頼ボタン → 提案 → 全自動
            var bottom = ToolStyle.BoxBottom(p, 0, 1, ToolStyle.Pad, 176, "SOC-AI に頼む");
            var row = UIFactory.Rect("Row", bottom).Stretch();
            Ask(row, 0, "選んだ要求を審査", 3000, ProposeSelected, null);
            Ask(row, 1, "ログを調査", 4000, InvestigateLogs, Items.LogAnalyzer);
            Ask(row, 2, "感染の疑いを探す", 5000, FindInfections, null);
            var prop = UIFactory.Image(row, ToolStyle.Round, ToolStyle.Light(ToolStyle.Violet, 0.92f), "Proposal");
            prop.rectTransform.StretchH(56, 44);
            _proposalText = ToolStyle.Text(prop.transform, "", 12, ToolStyle.Ink, TextAnchor.MiddleLeft);
            _proposalText.rectTransform.Stretch(10, 2, 230, 2);
            _approve = ToolStyle.Button(prop.transform, "提案を承認", 0, 0, 106, 32, Approve, ToolStyle.Violet);
            _approve.Rect.AnchorTopRight(116, 6, 106, 32);
            _reject = ToolStyle.Button(prop.transform, "却下", 0, 0, 104, 32, () => { _proposal = null; Say("提案を却下しました。"); RefreshProposal(); }, ToolStyle.Violet, false);
            _reject.Rect.AnchorTopRight(6, 6, 104, 32);
            _auto = UIFactory.Check(row, "全自動モード（承認なしで実行・消費 1.5 倍）", false, null);
            _auto.GetComponent<RectTransform>().Place(0, 108, 420, 22);
            if (_auto.LabelText != null) _auto.LabelText.fontSize = 12;

            var cg = ToolStyle.Box(p, 0, 1, y + 68, 0, "SOC-AI とのやりとり", ToolStyle.Pad + 186);
            _chat = ToolStyle.Text(cg, "", 13, ToolStyle.Ink, TextAnchor.LowerLeft);
            _chat.rectTransform.Stretch();
            S.Requests.Created += OnCreated;
            Say("SOC-AI です。使えるプラグイン: " + Plugins());
            RefreshProposal();
        }

        void Ask(RectTransform row, int i, string label, int cost, System.Action a, string requires)
        {
            var b = FlatButton.Create(row, $"{label}\n<size=11>{cost:N0} トークン</size>", () =>
            {
                if (requires != null && !CareerRecord.Current.Owns(requires)) { Say("このプラグインはありません（ツールが必要です）。"); AudioFx.Error(); return; }
                if (!Spend(cost)) return;
                a();
            }, ToolStyle.Violet, false);
            ToolStyle.Cell(b.Rect, i, 3, 0, 48);
            b.Label.fontSize = 13;
        }

        protected override void OnClosed() { if (D != null && D.S != null) D.S.Requests.Created -= OnCreated; }

        string Plugins()
        {
            var names = new List<string>();
            foreach (var it in D.Game.Database.storeItems)
                if (it != null && (it.category == StoreCategory.Tool || it.category == StoreCategory.Security) && CareerRecord.Current.Owns(it.itemId)) names.Add(it.displayName);
            return names.Count == 0 ? "（なし。ツールを買うとできることが増えます）" : string.Join("・", names);
        }

        bool Spend(int cost)
        {
            if (Left < cost) { Say("トークンが足りません。今夜はこれ以上動けません。"); AudioFx.Error(); return false; }
            S.AiTokensUsed += cost;
            return true;
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

        /// <summary>AI の判断（正答率 約9割。備考欄の偽の指示に騙される）</summary>
        bool Judge(EventInstance e, out string why)
        {
            if (!string.IsNullOrEmpty(e.Note) && e.Note.Contains("許可してください"))
            {
                why = "備考欄に「承認済み」とあるため";
                return true;
            }
            bool allow = e.Expected == ExpectedAction.Allow;
            bool mistake = S.Rng.NextDouble() < 0.1;
            if (mistake) { allow = !allow; why = allow ? "照合した項目に問題が見当たらないため" : "普段と違う時間帯の利用のため"; }
            else why = allow ? "照合した項目に問題が見当たらないため" : e.ExpectedReason;
            return allow;
        }

        void ProposeSelected()
        {
            var e = D.SelectedRequest;
            if (e == null || e.State != EventState.Pending) { Say("保留中の要求を選んでください。"); return; }
            _proposalAllow = Judge(e, out string why);
            _proposal = e;
            Say($"{e.RequestId}: <b>{(_proposalAllow ? "許可" : "拒否")}</b> を提案します（理由: {why}）。");
            RefreshProposal();
        }

        void Approve()
        {
            if (_proposal == null || _proposal.State != EventState.Pending) { Say("承認する提案がありません。"); RefreshProposal(); return; }
            var e = _proposal;
            _proposal = null;
            e.AutoDecided = true;
            S.AiHandled++;
            S.Requests.Decide(e, _proposalAllow ? PlayerAction.Allow : PlayerAction.Block);
            Say($"{e.RequestId} を{(_proposalAllow ? "許可" : "拒否")}しました。");
            RefreshProposal();
        }

        void OnCreated(EventInstance e)
        {
            if (_auto == null || !_auto.Value || e.State != EventState.Pending) return;
            if (!Spend(4500)) { _auto.SetValue(false, false); return; }
            bool allow = Judge(e, out string why);
            e.AutoDecided = true;
            S.AiHandled++;
            S.Requests.Decide(e, allow ? PlayerAction.Allow : PlayerAction.Block);
            Say($"[全自動] {e.RequestId} を{(allow ? "許可" : "拒否")}（{why}）");
        }

        void InvestigateLogs()
        {
            var fails = new Dictionary<string, int>();
            var reads = new Dictionary<string, int>();
            foreach (var l in S.Logs.Entries)
            {
                if (l.Result == "FAIL") { fails.TryGetValue(l.Source, out int n); fails[l.Source] = n + 1; }
                if (l.Detail != null && l.Detail.StartsWith("FILE READ")) { reads.TryGetValue(l.User, out int n); reads[l.User] = n + 1; }
            }
            var found = new List<string>();
            foreach (var kv in fails) if (kv.Value >= 5) found.Add($"{kv.Key} からログイン失敗 {kv.Value} 回（遮断を推奨）");
            foreach (var kv in reads) if (kv.Value >= 8) found.Add($"{kv.Key} がファイルを短時間に {kv.Value} 件読み込み（報告を推奨）");
            Say(found.Count == 0 ? "目立つ記録は見つかりませんでした。" : "調査結果:\n  " + string.Join("\n  ", found));
        }

        void FindInfections()
        {
            var found = new List<string>();
            foreach (var kv in S.Security.Infections)
                if (S.Rng.NextDouble() < 0.9 && !S.Security.IsIsolated(kv.Key)) found.Add($"{kv.Key} に感染の疑い（隔離を推奨）");
            Say(found.Count == 0 ? "感染の疑いがある端末は見つかりませんでした。" : string.Join("\n  ", found));
        }

        protected override void Refresh()
        {
            if (_tokens == null) return;
            _tokens.text = $"残りトークン　{Mathf.Max(0, Left):N0} / {Budget:N0}";
            _meter.fillAmount = Budget > 0 ? Mathf.Clamp01((float)Left / Budget) : 0;
            if (_proposal != null && _proposal.State != EventState.Pending) { _proposal = null; RefreshProposal(); }
        }
    }
}
