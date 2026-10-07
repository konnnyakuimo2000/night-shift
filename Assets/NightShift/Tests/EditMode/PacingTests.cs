using System.Collections.Generic;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NightShift.Tests
{
    /// <summary>仕事の量: 業務中、何もすることがない時間を10秒（実時間）以上つくらない。手が空いたら仕事が追加で届く</summary>
    public class PacingTests : NightShiftTest
    {
        const float Dt = 0.1f;   // 1フレーム（実時間・秒）

        public struct Result { public float MaxIdle, Night, MinNight; public int Handled, Extra; }

        /// <summary>プレイヤー役: 届いた仕事を reaction 秒考えてから正しく片付ける</summary>
        static Result Play(int day, int seed, float reaction)
        {
            var s = TestData.Session(day, seed);
            s.StartShift();
            var seen = new Dictionary<object, float>();
            float clock = 0, idle = 0, maxIdle = 0;
            int handled = 0;
            bool Ready(object o) { if (!seen.ContainsKey(o)) seen[o] = clock; return clock - seen[o] >= reaction; }
            for (int i = 0; i < 30000 && s.Phase != ShiftPhase.Complete; i++)
            {
                s.Tick(Dt);
                clock += Dt;
                if (s.Phase == ShiftPhase.Running)
                {
                    if (s.HasWork) idle = 0; else idle += Dt;
                    // 終業の予告（残り10分）の後は、新しい仕事を出さない
                    if (s.Time.CurrentMinute < s.Day.endWarningMinute) maxIdle = Mathf.Max(maxIdle, idle);
                }
                foreach (var e in new List<EventInstance>(s.Requests.Pending))
                    if (Ready(e)) { s.Requests.Decide(e, e.Expected == ExpectedAction.Allow ? PlayerAction.Allow : PlayerAction.Block); handled++; }
                foreach (var t in new List<Ticket>(s.HelpDesk.Tickets))
                {
                    if (!t.IsOpen || !Ready(t)) continue;
                    if (t.Type == HelpDeskType.Binary) s.HelpDesk.Answer(t, t.CorrectIndex, s.OperatorName);
                    else t.State = TicketState.Resolved;
                    handled++;
                }
                if (s.OpenTaskCount > 0 && Ready(s.Tasks)) s.MarkPasswordChanged();
                if (s.OvertimeOffered) s.DeclineOvertime();
            }
            Assert.AreEqual(ShiftPhase.Complete, s.Phase, $"DAY{day}: 夜が終わる");
            return new Result
            {
                MaxIdle = maxIdle, Night = clock, Handled = handled, Extra = s.Director.ExtraSpawned,
                MinNight = (s.Day.endMinute - s.Day.startMinute) * s.Day.realSecondsPerGameMinute
            };
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(12)] [TestCase(16)] [TestCase(20)] [TestCase(24)] [TestCase(28)] [TestCase(30)]
        public void NoIdleStretchOfTenSeconds_AndNightLengthUnchanged(int day)
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var r = Play(day, seed, 2.5f);
                TestContext.WriteLine($"DAY{day} seed{seed}: 最長の待ち {r.MaxIdle:0.0} 秒 / 夜 {r.Night / 60f:0.0} 分 / 処理 {r.Handled} 件（うち追加 {r.Extra} 件）");
                Assert.Less(r.MaxIdle, 10f, $"DAY{day} seed{seed}: 何もすることがない時間が {r.MaxIdle:0.0} 秒続いた");
                Assert.GreaterOrEqual(r.Night, r.MinNight - 0.5f, "時計は早送りしない（夜の長さは変わらない）");
            }
        }

        [TestCase(8)] [TestCase(20)] [TestCase(30)]
        public void FasterPlayersHandleMoreTasks(int day)
        {
            int fast = 0, slow = 0;
            for (int seed = 1; seed <= 3; seed++)
            {
                fast += Play(day, seed, 1.5f).Handled;
                slow += Play(day, seed, 6f).Handled;
            }
            TestContext.WriteLine($"DAY{day}: 速いプレイヤー {fast / 3f:0.0} 件 / 遅いプレイヤー {slow / 3f:0.0} 件（1夜あたり）");
            Assert.Greater(fast, slow * 1.2f, "速く片付ける人ほど、多くの仕事を扱える");
        }

        [Test]
        public void NoExtraWorkAfterEndWarning()
        {
            var s = TestData.Session(20, 1);
            s.StartShift();
            s.Time.JumpTo(s.Day.endWarningMinute + 1);
            int before = s.Director.ExtraSpawned;
            for (int i = 0; i < 100 && s.Phase == ShiftPhase.Running; i++)
            {
                s.Tick(Dt);
                foreach (var e in new List<EventInstance>(s.Requests.Pending)) s.Requests.Decide(e, PlayerAction.Block);
                foreach (var t in s.HelpDesk.Tickets) if (t.IsOpen) t.State = TicketState.Resolved;
            }
            Assert.AreEqual(before, s.Director.ExtraSpawned, "終業の予告の後は追加しない");
        }
    }
}
