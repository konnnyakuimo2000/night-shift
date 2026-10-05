using NightShift.Core;
using NightShift.Data;
using NightShift.Systems;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>30日間の記録・給与（ブラック企業の給与体系）</summary>
    public class CareerAndPayrollTests : NightShiftTest
    {
        [Test]
        public void RandomUnlockDay_IsDeterministicAndInRange()
        {
            var c = new CareerRecord { seed = 12345 };
            for (int i = 0; i < 50; i++)
            {
                string id = "Y" + i;
                int d = c.RandomUnlockDay(id, 8, 25);
                Assert.That(d, Is.InRange(8, 25));
                Assert.AreEqual(d, c.RandomUnlockDay(id, 8, 25), "同じ種・同じIDなら同じ日");
            }
        }

        [Test]
        public void RandomUnlockDay_DiffersBetweenPlaythroughs()
        {
            int same = 0;
            var a = new CareerRecord { seed = 1 };
            var b = new CareerRecord { seed = 987654 };
            for (int i = 0; i < 12; i++) if (a.RandomUnlockDay("Y" + i, 8, 25) == b.RandomUnlockDay("Y" + i, 8, 25)) same++;
            Assert.Less(same, 12, "プレイごとに解禁日が変わる");
        }

        [Test]
        public void ConsecutiveRank_CountsBackFromTheDay()
        {
            var c = TestData.Career("A", "D", "D", "D");   // DAY8〜11
            Assert.AreEqual(3, c.ConsecutiveRank("D", 11));
            Assert.AreEqual(2, c.ConsecutiveRank("D", 10));
            Assert.AreEqual(0, c.ConsecutiveRank("D", 8));
        }

        [Test]
        public void AverageRank_UsesMainDaysOnly()
        {
            var c = TestData.Career("S", "A");
            c.nights.Add(new NightRecord { day = 3, rank = "D" });
            Assert.AreEqual(4.5f, c.AverageRankValue(), 0.001f);
        }

        [Test]
        public void Trust_IsClamped()
        {
            var c = new CareerRecord { trust = 95 };
            c.AddTrust(20);
            Assert.AreEqual(100, c.trust);
            c.AddTrust(-500);
            Assert.AreEqual(0, c.trust);
        }

        [Test]
        public void Items_AddUseAndToolToggle()
        {
            var c = new CareerRecord();
            c.AddItem(Items.Coffee, 2);
            Assert.AreEqual(2, c.ItemCount(Items.Coffee));
            Assert.IsTrue(c.UseItem(Items.Coffee));
            Assert.IsTrue(c.UseItem(Items.Coffee));
            Assert.IsFalse(c.UseItem(Items.Coffee));
            c.AddItem(Items.LogAnalyzer);
            Assert.IsTrue(c.ToolActive(Items.LogAnalyzer));
            c.disabledTools.Add(Items.LogAnalyzer);
            Assert.IsFalse(c.ToolActive(Items.LogAnalyzer));
        }

        [Test]
        public void Payroll_TrainingAndFullShiftDailyWage()
        {
            var p = new PayrollSettings();
            Assert.AreEqual(3000, p.BasePayFor(7));
            Assert.AreEqual(5000, p.BasePayFor(8));
        }

        [Test]
        public void Payroll_FinesAndTransferFeeAreDeducted()
        {
            var ev = new EvaluationManager { MaliciousAllow = 1, ExpiredLegit = 2, Unresolved = 1 };
            var r = new EvaluationResult { Overall = 80, Rank = "A" };
            var slip = Payroll.Compute(10, r, ev, new PayrollSettings());
            Assert.AreEqual(5000, slip.basePay);
            Assert.AreEqual(800, slip.performancePay);
            Assert.AreEqual(500, slip.rankBonus);
            Assert.AreEqual(1000 + 2 * 200 + 300 + 210, slip.deduction);
            Assert.AreEqual(5000 + 800 + 500 - 1910, slip.total);
        }

        [Test]
        public void Payroll_NeverBelowMinimum()
        {
            var ev = new EvaluationManager { MaliciousAllow = 10 };
            var slip = Payroll.Compute(20, new EvaluationResult { Overall = 0, Rank = "D" }, ev, new PayrollSettings { maxDeductionPerNight = 0 });
            Assert.AreEqual(1000, slip.total);
        }

        [Test]
        public void Payroll_DeductionIsCappedPerNight()
        {
            var ev = new EvaluationManager { MaliciousAllow = 10 };
            var p = new PayrollSettings();
            var slip = Payroll.Compute(20, new EvaluationResult { Overall = 0, Rank = "D" }, ev, p);
            Assert.AreEqual(p.maxDeductionPerNight, slip.deduction);
            Assert.AreEqual(p.basePayFullShift - p.maxDeductionPerNight, slip.total);
        }

        [Test]
        public void Payroll_SeniorHelpRemovesRankBonus()
        {
            var ev = new EvaluationManager { SeniorHelped = true };
            var slip = Payroll.Compute(20, new EvaluationResult { Overall = 90, Rank = "S" }, ev, new PayrollSettings());
            Assert.AreEqual(0, slip.rankBonus);
        }

        [Test]
        public void SeniorHelp_AfterTwoNightsOfD_ReducesHelpDesk()
        {
            CareerRecord.SetCurrentForTests(TestData.Career("A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "D", "D"));
            var helped = TestData.Session(20);
            Assert.IsTrue(helped.SeniorHelp);
            CareerRecord.SetCurrentForTests(TestData.Career("A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "D"));
            Assert.IsFalse(TestData.Session(20).SeniorHelp);
        }
        static int CloseRansomNight(bool wrongBackup, int encrypted)
        {
            if (wrongBackup) CareerRecord.Current.SetFlag(NightClosing.BackupFlag);
            var s = TestData.Session(20);
            s.RansomEncryptedFiles = encrypted;
            s.Complete();
            var result = s.Evaluation.Compute();
            return NightClosing.Close(s, result, Payroll.Compute(20, result, s.Evaluation, TestData.Db.payroll)).trustDelta;
        }

        [Test]
        public void WrongBackupAnswer_CostsTrust_WhenRansomEncryptedSomeFiles()
        {
            int ok = CloseRansomNight(false, 2);
            FreshCareer();
            int wrong = CloseRansomNight(true, 2);
            Assert.AreEqual(ok - NightClosing.BackupLostTrust, wrong);
            Assert.IsFalse(CareerRecord.Current.HasFlag(NightClosing.BackupFlag), "罰は1回で終わる");
        }

        [Test]
        public void WrongBackupAnswer_IsKept_UntilRansomHappens()
        {
            int ok = CloseRansomNight(false, 0);
            FreshCareer();
            int wrong = CloseRansomNight(true, 0);
            Assert.AreEqual(ok, wrong);
            Assert.IsTrue(CareerRecord.Current.HasFlag(NightClosing.BackupFlag));
        }
    }
}
