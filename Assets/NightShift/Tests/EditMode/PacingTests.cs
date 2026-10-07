using System.Collections.Generic;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NightShift.Tests
{
    /// <summary>待ち時間の詰め: 業務中、プレイヤーが何もすることがない時間を10秒（実時間）以上つくらない</summary>
    public class PacingTests : NightShiftTest
    {
        const float Dt = 0.1f;          // 1フレーム（実時間・秒）
        const float Reaction = 2.5f;    // プレイヤー役が1件を片付けるまでの時間（実時間・秒）

        /// <summary>プレイヤー役: 届いた仕事を少し考えてから正しく片付ける。何もすることがない最長の時間（実時間・秒）と、夜の長さを返す</summary>
        static (float maxIdle, float nightSeconds) Play(int day, int seed)
        {
            var s = TestData.Session(day, seed);
            s.StartShift();
            var seen = new Dictionary<object, float>();
            float clock = 0, idle = 0, maxIdle = 0;
            bool Ready(object o) { if (!seen.ContainsKey(o)) seen[o] = clock; return clock - seen[o] >= Reaction; }
            for (int i = 0; i < 20000 && s.Phase != ShiftPhase.Complete; i++)
            {
                s.Tick(Dt);
                clock += Dt;
                if (s.Phase == ShiftPhase.Running)
                {
                    if (s.HasWork) idle = 0; else idle += Dt;
                    maxIdle = Mathf.Max(maxIdle, idle);
                }
                foreach (var e in new List<EventInstance>(s.Requests.Pending))
                    if (Ready(e)) s.Requests.Decide(e, e.Expected == ExpectedAction.Allow ? PlayerAction.Allow : PlayerAction.Block);
                foreach (var t in new List<Ticket>(s.HelpDesk.Tickets))
                {
                    if (!t.IsOpen || !Ready(t)) continue;
                    if (t.Type == HelpDeskType.Binary) s.HelpDesk.Answer(t, t.CorrectIndex, s.OperatorName);
                    else t.State = TicketState.Resolved;
                }
                if (s.OpenTaskCount > 0 && Ready(s.Tasks)) s.MarkPasswordChanged();
                foreach (var m in s.Mail.Inbox) if (!m.Read && m.ReceivedAt >= 0 && Ready(m)) s.Mail.MarkRead(m);
                if (s.OvertimeOffered) s.DeclineOvertime();
            }
            Assert.AreEqual(ShiftPhase.Complete, s.Phase, $"DAY{day}: 夜が終わる");
            return (maxIdle, clock);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(12)] [TestCase(16)] [TestCase(20)] [TestCase(24)] [TestCase(28)] [TestCase(30)]
        public void NoIdleStretchOfTenSeconds(int day)
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var (maxIdle, night) = Play(day, seed);
                TestContext.WriteLine($"DAY{day} seed{seed}: 最長の待ち {maxIdle:0.0} 秒 / 夜の長さ {night / 60f:0.0} 分");
                Assert.Less(maxIdle, 10f, $"DAY{day} seed{seed}: 何もすることがない時間が {maxIdle:0.0} 秒続いた");
            }
        }

        [Test]
        public void FastForward_StartsAfterThreeIdleSeconds_AndStopsWhenWorkArrives()
        {
            var s = TestData.Session(20, 1);
            s.StartShift();
            for (int i = 0; i < 32 && !s.HasWork; i++) s.Tick(Dt);
            if (s.HasWork) Assert.Inconclusive("最初の仕事が早く届いた");
            Assert.IsTrue(s.FastForwarding, "3秒何もなければ早送り");
            for (int i = 0; i < 200 && !s.HasWork; i++) s.Tick(Dt);
            Assert.IsTrue(s.HasWork, "早送り・時刻の繰り上げで次の仕事が届く");
            s.Tick(Dt);
            Assert.IsFalse(s.FastForwarding, "仕事がある間は通常の速さ");
        }

        [Test]
        public void SkipNeverJumpsPastQuestTimers()
        {
            var s = TestData.Session(20, 1);
            s.StartShift();
            float fired = -1;
            s.Quests.At(130, "TEST", () => fired = s.Time.CurrentMinute);
            for (int i = 0; i < 20000 && fired < 0 && s.Phase == ShiftPhase.Running; i++)
            {
                s.Tick(Dt);
                foreach (var e in new List<EventInstance>(s.Requests.Pending)) s.Requests.Decide(e, PlayerAction.Block);
                foreach (var t in s.HelpDesk.Tickets) if (t.IsOpen) t.State = TicketState.Resolved;
            }
            Assert.That(fired, Is.InRange(130f, 131f), "サブクエストの作業時刻を飛び越さない");
        }
    }
}
