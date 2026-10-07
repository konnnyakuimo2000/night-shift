using System;
using System.Collections.Generic;
using System.Globalization;
using NightShift.Data;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>初期データ（30日分）の整合性</summary>
    public class DataFactoryTests : NightShiftTest
    {
        static GameDatabase Db => TestData.Db;

        [Test]
        public void ThirtyDays_NumberedOneToThirty()
        {
            Assert.AreEqual(30, Db.days.Count);
            for (int n = 1; n <= 30; n++) Assert.IsNotNull(Db.FindDay(n), $"DAY{n} がない");
        }

        [Test]
        public void TutorialDays_AreFourHours_WithoutExpiry()
        {
            for (int n = 1; n <= 7; n++)
            {
                var d = TestData.Day(n);
                Assert.AreEqual(240f, d.endMinute, $"DAY{n}");
                Assert.IsFalse(d.requestsExpire, $"DAY{n}");
                Assert.IsFalse(d.randomEvents, $"DAY{n}");
            }
        }

        [Test]
        public void MainDays_AreSixHours_RandomWithExpiry()
        {
            for (int n = 8; n <= 30; n++)
            {
                var d = TestData.Day(n);
                Assert.AreEqual(360f, d.endMinute, $"DAY{n}");
                Assert.IsTrue(d.requestsExpire, $"DAY{n}");
                Assert.IsTrue(d.randomEvents, $"DAY{n}");
                Assert.Greater(d.requestCount, 0, $"DAY{n}");
                Assert.IsNotEmpty(d.routinePool, $"DAY{n}");
                Assert.IsNotEmpty(d.violationPool, $"DAY{n}");
                Assert.IsNotEmpty(d.helpDeskPool, $"DAY{n}");
                Assert.AreEqual("06:00", d.EndLabel, $"DAY{n}");
                Assert.IsFalse(string.IsNullOrEmpty(d.briefingBody), $"DAY{n} 業務連絡");
            }
        }

        [Test]
        public void MainDays_DatesAreIncreasingWeekdays()
        {
            DateTime prev = DateTime.MinValue;
            for (int n = 8; n <= 30; n++)
            {
                var date = DateTime.ParseExact(TestData.Day(n).dateLabel.Split(' ')[0], "yyyy/MM/dd", CultureInfo.InvariantCulture);
                Assert.AreNotEqual(DayOfWeek.Saturday, date.DayOfWeek, $"DAY{n}");
                Assert.AreNotEqual(DayOfWeek.Sunday, date.DayOfWeek, $"DAY{n}");
                Assert.Greater(date, prev, $"DAY{n}");
                prev = date;
            }
        }

        [Test]
        public void Difficulty_GrowsTowardTheEnd()
        {
            Assert.Greater(TestData.Day(30).requestCount, TestData.Day(8).requestCount);
            Assert.Greater(TestData.Day(30).violationRatio, TestData.Day(8).violationRatio);
            Assert.Greater(TestData.Day(30).rules.Count, TestData.Day(8).rules.Count);
        }

        [TestCase(9, "requireSourceIpMatch")]
        [TestCase(10, "requireOnShift")]
        [TestCase(12, "requireActiveEmployment")]
        [TestCase(14, "blockOperatorAccount")]
        public void NewRules_StartOnTheirDay(int day, string field)
        {
            bool Get(int d) => (bool)typeof(PolicyRuleSet).GetField(field).GetValue(TestData.Day(d).policy);
            Assert.IsFalse(Get(day - 1), $"DAY{day - 1} ではまだ無効");
            Assert.IsTrue(Get(day), $"DAY{day} から有効");
            Assert.IsTrue(Get(30), "最終日まで続く");
        }

        [TestCase(Features.Store, 8)]
        [TestCase(Features.IpCheck, 9)]
        [TestCase(Features.FirewallBlock, 11)]
        [TestCase(Features.EmploymentStatus, 12)]
        [TestCase(Features.Report, 13)]
        [TestCase(Features.ServerManagement, 15)]
        [TestCase(Features.Isolation, 16)]
        [TestCase(Features.UserManagement, 19)]
        public void Features_UnlockOnTheirDay(string feature, int day)
        {
            Assert.IsFalse(TestData.Day(day - 1).IsUnlocked(feature), $"{feature} は DAY{day - 1} では未解放");
            Assert.IsTrue(TestData.Day(day).IsUnlocked(feature), $"{feature} は DAY{day} から");
            Assert.IsTrue(TestData.Day(30).IsUnlocked(feature));
        }

        [Test]
        public void HelpDeskPool_AllHaveExplanationAndIpaSource()
        {
            foreach (var h in TestData.Day(30).helpDeskPool)
            {
                Assert.IsFalse(string.IsNullOrEmpty(h.explanation), h.definitionId);
                Assert.IsFalse(string.IsNullOrEmpty(h.sourceNote), h.definitionId);
                StringAssert.Contains("試験", h.sourceNote, h.definitionId);
                StringAssert.Contains("（改変）", h.sourceNote, h.definitionId);
                if (h.type == HelpDeskType.Binary)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(h.correctOption), h.definitionId);
                    Assert.IsFalse(string.IsNullOrEmpty(h.incorrectOption), h.definitionId);
                    StringAssert.StartsWith(h.correctIsYes ? "はい" : "いいえ", h.correctOption, h.definitionId);
                }
                else Assert.IsNotEmpty(h.resolutionConditions, h.definitionId);
            }
        }

        [Test]
        public void YesQuestions_UnlockRandomly()
        {
            int yes = 0;
            foreach (var h in TestData.Day(30).helpDeskPool)
            {
                if (!h.correctIsYes) continue;
                yes++;
                Assert.IsTrue(h.randomUnlock, h.definitionId);
                Assert.GreaterOrEqual(h.randomUnlockMin, 8);
                Assert.LessOrEqual(h.randomUnlockMax, 25);
            }
            Assert.AreEqual(12, yes);
        }

        [Test]
        public void HelpDeskIds_AreUnique()
        {
            var seen = new HashSet<string>();
            foreach (var h in TestData.Day(30).helpDeskPool) Assert.IsTrue(seen.Add(h.definitionId), h.definitionId);
        }

        [Test]
        public void StoreItems_AreConsistent()
        {
            Assert.AreEqual(21, Db.storeItems.Count);
            var ids = new HashSet<string>();
            foreach (var it in Db.storeItems)
            {
                Assert.IsTrue(ids.Add(it.itemId), it.itemId);
                Assert.Greater(it.price, 0, it.itemId);
                Assert.GreaterOrEqual(it.unlockDay, 8, it.itemId);
                if (!string.IsNullOrEmpty(it.prerequisite))
                {
                    var pre = Db.FindItem(it.prerequisite);
                    Assert.IsNotNull(pre, it.itemId + " の前提がない");
                    Assert.LessOrEqual(pre.unlockDay, it.unlockDay, it.itemId + " の前提が後から並ぶ");
                }
            }
            Assert.AreEqual(120, Db.FindItem(Items.Coffee).price);
            Assert.AreEqual(19800, Db.FindItem(Items.Monitor1).price);
        }

        [Test]
        public void Employees_HaveUniqueIdsAndDevices()
        {
            var ids = new HashSet<string>();
            var devices = new HashSet<string>();
            foreach (var e in Db.employees)
            {
                Assert.IsTrue(ids.Add(e.employeeId), e.employeeId);
                Assert.IsTrue(devices.Add(e.deviceId), e.deviceId);
            }
        }

        [Test]
        public void Predecessor_IsRetiredAndNameless()
        {
            var e = Db.employees.Find(x => x.employeeId == "E-0004");
            Assert.IsNotNull(e);
            Assert.AreEqual(EmploymentStatus.Retired, e.employment);
            Assert.IsTrue(e.nameRedacted);
            Assert.AreEqual("(削除済み)", e.DisplayName);
        }
    }
}
