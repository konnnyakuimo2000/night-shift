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
            if (Database != null && !string.IsNullOrEmpty(Database.companyNameJa)) MailManager.CompanyName = Database.companyNameJa;
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

        public void ShowClockIn() => SetScreen(ClockInScreen.Build(ScreenLayer, Database.GetDay(CurrentDay), OperatorName, Database.companyNameJa, name =>
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
            // その夜の開始時点の記録を残す（やり直したら戻す）
            if (_snapshotDay != CurrentDay)
            {
                CareerRecord.Current.extraTokensTonight = 0;
                _careerSnapshot = CareerRecord.Current.Snapshot();
                _walletSnapshot = Wallet.Snapshot();
                _snapshotDay = CurrentDay;
                SaveWeekSnapshot(CurrentDay);
            }
            else
            {
                if (!string.IsNullOrEmpty(_careerSnapshot)) CareerRecord.Restore(_careerSnapshot);
                if (!string.IsNullOrEmpty(_walletSnapshot)) Wallet.Restore(_walletSnapshot);
            }
            Session = new ShiftSession(Database, day, System.Environment.TickCount, OperatorName);
            Session.ShiftCompleted += OnShiftCompleted;
            _desktop = ShiftDesktop.Build(ScreenLayer, this, Session);
            Session.Ui = _desktop;
            AddPayslipMail(day);
            AddCareerMails(day);
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
            if (day.passwordChangeAtMinute >= 0 || Account.Changed)
                result.Stats.Add(new System.Collections.Generic.KeyValuePair<string, string>("Operator Password",
                    Account.Changed ? $"変更済み（強度: {PasswordPolicy.StrengthLabel(Account.Strength)}{(Account.WrittenDown ? "・付箋あり" : "")}）" : "未変更（初期パスワード）"));
            var slip = Payroll.Compute(day.dayNumber, result, Session.Evaluation, Database.payroll);
            var previous = Wallet.Record(slip, out bool updated);
            NightClosing.Close(Session, result, slip);
            _pendingGameOver = EndingResolver.CheckGameOver(CareerRecord.Current, Session);
            _finalOutcome = EndingResolver.Outcome(Session);
            if (_pendingGameOver == null) SaveProgress(CurrentDay + 1);
            SetScreen(EvaluationScreen.Build(ScreenLayer, day, result, slip, previous, updated, Wallet.Balance, GoToNextDay, RestartDay), GameState.Evaluation);
        }

        /// <summary>翌日へ。翌日の定義が無ければ COMING SOON。</summary>
        void GoToNextDay()
        {
            if (_pendingGameOver != null) { ShowGameOver(_pendingGameOver); return; }
            if (CurrentDay >= DefaultDataFactory.LastDay) { ShowEnding(); return; }
            int next = CurrentDay + 1;
            if (Database.FindDay(next) != null)
            {
                CurrentDay = next;
                ShowDayStart();
            }
            else ShowComingSoon();
        }

        EndingResolver.GameOver _pendingGameOver;
        FinalOutcome _finalOutcome;

        /// <summary>1日の始まり: DAY8 からは朝のネットニュース → タイムカード</summary>
        public void ShowDayStart()
        {
            var day = Database.GetDay(CurrentDay);
            if (CurrentDay >= DefaultDataFactory.MainStartDay && day != null)
                SetScreen(NewsScreen.Build(ScreenLayer, Database, day, ShowClockIn), GameState.ClockIn);
            else ShowClockIn();
        }

        void ShowGameOver(EndingResolver.GameOver go)
        {
            var career = CareerRecord.Current;
            if (!career.gameOversSeen.Contains(go.Id)) career.gameOversSeen.Add(go.Id);
            career.Save();
            _pendingGameOver = null;
            int weekStart = WeekStart(CurrentDay);
            bool hasWeek = PlayerPrefs.HasKey(WeekKey(weekStart));
            SetScreen(GameOverScreen.Build(ScreenLayer, go, OperatorName,
                () => RestartDay(),
                hasWeek ? () => RetryWeek(weekStart) : (System.Action)null,
                ShowTitle), GameState.Complete);
        }

        void ShowEnding()
        {
            var ending = EndingResolver.Resolve(CareerRecord.Current, _finalOutcome);
            SetScreen(EndingScreen.Build(ScreenLayer, ending, _finalOutcome, OperatorName, Wallet, ShowTitle), GameState.Complete);
        }

        // ---------------- 週の初日のセーブ（ゲームオーバー後のやり直し用） ----------------
        static readonly int[] WeekStarts = { 8, 15, 22, 28 };
        static int WeekStart(int day)
        {
            int ws = DefaultDataFactory.MainStartDay;
            foreach (var w in WeekStarts) if (day >= w) ws = w;
            return ws;
        }
        static string WeekKey(int day) => "NightShift.Week." + day;
        const string WeekSep = "#WALLET#";

        void SaveWeekSnapshot(int day)
        {
            if (System.Array.IndexOf(WeekStarts, day) < 0) return;
            try
            {
                PlayerPrefs.SetString(WeekKey(day), CareerRecord.Current.Snapshot() + WeekSep + Wallet.Snapshot());
                PlayerPrefs.Save();
            }
            catch { }
        }

        void RetryWeek(int day)
        {
            try
            {
                var parts = PlayerPrefs.GetString(WeekKey(day), "").Split(new[] { WeekSep }, System.StringSplitOptions.None);
                if (parts.Length == 2) { CareerRecord.Restore(parts[0]); Wallet.Restore(parts[1]); }
            }
            catch { }
            CurrentDay = day;
            _snapshotDay = -1;
            ShowDayStart();
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

        /// <summary>30日間の記録に応じた業務メール（警告・週間評価）</summary>
        void AddCareerMails(DayDefinition day)
        {
            var career = CareerRecord.Current;
            int n = day.dayNumber;
            if (n <= DefaultDataFactory.MainStartDay) return;
            string date = (string.IsNullOrEmpty(day.clockInDateLabel) ? day.dateLabel : day.clockInDateLabel) + " 20:00";
            void Mail(string id, string from, string subject, string body) => Session.Mail.Add(new MailMessage
            {
                MailId = id, From = MailManager.Sender(from), FromAddress = "morita@" + MailManager.CompanyDomain,
                To = MailManager.Recipient(OperatorName), Subject = subject, DateLabel = date,
                Body = $"{OperatorName} 様\n\n{body}\n" + MailManager.Signature(from), Day = n
            }, false);

            if (career.ConsecutiveRank("D", n - 1) == 2)
                Mail("WARN-GO1-" + n, "情報システム部 課長 森田", "【最終警告】業務評価について",
                    "2夜続けて、業務評価が最低でした。\n<b>次も同じ評価なら、契約は更新しません。</b>");
            if (career.accountCompromised == 1 && !career.HasFlag("MAILED_GO3"))
            {
                career.SetFlag("MAILED_GO3");
                Mail("WARN-GO3-" + n, "情報システム部 セキュリティ担当", "【警告】あなたのアカウントが不正に使われました",
                    "あなたのIDが、攻撃に使われました。パスワードを直ちに変更してください。\n<b>次に同じことが起きた場合は、相応の処分を検討します。</b>\n付箋にパスワードを書いていないか、確認してください。");
            }
            if (career.ConsecutiveOvertime(n - 1) >= 5)
                Mail("WARN-GO4-" + n, "カスタマーサポート部 中村", "大丈夫ですか？",
                    "毎朝、夜勤明けのあなたを見かけますが、顔色が良くないです。\n無理しないでくださいね。缶コーヒーばかり飲んでいると、倒れちゃいますよ。");
            if (n == 15 || n == 22 || n == 28)
            {
                int from = n == 15 ? 8 : n == 22 ? 15 : 22, cnt = 0, mal = 0, fines = 0;
                float sum = 0;
                foreach (var r in career.nights)
                {
                    if (r.day < from || r.day >= n) continue;
                    cnt++; sum += CareerRecord.RankValue(r.rank); mal += r.maliciousAllowed; fines += r.fines;
                }
                string[] ranks = { "", "D", "C", "B", "A", "S" };
                string avg = cnt == 0 ? "-" : ranks[Mathf.Clamp(Mathf.RoundToInt(sum / cnt), 1, 5)];
                Mail("WEEKLY-" + n, "情報システム部 課長 森田", "【週間評価】先週の業務について",
                    $"先週（DAY{from}〜{n - 1}）の評価をお知らせします。\n\n平均ランク: <b>{avg}</b>\n不正な接続の許可: {mal} 件\n罰金（業務改善協力金）: {OperatorWallet.Yen(fines)}\n\n引き続き、緊張感を持って業務にあたってください。");
            }
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
            sb.AppendLine($"DAY {slip.day} の日当を支給しました（深夜手当込み）。");
            sb.AppendLine();
            sb.AppendLine($"　日当（深夜手当込み）{Y(slip.basePay)}");
            sb.AppendLine($"　評価手当（{slip.score}点）　{Y(slip.performancePay)}");
            sb.AppendLine($"　精勤手当（{slip.rank}）　{Y(slip.rankBonus)}");
            if (slip.deduction > 0) sb.AppendLine($"　罰金・控除　　　−{Y(slip.deduction)}（{string.Join("、", slip.deductionNotes)}）");
            sb.AppendLine($"　<b>支給額　　　　　{Y(slip.total)}</b>");
            sb.AppendLine();
            sb.AppendLine($"現在の所持金: <b>{Y(Wallet.Balance)}</b>");
            sb.AppendLine();
            sb.Append(MailManager.Signature("総務部 給与担当"));
            Session.Mail.Add(new MailMessage
            {
                MailId = "PAYSLIP-" + slip.day,
                From = MailManager.Sender("総務部 給与担当"),
                FromAddress = "kyuyo@" + MailManager.CompanyDomain,
                To = MailManager.Recipient(OperatorName),
                Subject = $"【給与明細】DAY {slip.day} 日当",
                DateLabel = (string.IsNullOrEmpty(day.clockInDateLabel) ? day.dateLabel : day.clockInDateLabel) + " 18:00",
                Body = sb.ToString(),
                Day = day.dayNumber
            }, false);
        }

        string _careerSnapshot, _walletSnapshot;
        int _snapshotDay = -1;

        void StartNewGame()
        {
            CurrentDay = 1;
            CareerRecord.StartNew();
            _snapshotDay = -1;
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
            ShowDayStart();
        }

        void ContinueGame()
        {
            CurrentDay = ReachedDay;
            Account = OperatorAccount.Load();
            try { OperatorName = PlayerPrefs.GetString(NameKey, OperatorName); } catch { }
            ShowDayStart();
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
