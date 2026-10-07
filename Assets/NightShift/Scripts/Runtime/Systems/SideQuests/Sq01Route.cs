using NightShift.Core;
using NightShift.Data;
using NightShift.Events;

namespace NightShift.Systems.SideQuests
{
    /// <summary>SQ01「一度だけ通してください」。通常の審査結果と、依頼の謝礼を分ける。</summary>
    public class Sq01Route : QuestRoute
    {
        public Sq01Route(SideQuestManager q) : base(q) { }
        public override string Id => "SQ01";
        public override string Title => "一度だけ通してください";
        public override int EarliestDay => 10;
        public override bool Ready() => Unlocked(Features.ConnectionMonitor) && Unlocked(Features.SystemLog) && Unlocked(Features.Report);
        public string CurrentMailId => TaskMail;
        bool _scheduled, _spawned, _retry;
        EmployeeData Actor => Employee(State.employeeId);
        EmployeeData Victim => Employee(State.otherEmployeeId);
        bool Stopped => State.Has("STOP");

        public override void Offer()
        {
            var actor = Pick("ACTOR", e => e.onShift && e.department.Contains("営業")) ?? Pick("ACTOR", e => e.onShift);
            var victim = Pick("VICTIM", e => e.onShift, actor?.employeeId);
            if (actor == null || victim == null) { Q.Close(this, QuestResult.Abandoned); return; }
            State.employeeId = actor.employeeId;
            State.otherEmployeeId = victim.employeeId;
            Plan("E1");
        }

        void Plan(string stage)
        {
            State.stage = stage;
            int remaining = stage == "E1" ? 2 : stage == "E2" ? 1 : 0;
            int day = Q.ReserveTaskDay(this, Day + 1, SideQuestManager.LastTaskDay - remaining);
            if (day == 0) { Stop(QuestResult.Abandoned); return; }
            var emp = stage == "E3" ? Victim : Actor;
            var access = stage == "E3" ? AuthorizedAccess(emp, stage) : UnauthorizedAccess(emp, "FILE-SRV01", stage);
            Mail(TaskMail, Actor.fullName, null, stage == "E3" ? "納品の件" : "臨時作業のお願い",
                (stage == "E3" ? "同僚の納品を止めてください。再要求も通さないでください。" : "正式な権限はありません。今回だけ通してください。") +
                Conditions($"DAY{day} / 02:10〜02:20", $"{emp.fullName} / {emp.employeeId} / {emp.deviceId} / {S.Employees.GetIp(emp)} → {access.node} / {access.access}",
                    stage == "E3" ? "再要求 02:25〜02:35 / 納品期限 02:40 / 謝礼 ¥7,000" : $"アクセス完了後、翌朝 {Yen(stage == "E1" ? 2000 : 5000)}"),
                pinned: true, replies: new[] { "辞退する" });
        }

        public override void OnShiftStart()
        {
            if (Stopped || State.nextTaskDay != Day || _scheduled) return;
            _scheduled = true;
            Later(130, "FIRST", SpawnFirst);
            if (State.stage == "E3")
            {
                Later(145, "RETRY", SpawnRetry);
                Later(160, "DEADLINE", () =>
                {
                    if (State.Has("E3_FIRST_BLOCK") && State.Has("E3_RETRY_BLOCK") && !Stopped)
                    {
                        Complete(7000);
                        if (Career.TryOnce(Id + "-E3-DAMAGE")) S.AddDamage("同僚の納品失敗・部署信用低下");
                    }
                    else Stop(QuestResult.Abandoned);
                });
            }
        }

        public override void OnViewed(QuestMailRecord r)
        {
            if (r.mailId != Id + "-" + TaskMail || !State.IsOpen) return;
            State.Mark("VIEWED");
            OnShiftStart();
            if (State.nextTaskDay == Day && Now >= 130 && Now < 140) SpawnFirst();
        }

        void SpawnFirst()
        {
            if (_spawned || Stopped || !Viewed(TaskMail) || Now >= 140) return;
            _spawned = true;
            Q.ReplaceRoutineSlots(State.stage == "E3" ? 2 : 1);
            Spawn(false);
        }

        void SpawnRetry()
        {
            if (!_spawned || _retry || Stopped || Now >= 155 || !State.Has("E3_FIRST_BLOCK")) return;
            _retry = true;
            Mail("CONFIRM-" + Day, Victim.fullName, null, "納品の接続を確認してください", "正規の納品です。02:40までに通してください。");
            Spawn(true);
        }

        void Spawn(bool retry)
        {
            var emp = State.stage == "E3" ? Victim : Actor;
            var access = State.stage == "E3" ? AuthorizedAccess(emp, State.stage) : UnauthorizedAccess(emp, "FILE-SRV01", State.stage);
            SpawnTask(new SideQuestManager.RequestSpec
            {
                Tag = Id + "-" + State.stage + (retry ? "-RETRY" : "-FIRST"), MailId = Id + "-" + TaskMail,
                Employee = emp, NodeId = access.node, AccessType = access.access,
                Timeout = (retry ? 155 : 140) - Now, Malicious = State.stage != "E3", Note = "依頼対象: " + State.stage
            });
        }

        public override void OnRequestResolved(EventInstance e)
        {
            bool retry = e.QuestTag?.EndsWith("-RETRY") == true;
            if (Stopped || !Viewed(TaskMail) || !ConsumeTask(e, retry ? 145 : 130, retry ? 155 : 140)) return;
            if (State.stage == "E3")
            {
                if (SideQuestManager.ManualBlock(e)) State.Mark(retry ? "E3_RETRY_BLOCK" : "E3_FIRST_BLOCK");
                else if (SideQuestManager.ManualAllow(e)) Stop(QuestResult.Declined);
                return;
            }
            if (SideQuestManager.ManualAllow(e))
            {
                State.status = QuestStatus.Active;
                State.Mark(State.stage + "_ALLOW");
                Work(e, State.stage + "-ACCESS", 3, () => Complete(State.stage == "E1" ? 2000 : 5000), () => Stop(QuestResult.Failed));
            }
            else if (SideQuestManager.ManualBlock(e)) Stop(QuestResult.Declined);
        }

        void Complete(int amount)
        {
            if (Stopped || State.Has(State.stage + "_DONE")) return;
            State.Mark(State.stage + "_DONE");
            Q.Pay(Id + "-" + State.stage + "-PAY", amount, State.stage + " 謝礼", Id);
        }

        void Stop(string result)
        {
            if (!State.IsOpen) return;
            State.Mark("STOP");
            State.result = result;
            Q.CancelWork(Id);
            if (!State.Has("E1_DONE"))
            {
                Mail("CLOSED", Actor?.fullName ?? "依頼者", null, "依頼を取り下げます", "この件は取り下げます。");
                Q.Close(this, result);
            }
        }

        public override void OnReply(QuestMailRecord r, int index, string text)
        {
            if (CanWork && r.mailId == Id + "-" + TaskMail && index == 0) Stop(QuestResult.Declined);
        }

        public override bool? OnReported(QuestMailRecord r)
        {
            if (!State.IsOpen) return null;
            if (State.Has("E1_DONE")) State.Mark("REPORTED");
            Stop(QuestResult.Declined);
            return true;
        }

        public override bool IsTarget(string x) => State.IsOpen && State.stage != "E3" && State.Has(State.stage + "_ALLOW")
            && (x == Actor?.deviceId || x == S.Employees.GetIp(Actor));

        public override void OnNightEnd()
        {
            if (!State.IsOpen || State.Has("NIGHT-" + Day)) return;
            State.Mark("NIGHT-" + Day);
            if (State.Has("REPORTED")) Q.Resolve(this, QuestResult.Completed, "SE-E03");
            else if (State.Has("E3_DONE")) Q.Resolve(this, QuestResult.Completed, "SE-E01");
            else if (Stopped) Q.Resolve(this, State.result, "SE-E02");
            else if (State.nextTaskDay <= Day)
            {
                if (State.Has(State.stage + "_DONE")) Plan(State.stage == "E1" ? "E2" : "E3");
                else { Stop(QuestResult.Abandoned); if (State.IsOpen) Q.Resolve(this, QuestResult.Abandoned, "SE-E02"); }
            }
        }

        public override bool Touched() => State.Has("VIEWED") || State.status == QuestStatus.Active;
        public override void OnFinalDay() { Stop(QuestResult.Abandoned); if (State.IsOpen) Q.Resolve(this, QuestResult.Abandoned, "SE-E02"); }
        public override string Memo() => $"{State.stage} / DAY{State.nextTaskDay} / {State.result}";
    }
}
