using NightShift.Controls;
using NightShift.Data;
using NightShift.Systems;
using NightShift.UI;
using NightShift.UI.Screens;
using NightShift.UI.Shift;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift.Core
{
    public enum GameState { Title, ClockIn, Login, DayIntro, Briefing, Shift, Complete, Evaluation, ComingSoon }

    /// <summary>
    /// GameManager
    /// ├ TimeManager / DayManager(DayDefinition) / EventDirector / EmployeeManager / NetworkManager
    /// ├ LogManager / HelpDeskManager(+VirtualPC) / EvaluationManager / NotificationManager
    /// └ WindowManager（UI）
    /// シフト中の状態はすべて ShiftSession に集約し、DAY Restart は作り直しで行う。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameDatabase Database { get; private set; }
        public ShiftSession Session { get; private set; }
        public GameState State { get; private set; }
        public int CurrentDay { get; private set; } = 1;
        public const string DefaultOperatorName = "night01";
        /// <summary>タイムカードで入力したユーザー名。ログインIDとして使う。</summary>
        public string OperatorName { get; private set; } = DefaultOperatorName;
        /// <summary>プレイヤーのアカウント（パスワード等）。日をまたいで保持する。</summary>
        public OperatorAccount Account { get; private set; } = new OperatorAccount();
        /// <summary>給与・所持金（後のストアで使う）</summary>
        public OperatorWallet Wallet { get; private set; } = OperatorWallet.Load();

        public RectTransform ScreenLayer { get; private set; }
        public RectTransform OverlayLayer { get; private set; }
        public RectTransform DebugLayer { get; private set; }
        public Canvas Canvas { get; private set; }

        ShiftDesktop _desktop;
        readonly System.Collections.Generic.HashSet<string> _readMarks = new System.Collections.Generic.HashSet<string>();

        /// <summary>業務開始前に読んだもの（日ごと）。同じ日をやり直しても引き継ぐ。</summary>
        public bool IsRead(int day, string id) => _readMarks.Contains(day + ":" + id);
        public void MarkRead(int day, string id) => _readMarks.Add(day + ":" + id);
        CanvasScaler _scaler;

        /// <summary>UI の拡大率（設定）を CanvasScaler に反映</summary>
        void ApplyUiScale()
        {
            if (_scaler == null) return;
            float k = Mathf.Max(0.5f, GameSettings.Current.uiScalePercent / 100f);
            _scaler.referenceResolution = new Vector2(1280f / k, 800f / k);
        }
        GameObject _screen;
        float _completeTimer = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (Instance != null || FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("NightShift.GameManager").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameSettings.Current.ApplyAll();
            GameSettings.Changed += ApplyUiScale;
            Application.runInBackground = true;
            Database = GameDatabase.Load();
            AudioFx.Init(gameObject);
            RetroCursor.Install(gameObject);
            KeyboardCapture.Install();
            BuildCanvas();
        }

        void Start() => ShowTitle();

        void OnApplicationFocus(bool focus) => KeyboardCapture.AppFocused = focus;

        void OnApplicationQuit() => KeyboardCapture.Uninstall();

        void OnDestroy()
        {
            GameSettings.Changed -= ApplyUiScale;
            if (Instance == this) KeyboardCapture.Uninstall();
        }

        void BuildCanvas()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es);
            }
            var go = new GameObject("NightShift.Canvas", typeof(RectTransform));
            DontDestroyOnLoad(go);
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            _scaler = scaler;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var bg = UIFactory.Solid(go.transform, Color.black, "Background");
            bg.rectTransform.Stretch();
            ScreenLayer = UIFactory.Rect("Screens", go.transform).Stretch();
            OverlayLayer = UIFactory.Rect("Overlay", go.transform).Stretch();
            DebugLayer = UIFactory.Rect("Debug", go.transform).Stretch();
            PopupMenu.OverlayRoot = OverlayLayer;
            ApplyUiScale();
        }

        void ClearScreen()
        {
            PopupMenu.CloseAll();
            if (_screen != null) Destroy(_screen);
            _screen = null;
        }

        void SetScreen(GameObject screen, GameState state)
        {
            ClearScreen();
            _screen = screen;
            State = state;
        }

        // ---------------- Flow ----------------
        public void ShowTitle()
        {
            DisposeShift();
            int reached = ReachedDay;
            SetScreen(TitleScreen.Build(ScreenLayer, StartNewGame, reached > 1 ? ContinueGame : (System.Action)null, reached, Quit, Database.days.Count, StartFromDay), GameState.Title);
        }

        public void ShowClockIn() => SetScreen(ClockInScreen.Build(ScreenLayer, Database.GetDay(CurrentDay), OperatorName, name =>
        {
            OperatorName = name;
            if (Account.UserId != name) { Account.UserId = name; Account.Save(); }
            ShowLogin();
        }), GameState.ClockIn);

        public void ShowLogin() => SetScreen(LoginScreen.Build(ScreenLayer, Database, Account, ShowDayIntro, ShowTitle), GameState.Login);

        public void ShowDayIntro()
        {
            var day = Database.GetDay(CurrentDay);
            SetScreen(DayIntroScreen.Build(ScreenLayer, day, ShowBriefing), GameState.DayIntro);
        }

        /// <summary>デスクトップを作り、業務連絡を表示（時計はまだ止まっている）</summary>
        public void ShowBriefing()
        {
            ClearScreen();
            DisposeShift();
            var day = Database.GetDay(CurrentDay);
            Session = new ShiftSession(Database, day, System.Environment.TickCount, OperatorName);
            Session.ShiftCompleted += OnShiftCompleted;
            _desktop = ShiftDesktop.Build(ScreenLayer, this, Session);
            Session.Ui = _desktop;
            AddPayslipMail(day);
            _screen = _desktop.gameObject;
            State = GameState.Briefing;
            _desktop.ShowBriefing(StartShift);
            AudioFx.Startup();
        }

        void StartShift()
        {
            if (Session == null) return;
            Session.StartShift();
            State = GameState.Shift;
            _desktop.OnShiftStarted();
        }

        void OnShiftCompleted()
        {
            State = GameState.Complete;
            _desktop.ShowShiftComplete();
            AudioFx.Chime();
            _completeTimer = 3.5f;
        }

        void ShowEvaluation()
        {
            var result = Session.Evaluation.Compute();
            var day = Session.Day;
            ClearScreen();
            DisposeShift(keepSession: true);
            SaveProgress(CurrentDay + 1);
            if (day.passwordChangeAtMinute >= 0 || Account.Changed)
                result.Stats.Add(new System.Collections.Generic.KeyValuePair<string, string>("Operator Password",
                    Account.Changed ? $"変更済み（強度: {PasswordPolicy.StrengthLabel(Account.Strength)}{(Account.WrittenDown ? "・付箋あり" : "")}）" : "未変更（初期パスワード）"));
            var slip = Payroll.Compute(day.dayNumber, result, Session.Evaluation, Database.payroll);
            var previous = Wallet.Record(slip, out bool updated);
            SetScreen(EvaluationScreen.Build(ScreenLayer, day, result, slip, previous, updated, Wallet.Balance, GoToNextDay, RestartDay), GameState.Evaluation);
        }

        /// <summary>翌日へ。翌日の定義が無ければ COMING SOON。</summary>
        void GoToNextDay()
        {
            int next = CurrentDay + 1;
            if (Database.FindDay(next) != null)
            {
                CurrentDay = next;
                ShowClockIn();
            }
            else ShowComingSoon();
        }

        void ShowComingSoon() => SetScreen(ComingSoonScreen.Build(ScreenLayer, CurrentDay, CurrentDay + 1, RestartDay, ShowTitle), GameState.ComingSoon);

        // ---------------- Progress ----------------
        const string ProgressKey = "NightShift.ReachedDay";
        const string NameKey = "NightShift.OperatorName";

        /// <summary>到達した日（続きから開始できる日）</summary>
        public int ReachedDay
        {
            get
            {
                int d = 1;
                try { d = PlayerPrefs.GetInt(ProgressKey, 1); } catch { }
                if (Database.FindDay(d) == null) d = Mathf.Max(1, d - 1);
                return Mathf.Max(1, d);
            }
        }

        void SaveProgress(int day)
        {
            try
            {
                if (Database.FindDay(day) == null) return;
                if (day > PlayerPrefs.GetInt(ProgressKey, 1)) PlayerPrefs.SetInt(ProgressKey, day);
                PlayerPrefs.SetString(NameKey, OperatorName);
                PlayerPrefs.Save();
            }
            catch { }
        }

        /// <summary>前日の給与明細をメールで届ける</summary>
        void AddPayslipMail(DayDefinition day)
        {
            var slip = Wallet.Find(day.dayNumber - 1);
            if (slip == null) return;
            string Y(int v) => OperatorWallet.Yen(v);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{OperatorName} 様");
            sb.AppendLine();
            sb.AppendLine($"DAY {slip.day} の夜勤手当を支給しました。");
            sb.AppendLine();
            sb.AppendLine($"　基本給（夜勤）　　{Y(slip.basePay)}");
            sb.AppendLine($"　評価手当（{slip.score}点）　{Y(slip.performancePay)}");
            sb.AppendLine($"　ランク賞与（{slip.rank}）　{Y(slip.rankBonus)}");
            if (slip.deduction > 0) sb.AppendLine($"　減額　　　　　　−{Y(slip.deduction)}（{string.Join("、", slip.deductionNotes)}）");
            sb.AppendLine($"　<b>支給額　　　　　{Y(slip.total)}</b>");
            sb.AppendLine();
            sb.AppendLine($"現在の所持金: <b>{Y(Wallet.Balance)}</b>");
            sb.AppendLine();
            sb.Append("総務部 給与担当");
            Session.Mail.Add(new MailMessage
            {
                MailId = "PAYSLIP-" + slip.day,
                From = "総務部 給与担当",
                To = $"夜間IT運用担当 {OperatorName} 様",
                Subject = $"【給与明細】DAY {slip.day} 夜勤手当",
                DateLabel = (string.IsNullOrEmpty(day.clockInDateLabel) ? day.dateLabel : day.clockInDateLabel) + " 18:00",
                Body = sb.ToString(),
                Day = day.dayNumber
            }, false);
        }

        void StartNewGame()
        {
            CurrentDay = 1;
            Wallet.ResetAll();
            Account = OperatorAccount.NewGame(OperatorName);
            Account.Save();
            ShowClockIn();
        }

        /// <summary>デバッグモード: 任意の日から開始（アカウント・所持金はそのまま）</summary>
        void StartFromDay(int day)
        {
            if (Database.FindDay(day) == null) return;
            CurrentDay = day;
            Account = OperatorAccount.Load();
            try { OperatorName = PlayerPrefs.GetString(NameKey, OperatorName); } catch { }
            if (string.IsNullOrEmpty(Account.UserId) || Account.UserId == DefaultOperatorName) Account.UserId = OperatorName;
            ShowClockIn();
        }

        void ContinueGame()
        {
            CurrentDay = ReachedDay;
            Account = OperatorAccount.Load();
            try { OperatorName = PlayerPrefs.GetString(NameKey, OperatorName); } catch { }
            ShowClockIn();
        }

        /// <summary>デバッグ用: 指定日を最初から開始</summary>
        public void StartDay(int day)
        {
            if (Database.FindDay(day) == null) return;
            CurrentDay = day;
            RestartDay();
        }

        public void RestartDay()
        {
            ClearScreen();
            DisposeShift();
            ShowBriefing();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void DisposeShift(bool keepSession = false)
        {
            if (_desktop != null)
            {
                _desktop.Dispose();
                if (_screen == _desktop.gameObject) _screen = null;
                Destroy(_desktop.gameObject);
                _desktop = null;
            }
            foreach (Transform t in OverlayLayer) Destroy(t.gameObject);
            if (!keepSession) Session = null;
        }

        void Update()
        {
            if (Session != null && (State == GameState.Shift || State == GameState.Briefing || State == GameState.Complete))
            {
                if (State == GameState.Shift) Session.Tick(Time.unscaledDeltaTime);
                _desktop?.Tick();
            }
            if (_completeTimer > 0)
            {
                _completeTimer -= Time.unscaledDeltaTime;
                if (_completeTimer <= 0) { _completeTimer = -1; ShowEvaluation(); }
            }
            var keys = ShortcutInput.Poll();
            if (_desktop != null) _desktop.HandleShortcuts(keys);
        }
    }
}
