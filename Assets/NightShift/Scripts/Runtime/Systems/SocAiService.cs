using System.Collections.Generic;
using NightShift.Data;
using NightShift.Events;

namespace NightShift.Systems
{
    public enum AiTask { Review, Logs, Infections }

    /// <summary>SOC-AI への依頼1件。対象は受け付けた時点で固定する</summary>
    public class AiJob
    {
        public AiTask Task;
        public EventInstance Target;
        public int Cost;
        public bool Done;
        public bool Refunded;
    }

    /// <summary>
    /// サイバー防御AI「SOC-AI」の中身（画面は SocAiTool）。持っていて有効なツールだけをプラグインとして使う。
    /// 依頼は受付時に対象・必要なプラグイン・状態・トークンを確かめてから消費する。
    /// 考えている間に対象が処理済みになったら実行せず、消費したトークンを一度だけ返す。
    /// 正答率は約9割で、備考欄の偽の指示に騙される（これまでどおり）。
    /// </summary>
    public class SocAiService
    {
        public const int BaseTokens = 30000;
        public const int ReviewCost = 3000, LogsCost = 4000, InfectionsCost = 5000, AutoCost = 4500;

        readonly ShiftSession _s;
        public AiJob Pending { get; private set; }

        public SocAiService(ShiftSession s) { _s = s; }

        static Core.CareerRecord Career => Core.CareerRecord.Current;
        public int Budget => BaseTokens + Career.extraTokensTonight;
        public int Left => Budget - _s.AiTokensUsed;

        public static int CostOf(AiTask t) => t == AiTask.Review ? ReviewCost : t == AiTask.Logs ? LogsCost : InfectionsCost;

        /// <summary>依頼に必要なプラグイン（ツール）。どれか1つが有効ならよい</summary>
        public static string[] Requires(AiTask t)
        {
            switch (t)
            {
                case AiTask.Review: return new[] { Items.AutoReview };            // 審査には自動審査の機能
                case AiTask.Logs: return new[] { Items.LogAnalyzer };             // ログ調査にはログ解析
                default: return new[] { Items.Vaccine, Items.RemoteKit };          // 感染調査は検査・診断の機能を通して
            }
        }

        public static bool HasPlugin(AiTask t)
        {
            foreach (var id in Requires(t)) if (Career.ToolActive(id)) return true;
            return false;
        }

        /// <summary>依頼できない理由（null = 依頼できる）。トークンは消費しない</summary>
        public string Validate(AiTask t, EventInstance target)
        {
            if (!Career.ToolActive(Items.SocAi)) return "SOC-AI が無効になっています。";
            if (Pending != null) return "前の依頼を考えています。";
            if (!HasPlugin(t)) return "このプラグインはありません（必要なツールが無いか、無効になっています）。";
            if (t == AiTask.Review && (target == null || target.State != EventState.Pending)) return "保留中の要求を選んでください。";
            if (Left < CostOf(t)) return "トークンが足りません。今夜はこれ以上動けません。";
            return null;
        }

        /// <summary>依頼を受け付ける（確かめてからトークンを消費し、対象を固定する）</summary>
        public AiJob Ask(AiTask t, EventInstance target, out string error)
        {
            error = Validate(t, target);
            if (error != null) return null;
            var job = new AiJob { Task = t, Target = t == AiTask.Review ? target : null, Cost = CostOf(t) };
            _s.AiTokensUsed += job.Cost;
            Pending = job;
            return job;
        }

        /// <summary>返却は一度だけ</summary>
        public void Refund(AiJob job)
        {
            if (job == null || job.Refunded) return;
            job.Refunded = true;
            _s.AiTokensUsed -= job.Cost;
        }

        /// <summary>考えている途中でやめた（画面を閉じた・夜が終わった）。実行せずに返す</summary>
        public void Cancel()
        {
            if (Pending == null) return;
            var job = Pending;
            Pending = null;
            if (job.Done) return;
            job.Done = true;
            Refund(job);
        }

        public class Answer
        {
            public string Message;
            public EventInstance Proposal;
            public bool ProposeAllow;
        }

        /// <summary>考え終わった。二度呼んでも実行は一度だけ</summary>
        public Answer Complete(AiJob job)
        {
            if (job == null || job.Done) return null;
            job.Done = true;
            if (ReferenceEquals(Pending, job)) Pending = null;
            switch (job.Task)
            {
                case AiTask.Review:
                {
                    var e = job.Target;
                    if (e == null || e.State != EventState.Pending)
                    {
                        Refund(job);
                        return new Answer { Message = $"{e?.RequestId ?? "要求"} はすでに処理されていました。依頼を取り消し、トークンを返しました。" };
                    }
                    bool allow = Judge(e, out string why);
                    return new Answer { Proposal = e, ProposeAllow = allow, Message = $"{e.RequestId}: <b>{(allow ? "許可" : "拒否")}</b> を提案します（理由: {why}）。" };
                }
                case AiTask.Logs: return new Answer { Message = InvestigateLogs() };
                default: return new Answer { Message = FindInfections() };
            }
        }

        /// <summary>全自動モードは、自動審査の機能が有効なときだけ</summary>
        public static bool CanAuto => Career.ToolActive(Items.SocAi) && Career.ToolActive(Items.AutoReview);

        /// <summary>全自動: 届いた要求をその場で判断する。処理済み・サブクエストの対象・機能なしでは消費しない</summary>
        public string AutoDecide(EventInstance e, out bool outOfTokens)
        {
            outOfTokens = false;
            if (e == null || e.State != EventState.Pending || e.QuestTag != null || !CanAuto) return null;
            if (Left < AutoCost) { outOfTokens = true; return null; }
            _s.AiTokensUsed += AutoCost;
            bool allow = Judge(e, out string why);
            e.AutoDecided = true;
            _s.AiHandled++;
            _s.Requests.Decide(e, allow ? PlayerAction.Allow : PlayerAction.Block);
            // 防御の設定で拒否に変わった場合は、実際の結果を伝える
            return $"[全自動] {e.RequestId} を{(e.State == EventState.Allowed ? "許可" : "拒否")}（{why}）";
        }

        /// <summary>AI の判断（正答率 約9割。備考欄の偽の指示に騙される）。理由は要求に見えている項目だけから作る</summary>
        public bool Judge(EventInstance e, out string why)
        {
            if (!string.IsNullOrEmpty(e.Note) && e.Note.Contains("許可してください"))
            {
                why = "備考欄に「承認済み」とあるため";
                return true;
            }
            bool allow = e.Expected == ExpectedAction.Allow;
            bool mistake = _s.Rng.NextDouble() < 0.1;
            if (mistake) { allow = !allow; why = allow ? "照合した項目に問題が見当たらないため" : "普段と違う時間帯の利用のため"; }
            else why = allow ? "照合した項目に問題が見当たらないため" : e.ExpectedReason;
            return allow;
        }

        string InvestigateLogs()
        {
            var fails = new Dictionary<string, int>();
            var reads = new Dictionary<string, int>();
            foreach (var l in _s.Logs.Entries)
            {
                if (l.Result == "FAIL") { fails.TryGetValue(l.Source, out int n); fails[l.Source] = n + 1; }
                if (VisibleDetail(l).StartsWith("FILE READ")) { reads.TryGetValue(l.User, out int n); reads[l.User] = n + 1; }
            }
            var found = new List<string>();
            foreach (var kv in fails) if (kv.Value >= 5) found.Add($"{kv.Key} からログイン失敗 {kv.Value} 回（遮断を推奨）");
            foreach (var kv in reads) if (kv.Value >= 8) found.Add($"{kv.Key} がファイルを短時間に {kv.Value} 件読み込み（報告を推奨）");
            return found.Count == 0 ? "目立つ記録は見つかりませんでした。" : "調査結果:\n  " + string.Join("\n  ", found);
        }

        /// <summary>画面に見えているログの本文（文字化けしたサーバーの記録は AI にも読めない）</summary>
        public System.Func<LogEntry, string> VisibleDetail = l => l.Detail ?? "";

        /// <summary>
        /// 感染の疑いを探す。使うのは観測できる手がかりだけ（ログの定期通信・暗号化の書き込み、リモートPCで見えたプロセス）。
        /// ウイルス名を断定するのは、VACCINE が有効で定義ファイルが対応しているときだけ。それ以外は「症状からの疑い」
        /// </summary>
        string FindInfections()
        {
            var suspects = new Dictionary<string, string>(); // 端末 → 手がかり
            foreach (var l in _s.Logs.Entries)
            {
                string d = VisibleDetail(l);
                string clue = d.StartsWith("POST /gate.php") ? "社外への定期通信" : d.StartsWith("FILE WRITE") && d.Contains(".briar") ? "ファイルの連続書き換え" : null;
                if (clue == null) continue;
                string dev = DeviceForIp(l.Source);
                if (dev != null && !suspects.ContainsKey(dev)) suspects[dev] = clue;
            }
            foreach (var t in _s.HelpDesk.Tickets)
            {
                if (t.PC == null || t.FirstResponseAt < 0 || suspects.ContainsKey(t.PC.Hostname)) continue; // 接続して見たPCだけ
                foreach (var v in VirusCatalog.All)
                    if (v.Process != null && t.PC.RunningProcesses.Exists(p => p.Name == v.Process)) { suspects[t.PC.Hostname] = $"見慣れないプロセス {v.Process}"; break; }
            }
            var found = new List<string>();
            bool vaccine = Career.ToolActive(Items.Vaccine);
            foreach (var kv in suspects)
            {
                if (_s.Security.IsIsolated(kv.Key) || _s.Rng.NextDouble() >= 0.9) continue; // AI も見落とす
                string v = _s.Security.InfectionOf(kv.Key);
                if (vaccine && v != null && VirusCatalog.Detectable(v))
                    found.Add($"{kv.Key}: {VirusCatalog.Name(v)} を検出（{kv.Value}。隔離・駆除を推奨）");
                else
                    found.Add($"{kv.Key}: 感染の疑い（{kv.Value}から推定・未確定。隔離を推奨）");
            }
            return found.Count == 0 ? "感染の疑いがある端末は見つかりませんでした。" : string.Join("\n  ", found);
        }

        string DeviceForIp(string ip)
        {
            string found = null;
            foreach (var emp in _s.Employees.All)
            {
                if (_s.Employees.GetIp(emp) != ip) continue;
                if (found != null && found != emp.deviceId) return null;
                found = emp.deviceId;
            }
            return found;
        }
    }
}
