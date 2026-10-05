using System;
using System.Collections.Generic;
using NightShift.Events;

namespace NightShift.Systems
{
    public class CategoryScore
    {
        public string Key;
        public string Label;
        public int Score;     // 0-100
        public string Comment;
    }

    public class EvaluationResult
    {
        public List<CategoryScore> Categories = new List<CategoryScore>();
        public int Overall;
        public string Rank;
        public List<KeyValuePair<string, string>> Stats = new List<KeyValuePair<string, string>>();
    }

    /// <summary>判断結果の内部記録と評価計算。プレイヤーには途中で正誤を見せない。</summary>
    public class EvaluationManager
    {
        public int CorrectAllow, CorrectBlock, FalsePositive, MaliciousAllow, PolicyViolation, ExpiredLegit, ExpiredBlock;
        public int ExpectedAllowTotal, ExpectedBlockTotal;
        public int HelpDeskCorrect, HelpDeskIncorrect, RemoteResolved, RemoteFailed, Unresolved;
        public int TicketsTotal;
        public int RemoteReportsWrong;
        public bool SeniorHelped;          // 先輩に手伝ってもらった夜（精勤手当なし）
        // 本番業務: 遮断・隔離・停止・報告の正誤、被害の数
        public int SecurityActionsCorrect, SecurityActionsWrong, DamageCount;
        readonly List<float> _requestResponseTimes = new List<float>();
        readonly List<float> _ticketResponseTimes = new List<float>();

        public void RegisterRequest(EventInstance e)
        {
            if (e.Expected == Data.ExpectedAction.Block) ExpectedBlockTotal++;
            else ExpectedAllowTotal++;
        }

        public DecisionOutcome RecordDecision(EventInstance e)
        {
            DecisionOutcome o;
            bool shouldBlock = e.Expected == Data.ExpectedAction.Block;
            if (e.PlayerAction == PlayerAction.Allow)
                o = shouldBlock ? (e.IsMalicious ? DecisionOutcome.MaliciousAllow : DecisionOutcome.PolicyViolation) : DecisionOutcome.CorrectAllow;
            else
                o = shouldBlock ? DecisionOutcome.CorrectBlock : DecisionOutcome.FalsePositive;

            switch (o)
            {
                case DecisionOutcome.CorrectAllow: CorrectAllow++; break;
                case DecisionOutcome.CorrectBlock: CorrectBlock++; break;
                case DecisionOutcome.FalsePositive: FalsePositive++; break;
                case DecisionOutcome.MaliciousAllow: MaliciousAllow++; break;
                case DecisionOutcome.PolicyViolation: PolicyViolation++; break;
            }
            _requestResponseTimes.Add(e.ResponseTime);
            e.Outcome = o;
            return o;
        }

        public DecisionOutcome RecordExpired(EventInstance e)
        {
            var o = e.Expected == Data.ExpectedAction.Block ? DecisionOutcome.ExpiredBlock : DecisionOutcome.ExpiredLegit;
            if (o == DecisionOutcome.ExpiredBlock) ExpiredBlock++; else ExpiredLegit++;
            _requestResponseTimes.Add(e.Definition != null ? e.Definition.timeoutMinutes : 30f);
            e.Outcome = o;
            return o;
        }

        public void RecordTicketCreated() => TicketsTotal++;
        public void RecordTicketFirstResponse(float minutes) => _ticketResponseTimes.Add(minutes);

        public float AverageResponseTime
        {
            get
            {
                float sum = 0; int n = 0;
                foreach (var t in _requestResponseTimes) { sum += t; n++; }
                foreach (var t in _ticketResponseTimes) { sum += t; n++; }
                return n == 0 ? 0 : sum / n;
            }
        }

        static int Clamp(float v) => (int)Math.Round(Math.Max(0, Math.Min(100, v)));

        public EvaluationResult Compute()
        {
            var r = new EvaluationResult();

            // SECURITY: 拒否すべき要求をどれだけ止めたか。悪意ある要求の許可は大きく減点。
            int blockSeen = CorrectBlock + MaliciousAllow + PolicyViolation + ExpiredBlock;
            float security = blockSeen == 0 ? 100 : 100f * (CorrectBlock + ExpiredBlock * 0.4f) / blockSeen;
            security -= MaliciousAllow * 20 + PolicyViolation * 8;
            security += SecurityActionsCorrect * 4 - SecurityActionsWrong * 8 - DamageCount * 25;

            // ACCURACY: すべての判断の正確さ（期限切れは不正解扱い）
            int decided = CorrectAllow + CorrectBlock + FalsePositive + MaliciousAllow + PolicyViolation + ExpiredLegit + ExpiredBlock;
            float accuracy = decided == 0 ? 100 : 100f * (CorrectAllow + CorrectBlock) / decided;

            // AVAILABILITY: 正常業務を止めなかったか（全拒否戦略はここが大きく下がる）
            int allowSeen = CorrectAllow + FalsePositive + ExpiredLegit;
            float availReq = allowSeen == 0 ? 100 : 100f * CorrectAllow / allowSeen;
            int remoteTotal = RemoteResolved + RemoteFailed;
            float availPc = remoteTotal == 0 ? 100 : 100f * RemoteResolved / Math.Max(1, remoteTotal + 0);
            float availability = availReq * 0.8f + availPc * 0.2f;

            // HELP DESK
            float helpdesk = TicketsTotal == 0 ? 100 : 100f * (HelpDeskCorrect + RemoteResolved) / TicketsTotal - RemoteReportsWrong * 3;

            // RESPONSE TIME: 平均応答時間（ゲーム内分）。3分以内で満点、30分で0点。
            float avg = AverageResponseTime;
            float response = avg <= 3 ? 100 : 100f * (1f - (avg - 3f) / 27f);

            r.Categories.Add(new CategoryScore { Key = "SECURITY", Label = "SECURITY", Score = Clamp(security), Comment = SecurityComment() });
            r.Categories.Add(new CategoryScore { Key = "ACCURACY", Label = "ACCURACY", Score = Clamp(accuracy), Comment = $"{CorrectAllow + CorrectBlock} / {decided} 件の判断が適切" });
            r.Categories.Add(new CategoryScore { Key = "HELPDESK", Label = "HELP DESK", Score = Clamp(helpdesk), Comment = TicketsTotal == 0 ? "本日は担当外" : $"相談 {HelpDeskCorrect}/{HelpDeskCorrect + HelpDeskIncorrect}  リモート解決 {RemoteResolved}" });
            r.Categories.Add(new CategoryScore { Key = "RESPONSE", Label = "RESPONSE TIME", Score = Clamp(response), Comment = $"平均応答 {avg:0.0} 分" });
            r.Categories.Add(new CategoryScore { Key = "AVAILABILITY", Label = "AVAILABILITY", Score = Clamp(availability), Comment = FalsePositive + ExpiredLegit > 0 ? $"正常業務の停止 {FalsePositive + ExpiredLegit} 件" : "正常業務への影響なし" });

            float overall = Clamp(security) * 0.30f + Clamp(accuracy) * 0.20f + Clamp(helpdesk) * 0.20f + Clamp(response) * 0.10f + Clamp(availability) * 0.20f;
            r.Overall = Clamp(overall);
            r.Rank = r.Overall >= 92 ? "S" : r.Overall >= 80 ? "A" : r.Overall >= 65 ? "B" : r.Overall >= 50 ? "C" : "D";

            void S(string k, object v) => r.Stats.Add(new KeyValuePair<string, string>(k, v.ToString()));
            S("Correct Allow", CorrectAllow);
            S("Correct Block", CorrectBlock);
            S("False Positive", FalsePositive);
            S("Malicious Allow", MaliciousAllow);
            S("Policy Violation Allow", PolicyViolation);
            S("Expired Requests", ExpiredLegit + ExpiredBlock);
            S("Help Desk Correct Answers", HelpDeskCorrect);
            S("Help Desk Incorrect Answers", HelpDeskIncorrect);
            S("Remote Tickets Resolved", RemoteResolved);
            S("Remote Tickets Failed", RemoteFailed);
            S("Unresolved Tickets", Unresolved);
            S("Average Response Time", $"{avg:0.0} min");
            if (SecurityActionsCorrect + SecurityActionsWrong > 0) S("Security Actions (OK / NG)", $"{SecurityActionsCorrect} / {SecurityActionsWrong}");
            if (DamageCount > 0) S("Incidents", DamageCount);
            return r;
        }

        string SecurityComment()
        {
            if (DamageCount > 0) return $"被害が {DamageCount} 件発生";
            if (MaliciousAllow > 0) return $"不正な接続を {MaliciousAllow} 件許可";
            if (PolicyViolation > 0) return $"ルール違反の接続を {PolicyViolation} 件許可";
            if (ExpiredBlock > 0) return $"不審な要求の放置 {ExpiredBlock} 件";
            return "不正な接続の許可なし";
        }
    }
}
