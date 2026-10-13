using System.Linq;
using NightShift.Core;
using NightShift.Data;
using NightShift.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NightShift.Tests
{
    /// <summary>参考文章のリザルトへの移設（§8）・やり直し・残業・終了時集計（§10）</summary>
    public class ReviewAndClosingTests : NightShiftTest
    {
        static HelpDeskDefinition Binary(string exp, string src)
        {
            var d = ScriptableObject.CreateInstance<HelpDeskDefinition>();
            d.definitionId = "TEST_" + (exp ?? "") + (src ?? "");
            d.type = HelpDeskType.Binary;
            d.subject = "テストの相談";
            d.message = "{name}です。";
            d.correctOption = "正しい答え"; d.incorrectOption = "誤った答え";
            d.replyAfterAnswer = "ありがとうございます。";
            d.explanation = exp; d.sourceNote = src;
            return d;
        }

        static void AnswerCorrectly(ShiftSession s, Ticket t) => s.HelpDesk.Answer(t, t.CorrectIndex, "tester");

        // 26
        [Test]
        public void NoReferenceInChat_ButEmployeeReplyRemains()
        {
            var s = TestData.Session(9);
            var emp = s.Employees.All[0];
            var t = s.HelpDesk.Create(Binary("解説の文", "出典の文"), emp);
            AnswerCorrectly(s, t);
            Assert.IsFalse(t.Messages.Any(m => m.From == "【参考】" || m.Text.Contains("解説の文")));
            Assert.IsTrue(t.Messages.Any(m => m.From == emp.fullName && m.Text.Contains("ありがとうございます")), "社員の返信は残る");

            var r = s.HelpDesk.Create(TestData.FindHelpDesk("R5_PATCHES"), emp);
            r.PC.ApplyPatches();
            Assert.IsTrue(s.HelpDesk.Report(r, "tester"));
            Assert.IsFalse(r.Messages.Any(m => m.From == "【参考】"));
            Assert.IsTrue(r.Messages.Count >= 3, "依頼文・報告・社員の返信は残る");
        }

        // 27・28
        [Test]
        public void Review_ShowsReferencesForAllTonightsTickets_WithoutChangingMistakeCount()
        {
            var s = TestData.Session(9);
            var emp = s.Employees.All[0];
            string before = NightReview.Build(s);
            var ok = s.HelpDesk.Create(Binary("正解の解説", "正解の出典"), emp);
            AnswerCorrectly(s, ok);
            var ng = s.HelpDesk.Create(Binary("誤答の解説", "誤答の出典"), emp);
            s.HelpDesk.Answer(ng, 1 - ng.CorrectIndex, "tester");
            var open = s.HelpDesk.Create(Binary("未解決の解説", null), emp);
            int correct = s.Evaluation.HelpDeskCorrect, wrong = s.Evaluation.HelpDeskIncorrect;

            string review = NightReview.Build(s);
            StringAssert.Contains("正解の解説", review);
            StringAssert.Contains("正解の出典", review);
            StringAssert.Contains("誤答の解説", review);
            StringAssert.Contains("未解決の解説", review);
            Assert.AreEqual(1, Count(review, "誤答の解説"), "同じ解説を重ねない");
            StringAssert.Contains("今夜の見落とし・誤り: 2 件", review, "正解した問い合わせは誤りに数えない");
            Assert.AreEqual(correct, s.Evaluation.HelpDeskCorrect);
            Assert.AreEqual(wrong, s.Evaluation.HelpDeskIncorrect);
            StringAssert.DoesNotContain("ITサポートの解説・参考", before, "届いていない問い合わせの解説は出さない");
        }

        static int Count(string text, string word) => (text.Length - text.Replace(word, "").Length) / word.Length;

        // 29
        [Test]
        public void References_HandleOnlyExplanation_OnlySource_AndNeither()
        {
            var s = TestData.Session(9);
            var emp = s.Employees.All[0];
            AnswerCorrectly(s, s.HelpDesk.Create(Binary("解説だけ", null), emp));
            AnswerCorrectly(s, s.HelpDesk.Create(Binary(null, "出典だけ"), emp));
            var none = s.HelpDesk.Create(Binary(null, null), emp);
            AnswerCorrectly(s, none);
            string refs = NightReview.References(s);
            StringAssert.Contains("解説: 解説だけ", refs);
            StringAssert.Contains("出典: 出典だけ", refs);
            StringAssert.DoesNotContain(none.TicketId, refs, "両方ないものは載せない");
            var s2 = TestData.Session(9);
            AnswerCorrectly(s2, s2.HelpDesk.Create(Binary(null, null), s2.Employees.All[0]));
            Assert.AreEqual("", NightReview.References(s2), "参考欄そのものを作らない");
            var s3 = TestData.Session(9);
            string longText = new string('あ', 3000);
            AnswerCorrectly(s3, s3.HelpDesk.Create(Binary(longText, null), s3.Employees.All[0]));
            StringAssert.Contains(longText, NightReview.Build(s3), "長文も全文");
        }

        // 38・39
        [Test]
        public void WeekSnapshot_PacksAccount_AndReadsLegacyWithoutOne()
        {
            var acc = new OperatorAccount { Password = "Night#2468", Changed = true, WrittenDown = true, ResetCount = 2, Strength = 3 };
            string packed = WeekSnapshot.Pack("{\"c\":1}", "{\"w\":2}", acc.Snapshot());
            Assert.IsTrue(WeekSnapshot.TryUnpack(packed, out var c, out var w, out var a));
            Assert.AreEqual("{\"c\":1}", c);
            Assert.AreEqual("{\"w\":2}", w);
            var back = JsonUtility.FromJson<OperatorAccount>(a);
            Assert.AreEqual("Night#2468", back.Password);
            Assert.IsTrue(back.WrittenDown);
            Assert.AreEqual(2, back.ResetCount);

            Assert.IsTrue(WeekSnapshot.TryUnpack("{\"c\":1}#WALLET#{\"w\":2}", out c, out w, out a), "旧形式も読める");
            Assert.IsNull(a, "旧形式にはアカウントがない（推測して作らない）");
            Assert.IsFalse(WeekSnapshot.TryUnpack("", out _, out _, out _));
            Assert.IsFalse(WeekSnapshot.TryUnpack("broken", out _, out _, out _));
        }

        static void RunGameMinutes(ShiftSession s, float minutes)
        {
            float step = s.Time.RealSecondsPerGameMinute * 0.5f;
            for (float t = 0; t < minutes && s.Phase != ShiftPhase.Complete; t += 0.5f) s.Tick(step);
        }

        // 40
        [Test]
        public void Overtime_IsOffered_ForRestoreOnly_AndWaitsForIt()
        {
            var s = TestData.Session(20);
            CareerRecord.Current.AddItem(Items.Coffee, 2);
            s.StartShift();
            var st = s.Servers.Find("WEB-SRV01");
            s.Time.JumpTo(s.Day.endMinute - 5);
            s.Servers.StartRestore(st);
            s.EndShift();
            Assert.IsTrue(s.OvertimeOffered, "復元だけが残っていても残業できる");
            s.StartOvertime();
            Assert.AreEqual(1, CareerRecord.Current.ItemCount(Items.Coffee), "コーヒーは1本だけ");
            RunGameMinutes(s, 5);
            Assert.AreEqual(ShiftPhase.Overtime, s.Phase, "復元が終わるまで早期終了しない");
            RunGameMinutes(s, 20);
            Assert.IsFalse(st.Restoring);
            Assert.AreEqual(ShiftPhase.Complete, s.Phase, "片付いたら早く終わる");
            Assert.Less(s.Time.CurrentMinute, s.OvertimeEnd);
        }

        // 41
        [Test]
        public void Overtime_StillEndsAtCap_AndUnfinishedRestoreIsShownAsUnfinished()
        {
            var s = TestData.Session(20);
            CareerRecord.Current.AddItem(Items.Coffee);
            s.StartShift();
            var st = s.Servers.Find("WEB-SRV01");
            s.Servers.StartRestore(st);
            st.RestoreDoneAt = s.Day.endMinute + 90; // 上限より後に終わる
            s.Time.JumpTo(s.Day.endMinute);
            s.EndShift();
            s.StartOvertime();
            RunGameMinutes(s, 40);
            Assert.AreEqual(ShiftPhase.Complete, s.Phase, "30分の上限では終わる");
            Assert.LessOrEqual(s.Time.CurrentMinute, s.OvertimeEnd + 1f);
            Assert.IsTrue(st.Restoring, "未完了を復元済みにしない");
            StringAssert.Contains("復元が終わる前に業務が終了", NightReview.Build(s));
            Assert.IsTrue(s.Logs.Entries.Any(l => l.Result == "UNFINISHED"));
        }

        // 42
        [Test]
        public void BackupLoss_IsConfirmedOnce_BeforeEvaluation()
        {
            var s = TestData.Session(20);
            CareerRecord.Current.SetFlag(NightClosing.BackupFlag);
            s.RansomEncryptedFiles = 3;
            NightClosing.ConfirmEndOfNightDamage(s);
            NightClosing.ConfirmEndOfNightDamage(s);
            Assert.AreEqual(1, s.Damage.Count(d => d == NightClosing.BackupLostDamage));
            Assert.AreEqual(1, s.Evaluation.DamageCount, "被害の件数と一覧がそろう");
            Assert.IsFalse(CareerRecord.Current.HasFlag(NightClosing.BackupFlag), "フラグは消費される");
            var result = s.Evaluation.Compute();
            int trust = CareerRecord.Current.trust;
            var n = NightClosing.Close(s, result, null);
            Assert.IsTrue(n.damaged);
            Assert.AreEqual(1, s.Damage.Count(d => d == NightClosing.BackupLostDamage), "締めで二重に足さない");

            // 喪失の条件を満たさない夜（フラグなし）は何も足さない
            var s2 = TestData.Session(20);
            s2.RansomEncryptedFiles = 3;
            NightClosing.ConfirmEndOfNightDamage(s2);
            Assert.AreEqual(0, s2.Damage.Count);
        }

        [Test]
        public void BackupLoss_TrustDelta_MatchesPreviousTotal()
        {
            // 以前: 汎用の被害 -1/件（喪失は数えない）＋専用 -3。並べ替え後も同じ合計になる
            var lost = TestData.Session(20);
            CareerRecord.Current.SetFlag(NightClosing.BackupFlag);
            lost.RansomEncryptedFiles = 3;
            NightClosing.ConfirmEndOfNightDamage(lost);
            var n1 = NightClosing.Close(lost, lost.Evaluation.Compute(), null);

            CareerRecord.SetCurrentForTests(new CareerRecord { seed = 42, trust = 70 });
            var kept = TestData.Session(20);
            kept.RansomEncryptedFiles = 3;
            var n2 = NightClosing.Close(kept, kept.Evaluation.Compute(), null);
            // 被害の件数が評価に入るぶんランクが変わることがある。そのぶんを除いて -3 の差
            int rankDiff = RankBonus(n1.rank) - RankBonus(n2.rank);
            Assert.AreEqual(n2.trustDelta - NightClosing.BackupLostTrust + rankDiff, n1.trustDelta);
            Assert.IsTrue(n1.damaged, "夜の記録には被害として残る");
        }

        static int RankBonus(string r) => r == "S" ? 3 : r == "A" ? 2 : r == "B" ? 1 : r == "D" ? -1 : 0;
    }
}
