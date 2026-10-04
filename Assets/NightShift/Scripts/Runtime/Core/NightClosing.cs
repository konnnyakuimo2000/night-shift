using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;

namespace NightShift.Core
{
    /// <summary>
    /// 夜の終わりの集計。シフトの結果を 30日間の記録（CareerRecord）にまとめる。
    /// 会社の信用の増減（ENDINGS.md §2.1）、間違えた問い合わせのフラグ、出題回数などを更新する。
    /// </summary>
    public static class NightClosing
    {
        public static NightRecord Close(ShiftSession s, EvaluationResult result, PaySlip slip)
        {
            var career = CareerRecord.Current;
            var ev = s.Evaluation;
            var day = s.Day;
            var n = new NightRecord
            {
                day = day.dayNumber,
                rank = result.Rank,
                score = result.Overall,
                maliciousAllowed = ev.MaliciousAllow,
                fines = slip != null ? slip.deduction : 0,
                overtime = s.Overtime,
                aiHandled = s.AiHandled,
                totalRequests = s.Requests.Processed.Count,
                correctReports = s.CorrectReports
            };

            // 脅威による被害
            foreach (var run in s.Director.Threats.Runs)
                if (run.Compromised && !s.Damage.Contains(run.Definition.compromiseLog)) s.Damage.Add(run.Definition.compromiseLog);
            n.damaged = s.Damage.Count > 0 || ev.MaliciousAllow > 0;

            // 自分のアカウントの被害（自分のIDの要求を許可した）
            foreach (var e in s.Requests.Processed)
                if (e.IsOperatorAccount && e.State == EventState.Allowed) s.OperatorCompromised = true;
            if (s.OperatorCompromised) career.accountCompromised++;

            // 問い合わせ: 出題回数と、間違えた場合のフラグ
            foreach (var t in s.HelpDesk.Tickets)
            {
                if (day.randomEvents) career.CountHelpDesk(t.Definition.definitionId);
                bool wrong = (t.Type == HelpDeskType.Binary && t.AnsweredIndex >= 0 && t.AnsweredIndex != t.CorrectIndex)
                             || t.State == TicketState.Failed;
                if (wrong && !string.IsNullOrEmpty(t.Definition.flagOnWrong)) career.SetFlag(t.Definition.flagOnWrong);
            }

            // 会社の信用（本番業務のみ）
            if (day.dayNumber >= DefaultDataFactory.MainStartDay)
            {
                int delta = 0;
                delta -= 3 * ev.MaliciousAllow;
                delta -= ev.ExpiredLegit + ev.FalsePositive + ev.Unresolved;
                delta -= 8 * s.Damage.Count;
                if (!n.damaged) delta += 2;
                if (result.Rank == "S") delta += 2;
                delta += s.CorrectReports;
                n.trustDelta = delta;
                career.AddTrust(delta);
            }

            career.RecordNight(n);
            career.Save();
            return n;
        }
    }
}
