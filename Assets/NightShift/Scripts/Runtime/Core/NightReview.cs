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
            foreach (var st in s.Servers.Servers)
                if (st.Restoring) { sb.AppendLine($"<color=#B07000>未完了:</color> {st.Id} のバックアップからの復元が終わる前に業務が終了した（復元は済んでいません）"); n++; }

            string head = n == 0 ? "今夜は、判断の誤り・解決できなかった問い合わせ・被害はありませんでした。" : $"今夜の見落とし・誤り: {n} 件\n\n" + sb;
            string refs = References(s);
            return refs.Length == 0 ? head : head.TrimEnd() + "\n\n" + refs;
        }

        /// <summary>
        /// ITサポートの解説・参考（業務中の会話からここへ移した）。正解・解決したものも含め、今夜届いた問い合わせだけ。
        /// 誤り・未解決の問い合わせは上の一覧に解説が出ているので、ここには重ねない（件数にも数えない）
        /// </summary>
        public static string References(ShiftSession s)
        {
            var sb = new StringBuilder();
            foreach (var t in s.HelpDesk.Tickets)
            {
                var d = t.Definition;
                bool hasExp = !string.IsNullOrEmpty(d.explanation), hasSrc = !string.IsNullOrEmpty(d.sourceNote);
                if (!hasExp && !hasSrc) continue;
                bool wrongAnswer = t.Type == Data.HelpDeskType.Binary && t.AnsweredIndex >= 0 && t.AnsweredIndex != t.CorrectIndex;
                bool unsolved = t.State == TicketState.Failed || t.State == TicketState.Unresolved || t.IsOpen;
                if (wrongAnswer || unsolved) continue;
                string result = t.Type == Data.HelpDeskType.Binary ? "正しく回答" : "解決";
                sb.AppendLine($"<b>{t.TicketId}</b>  {t.Subject}（{(t.Employee != null ? t.Employee.fullName : "-")}）　<color=#1F7F40>{result}</color>");
                if (hasExp) sb.AppendLine($"   解説: {d.explanation}");
                if (hasSrc) sb.AppendLine($"   出典: {d.sourceNote}");
                sb.AppendLine();
            }
            return sb.Length == 0 ? "" : "<b>ITサポートの解説・参考</b>\n\n" + sb.ToString().TrimEnd();
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
