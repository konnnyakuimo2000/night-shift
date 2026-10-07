using NightShift.Core;
using NightShift.Data;
using NightShift.Events;
using UnityEngine;

namespace NightShift.Systems.SideQuests
{
    /// <summary>SQ02「情報提供のお願い」。取得完了後だけ転送を出し、流出ごとに一度だけ入金する。</summary>
    public class Sq02Route : QuestRoute
    {
        public Sq02Route(SideQuestManager q) : base(q) { }
        public override string Id => "SQ02";
        public override string Title => "情報提供のお願い";
        public override int EarliestDay => 16;
        public override bool Ready() => Unlocked(Features.ConnectionMonitor) && Unlocked(Features.SystemLog) && Unlocked(Features.Isolation) && Unlocked(Features.Report);
        public const string DropId = "drop.rival.example";
        const string From = "事業調査窓口", Address = "desk@rival.example";
        public string CurrentMailId => TaskMail;
        EmployeeData Actor => Employee(State.employeeId);
        bool _scheduled, _acquire, _transfer;
        bool Stopped => State.Has("STOP");
        string Material => State.stage == "R1" ? "社外秘の営業計画" : State.stage == "R2" ? "未公開の価格・入札資料" : "次期商品の非公開資料";
        int Reward => State.stage == "R1" ? 6000 : State.stage == "R2" ? 10000 : 15000;

        public override void Offer()
        {
            var emp = Pick("ACTOR", e => e.onShift);
            if (emp == null) { Q.Close(this, QuestResult.Abandoned); return; }
            State.employeeId = emp.employeeId;
            State.stage = "INVITE";
            Mail("INVITE", From, Address, "業務についてのご相談", "資料提供についてご相談があります。条件をお送りします。",
                replies: new[] { "相談を受ける", "辞退する" });
        }

        public override void OnReply(QuestMailRecord r, int index, string text)
        {
            if (!CanWork || Stopped) return;
            if (r.mailId == Id + "-INVITE" && State.stage == "INVITE")
            {
                State.Mark("REPLIED");
                if (index == 0) Plan("R1"); else Stop(QuestResult.Declined);
            }
            else if (r.mailId == Id + "-" + TaskMail && index == 0) Stop(QuestResult.Declined);
        }

        void Plan(string stage)
        {
            State.stage = stage;
            int remaining = stage == "R1" ? 2 : stage == "R2" ? 1 : 0;
            int day = Q.ReserveTaskDay(this, Day + 1, SideQuestManager.LastTaskDay - remaining);
            if (day == 0) { Stop(QuestResult.Abandoned); return; }
            var a = UnauthorizedAccess(Actor, "FILE-SRV01", stage);
            Mail(TaskMail, From, Address, "資料取引 " + stage, "通常の権限では扱えない資料です。転送完了分だけお支払いします。" +
                Conditions($"DAY{day} / 取得 02:10〜02:20 / 転送 02:25〜02:35（許可後3分）",
                    $"{Actor.fullName} / {Actor.employeeId} / {Actor.deviceId} / {S.Employees.GetIp(Actor)} → {a.node} / {a.access}",
                    $"{Material} → {DropId} / 翌朝 {Yen(Reward)} / 隔離・遮断で転送中断",
                    "3取引で契約資料が揃えば採用案内。承諾すると現在の勤務を終了します。"),
                pinned: true, replies: new[] { "辞退する" });
        }

        public override void OnShiftStart()
        {
            if (Stopped || State.nextTaskDay != Day || _scheduled) return;
            _scheduled = true;
            Later(130, "ACQUIRE", SpawnAcquire);
            Later(145, "TRANSFER", SpawnTransfer);
        }

        public override void OnViewed(QuestMailRecord r)
        {
            if (!State.IsOpen || r.mailId != Id + "-" + TaskMail) return;
            State.Mark("VIEWED");
            OnShiftStart();
            if (State.nextTaskDay == Day && Now >= 130 && Now < 140) SpawnAcquire();
        }

        void SpawnAcquire()
        {
            if (_acquire || Stopped || !Viewed(TaskMail) || Now >= 140) return;
            _acquire = true;
            Q.ReplaceRoutineSlots(2);
            var a = UnauthorizedAccess(Actor, "FILE-SRV01", State.stage);
            SpawnTask(new SideQuestManager.RequestSpec
            {
                Tag = Id + "-" + State.stage + "-ACQUIRE", MailId = Id + "-" + TaskMail,
                Employee = Actor, NodeId = a.node, AccessType = a.access, Timeout = 140 - Now, Malicious = true, Note = Material
            });
        }

        void SpawnTransfer()
        {
            if (_transfer || Stopped || !State.Has(State.stage + "_ACQUIRED") || Now >= 155) return;
            _transfer = true;
            // 実際の通信先ではない。資料区分の持出権限を通常の権限審査へ渡す。
            var node = ScriptableObject.CreateInstance<NetworkNodeDefinition>();
            node.hideFlags = HideFlags.DontSave;
            node.nodeId = DropId; node.displayName = "社外資料受取窓口";
            node.zone = NodeZone.External; node.role = NodeRole.Internet; node.ipAddress = "198.51.100.77";
            node.accessTypes.Add(new AccessTypeEntry { accessType = Material + "（社外転送）", requiredPermission = "CONFIDENTIAL_EXPORT", serviceId = "FILE" });
            SpawnTask(new SideQuestManager.RequestSpec
            {
                Tag = Id + "-" + State.stage + "-TRANSFER", MailId = Id + "-" + TaskMail,
                Employee = Actor, NodeId = DropId, Destination = node, AccessType = node.accessTypes[0].accessType,
                Timeout = 155 - Now, Malicious = true, Note = Material + " / 転送3分 / 隔離・遮断で中断"
            });
        }

        public override void OnRequestResolved(EventInstance e)
        {
            bool transfer = e.QuestTag?.EndsWith("-TRANSFER") == true;
            if (Stopped || !Viewed(TaskMail) || !ConsumeTask(e, transfer ? 145 : 130, transfer ? 155 : 140)) return;
            if (SideQuestManager.ManualAllow(e))
            {
                State.status = QuestStatus.Active;
                State.Mark("PARTICIPATED");
                State.Mark(State.stage + (transfer ? "_TRANSFER_ALLOWED" : "_ACQUIRE_ALLOWED"));
                Work(e, State.stage + (transfer ? "-TRANSFER" : "-ACQUIRE"), 3,
                    () => { if (transfer) Complete(); else State.Mark(State.stage + "_ACQUIRED"); },
                    () => Stop(QuestResult.Failed));
            }
            else if (SideQuestManager.ManualBlock(e)) Stop(QuestResult.Declined);
        }

        void Complete()
        {
            if (Stopped || State.Has(State.stage + "_DONE")) return;
            State.Mark(State.stage + "_DONE");
            Q.Pay(Id + "-" + State.stage + "-PAY", Reward, Material + " 提供料", Id);
            if (Career.TryOnce(Id + "-" + State.stage + "-DAMAGE")) S.AddDamage(Material + "の社外流出");
            Mail(State.stage + "-RESULT", State.stage == "R1" ? "営業部" : From, null,
                State.stage == "R1" ? "情報が漏れているようです" : State.stage == "R2" ? "取引失注のお知らせ" : "採用判断のお知らせ",
                State.stage == "R1" ? "営業計画が社外に伝わっています。記録の確認をお願いします。" : State.stage == "R2" ? "価格情報の流出により取引を失いました。" : "契約の資料が揃いました。朝に採用への回答をお願いします。", nextMorning: true);
        }

        void Stop(string result)
        {
            if (!State.IsOpen) return;
            State.Mark("STOP"); State.result = result;
            Q.CancelWork(Id);
            if (!State.Has("PARTICIPATED")) Q.Close(this, result);
        }

        public override bool? OnReported(QuestMailRecord r)
        {
            if (!State.IsOpen) return null;
            if (State.Has("PARTICIPATED")) State.Mark("REPORTED");
            Stop(QuestResult.Declined);
            return true;
        }

        public override bool IsTarget(string x) => State.IsOpen && State.Has("PARTICIPATED") &&
            (x == Actor?.deviceId || x == S.Employees.GetIp(Actor) || x == DropId);

        public override void OnNightEnd()
        {
            if (!State.IsOpen || State.Has("NIGHT-" + Day)) return;
            State.Mark("NIGHT-" + Day);
            if (State.Has("REPORTED")) Q.Resolve(this, QuestResult.Completed, "SE-R04");
            else if (Stopped) Q.Resolve(this, State.result, "SE-R03");
            else if (State.Has("R3_DONE")) State.Mark("OFFER_READY");
            else if (State.stage != "INVITE" && State.nextTaskDay <= Day)
            {
                if (State.Has(State.stage + "_DONE")) Plan(State.stage == "R1" ? "R2" : "R3");
                else { Stop(QuestResult.Abandoned); if (State.IsOpen) Q.Resolve(this, QuestResult.Abandoned, "SE-R03"); }
            }
        }

        public override MorningChoice MorningChoice() => State.IsOpen && State.Has("OFFER_READY") ? new MorningChoice
        {
            QuestId = Id, Key = "EMPLOYMENT", Title = "採用のご案内", Accept = "転職する", Decline = "辞退する",
            Text = "3取引が完了し、契約の資料が揃いました。\n承諾すると現在の勤務と周回を終了します。"
        } : null;

        public override void OnMorningChoice(MorningChoice c, bool accept)
        {
            if (!CanWork || c.Key != "EMPLOYMENT" || !State.Has("OFFER_READY")) return;
            Q.Resolve(this, QuestResult.Completed, State.Has("REPORTED") ? "SE-R04" : Stopped ? "SE-R03" : accept ? "SE-R01" : "SE-R02");
        }

        public override void OnFinalDay()
        {
            if (State.Has("OFFER_READY")) return;
            Stop(QuestResult.Abandoned);
            if (State.IsOpen) Q.Resolve(this, QuestResult.Abandoned, "SE-R03");
        }
        public override bool Touched() => State.Has("REPLIED") || State.Has("VIEWED");
        public override string Memo() => $"{State.stage} / DAY{State.nextTaskDay} / {Material}";
    }
}
