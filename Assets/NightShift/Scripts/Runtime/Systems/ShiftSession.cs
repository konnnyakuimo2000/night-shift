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

    public enum ShiftPhase { NotStarted, Running, Grace, Complete }

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

        public IShiftUi Ui { get; set; }
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

        public event Action EndWarning;
        public event Action ShiftEnded;      // 04:00
        public event Action ShiftCompleted;  // 猶予終了 → 評価へ

        public ShiftSession(GameDatabase db, DayDefinition day, int seed, string operatorName = "night01")
        {
            OperatorName = string.IsNullOrEmpty(operatorName) ? "night01" : operatorName;
            Database = db;
            Day = day;
            Seed = seed;
            Rng = new Random(seed);
            Time = new TimeManager(day.startMinute, day.endMinute, day.realSecondsPerGameMinute);
            Employees = new EmployeeManager(db.employees, Rng);
            Network = new NetworkManager(db.nodes);
            Logs = new LogManager();
            Notifications = new NotificationManager();
            Evaluation = new EvaluationManager();
            Requests = new RequestManager(this);
            HelpDesk = new HelpDeskManager(this);
            Factory = new EventFactory(this);
            Director = new EventDirector(this);
            Mail = new MailManager();
            // 受信トレイ: これまでの業務連絡（既読）と今日の業務連絡（未読）
            for (int n = 1; n <= day.dayNumber; n++)
            {
                var d = db.FindDay(n);
                if (d != null) Mail.Add(MailManager.FromBriefing(d, OperatorName, n < day.dayNumber), false);
            }
        }

        public void StartShift()
        {
            if (Phase != ShiftPhase.NotStarted) return;
            Director.ScheduleDay(Day);
            Logs.Add(Time.CurrentMinute, "NIGHT-OPS", "AUTH-SRV01", OperatorName, "LOGIN", "OK", "夜間運用担当 ログイン");
            Time.Start();
            Phase = ShiftPhase.Running;
        }

        public void Tick(float unscaledDelta)
        {
            switch (Phase)
            {
                case ShiftPhase.Running:
                {
                    float delta = Time.Tick(unscaledDelta);
                    float now = Time.CurrentMinute;
                    if (delta > 0)
                    {
                        Director.Tick(now, delta);
                        Requests.Tick(now);
                        HelpDesk.Tick(now, delta);
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
                case ShiftPhase.Grace:
                {
                    // 期限切れなしの日は、未処理の要求がなくなるまで待つ
                    if (!Time.Paused && Day.requestsExpire) GraceRemaining -= unscaledDelta;
                    float delta = unscaledDelta / Time.RealSecondsPerGameMinute * Time.DebugTimeScale;
                    if (!Time.Paused) HelpDesk.Tick(Time.CurrentMinute, delta);
                    if ((Day.requestsExpire && GraceRemaining <= 0) || Requests.Pending.Count == 0) Complete();
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
            ShiftEnded?.Invoke();
            if (Requests.Pending.Count > 0)
                Notifications.Notify(NotificationType.ShiftNotice, "業務終了", $"04:00 になりました。保留中の要求 {Requests.Pending.Count} 件を処理してください。", Time.CurrentMinute, () => Ui?.OpenConnectionMonitor(null));
        }

        public void Complete()
        {
            if (Phase == ShiftPhase.Complete) return;
            if (Phase == ShiftPhase.Running) EndShift();
            Requests.ExpireAll();
            HelpDesk.CloseShift();
            Phase = ShiftPhase.Complete;
            ShiftCompleted?.Invoke();
        }
    }
}
