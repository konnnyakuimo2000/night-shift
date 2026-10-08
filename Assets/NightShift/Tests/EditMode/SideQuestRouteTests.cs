using System.Linq;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using NightShift.Systems.SideQuests;
using NightShift.UI.Shift;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>SUBQUEST_SPEC §8（7〜11）、HACKER_ROUTE_SPEC §6。通常の審査・入金・分岐を別々に確認する。</summary>
    public class SideQuestRouteTests : NightShiftTest
    {
        OperatorWallet _wallet;
        [SetUp]
        public void Wallet()
        {
            _wallet = new OperatorWallet();
            SideQuestManager.WalletProvider = () => _wallet;
        }

        static ShiftSession Start(int day)
        {
            var s = TestData.Session(day);
            s.StartShift();
            return s;
        }
        static void Advance(ShiftSession s, float to)
        {
            for (float t = s.Time.CurrentMinute; t <= to && !s.Time.Paused; t += 0.25f)
            {
                s.Time.JumpTo(t);
                s.Requests.Tick(t);
                s.Quests.Tick(t, 0.25f);
            }
        }
        static MailMessage TaskMail(ShiftSession s, string id)
        {
            var st = s.Quests.Route(id).State;
            return s.Quests.Message(id, st.stage + "-DAY" + st.nextTaskDay);
        }
        static void View(ShiftSession s, string id) => s.Quests.OnMailShown(TaskMail(s, id));
        static void Reply(ShiftSession s, string mailId, int index)
        {
            var mail = s.Mail.Find(mailId);
            Assert.IsNotNull(mail, mailId);
            Assert.IsTrue(s.Mail.SendReply(mail, index, mail.Replies[index]));
        }
        static EventInstance Request(ShiftSession s, string id) => s.Requests.Pending.Single(e => e.QuestTag?.StartsWith(id + "-") == true);
        static EventInstance Decide(ShiftSession s, string id, PlayerAction action, float time, bool automatic = false)
        {
            Advance(s, time);
            var e = Request(s, id);
            e.AutoDecided = automatic;
            s.Requests.Decide(e, action);
            return e;
        }
        static ShiftSession NextTask(ShiftSession s, string id)
        {
            int day = s.Quests.Route(id).State.nextTaskDay;
            Assert.That(day, Is.InRange(s.Day.dayNumber + 1, 29));
            var next = Start(day);
            View(next, id);
            return next;
        }
        static ShiftSession Offer(string id, int day = 20)
        {
            var s = Start(day);
            s.Quests.DebugOffer(id);
            return s;
        }
        static ShiftSession EmployeeStage2()
        {
            var s = NextTask(Offer("SQ01"), "SQ01");
            Decide(s, "SQ01", PlayerAction.Allow, 130);
            Advance(s, 133);
            s.Quests.OnNightEnd();
            return NextTask(s, "SQ01");
        }
        static ShiftSession EmployeeStage3()
        {
            var s = EmployeeStage2();
            Decide(s, "SQ01", PlayerAction.Allow, 130);
            Advance(s, 133);
            s.Quests.OnNightEnd();
            return NextTask(s, "SQ01");
        }
        static ShiftSession RivalFirst()
        {
            var s = Offer("SQ02");
            Reply(s, "SQ02-INVITE", 0);
            Assert.AreEqual(QuestStatus.Offered, s.Quests.Route("SQ02").State.status, "相談への回答だけでは加担しない");
            return NextTask(s, "SQ02");
        }
        static void Trade(ShiftSession s)
        {
            Decide(s, "SQ02", PlayerAction.Allow, 130);
            Advance(s, 133);
            Decide(s, "SQ02", PlayerAction.Allow, 145);
            Advance(s, 148);
        }
        static ShiftSession HackerFirst(int day = 17)
        {
            var s = Offer("SQ06", day);
            Reply(s, "SQ06-INVITE", 0);
            return NextTask(s, "SQ06");
        }
        static ShiftSession HackerSecond()
        {
            var s = HackerFirst();
            Decide(s, "SQ06", PlayerAction.Block, 130);
            s.Quests.OnNightEnd();
            return NextTask(s, "SQ06");
        }
        static ShiftSession HackerFinal(bool cooperate = false)
        {
            var s = HackerSecond();
            Decide(s, "SQ06", cooperate ? PlayerAction.Allow : PlayerAction.Block, 140);
            if (cooperate) Advance(s, 143);
            else Reply(s, "SQ06-N2-STOP", 1);
            s.Quests.OnNightEnd();
            return NextTask(s, "SQ06");
        }

        // ================= SQ01 =================
        [Test]
        public void Employee_IllegalApprovalKeepsNormalPenalty_AndPaysOnlyOnCompletion()
        {
            var s = NextTask(Offer("SQ01"), "SQ01");
            int before = _wallet.Balance;
            var e = Decide(s, "SQ01", PlayerAction.Allow, 130);
            Assert.AreEqual(ExpectedAction.PolicyDerived, e.Definition.expectedAction);
            Assert.AreEqual(ExpectedAction.Block, e.Expected);
            Assert.AreEqual(1, s.Evaluation.MaliciousAllow);
            Assert.AreEqual(0, s.Quests.PendingPay("SQ01"));
            Advance(s, 133);
            Assert.AreEqual(2000, s.Quests.PendingPay("SQ01"));
            Assert.AreEqual(before, _wallet.Balance);
            s.Quests.OnNightEnd(); s.Quests.OnNightEnd();
            Assert.AreEqual(before + 2000, _wallet.Balance);
            Assert.AreEqual(1, _wallet.ledger.Count(e1 => e1.source == "SQ01"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Employee_FirstRejectionOrReportHasNoEnding(bool report)
        {
            var s = NextTask(Offer("SQ01"), "SQ01");
            if (report) s.Quests.OnMailReported(TaskMail(s, "SQ01"));
            else Decide(s, "SQ01", PlayerAction.Block, 130);
            s.Quests.OnNightEnd();
            Assert.AreEqual(QuestStatus.Resolved, s.Quests.Route("SQ01").State.status);
            Assert.IsNull(s.Quests.Route("SQ01").State.endingId);
            Assert.AreEqual(0, _wallet.SumSource("SQ01"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Employee_RetryRescueOrTimeoutDoesNotEarnSabotageReward(bool rescue)
        {
            var s = EmployeeStage3();
            var e = Decide(s, "SQ01", PlayerAction.Block, 130);
            Assert.AreEqual(ExpectedAction.Allow, e.Expected, "同僚の要求は正規");
            Assert.AreEqual(1, s.Evaluation.FalsePositive);
            if (rescue) Decide(s, "SQ01", PlayerAction.Allow, 145);
            Advance(s, 161);
            s.Quests.OnNightEnd();
            Assert.AreEqual("SE-E02", s.Quests.Route("SQ01").State.endingId);
            Assert.AreEqual(7000, _wallet.SumSource("SQ01"), "以前の2000+5000だけを残す");
            Assert.AreEqual(0, s.Damage.Count);
        }

        [TestCase(false, "SE-E01")]
        [TestCase(true, "SE-E03")]
        public void Employee_FullCompletionAndReportPriority(bool report, string ending)
        {
            var s = EmployeeStage3();
            Decide(s, "SQ01", PlayerAction.Block, 130);
            Decide(s, "SQ01", PlayerAction.Block, 145);
            Advance(s, 160);
            if (report) s.Quests.OnMailReported(TaskMail(s, "SQ01"));
            s.Quests.OnNightEnd(); s.Quests.OnNightEnd();
            Assert.AreEqual(ending, s.Quests.Route("SQ01").State.endingId);
            Assert.AreEqual(14000, _wallet.SumSource("SQ01"));
            Assert.AreEqual(1, s.Damage.Count);
            Assert.AreEqual(1, CareerRecord.Current.subEndingsSeen.Count(x => x.StartsWith("SE-E")));
        }

        // ================= SQ02 =================
        [TestCase(false)]
        [TestCase(true)]
        public void Rival_AcquisitionRejectedOrInterruptedNeverSpawnsTransfer(bool interrupt)
        {
            var s = RivalFirst();
            var e = Decide(s, "SQ02", interrupt ? PlayerAction.Allow : PlayerAction.Block, 130);
            if (interrupt) s.Security.Isolate(e.Device);
            Advance(s, 155);
            Assert.IsFalse(s.Requests.Processed.Concat(s.Requests.Pending).Any(r => r.QuestTag?.EndsWith("-TRANSFER") == true));
            s.Quests.OnNightEnd();
            Assert.AreEqual(0, _wallet.SumSource("SQ02"));
            Assert.AreEqual(interrupt ? "SE-R03" : null, s.Quests.Route("SQ02").State.endingId);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Rival_TransferIsolationOrDisconnectionStopsPayment(bool block)
        {
            var s = RivalFirst();
            Decide(s, "SQ02", PlayerAction.Allow, 130);
            Advance(s, 133);
            var e = Decide(s, "SQ02", PlayerAction.Allow, 145);
            Assert.AreEqual(Sq02Route.DropId, e.Destination.nodeId);
            Assert.AreEqual(NodeZone.External, e.Destination.zone);
            Assert.AreEqual(ExpectedAction.Block, e.Expected);
            Advance(s, 146);
            if (block) s.Security.Block(Sq02Route.DropId); else s.Security.Isolate(e.Device);
            Advance(s, 150);
            s.Quests.OnNightEnd();
            Assert.AreEqual("SE-R03", s.Quests.Route("SQ02").State.endingId);
            Assert.AreEqual(0, _wallet.SumSource("SQ02"));
            Assert.AreEqual(2, s.Evaluation.MaliciousAllow, "既に行った二つの許可は残る");
            Assert.AreEqual(0, s.Damage.Count, "未流出の被害を重ねない");
        }

        [TestCase(false, "SE-R03")]
        [TestCase(true, "SE-R04")]
        public void Rival_PreviousPaymentSurvivesDeclineOrReport(bool report, string ending)
        {
            var s = RivalFirst(); Trade(s);
            var route = s.Quests.Route("SQ02");
            foreach (var e in s.Requests.Processed.ToArray()) route.OnRequestResolved(e);
            View(s, "SQ02");
            s.Quests.OnNightEnd();
            s = NextTask(s, "SQ02");
            if (report) s.Quests.OnMailReported(TaskMail(s, "SQ02"));
            else Decide(s, "SQ02", PlayerAction.Block, 130);
            s.Quests.OnNightEnd();
            Assert.AreEqual(6000, _wallet.SumSource("SQ02"));
            Assert.AreEqual(ending, s.Quests.Route("SQ02").State.endingId);
        }

        [TestCase(false, "SE-R02")]
        [TestCase(true, "SE-R01")]
        public void Rival_ThreeTradesOfferExplicitEmploymentChoice(bool accept, string ending)
        {
            var s = RivalFirst();
            for (int i = 0; i < 3; i++)
            {
                Trade(s);
                Assert.AreEqual(1, s.Damage.Count);
                Assert.IsNull(EndingResolver.CheckGameOver(CareerRecord.Current, s));
                s.Quests.OnNightEnd();
                if (i < 2) s = NextTask(s, "SQ02");
            }
            Assert.AreEqual(31000, _wallet.SumSource("SQ02"));
            var choice = s.Quests.PendingMorningChoice();
            Assert.IsNotNull(choice);
            s.Quests.ChooseMorning(choice, accept); s.Quests.ChooseMorning(choice, !accept);
            Assert.AreEqual(ending, s.Quests.Route("SQ02").State.endingId);
            Assert.AreEqual(accept, s.Quests.Ended);
            Assert.AreEqual(1, CareerRecord.Current.subEndingsSeen.Count(x => x.StartsWith("SE-R")));
        }

        // ================= SQ06 =================
        [Test]
        public void Hacker_ReadingInviteDoesNotJoin_InitialDeclineIsHarmless()
        {
            var s = Offer("SQ06", 17);
            s.Quests.OnMailShown(s.Mail.Find("SQ06-INVITE"));
            Assert.AreEqual(QuestStatus.Offered, s.Quests.Route("SQ06").State.status);
            Reply(s, "SQ06-INVITE", 1);
            Advance(s, 200);
            Assert.IsFalse(s.Requests.Pending.Any(e => e.QuestTag?.StartsWith("SQ06") == true));
            Assert.AreEqual(0, s.Damage.Count);
            Assert.IsNull(s.Quests.Route("SQ06").State.endingId);
            Assert.AreEqual(0, _wallet.SumSource("SQ06"));
        }

        [Test]
        public void Hacker_N1CorrectBlockHasEvidenceAndNoCash()
        {
            var s = HackerFirst();
            var e = Decide(s, "SQ06", PlayerAction.Block, 130);
            Assert.AreEqual(ExpectedAction.Block, e.Expected);
            Assert.AreEqual(1, s.Evaluation.CorrectBlock);
            Assert.IsTrue(s.Logs.Entries.Any(l => l.Result == "MISMATCH"));
            s.Quests.OnNightEnd();
            Assert.AreEqual("N2", s.Quests.Route("SQ06").State.stage);
            Assert.AreEqual(0, _wallet.SumSource("SQ06"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Hacker_N1FailureRetriesOnceThenCloses(bool allow)
        {
            var s = HackerFirst();
            for (int i = 0; i < 2; i++)
            {
                if (allow) Decide(s, "SQ06", PlayerAction.Allow, 130); else Advance(s, 141);
                Assert.IsNull(s.FatalIncident);
                s.Quests.OnNightEnd();
                if (i == 0) s = NextTask(s, "SQ06");
            }
            Assert.AreEqual(QuestStatus.Resolved, s.Quests.Route("SQ06").State.status);
            Assert.IsNull(s.Quests.Route("SQ06").State.endingId);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Hacker_N2RejectedOrInterruptedCanDeclineWithoutEnding(bool interrupt)
        {
            var s = HackerSecond();
            var e = Decide(s, "SQ06", interrupt ? PlayerAction.Allow : PlayerAction.Block, 140);
            if (interrupt) { Advance(s, 141); s.Security.Isolate(e.Device); }
            Reply(s, "SQ06-N2-STOP", 0);
            Advance(s, 150); s.Quests.OnNightEnd();
            Assert.IsNull(s.Quests.Route("SQ06").State.endingId);
            Assert.AreEqual(0, _wallet.SumSource("SQ06"));
            Assert.AreEqual(0, s.Damage.Count);
        }

        [TestCase("REJECT")]
        [TestCase("INTERRUPT")]
        [TestCase("EXPIRE")]
        public void Hacker_N2NoReplyClosesWithoutEndingAtEndOfFollowingNight(string stop)
        {
            var s = HackerSecond();
            if (stop == "EXPIRE") Advance(s, 151);
            else
            {
                var e = Decide(s, "SQ06", stop == "INTERRUPT" ? PlayerAction.Allow : PlayerAction.Block, 140);
                if (stop == "INTERRUPT") { Advance(s, 141); s.Security.Isolate(e.Device); }
            }
            s.Quests.OnNightEnd(); s.Quests.OnNightEnd();
            Assert.AreEqual(QuestStatus.Active, s.Quests.Route("SQ06").State.status, "停止した夜は返信待ち");
            string saved = CareerRecord.Current.Snapshot();
            CareerRecord.Restore(saved);
            s = Start(s.Day.dayNumber + 1);
            s.Quests.OnMailShown(s.Mail.Find("SQ06-N2-STOP"));
            Advance(s, 359);
            Assert.AreEqual(QuestStatus.Active, s.Quests.Route("SQ06").State.status, "翌夜の業務中は返信可能");
            s.Quests.OnNightEnd(); s.Quests.OnNightEnd();
            var state = s.Quests.Route("SQ06").State;
            Assert.AreEqual(QuestStatus.Resolved, state.status);
            Assert.AreEqual(QuestResult.Abandoned, state.result);
            Assert.That(state.endingId, Is.Null.Or.Empty, "保存・復元後も結末IDが付いていないこと");
            Assert.IsFalse(s.Quests.Ended);
            Assert.AreEqual(0, _wallet.SumSource("SQ06"));
            Reply(s, "SQ06-N2-STOP", 1);
            Assert.AreEqual(QuestStatus.Resolved, state.status, "終了後の返信では再開しない");
            Assert.AreEqual("N2", state.stage);
        }

        [Test]
        public void Hacker_N2ReplyDuringFollowingNightStillOpensDefenseRoute()
        {
            var s = HackerSecond();
            Decide(s, "SQ06", PlayerAction.Block, 140);
            s.Quests.OnNightEnd();
            s = Start(s.Day.dayNumber + 1);
            Advance(s, 359);
            Reply(s, "SQ06-N2-STOP", 1);
            s.Quests.OnNightEnd();
            Assert.AreEqual(QuestStatus.Active, s.Quests.Route("SQ06").State.status);
            Assert.AreEqual("N3", s.Quests.Route("SQ06").State.stage);
            Assert.AreEqual(s.Day.dayNumber + 1, s.Quests.Route("SQ06").State.nextTaskDay);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Hacker_DefenseDoesNotRequireN2ComplicityOrTool(bool cooperate)
        {
            var s = HackerFinal(cooperate);
            Assert.IsFalse(CareerRecord.Current.Owns(Items.SignalDesk));
            Decide(s, "SQ06", PlayerAction.Block, 170);
            s.Quests.OnMailAction(TaskMail(s, "SQ06"), "DEFEND");
            s.Quests.OnMailAction(TaskMail(s, "SQ06"), "DEFEND");
            Assert.IsTrue(s.Security.IsIsolated(Sq06Route.FakeDevice));
            Assert.IsTrue(s.Security.WasReported(Sq06Route.SourceIp));
            Assert.AreEqual("SE-N03", s.Quests.Route("SQ06").State.endingId);
            Assert.IsFalse(s.Quests.Ended);
            Assert.AreEqual(1, s.CorrectReports);
            Assert.AreEqual(cooperate ? 5000 : 0, _wallet.SumSource("SQ06"));
        }

        [Test]
        public void Hacker_N3CanBeStoppedBeforeCompletionAndReported()
        {
            var s = HackerFinal();
            var e = Decide(s, "SQ06", PlayerAction.Allow, 170);
            Assert.IsNotEmpty(e.QuestConfirm);
            Advance(s, 171);
            s.Quests.OnMailAction(TaskMail(s, "SQ06"), "DEFEND");
            Advance(s, 175);
            Assert.AreEqual("SE-N03", s.Quests.Route("SQ06").State.endingId);
            Assert.AreEqual(0, _wallet.SumSource("SQ06"));
            Assert.AreEqual(0, s.Damage.Count);
        }

        [TestCase(false, "SE-N02")]
        [TestCase(true, "SE-N01")]
        public void Hacker_CompletionPausesForExplicitChoice_AndDoesNotAlsoCauseGO2(bool join, string ending)
        {
            var s = HackerFinal(true);
            int endings = 0;
            s.Quests.TerminalEnding += _ => endings++;
            Decide(s, "SQ06", PlayerAction.Allow, 170);
            Assert.IsNull(s.Quests.PendingChoice, "許可だけでは加入しない");
            Advance(s, 173);
            Assert.AreEqual(15000, _wallet.SumSource("SQ06"));
            Assert.AreEqual(1, s.Damage.Count);
            Assert.IsNull(EndingResolver.CheckGameOver(CareerRecord.Current, s));
            Assert.IsTrue(s.Time.Paused);
            float now = s.Time.CurrentMinute;
            s.Tick(100);
            Assert.AreEqual(now, s.Time.CurrentMinute, "二択の裏で期限が進まない");
            var choice = s.Quests.PendingChoice;
            Assert.IsNotNull(choice);
            s.Quests.ChooseMorning(choice, join); s.Quests.ChooseMorning(choice, !join);
            Assert.AreEqual(ending, s.Quests.Route("SQ06").State.endingId);
            Assert.AreEqual(1, endings);
        }

        [Test]
        public void Hacker_PostCompletionReportCannotUndoAttack()
        {
            var s = HackerFinal();
            Decide(s, "SQ06", PlayerAction.Allow, 170); Advance(s, 173);
            s.Quests.OnMailReported(TaskMail(s, "SQ06"));
            Assert.AreEqual("SE-N02", s.Quests.Route("SQ06").State.endingId);
            Assert.AreEqual(1, s.Damage.Count);
            Assert.AreEqual(10000, _wallet.SumSource("SQ06"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Hacker_N3RejectionOnlyOrExpiryDoesNotStopCompany(bool reject)
        {
            var s = HackerFinal();
            if (reject) Decide(s, "SQ06", PlayerAction.Block, 170);
            Advance(s, 181); s.Quests.OnNightEnd();
            Assert.IsNull(s.Quests.Route("SQ06").State.endingId);
            Assert.AreEqual(0, s.Damage.Count);
            Assert.AreEqual(0, _wallet.SumSource("SQ06"));
        }

        [Test]
        public void Hacker_PreexistingFatalIncidentWinsOverSubEnding()
        {
            var s = HackerFinal();
            Decide(s, "SQ06", PlayerAction.Allow, 170);
            s.AddDamage("別の致命的事件", true);
            Advance(s, 173);
            Assert.IsNotNull(EndingResolver.CheckGameOver(CareerRecord.Current, s));
            Assert.IsNull(s.Quests.PendingChoice);
            Assert.IsFalse(s.Quests.Route("SQ06").State.Has("N3_DONE"));
            Assert.AreEqual(0, _wallet.SumSource("SQ06"));
        }

        [Test]
        public void Hacker_ActiveHorrorPostponesFinalTaskUntilResolved()
        {
            var s = HackerSecond();
            Decide(s, "SQ06", PlayerAction.Block, 140);
            CareerRecord.Current.Quest("SQ03").status = QuestStatus.Active;
            Reply(s, "SQ06-N2-STOP", 1);
            Assert.AreEqual(0, s.Quests.Route("SQ06").State.nextTaskDay);
            s.Quests.OnNightEnd();
            Assert.AreEqual(0, s.Quests.Route("SQ06").State.nextTaskDay);
            s = Start(s.Day.dayNumber + 1);
            CareerRecord.Current.Quest("SQ03").status = QuestStatus.Resolved;
            s.Quests.OnNightEnd();
            Assert.AreEqual(s.Day.dayNumber + 1, s.Quests.Route("SQ06").State.nextTaskDay);
        }

        [Test]
        public void Hacker_LastOfferAndOneRetryStillFitBeforeDay30()
        {
            var s = HackerFirst(23);
            Advance(s, 141); s.Quests.OnNightEnd();
            s = NextTask(s, "SQ06");
            Decide(s, "SQ06", PlayerAction.Block, 130); s.Quests.OnNightEnd();
            s = NextTask(s, "SQ06");
            Decide(s, "SQ06", PlayerAction.Block, 140); Reply(s, "SQ06-N2-STOP", 1);
            s.Quests.OnNightEnd();
            s = NextTask(s, "SQ06");
            Assert.LessOrEqual(s.Day.dayNumber, 29);
        }

        // ================= 操作・復帰の境界 =================
        [TestCase("SQ01")]
        [TestCase("SQ02")]
        [TestCase("SQ06")]
        public void Routes_UnreadTaskDoesNotGenerateExtraRequests(string id)
        {
            var s = Offer(id);
            if (id != "SQ01") Reply(s, id + "-INVITE", 0);
            s = Start(s.Quests.Route(id).State.nextTaskDay);
            Advance(s, 190);
            Assert.IsFalse(s.Requests.Pending.Concat(s.Requests.Processed).Any(e => e.QuestTag?.StartsWith(id + "-") == true));
            Assert.AreEqual(0, s.Quests.PendingPay(id));
        }

        [TestCase("SQ01")]
        [TestCase("SQ02")]
        [TestCase("SQ06")]
        public void Routes_AutomaticOrForgedRequestCannotProgress(string id)
        {
            var s = id == "SQ01" ? NextTask(Offer(id), id) : id == "SQ02" ? RivalFirst() : HackerFinal();
            float time = id == "SQ06" ? 170 : 130;
            Advance(s, time);
            var actual = Request(s, id);
            var forged = new EventInstance { QuestTag = actual.QuestTag, State = EventState.Allowed, Employee = actual.Employee, Device = actual.Device, SourceIp = actual.SourceIp, Destination = actual.Destination, AccessType = actual.AccessType };
            s.Quests.Route(id).OnRequestResolved(forged);
            actual.AutoDecided = true;
            s.Requests.Decide(actual, PlayerAction.Allow);
            Advance(s, time + 5);
            Assert.AreEqual(0, s.Quests.PendingPay(id));
            Assert.IsNull(s.Quests.PendingChoice);
            Assert.IsFalse(s.Quests.Jobs.Any(j => j.Id.StartsWith(id + "-")));
        }

        [TestCase(139.75f, true)]
        [TestCase(140f, false)]
        [TestCase(140.25f, false)]
        public void Employee_ApprovalWindowIsHalfOpen(float time, bool valid)
        {
            var s = NextTask(Offer("SQ01"), "SQ01");
            Advance(s, 130);
            var e = Request(s, "SQ01");
            s.Time.JumpTo(time);
            s.Requests.Decide(e, PlayerAction.Allow);
            Advance(s, time + 3);
            Assert.AreEqual(valid ? 2000 : 0, s.Quests.PendingPay("SQ01"));
        }

        [Test]
        public void Routes_ReserveDifferentNightsAndReplaceRoutineSlots()
        {
            var s = Offer("SQ01");
            s.Quests.DebugOffer("SQ02"); Reply(s, "SQ02-INVITE", 0);
            Assert.AreNotEqual(s.Quests.Route("SQ01").State.nextTaskDay, s.Quests.Route("SQ02").State.nextTaskDay);
            s = NextTask(s, "SQ01");
            int before = s.Director.Scheduler.Items.Count(i => i.Label.StartsWith("ROUTINE ") && !i.Fired);
            Advance(s, 130);
            Assert.AreEqual(before - 1, s.Director.Scheduler.Items.Count(i => i.Label.StartsWith("ROUTINE ") && !i.Fired));
        }

        [Test]
        public void Hacker_RetryRestoresMoneyDamageAndProgressTogether()
        {
            var s = HackerSecond();
            string career = CareerRecord.Current.Snapshot(), wallet = _wallet.Snapshot();
            Decide(s, "SQ06", PlayerAction.Allow, 140); Advance(s, 143); s.Quests.OnNightEnd();
            Assert.AreEqual(5000, _wallet.SumSource("SQ06"));
            Assert.AreEqual(1, s.Damage.Count);
            CareerRecord.Restore(career); _wallet.Restore(wallet);
            var retry = Start(s.Day.dayNumber);
            Assert.AreEqual(0, _wallet.SumSource("SQ06"));
            Assert.AreEqual(0, retry.Damage.Count);
            Assert.IsFalse(retry.Quests.Route("SQ06").State.Has("N2_DONE"));
            View(retry, "SQ06");
            Decide(retry, "SQ06", PlayerAction.Allow, 140); Advance(retry, 143); retry.Quests.OnNightEnd();
            Assert.AreEqual(5000, _wallet.SumSource("SQ06"));
            Assert.AreEqual(1, _wallet.ledger.Count(e => e.source == "SQ06"));
        }

        [Test]
        public void Hacker_SignalDeskUnlocksOnlyAfterReplyAndIsNotConsumable()
        {
            var s = Offer("SQ06", 17);
            var item = s.Database.FindItem(Items.SignalDesk);
            Assert.AreEqual(3000, item.price);
            Assert.AreEqual(Sq06Route.InvitedFlag, item.questFlag);
            Assert.IsFalse(item.consumable, "非消耗品は既存購買の購入済みチェック対象");
            Assert.AreEqual(1, item.maxStock);
            Assert.IsFalse(CareerRecord.Current.HasFlag(item.questFlag));
            Assert.IsFalse(StoreView.OnSale(item, 17));
            Reply(s, "SQ06-INVITE", 0);
            Assert.IsTrue(CareerRecord.Current.HasFlag(item.questFlag));
            Assert.IsTrue(StoreView.OnSale(item, 17));
            Assert.IsFalse(CareerRecord.Current.Owns(Items.SignalDesk), "返信で自動購入しない");
            Assert.IsNull(StoreView.StockBlockedReason(item));
            CareerRecord.Current.AddItem(item.itemId);
            Assert.IsNotNull(StoreView.StockBlockedReason(item), "購入済みのツールは再注文できない");
        }

        [Test]
        public void Hacker_CompletedChoiceRestoresWithoutReplayingJobOrPayment()
        {
            var s = HackerFinal(true);
            Decide(s, "SQ06", PlayerAction.Allow, 170); Advance(s, 173);
            string saved = CareerRecord.Current.Snapshot();
            string money = _wallet.Snapshot();
            s.Quests.ChooseMorning(s.Quests.PendingChoice, true);
            CareerRecord.Restore(saved); _wallet.Restore(money);
            var restored = TestData.Session(s.Day.dayNumber);
            restored.Quests.Route<Sq06Route>().ResumeChoice();
            Assert.IsNotNull(restored.Quests.PendingChoice);
            Assert.IsTrue(restored.Time.Paused);
            Assert.AreEqual(0, restored.Quests.Jobs.Count);
            Assert.AreEqual(15000, _wallet.SumSource("SQ06"));
            restored.Quests.ChooseMorning(restored.Quests.PendingChoice, false);
            Assert.AreEqual("SE-N02", restored.Quests.Route("SQ06").State.endingId);
            Assert.AreEqual(15000, _wallet.SumSource("SQ06"));
        }

        [TestCase("SQ01")]
        [TestCase("SQ02")]
        public void PaidRoutes_ReportStopsOutstandingJobAndFutureRequests(string id)
        {
            var s = id == "SQ01" ? EmployeeStage2() : RivalFirst();
            Decide(s, id, PlayerAction.Allow, 130);
            s.Quests.OnMailReported(TaskMail(s, id));
            Advance(s, 160); s.Quests.OnNightEnd();
            Assert.IsFalse(s.Quests.Jobs.Any(j => !j.Finished));
            Assert.AreEqual(id == "SQ01" ? "SE-E03" : "SE-R04", s.Quests.Route(id).State.endingId);
            Assert.AreEqual(id == "SQ01" ? 2000 : 0, _wallet.SumSource(id));
        }
    }
}
