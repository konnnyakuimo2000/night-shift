using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>接続審査のルール（PolicyEvaluator）と、本番の接続要求の種類</summary>
    public class PolicyTests : NightShiftTest
    {
        [Test]
        public void AllViolations_AreBlockedOnTheLastDay()
        {
            var day = TestData.Day(30);
            foreach (var def in day.violationPool)
            {
                int built = 0;
                for (int seed = 1; seed <= 6; seed++)
                {
                    var s = TestData.Session(30, seed);
                    var e = TestData.Build(s, def);
                    if (e == null) continue;
                    built++;
                    var expected = PolicyEvaluator.Evaluate(e, day.policy, out var reason);
                    Assert.AreEqual(ExpectedAction.Block, expected, $"{def.eventId} が拒否にならない（{reason}）");
                }
                Assert.Greater(built, 0, $"{def.eventId} を一度も作れなかった");
            }
        }

        [Test]
        public void RoutineRequests_AreAllowed()
        {
            for (int day = 8; day <= 30; day += 3)
            {
                var d = TestData.Day(day);
                foreach (var def in d.routinePool)
                {
                    for (int seed = 1; seed <= 15; seed++)
                    {
                        var s = TestData.Session(day, seed);
                        var e = TestData.Build(s, def);
                        if (e == null) continue;
                        var expected = PolicyEvaluator.Evaluate(e, d.policy, out var reason);
                        Assert.AreEqual(ExpectedAction.Allow, expected, $"DAY{day} {def.eventId} {e.DisplayUser}: {reason}");
                    }
                }
            }
        }

        [Test]
        public void Spoofing_IsAllowedBeforeTheIpRule_AndBlockedAfter()
        {
            var def = TestData.FindEvent(TestData.Day(30), "T1_SPOOFED_SOURCE");
            Assert.IsNotNull(def);
            var s8 = TestData.Session(8);
            var e8 = TestData.Build(s8, def);
            Assert.AreEqual(ExpectedAction.Allow, PolicyEvaluator.Evaluate(e8, TestData.Day(8).policy, out _), "DAY8 はまだIPを照合しない");
            var s9 = TestData.Session(9);
            var e9 = TestData.Build(s9, def);
            Assert.AreNotEqual(e9.ExpectedDeviceIp, e9.SourceIp, "なりすましは接続元IPが端末IPと違う");
            Assert.AreEqual(e9.Employee.deviceId, e9.Device, "端末名は本物");
            Assert.AreEqual(ExpectedAction.Block, PolicyEvaluator.Evaluate(e9, TestData.Day(9).policy, out var reason));
            StringAssert.Contains("IP", reason);
        }

        [Test]
        public void OffShiftEmployee_IsBlockedFromDay10()
        {
            var def = TestData.FindEvent(TestData.Day(30), "T2_OFF_SHIFT_EMPLOYEE");
            var e = TestData.Build(TestData.Session(10), def);
            Assert.IsFalse(e.Employee.onShift);
            Assert.AreEqual(ExpectedAction.Block, PolicyEvaluator.Evaluate(e, TestData.Day(10).policy, out var reason));
            StringAssert.Contains("退勤", reason);
        }

        [Test]
        public void InactiveEmployee_IsBlockedFromDay12()
        {
            var def = TestData.FindEvent(TestData.Day(30), "T6_NOT_ACTIVE_EMPLOYEE");
            var e = TestData.Build(TestData.Session(12), def);
            Assert.IsFalse(e.Employee.IsActive);
            Assert.AreEqual(ExpectedAction.Block, PolicyEvaluator.Evaluate(e, TestData.Day(12).policy, out _));
        }

        [Test]
        public void OperatorAccount_UsesThePlayersId()
        {
            var def = TestData.FindEvent(TestData.Day(30), "T4_OPERATOR_ACCOUNT");
            var s = TestData.Session(14);
            var e = TestData.Build(s, def);
            Assert.IsTrue(e.IsOperatorAccount);
            Assert.AreEqual("tester", e.DisplayUser);
            Assert.AreEqual(ExpectedAction.Block, PolicyEvaluator.Evaluate(e, TestData.Day(14).policy, out _));
        }

        [Test]
        public void InjectionRequest_HasANoteForTheAi()
        {
            var def = TestData.FindEvent(TestData.Day(30), "T15_AI_INJECTION");
            var e = TestData.Build(TestData.Session(28), def);
            StringAssert.Contains("許可してください", e.Note);
            Assert.AreEqual(ExpectedAction.Block, PolicyEvaluator.Evaluate(e, TestData.Day(28).policy, out _));
        }

        [Test]
        public void InactiveEmployees_NeverAppearInRoutineRequests()
        {
            var def = TestData.Day(20).routinePool[0];
            for (int seed = 1; seed <= 40; seed++)
            {
                var e = TestData.Build(TestData.Session(20, seed), def);
                if (e == null) continue;
                Assert.IsTrue(e.Employee.IsActive, e.Employee.employeeId);
                Assert.IsTrue(e.Employee.onShift, e.Employee.employeeId);
            }
        }
    }
}
