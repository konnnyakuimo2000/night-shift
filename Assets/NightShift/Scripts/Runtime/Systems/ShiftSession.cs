using System;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;

namespace NightShift.Systems
{
    /// <summary>UI側への最小限の要求（通知クリックで該当画面へ移動など）</summary>
    public interface IShiftUi
    {
        void OpenConnectionMonitor(EventInstance focus);
        void OpenHelpDesk(Ticket focus);
        void OpenLogViewer();
        void OpenPasswordChange();
        void OpenMailbox(MailMessage focus);
        void ShowStatus(string message);
    }

    public enum ShiftPhase { NotStarted, Running, Grace, Overtime, Complete }

    /// <summary>
    /// 1日分のシフトの全状態。DAY Restart はこのオブジェクトを作り直すだけで完結する。
    /// </summary>
    public class ShiftSession
    {
        public GameDatabase Database { get; }
        public DayDefinition Day { get; }
        public Random Rng { get; }
        public int Seed { get; }

        public TimeManager Time { get; }
        public EmployeeManager Employees { get; }
        public NetworkManager Network { get; }
        public LogManager Logs { get; }
        public NotificationManager Notifications { get; }
        public EvaluationManager Evaluation { get; }
        public RequestManager Requests { get; }
        public HelpDeskManager HelpDesk { get; }
        public EventFactory Factory { get; }
        public EventDirector Director { get; }
        public MailManager Mail { get; }
        public SecurityOps Security { get; }
        public ServerOps Servers { get; }
        /// <summary>VACCINE 99 の検査（対象は開始時に固定）</summary>
        public VaccineScanner Vaccine { get; }
        /// <summary>SOC-AI の依頼（受付時に検証・対象を固定）</summary>
        public SocAiService SocAi { get; }
        /// <summary>メール起点のサブクエスト</summary>
        public SideQuests.SideQuestManager Quests { get; }

        public IShiftUi Ui { get; set; }

        // ---- 本番業務の夜の記録（夜の終わりに CareerRecord へまとめる） ----
        /// <summary>この夜に出た被害（情報漏えい・改ざん・感染の拡大など）</summary>
        public readonly System.Collections.Generic.List<string> Damage = new System.Collections.Generic.List<string>();
        /// <summary>身代金ウイルス（V6）に暗号化されたファイルの数（途中で止めた場合も数える）</summary>
        public int RansomEncryptedFiles;
        public int CorrectReports, WrongReports, AiHandled, AutoAllowed, AiTokensUsed;
        public bool OperatorCompromised;
        public bool Overtime;
        /// <summary>業務終了時に「残業する／退勤する」を尋ねている（答えるまで猶予は進まない）</summary>
        public bool OvertimeOffered { get; private set; }
        public float OvertimeEnd { get; private set; }
        public const float OvertimeMinutes = 30f;

        bool CanOfferOvertime() =>
            Day.dayNumber >= DefaultDataFactory.MainStartDay && !Overtime
            && Core.CareerRecord.Current.ItemCount(Items.Coffee) > 0
            && (Requests.Pending.Count > 0 || HelpDesk.OpenCount > 0);

        /// <summary>缶コーヒーを1本飲んで、30分だけ残業する（新しい仕事は来ない・要求は期限切れにならない）</summary>
        public void StartOvertime()
        {
            if (!OvertimeOffered) return;
            OvertimeOffered = false;
            if (!Core.CareerRecord.Current.UseItem(Items.Coffee)) return;
            Overtime = true;
            Phase = ShiftPhase.Overtime;
            OvertimeEnd = Day.endMinute + OvertimeMinutes;
            Logs.Add(Time.CurrentMinute, "NIGHT-OPS", "-", OperatorName, "SHIFT", "OVERTIME", "残業（缶コーヒー）");
            Time.Start();
        }

        public void DeclineOvertime() => OvertimeOffered = false;
        /// <summary>致命的な被害（ゲームオーバー GO2 の原因）。空なら無し</summary>
        public string FatalIncident;
        public event Action<string> DamageOccurred;
        /// <summary>停電（V5 REDACT）の演出を要求する</summary>
        public event Action BlackoutRequested;
        public void RequestBlackout() { BlackoutRequested?.Invoke(); HorrorRequested?.Invoke("BLACKOUT"); }
        /// <summary>ホラー演出の要求（BSOD / BLACKOUT / MUSICBOX / GLITCH）</summary>
        public event Action<string> HorrorRequested;
        public void RequestHorror(string what) => HorrorRequested?.Invoke(what);

        /// <summary>感染記録（図鑑）を進める。図鑑を見るだけでは何も起きない（記録はここからだけ増える）</summary>
        public void Discover(string virusId, DiscoveryStage stage)
        {
            if (string.IsNullOrEmpty(virusId)) return;
            if (VirusDex.Record(Core.CareerRecord.Current, virusId, stage, Day.dayNumber))
                Logs.Add(Time.CurrentMinute, "NIGHT-OPS", "-", OperatorName, "VIRUSDEX", "UPDATE", $"感染記録: {VirusCatalog.Name(virusId)}（{VirusDex.Label(stage)}）");
        }

        public void AddDamage(string what, bool fatal = false)
        {
            Damage.Add(what);
            Evaluation.DamageCount++;
            if (fatal && string.IsNullOrEmpty(FatalIncident)) FatalIncident = what;
            Logs.Add(Time.CurrentMinute, "SECURITY", "-", "-", "INCIDENT", fatal ? "CRITICAL" : "DAMAGE", what);
            Notifications.Notify(NotificationType.SecurityAlert, fatal ? "重大インシデント" : "被害の発生", what, Time.CurrentMinute);
            DamageOccurred?.Invoke(what);
        }
        /// <summary>プレイヤーのユーザー名（ログインID）</summary>
        public string OperatorName { get; }
        public ShiftPhase Phase { get; private set; } = ShiftPhase.NotStarted;
        public bool EndWarningIssued { get; private set; }
        public float GraceRemaining { get; private set; }

        readonly System.Collections.Generic.List<OperatorTask> _tasks = new System.Collections.Generic.List<OperatorTask>();
        public System.Collections.Generic.IReadOnlyList<OperatorTask> Tasks => _tasks;
        public event Action TasksChanged;

        public int OpenTaskCount
        {
            get { int n = 0; foreach (var t in _tasks) if (!t.Done) n++; return n; }
        }

        public OperatorTask AddTask(string id, string title)
        {
            var t = new OperatorTask { TaskId = id, Title = title, CreatedAt = Time.CurrentMinute };
            _tasks.Add(t);
            TasksChanged?.Invoke();
            return t;
        }

        public void CompleteTask(string id)
        {
            foreach (var t in _tasks)
                if (t.TaskId == id && !t.Done) { t.Done = true; t.DoneAt = Time.CurrentMinute; }
            TasksChanged?.Invoke();
        }

        /// <summary>このシフト中にパスワードを変更済みか（依頼が出る前に変更した場合も含む）</summary>
        public bool PasswordChangedThisShift { get; private set; }

        public void MarkPasswordChanged()
        {
            PasswordChangedThisShift = true;
            CompleteTask(OperatorTask.PasswordChange);
        }

        public bool HasOpenTask(string id)
        {
            foreach (var t in _tasks) if (t.TaskId == id && !t.Done) return true;
            return false;
        }

        // ---- 仕事の量: プレイヤーが何もすることがない状態を10秒以上つくらない ----
        // 時計の速さは変えない。手が空いたら接続要求を追加で届けるので、速く正確にこなす人ほど多くの件数を扱える。
        /// <summary>何もすることがない状態がこの秒数（実時間）続いたら、接続要求を1件追加する</summary>
        public const float IdleSpawnAfter = 7f;
        /// <summary>何もすることがない状態が続いている実時間（秒）</summary>
        public float IdleRealSeconds { get; private set; }
        /// <summary>この夜でいちばん長かった、何もすることがない時間（実時間・秒）</summary>
        public float MaxIdleRealSeconds { get; private set; }

        /// <summary>プレイヤーに今することがあるか（保留中の要求・未対応の問い合わせ・依頼・リモート操作中・サブクエストの作業）</summary>
        public bool HasWork =>
            Requests.Pending.Count > 0 || HelpDesk.OpenCount > 0 || OpenTaskCount > 0
            || (HelpDesk.ActiveSession != null && HelpDesk.ActiveSession.Connected) || Quests.HasWork;

        void TrackIdle(float realDelta)
        {
            if (HasWork) { IdleRealSeconds = 0; return; }
            if (Time.Paused) return;
            IdleRealSeconds += realDelta;
            MaxIdleRealSeconds = Math.Max(MaxIdleRealSeconds, IdleRealSeconds);
            // 終業の予告（残り10分）を過ぎたら追加しない
            if (IdleRealSeconds < IdleSpawnAfter || Time.CurrentMinute >= Day.endWarningMinute) return;
            if (Director.SpawnExtra() != null) IdleRealSeconds = 0;
            else IdleRealSeconds = IdleSpawnAfter - 1f;   // 出せなかった（条件に合う社員がいない等）: 少し待って再挑戦
        }

        public event Action EndWarning;
        public event Action ShiftEnded;      // 04:00
        public event Action ShiftCompleted;  // 猶予終了 → 評価へ

        /// <summary>最低評価が2夜続いた翌夜: 日勤の先輩が残って、問い合わせを引き受けてくれる</summary>
        public bool SeniorHelp { get; private set; }
        public const int SeniorHelpTickets = 2;

        public ShiftSession(GameDatabase db, DayDefinition day, int seed, string operatorName = "night01")
        {
            SeniorHelp = day.dayNumber > DefaultDataFactory.MainStartDay && Core.CareerRecord.Current.ConsecutiveRank("D", day.dayNumber - 1) >= 2;
            OperatorName = string.IsNullOrEmpty(operatorName) ? "night01" : operatorName;
            Database = db;
            Day = day;
            Seed = seed;
            Rng = new Random(seed);
            Time = new TimeManager(day.startMinute, day.realSecondsPerGameMinute);
            Employees = new EmployeeManager(db.employees, Rng);
            Network = new NetworkManager(db.nodes);
            Logs = new LogManager();
            Notifications = new NotificationManager();
            Evaluation = new EvaluationManager { SeniorHelped = SeniorHelp };
            Requests = new RequestManager(this);
            HelpDesk = new HelpDeskManager(this);
            Factory = new EventFactory(this);
            Director = new EventDirector(this);
            Mail = new MailManager();
            Security = new SecurityOps(this);
            Servers = new ServerOps(this);
            Vaccine = new VaccineScanner(this);
            SocAi = new SocAiService(this);
            // 受信トレイ: これまでの業務連絡（既読）と今日の業務連絡（未読）
            for (int n = 1; n <= day.dayNumber; n++)
            {
                var d = db.FindDay(n);
                if (d != null) Mail.Add(MailManager.FromBriefing(d, OperatorName, n < day.dayNumber), false);
            }
            Quests = new SideQuests.SideQuestManager(this);
        }

        public void StartShift()
        {
            if (Phase != ShiftPhase.NotStarted) return;
            Director.ScheduleDay(Day);
            Logs.Add(Time.CurrentMinute, "NIGHT-OPS", "AUTH-SRV01", OperatorName, "LOGIN", "OK", "夜間運用担当 ログイン");
            Time.Start();
            Phase = ShiftPhase.Running;
            Quests.OnShiftStart();
        }

        public void Tick(float unscaledDelta)
        {
            switch (Phase)
            {
                case ShiftPhase.Running:
                {
                    TrackIdle(unscaledDelta);
                    float delta = Time.Tick(unscaledDelta);
                    float now = Time.CurrentMinute;
                    if (delta > 0)
                    {
                        Director.Tick(now, delta);
                        Requests.Tick(now);
                        HelpDesk.Tick(now, delta);
                        Servers.Tick(now);
                        Vaccine.Tick(now);
                        Quests.Tick(now, unscaledDelta);
                    }
                    if (!EndWarningIssued && now >= Day.endWarningMinute)
                    {
                        EndWarningIssued = true;
                        Notifications.Notify(NotificationType.ShiftNotice, "業務連絡", "業務終了まで残り10分です。", now);
                        EndWarning?.Invoke();
                    }
                    if (now >= Day.endMinute) EndShift();
                    break;
                }
                case ShiftPhase.Overtime:
                {
                    float delta = Time.Tick(unscaledDelta);
                    float now = Time.CurrentMinute;
                    if (delta > 0) { HelpDesk.Tick(now, delta); Servers.Tick(now); Vaccine.Tick(now); Quests.Tick(now, unscaledDelta); }
                    if (now >= OvertimeEnd || (Requests.Pending.Count == 0 && HelpDesk.OpenCount == 0)) Complete();
                    break;
                }
                case ShiftPhase.Grace:
                {
                    if (OvertimeOffered) break; // 残業するか答えるまで待つ
                    // 期限切れなしの日は、未処理の要求がなくなるまで待つ
                    if (!Time.Paused && Day.requestsExpire) GraceRemaining -= unscaledDelta;
                    float delta = unscaledDelta / Time.RealSecondsPerGameMinute * Time.DebugTimeScale;
                    if (!Time.Paused) HelpDesk.Tick(Time.CurrentMinute, delta);
                    // 接続要求も問い合わせも片付いたら終了。猶予がない日（研修）は、これまでどおり接続要求だけを見る
                    bool cleared = Requests.Pending.Count == 0 && (!Day.requestsExpire || HelpDesk.OpenCount == 0);
                    if ((Day.requestsExpire && GraceRemaining <= 0) || cleared) Complete();
                    break;
                }
            }
        }

        /// <summary>04:00。新規イベント停止 → 未処理要求があれば短い猶予</summary>
        public void EndShift()
        {
            if (Phase != ShiftPhase.Running) return;
            Time.JumpTo(Math.Max(Time.CurrentMinute, Day.endMinute));
            Time.Stop();
            Director.StopNewEvents();
            Logs.Add(Day.endMinute, "NIGHT-OPS", "-", OperatorName, "SHIFT", "END", "新規受付を停止");
            Phase = ShiftPhase.Grace;
            GraceRemaining = Day.graceRealSeconds;
            OvertimeOffered = CanOfferOvertime();
            ShiftEnded?.Invoke();
            if (Requests.Pending.Count > 0)
                Notifications.Notify(NotificationType.ShiftNotice, "業務終了", $"{Day.EndLabel} になりました。保留中の要求 {Requests.Pending.Count} 件を処理してください。", Time.CurrentMinute, () => Ui?.OpenConnectionMonitor(null));
        }

        public void Complete()
        {
            if (Phase == ShiftPhase.Complete) return;
            if (Phase == ShiftPhase.Running) EndShift();
            Requests.ExpireAll();
            HelpDesk.CloseShift();
            SocAi.Cancel(); // 考えている途中の依頼は実行せず、トークンを返す
            Phase = ShiftPhase.Complete;
            ShiftCompleted?.Invoke();
        }
    }
}
