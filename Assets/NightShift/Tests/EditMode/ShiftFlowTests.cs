using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using NightShift.UI.Shift;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>本番業務の1夜の流れ: ランダム配置・攻撃シナリオ・自動審査・残業</summary>
    public class ShiftFlowTests : NightShiftTest
    {
        /// <summary>ゲーム内時刻を進めて、予定されたイベントを発生させる</summary>
        static void AdvanceTo(ShiftSession s, float minute)
        {
            float now = s.Time.CurrentMinute;
            while (now < minute)
            {
                now = System.Math.Min(minute, now + 0.5f);
                s.Time.JumpTo(now);
                s.Director.Scheduler.Tick(now, false, true);
                s.Servers.Tick(now);
            }
        }

        [Test]
        public void Planner_SchedulesAtLeastTheDaysRequestCount()
        {
            var s = TestData.Session(20);
            s.StartShift();
            int requests = 0;
            foreach (var it in s.Director.Scheduler.Items)
                if (it.Label.StartsWith("ROUTINE") || it.Label.StartsWith("VIOLATION")) requests++;
            Assert.GreaterOrEqual(requests, s.Day.requestCount);
        }

        [Test]
        public void Planner_AllRequestsFallInsideTheShift()
        {
            var s = TestData.Session(25, 7);
            s.StartShift();
            foreach (var it in s.Director.Scheduler.Items)
                if (it.Label.StartsWith("ROUTINE") || it.Label.StartsWith("VIOLATION"))
                    Assert.That(it.Time, Is.InRange(s.Day.startMinute, s.Day.endMinute), it.Label);
        }

        [Test]
        public void WholeNight_ProducesRequestsAndExpiresTheIgnoredOnes()
        {
            var s = TestData.Session(12, 3);
            s.StartShift();
            AdvanceTo(s, 120);
            Assert.Greater(s.Requests.Pending.Count + s.Requests.Processed.Count, 0, "接続要求が届く");
            s.Requests.Tick(120);
            foreach (var e in s.Requests.Processed)
                if (e.State == EventState.Expired) Assert.LessOrEqual(e.TimeoutAt, 120f);
        }

        [Test]
        public void BruteForce_CompromisesUnlessSourceIsBlocked()
        {
            var day = TestData.Day(11);
            var def = TestData.FindThreat(day, "T3_BRUTE_FORCE");
            Assert.IsNotNull(def);

            var s1 = TestData.Session(11);
            var run1 = s1.Director.Threats.Schedule(s1.Director.Scheduler, def, 0);
            AdvanceTo(s1, 20);
            Assert.IsTrue(run1.Compromised, "遮断しないと総当たりが成功する");

            var s2 = TestData.Session(11);
            var run2 = s2.Director.Threats.Schedule(s2.Director.Scheduler, def, 0);
            AdvanceTo(s2, 2);
            s2.Security.Block(run2.AttackerIp);
            AdvanceTo(s2, 20);
            Assert.IsFalse(run2.Compromised, "遮断すれば防げる");
            Assert.Greater(s2.Evaluation.SecurityActionsCorrect, 0);
        }

        [Test]
        public void Ransomware_IsFatalUnlessIsolatedWithin45Minutes()
        {
            var def = TestData.FindThreat(TestData.Day(20), "T11_RANSOMWARE");
            var s1 = TestData.Session(20);
            s1.Director.Threats.Schedule(s1.Director.Scheduler, def, 0);
            AdvanceTo(s1, 40);
            Assert.IsNull(s1.FatalIncident, "45分までは間に合う");
            AdvanceTo(s1, 50);
            Assert.IsNotNull(s1.FatalIncident, "放置すると致命的");

            var s2 = TestData.Session(20);
            var run = s2.Director.Threats.Schedule(s2.Director.Scheduler, def, 0);
            AdvanceTo(s2, 5);
            Assert.IsNotNull(run.Victim);
            Assert.IsTrue(s2.Security.IsInfected(run.Victim.deviceId));
            s2.Security.Isolate(run.Victim.deviceId);
            AdvanceTo(s2, 50);
            Assert.IsNull(s2.FatalIncident, "隔離すれば防げる");
        }

        [Test]
        public void AdminTakeover_IsFatalOnlyIfAllowedAndNotSuspended()
        {
            var def = TestData.FindThreat(TestData.Day(19), "T10_ADMIN_TAKEOVER");

            // 拒否した（停電は起きうるが、致命的ではない）
            var s1 = TestData.Session(19);
            s1.Director.Threats.Schedule(s1.Director.Scheduler, def, 0);
            AdvanceTo(s1, 1);
            s1.Requests.Decide(s1.Requests.Pending[0], PlayerAction.Block);
            AdvanceTo(s1, 65);
            Assert.IsNull(s1.FatalIncident, "拒否していれば致命的にはならない");

            // 許可して、アカウントを止めなかった → 致命的
            var s2 = TestData.Session(19);
            s2.Director.Threats.Schedule(s2.Director.Scheduler, def, 0);
            AdvanceTo(s2, 1);
            s2.Requests.Decide(s2.Requests.Pending[0], PlayerAction.Allow);
            AdvanceTo(s2, 65);
            Assert.IsNotNull(s2.FatalIncident);

            // 許可しても、アカウントを止めれば防げる
            var s3 = TestData.Session(19);
            var run = s3.Director.Threats.Schedule(s3.Director.Scheduler, def, 0);
            AdvanceTo(s3, 1);
            s3.Requests.Decide(s3.Requests.Pending[0], PlayerAction.Allow);
            s3.Security.Suspend(run.Victim);
            AdvanceTo(s3, 65);
            Assert.IsNull(s3.FatalIncident);
        }

        [Test]
        public void WebDefacement_PreventedByKillingTheProcess()
        {
            var def = TestData.FindThreat(TestData.Day(17), "T9_WEB_DEFACE");
            var s1 = TestData.Session(17);
            s1.Director.Threats.Schedule(s1.Director.Scheduler, def, 0);
            AdvanceTo(s1, 31);
            Assert.IsTrue(s1.Servers.Find("WEB-SRV01").Defaced);

            var s2 = TestData.Session(17);
            s2.Director.Threats.Schedule(s2.Director.Scheduler, def, 0);
            AdvanceTo(s2, 6);
            var st = s2.Servers.Find("WEB-SRV01");
            var p = st.Processes.Find(x => x.Tag == "T9");
            Assert.IsNotNull(p, "不審なプロセスが置かれる");
            s2.Servers.KillProcess(st, p);
            AdvanceTo(s2, 31);
            Assert.IsFalse(st.Defaced);
        }

        [Test]
        public void InfectedPc_SpreadsUnlessIsolated()
        {
            var def = TestData.FindThreat(TestData.Day(16), "T7_INFECTED_PC");
            var s = TestData.Session(16);
            var run = s.Director.Threats.Schedule(s.Director.Scheduler, def, 0);
            AdvanceTo(s, 33);
            Assert.IsTrue(run.Compromised);
            int infected = 0;
            foreach (var kv in s.Security.Infections) infected++;
            Assert.Greater(infected, 1, "周りのPCに広がる");
        }

        [Test]
        public void AutonomousAi_ChangesRouteWhenBlocked_AndStopsWhenAllRangesBlocked()
        {
            var def = TestData.FindThreat(TestData.Day(28), "T14_DAYBREAK");
            var s = TestData.Session(28);
            var run = s.Director.Threats.Schedule(s.Director.Scheduler, def, 0);
            AdvanceTo(s, 3);
            Assert.AreEqual(1, s.Requests.Pending.Count, "最初の侵入の試み");
            string firstIp = s.Requests.Pending[0].SourceIp;
            s.Requests.Decide(s.Requests.Pending[0], PlayerAction.Block);
            AdvanceTo(s, 10);
            Assert.AreEqual(1, s.Requests.Pending.Count, "拒否すると別の経路から再挑戦");
            Assert.AreNotEqual(firstIp, s.Requests.Pending[0].SourceIp);

            foreach (var p in ThreatScenarioManager.AdaptivePrefixes) s.Security.Block(p + "*");
            Assert.AreEqual(0, s.Requests.Pending.Count, "遮断した範囲からの要求は自動で拒否");
            AdvanceTo(s, 30);
            Assert.IsTrue(run.Stopped, "経路がすべて遮断されると止まる");
        }

        [Test]
        public void AutoReview_AllowsOnlySimpleRequestsMatchingTheConditions()
        {
            var day = TestData.Day(12);
            var s = TestData.Session(12);
            var all = TestData.AllConditions();
            var routine = TestData.Build(s, day.routinePool[0]);
            Assert.IsTrue(AutoReviewTool.Eligible(s, routine, all), "正規の簡単な要求は自動許可");

            var spoof = TestData.Build(s, TestData.FindEvent(day, "T1_SPOOFED_SOURCE"));
            Assert.IsFalse(AutoReviewTool.Eligible(s, spoof, all), "IP照合を入れていれば止まる");
            var noIp = TestData.AllConditions();
            noIp.Remove(AutoReviewTool.Conditions[5]);
            Assert.IsTrue(AutoReviewTool.Eligible(s, spoof, noIp), "IP照合を外すと、なりすましが通ってしまう（攻撃者が狙う穴）");

            var vpn = TestData.Build(s, TestData.FindEvent(day, "REQ_ROUTINE_VPN"));
            Assert.IsFalse(AutoReviewTool.Eligible(s, vpn, all), "社外・VPN は常に手動");
        }

        [Test]
        public void ThreatChance_IntroductionDayAlwaysHappens()
        {
            // 初めて出る日（minimumDay）は確率に関係なく必ず起きる
            for (int seed = 1; seed <= 5; seed++)
            {
                var s = TestData.Session(16, seed);
                s.StartShift();
                bool found = false;
                foreach (var r in s.Director.Threats.Runs) if (r.Definition.scenarioId == "T7_INFECTED_PC") found = true;
                Assert.IsTrue(found, $"seed {seed}");
            }
        }
        [Test]
        public void Grace_WaitsForOpenHelpDeskTicket_EvenWithNoPendingRequests()
        {
            var s = TestData.Session(20);
            s.StartShift();
            var def = TestData.Day(20).helpDeskPool.Find(h => h.type == HelpDeskType.Binary);
            var t = s.HelpDesk.Create(def, s.Employees.All[0]);
            foreach (var e in new System.Collections.Generic.List<EventInstance>(s.Requests.Pending)) s.Requests.Decide(e, PlayerAction.Block);
            s.EndShift();
            s.DeclineOvertime();
            s.Tick(0.1f);
            Assert.AreEqual(ShiftPhase.Grace, s.Phase, "問い合わせが残っている間は、猶予が続く");
            s.HelpDesk.Answer(t, t.CorrectIndex, "tester");
            s.Tick(0.1f);
            Assert.AreEqual(ShiftPhase.Complete, s.Phase);
        }
    }
}
