using NightShift.Core;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>本編エンド（END1〜6）とゲームオーバー（GO1〜4）の判定。ENDINGS.md の表の順番どおりか</summary>
    public class EndingTests : NightShiftTest
    {
        static CareerRecord Career(string rank, int nights = 23)
        {
            var ranks = new string[nights];
            for (int i = 0; i < nights; i++) ranks[i] = rank;
            return TestData.Career(ranks);
        }

        [Test]
        public void Critical_IsEnd1_EvenIfEverythingElseIsPerfect()
        {
            var c = Career("S");
            c.clues.AddRange(new[] { "C1", "C2", "C3", "C4" });
            Assert.AreEqual("END1", EndingResolver.Resolve(c, FinalOutcome.Critical).Id);
        }

        [Test]
        public void LowTrust_IsBankruptcy()
        {
            var c = Career("S");
            c.trust = 25;
            Assert.AreEqual("END2", EndingResolver.Resolve(c, FinalOutcome.Defended).Id);
            Assert.AreEqual("END2", EndingResolver.Resolve(c, FinalOutcome.PartialDamage).Id);
        }

        [Test]
        public void AllCluesAndRankA_IsTrueEnd()
        {
            var c = Career("A");
            c.clues.AddRange(new[] { "C1", "C2", "C3", "C4" });
            var e = EndingResolver.Resolve(c, FinalOutcome.Defended);
            Assert.AreEqual("END3", e.Id);
            Assert.IsTrue(e.TrueRoute);
        }

        [Test]
        public void TrueEnd_NeedsCompleteDefense()
        {
            var c = Career("A");
            c.clues.AddRange(new[] { "C1", "C2", "C3", "C4" });
            Assert.AreNotEqual("END3", EndingResolver.Resolve(c, FinalOutcome.PartialDamage).Id);
        }

        [Test]
        public void ManyReportsAndRankA_IsHeadhunt()
        {
            var c = Career("A");
            c.nights[0].correctReports = 3;
            c.nights[1].correctReports = 2;
            Assert.AreEqual("END4", EndingResolver.Resolve(c, FinalOutcome.Defended).Id);
        }

        [Test]
        public void RankB_IsPromotion()
        {
            Assert.AreEqual("END5", EndingResolver.Resolve(Career("B"), FinalOutcome.Defended).Id);
        }

        [Test]
        public void Otherwise_IsContractRenewal()
        {
            Assert.AreEqual("END6", EndingResolver.Resolve(Career("C"), FinalOutcome.Defended).Id);
            Assert.AreEqual("END6", EndingResolver.Resolve(Career("S"), FinalOutcome.PartialDamage).Id);
        }

        [Test]
        public void Outcome_FromSession()
        {
            var s = TestData.Session(30);
            Assert.AreEqual(FinalOutcome.Defended, EndingResolver.Outcome(s));
            s.Damage.Add("x");
            Assert.AreEqual(FinalOutcome.PartialDamage, EndingResolver.Outcome(s));
            s.FatalIncident = "y";
            Assert.AreEqual(FinalOutcome.Critical, EndingResolver.Outcome(s));
        }

        // ---------------- ゲームオーバー ----------------
        [Test]
        public void GameOver_FatalIncident_IsGo2()
        {
            var s = TestData.Session(20);
            s.FatalIncident = "全社のファイルが暗号化された";
            Assert.AreEqual("GO2", EndingResolver.CheckGameOver(new CareerRecord(), s).Id);
        }

        [Test]
        public void GameOver_TooManyMaliciousAllowsInOneNight_IsGo2()
        {
            var s = TestData.Session(20);
            s.Evaluation.MaliciousAllow = EndingResolver.MaliciousLimit - 1;
            Assert.IsNull(EndingResolver.CheckGameOver(new CareerRecord(), s));
            s.Evaluation.MaliciousAllow = EndingResolver.MaliciousLimit;
            Assert.AreEqual("GO2", EndingResolver.CheckGameOver(new CareerRecord(), s).Id);
        }

        [Test]
        public void GameOver_ThirdAccountCompromise_IsGo3()
        {
            var c = new CareerRecord { accountCompromised = 3 };
            Assert.AreEqual("GO3", EndingResolver.CheckGameOver(c, TestData.Session(20)).Id);
            c.accountCompromised = 2;
            Assert.IsNull(EndingResolver.CheckGameOver(c, TestData.Session(20)), "2回目までは警告だけ");
        }

        [Test]
        public void GameOver_ThreeDsInARow_IsGo1()
        {
            var c = TestData.Career("A", "B", "D", "D", "D"); // DAY8〜12
            Assert.AreEqual("GO1", EndingResolver.CheckGameOver(c, TestData.Session(12)).Id);
            Assert.IsNull(EndingResolver.CheckGameOver(TestData.Career("A", "B", "D", "D"), TestData.Session(11)));
        }

        [Test]
        public void GameOver_SevenOvertimesInARow_IsGo4()
        {
            var c = new CareerRecord();
            for (int d = 8; d <= 14; d++) c.nights.Add(new NightRecord { day = d, rank = "B", overtime = true });
            Assert.AreEqual("GO4", EndingResolver.CheckGameOver(c, TestData.Session(14)).Id);
        }

        [Test]
        public void GameOver_NeverDuringTrainingOrOnTheLastNight()
        {
            var s7 = TestData.Session(7);
            s7.FatalIncident = "x";
            Assert.IsNull(EndingResolver.CheckGameOver(new CareerRecord(), s7));
            var s30 = TestData.Session(30);
            s30.FatalIncident = "x";
            Assert.IsNull(EndingResolver.CheckGameOver(new CareerRecord(), s30), "DAY30 は本編エンドで決着");
        }
    }
}
