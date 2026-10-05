using System.Collections.Generic;
using System.Text;
using NightShift.Events;
using NightShift.Systems;

namespace NightShift.Core
{
    /// <summary>
    /// 夜の振り返り。業務中は正誤を見せないが、終わった後は「どの要求の何を見落としたか」を読めるようにする。
    /// </summary>
    public static class NightReview
    {
        public static string Build(ShiftSession s)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var e in s.Requests.Processed)
            {
                string verdict = Verdict(e.Outcome);
                if (verdict == null) continue;
                n++;
                sb.AppendLine($"<b>{e.RequestId}</b>  {TimeManager.Format(e.GeneratedTime)}  {e.DisplayUser}（{e.DisplayEmployeeId}） {e.SourceLabel} → {e.DestinationLabel} {e.AccessType}");
                sb.AppendLine($"   <color=#B00000>{verdict}</color>{(e.AutoDecided ? "（自動処理）" : "")}");
                sb.AppendLine($"   正しい対応: {(e.Expected == Data.ExpectedAction.Block ? "拒否" : "許可")} — 理由: {e.ExpectedReason}");
                sb.AppendLine();
            }

            foreach (var t in s.HelpDesk.Tickets)
            {
                bool wrongAnswer = t.Type == Data.HelpDeskType.Binary && t.AnsweredIndex >= 0 && t.AnsweredIndex != t.CorrectIndex;
                bool unsolved = t.State == TicketState.Failed || t.State == TicketState.Unresolved || t.IsOpen;
                if (!wrongAnswer && !unsolved) continue;
                n++;
                sb.AppendLine($"<b>{t.TicketId}</b>  {t.Subject}（{(t.Employee != null ? t.Employee.fullName : "-")}）");
                sb.AppendLine($"   <color=#B00000>{(wrongAnswer ? "回答が誤り" : "解決できなかった")}</color>");
                if (t.Type == Data.HelpDeskType.Binary && t.Options != null && t.CorrectIndex >= 0 && t.CorrectIndex < t.Options.Length)
                    sb.AppendLine($"   正しい回答: {t.Options[t.CorrectIndex]}");
                if (!string.IsNullOrEmpty(t.Definition.explanation)) sb.AppendLine($"   解説: {t.Definition.explanation}");
                if (!string.IsNullOrEmpty(t.Definition.sourceNote)) sb.AppendLine($"   出典: {t.Definition.sourceNote}");
                sb.AppendLine();
            }

            foreach (var d in s.Damage) { sb.AppendLine($"<color=#B00000>被害:</color> {d}"); n++; }

            if (n == 0) return "今夜は、判断の誤り・解決できなかった問い合わせ・被害はありませんでした。";
            return $"今夜の見落とし・誤り: {n} 件\n\n" + sb;
        }

        static string Verdict(DecisionOutcome o)
        {
            switch (o)
            {
                case DecisionOutcome.MaliciousAllow: return "攻撃の要求を許可した";
                case DecisionOutcome.PolicyViolation: return "ルール違反の要求を許可した";
                case DecisionOutcome.FalsePositive: return "正規の要求を拒否した";
                case DecisionOutcome.ExpiredLegit: return "正規の要求が期限切れになった";
                case DecisionOutcome.ExpiredBlock: return "拒否すべき要求が期限切れになった（拒否していれば加点）";
                default: return null;
            }
        }
    }
}
