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
        public const string BackupFlag = "WRONG_BACKUP";
        public const int BackupLostTrust = 3;
        public const string BackupLostDamage = "共有フォルダのバックアップも一緒に暗号化され、戻せないファイルが残った";

        /// <summary>身代金ウイルスに一部のファイルを暗号化され、バックアップの問い合わせを間違えていたため戻せない</summary>
        public static bool BackupLost(ShiftSession s, CareerRecord career)
            => s.RansomEncryptedFiles > 0 && string.IsNullOrEmpty(s.FatalIncident) && career.HasFlag(BackupFlag);

        /// <summary>
        /// 夜の終わりに確定する被害を、評価・給与・夜の記録を計算する前に確定する（一度だけ）。
        /// バックアップの喪失は AddDamage を通すので、被害一覧・件数・夜の記録がそろう。専用の信用の減点（-3）は Close で一度だけ。
        /// </summary>
        public static void ConfirmEndOfNightDamage(ShiftSession s)
        {
            if (s.EndOfNightDamageConfirmed) return;
            s.EndOfNightDamageConfirmed = true;
            var career = CareerRecord.Current;
            // 脅威による被害（シナリオが成立したのに、まだ一覧にないもの）
            foreach (var run in s.Director.Threats.Runs)
                if (run.Compromised && !s.Damage.Contains(run.Definition.compromiseLog)) s.Damage.Add(run.Definition.compromiseLog);
            if (s.Day.dayNumber >= DefaultDataFactory.MainStartDay && BackupLost(s, career))
            {
                s.BackupLostConfirmed = true;
                career.ClearFlag(BackupFlag);
                s.AddDamage(BackupLostDamage);
            }
        }

        public static NightRecord Close(ShiftSession s, EvaluationResult result, PaySlip slip)
        {
            var career = CareerRecord.Current;
            var ev = s.Evaluation;
            var day = s.Day;
            ConfirmEndOfNightDamage(s); // 呼び忘れても、ここで一度だけ確定する
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
                // 1夜の増減は -15〜+8 程度に収める（上級者は信用を保てる、平均的なら横ばい、ミスが多いと倒産へ）
                int delta = 0;
                delta -= ev.MaliciousAllow;
                delta -= (ev.ExpiredLegit + ev.FalsePositive + ev.Unresolved + 2) / 4;
                // 被害: 1件 -1（1夜で最大 -3）。バックアップの喪失は下の専用の減点で数える（以前と同じ合計にする）
                delta -= System.Math.Min(3, s.Damage.Count - (s.BackupLostConfirmed ? 1 : 0));
                // 被害ゼロの夜の +2: バックアップの喪失は専用の減点で数えるので、ここでは被害に含めない（以前と同じ合計）
                bool damagedExceptBackup = s.Damage.Count - (s.BackupLostConfirmed ? 1 : 0) > 0 || ev.MaliciousAllow > 0;
                if (!damagedExceptBackup) delta += 2;
                delta += result.Rank == "S" ? 3 : result.Rank == "A" ? 2 : result.Rank == "B" ? 1 : result.Rank == "D" ? -1 : 0;
                delta += System.Math.Min(3, s.CorrectReports);
                // バックアップの問い合わせ（A11・A21・Y6）を間違えていた: 身代金ウイルスを途中で止めても、
                // 暗号化されたファイルをバックアップから戻せない（正しく答えていれば、バックアップから戻せる）
                if (s.BackupLostConfirmed) delta -= BackupLostTrust;
                n.trustDelta = delta;
                career.AddTrust(delta);
            }

            // 駆除していない感染は、次の夜に残る（経路 G）。隔離しただけの感染は消さず、活動停止中として残す。
            // 担当者のワークステーションも対象（社員名簿にない端末も捨てない）
            career.infections.Clear();
            foreach (var kv in s.Security.Infections)
                career.infections.Add(new EventDirector.CarriedInfection { VirusId = kv.Value, Device = kv.Key, Isolated = s.Security.IsIsolated(kv.Key) }.ToString());

            // 翌朝のネットニュースに載る出来事
            career.ClearFlag("NEWS_DEFACED"); career.ClearFlag("NEWS_BLACKOUT");
            foreach (var d in s.Damage)
            {
                if (d.Contains("GARBLE")) career.SetFlag("NEWS_DEFACED");
                if (d.Contains("REDACT")) career.SetFlag("NEWS_BLACKOUT");
            }

            career.RecordNight(n);
            career.Save();
            return n;
        }
    }
}
