using NightShift.Core;
using NightShift.Data;
using NightShift.Events;

namespace NightShift.Systems.SideQuests
{
    /// <summary>SQ06「NULL SIGNAL」。情報の確認、調査への加担、攻撃の阻止／完了を別々に記録する。</summary>
    public class Sq06Route : QuestRoute
    {
        public Sq06Route(SideQuestManager q) : base(q) { }
        public override string Id => "SQ06";
        public override string Title => "NULL SIGNAL";
        public override int EarliestDay => 17;
        public const string InvitedFlag = "SQ06_INVITED";
        public const string FakeDevice = "NS-RELAY-01", SourceIp = "10.10.99.61";
        public string CurrentMailId => TaskMail;
        public EventInstance TargetRequest { get; private set; }
        bool _scheduled, _spawned;
        EmployeeData Actor => Employee(State.employeeId);
        bool HorrorActive => Career.Quest("SQ03").status == QuestStatus.Active;
        float Start => State.stage == "N1" ? 130 : State.stage == "N2" ? 140 : 170;
        bool NightDone => State.Has(State.stage + "_DONE");
        public override bool Ready() => Unlocked(Features.ConnectionMonitor) && Unlocked(Features.SystemLog) && Unlocked(Features.Isolation) && Unlocked(Features.Report);

        public override void Offer()
        {
            var actor = Pick("ACTOR", e => e.onShift);
            if (actor == null) { Q.Close(this, QuestResult.Abandoned); return; }
            State.employeeId = actor.employeeId;
            State.stage = "INVITE";
            Mail("INVITE", "relay / NULL SIGNAL", "relay@null-signal.example", "接続の予告について",
                "登録端末を名乗る不審な要求を予告できます。\n確認するなら返信してください。",
                replies: new[] { "情報を確認する", "辞退する" });
        }

        public override void OnReply(QuestMailRecord r, int index, string text)
        {
            if (!CanWork) return;
            if (r.mailId == Id + "-INVITE" && State.stage == "INVITE")
            {
                if (index != 0) { Q.Close(this, QuestResult.Declined); return; }
                State.status = QuestStatus.Active;
                State.Mark("REPLIED");
                Career.SetFlag(InvitedFlag);
                Plan("N1");
            }
            else if (r.mailId == Id + "-N2-STOP" && State.Has("N2_STOPPED") && !State.Has("DEFENSE"))
            {
                if (index == 1) { State.Mark("DEFENSE"); Plan("N3"); }
                else Q.Close(this, QuestResult.Declined);
            }
            else if (r.mailId == Id + "-" + TaskMail && index == 0 && State.stage == "N1") Q.Close(this, QuestResult.Declined);
        }

        void Plan(string stage)
        {
            State.stage = stage;
            State.nextTaskDay = 0;
            if (stage == "N3" && HorrorActive)
            {
                Mail("HOLD-" + Day, "relay / NULL SIGNAL", null, "最終作業の延期", "今夜の最終作業はありません。日時は改めてお知らせします。");
                return;
            }
            int remaining = stage == "N1" ? 2 : stage == "N2" ? 1 : 0;
            int day = Q.ReserveTaskDay(this, Day + 1, SideQuestManager.LastTaskDay - remaining);
            if (day == 0) { Q.Close(this, QuestResult.Abandoned); return; }
            var a = Access();
            Mail(TaskMail, stage == "N2" ? "zero / NULL SIGNAL" : "relay / NULL SIGNAL", "relay@null-signal.example",
                stage == "N1" ? "02:10の接続を確認してください" : stage == "N2" ? "今度は通してください" : "最終作業・実行計画",
                (stage == "N1" ? "社員は本物ですが、接続元が違います。ログと名簿で確かめてください。" : stage == "N2" ? "権限のない記録領域を調査します。完了後に¥5,000を支払います。" : "記録取得に加え、全社サービスを停止します。完了後の離脱でも被害と許可記録は残ります。") +
                Conditions($"DAY{day} / {T(Start)}〜{T(Start + 10)}", $"{Actor.fullName} / {Actor.employeeId} / {(stage == "N1" ? Actor.deviceId : FakeDevice)} / {SourceIp} → {a.node} / {a.access}",
                    stage == "N1" ? $"本人の登録IP: {S.Employees.GetIp(Actor)} / 拒否で確認完了" : "処理は許可後3分。完了前なら隔離・切断で止まります。",
                    stage == "N3" ? "攻撃完了で¥10,000。その後に加入／離脱を選択（どちらも周回終了）。拒否だけで終えることも可能。" : "SIGNAL DESK（¥3,000）は任意。メール・ログ・監視画面だけでも対応できます。"),
                pinned: true, replies: stage == "N1" ? new[] { "辞退する" } : null,
                actionKey: stage == "N3" ? "DEFEND" : null, actionLabel: stage == "N3" ? "関連接続を隔離して報告" : null);
            if (stage == "N3")
                Mail("EMPLOYEE-" + day, "業務担当", null, "今夜だけは止めないでください", "納品が重なっています。今夜サービスが止まると全員の作業が失われます。");
        }

        (string node, string access) Access()
        {
            if (State.stage == "N2") return UnauthorizedAccess(Actor, "FILE-SRV01", "N2");
            var node = S.Database.nodes.Find(n => n != null && n.isAdministrative && n.accessTypes.Count > 0);
            return node != null ? (node.nodeId, node.accessTypes[0].accessType) : UnauthorizedAccess(Actor, "AUTH-SRV01", State.stage);
        }

        public override void OnShiftStart()
        {
            if (State.Has("N3_DONE")) { AskJoin(); return; }
            if (State.nextTaskDay != Day || _scheduled) return;
            if (State.stage == "N3" && HorrorActive) { Plan("N3"); return; }
            _scheduled = true;
            Later(Start - 2, "LOG", () =>
            {
                if (!Viewed(TaskMail)) return;
                S.Logs.Add(Now, S.Employees.GetIp(Actor), "FILE-SRV01", Actor.fullName, "WORK", "OK", "本人の登録端末で通常業務中");
                S.Logs.Add(Now, SourceIp, Access().node, Actor.fullName, "AUTH", "MISMATCH", $"{Id}: 登録IPとは異なる接続元 / 端末申告 {Actor.deviceId}");
            });
            Later(Start, "REQUEST", Spawn);
        }

        public override void OnViewed(QuestMailRecord r)
        {
            if (!State.IsOpen || r.mailId != Id + "-" + TaskMail) return;
            State.Mark("VIEWED-" + State.stage);
            OnShiftStart();
            if (State.nextTaskDay == Day && Now >= Start && Now < Start + 10) Spawn();
        }

        void Spawn()
        {
            if (_spawned || !Viewed(TaskMail) || Now >= Start + 10 || (State.stage == "N3" && HorrorActive)) return;
            _spawned = true;
            Q.ReplaceRoutineSlots(1);
            var a = Access();
            TargetRequest = SpawnTask(new SideQuestManager.RequestSpec
            {
                Tag = Id + "-" + State.stage + "-DAY" + Day, MailId = Id + "-" + TaskMail,
                Employee = Actor, Device = State.stage == "N1" ? Actor.deviceId : FakeDevice, SourceIp = SourceIp,
                NodeId = a.node, AccessType = a.access, Timeout = Start + 10 - Now, Malicious = true,
                Note = State.stage == "N3" ? "全社停止を含む処理 / 許可後3分 / 完了後は取り消せません" : "NULL SIGNAL の予告対象",
                Confirm = State.stage == "N3" ? "全社サービス停止を含む処理を許可します。\n3分で完了し、被害は取り消せません。完了後に加入／離脱を選び、どちらも周回を終了します。" : null
            });
            S.Notifications.Notify(NotificationType.Info, "NULL SIGNAL の作業時刻", $"{T(Start)} / {a.node}", Now, () => S.Ui?.OpenConnectionMonitor(TargetRequest));
        }

        public override void OnRequestResolved(EventInstance e)
        {
            if (!Viewed(TaskMail) || !ConsumeTask(e, Start, Start + 10)) return;
            if (State.stage == "N1")
            {
                State.Mark("N1-" + Day + "-" + e.State);
                if (SideQuestManager.ManualBlock(e))
                {
                    State.Mark("N1_DONE");
                    Mail("N1-RESULT", "relay / NULL SIGNAL", null, "予告の確認", "確認できました。次の対象はNS-RELAY-01です。\n登録外端末から記録領域へ接続します。", nextMorning: true);
                }
                return;
            }
            if (SideQuestManager.ManualBlock(e)) { Stopped(); return; }
            if (!SideQuestManager.ManualAllow(e)) return;
            State.Mark(State.stage + "_ALLOW");
            Work(e, State.stage + "-PROCESS", 3, Complete, Stopped);
        }

        void Complete()
        {
            if (NightDone) return;
            if (State.stage == "N3" && HorrorActive) { Q.CancelWork(Id); Plan("N3"); return; }
            State.Mark(State.stage + "_DONE");
            State.timer = 1;
            if (State.stage == "N2")
            {
                Q.Pay(Id + "-N2-PAY", 5000, "記録調査の謝礼", Id);
                if (Career.TryOnce(Id + "-N2-DAMAGE")) S.AddDamage("NULL SIGNAL による経営記録の無権限取得");
                Mail("N2-MEMO", "zero / NULL SIGNAL", null, "調査メモ", "労務記録と経営記録の断片が見つかった。\nこれだけで全社停止が正当化されるわけではない。");
            }
            else
            {
                // 同じ全社停止を一般GO2へ重ねない。通常の不正許可の評価は既に残っている。
                if (Career.TryOnce(Id + "-N3-DAMAGE")) S.AddDamage("専用重大事件: NULL SIGNAL による全社サービス停止");
                Career.SetFlag("SQ06_MAJOR_INCIDENT");
                Q.PayNow(Id + "-N3-PAY", 10000, "最終作業の謝礼", Id);
                Career.Save();
                AskJoin();
            }
        }

        void Stopped()
        {
            if (NightDone || State.Has(State.stage + "_STOPPED")) return;
            State.Mark(State.stage + "_STOPPED");
            if (State.stage == "N2")
                Mail("N2-STOP", "relay / NULL SIGNAL", null, "ここで降りますか", "ここで降りるか、記録を残して協力者を止めるか。\n調査を続ければ最終作業の予告を送ります。\n翌夜の終了までに返信がなければ、この件は終了します。",
                    replies: new[] { "ここで降りる", "止めるため調査する" });
        }

        public override void OnAction(QuestMailRecord r, string key)
        {
            if (!CanWork || key != "DEFEND" || r.mailId != Id + "-" + TaskMail || State.stage != "N3" || State.Has("N3_DONE")) return;
            if (!State.Has("N3_STOPPED") && !Q.JobRunning(Id + "-N3-PROCESS"))
            {
                Q.Note("NULL SIGNAL: 管理要求を拒否してから隔離・報告してください。");
                return;
            }
            S.Security.Isolate(FakeDevice);
            S.Security.Block(SourceIp);
            if (!S.Security.IsIsolated(FakeDevice) || !S.Security.IsBlocked(SourceIp) || !State.Has("N3_STOPPED")) return;
            S.Security.Report("NULL SIGNAL の攻撃計画・会社の記録・夜間担当の操作履歴", true, SourceIp);
            State.Mark("N3_ISOLATED"); State.Mark("N3_REPORTED");
            ResolveDefense();
        }

        public override bool? OnReported(QuestMailRecord r)
        {
            if (!State.IsOpen) return null;
            if (State.Has("N3_DONE")) { OnMorningChoice(JoinChoice(), false); return true; }
            if (State.stage == "N3") State.Mark("N3_REPORTED");
            return true;
        }

        void ResolveDefense()
        {
            if (!CanWork || State.Has("N3_DONE") || !State.Has("N3_STOPPED") || !State.Has("N3_REPORTED") || !S.Security.IsIsolated(FakeDevice)) return;
            State.Mark("N3_ISOLATED");
            Q.Note(State.Has("N2_DONE") ? "N2での許可と記録取得も調査記録に含まれます。" : "全社停止を阻止し、攻撃計画と会社の記録を報告しました。");
            Q.Resolve(this, QuestResult.Completed, "SE-N03");
        }

        public override bool IsTarget(string x) => State.IsOpen && (x == SourceIp || x == (State.stage == "N1" ? Actor?.deviceId : FakeDevice));
        public override void Tick(float now, float realDelta)
        {
            if (State.stage == "N3") ResolveDefense();
            var job = Q.Job(Id + "-" + State.stage + "-PROCESS");
            if (job != null && !job.Finished) State.timer = job.Progress(now);
        }

        void AskJoin()
        {
            if (!CanWork || !State.Has("N3_DONE") || HorrorActive || Q.PendingChoice != null) return;
            Q.RequestUi("CHOICE", JoinChoice());
        }

        public void ResumeChoice() => AskJoin();

        MorningChoice JoinChoice() => new MorningChoice
        {
            QuestId = Id, Key = "JOIN", Title = "NULL SIGNAL / 最終回答", Accept = "集団へ加わる", Decline = "加入せず離脱する",
            Text = "全社停止は完了しました。被害とあなたの許可記録は残ります。\n加入・離脱のどちらを選んでも現在の周回を終了します。"
        };

        public override void OnMorningChoice(MorningChoice c, bool accept)
        {
            if (!CanWork || c.Key != "JOIN" || !State.Has("N3_DONE") || HorrorActive) return;
            State.Mark(accept ? "JOINED" : "LEFT");
            Q.Resolve(this, QuestResult.Completed, accept ? "SE-N01" : "SE-N02");
        }

        public override void OnNightEnd()
        {
            if (!State.IsOpen || State.status != QuestStatus.Active || State.Has("NIGHT-" + Day)) return;
            State.Mark("NIGHT-" + Day);
            if (State.stage == "N3" && !NightDone && (State.nextTaskDay == 0 || HorrorActive)) { Plan("N3"); return; }
            if (State.nextTaskDay > Day) return;
            if (State.stage == "N1")
            {
                if (NightDone) Plan("N2");
                else if (State.counter++ == 0) Plan("N1");
                else Q.Close(this, QuestResult.Failed);
            }
            else if (State.stage == "N2")
            {
                if (NightDone) Plan("N3");
                else
                {
                    Stopped();
                    // 停止した夜には締めず、翌夜の終了まで返信を待つ。メールの日付は復帰後も残る。
                    var mail = Q.Record(Id, "N2-STOP");
                    if (Day > (mail?.day ?? State.nextTaskDay) && (mail?.repliedIndex ?? -1) < 0)
                        Q.Close(this, QuestResult.Abandoned);
                }
            }
            else if (!NightDone) Q.Close(this, QuestResult.Abandoned);
        }

        public override void OnFinalDay() { if (!State.Has("N3_DONE")) Q.Close(this, QuestResult.Abandoned); }
        public override string Memo() => $"{State.stage} / DAY{State.nextTaskDay} / {T(Start)}〜{T(Start + 10)}\n" +
            (State.Has("N3_DONE") ? "全社停止完了。加入／離脱の回答待ち。" : "処理は3分。完了前なら隔離・切断可能。完了後は取り消せません。");
    }
}
