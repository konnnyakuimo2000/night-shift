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
    public enum GameState { Title, ClockIn, Login, DayIntro, Briefing, Shift, Complete, Evaluation }

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
            Systems.SideQuests.SideQuestManager.WalletProvider = () => Wallet;
            if (Database != null && !string.IsNullOrEmpty(Database.companyNameJa)) MailManager.CompanyName = Database.companyNameJa;
            AudioFx.Init(gameObject);
            RetroCursor.Install(gameObject);
            BuildCanvas();
        }

        void Start() => ShowTitle();

        void OnDestroy()
        {
            GameSettings.Changed -= ApplyUiScale;
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
            // Expand: どの画面比率でも、論理キャンバスが 1280×800 を下回らない（16:9 で下端が欠けない）
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
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
            int resume = ResumeDay;
            SetScreen(TitleScreen.Build(ScreenLayer, StartNewGame, resume > 0 ? ContinueGame : (System.Action)null, resume, Quit, Database.days.Count, StartFromDay, DebugPresets), GameState.Title);
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
                _accountSnapshot = Account.Snapshot();
                _snapshotDay = CurrentDay;
                SaveWeekSnapshot(CurrentDay);
                SaveCheckpoint(CurrentDay);
            }
            else
            {
                if (!string.IsNullOrEmpty(_careerSnapshot)) CareerRecord.Restore(_careerSnapshot);
                if (!string.IsNullOrEmpty(_walletSnapshot)) Wallet.Restore(_walletSnapshot);
                // パスワード・付箋・初期化回数も、その夜の開始時点に戻す
                if (!string.IsNullOrEmpty(_accountSnapshot)) Account = OperatorAccount.Restore(_accountSnapshot);
            }
            Session = new ShiftSession(Database, day, System.Environment.TickCount, OperatorName);
            Session.ShiftCompleted += OnShiftCompleted;
            Session.Quests.TerminalEnding += id => _pendingSubEnding = id;
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
            // 夜の終わりに確定する被害（バックアップの喪失など）を、評価・給与の計算より先に確定する
            NightClosing.ConfirmEndOfNightDamage(Session);
            var result = Session.Evaluation.Compute();
            var day = Session.Day;
            ClearScreen();
            DisposeShift(keepSession: true);
            if (day.passwordChangeAtMinute >= 0 || Account.Changed)
                result.Stats.Add(new System.Collections.Generic.KeyValuePair<string, string>("パスワード",
                    Account.Changed ? $"変更済み（強度: {PasswordPolicy.StrengthLabel(Account.Strength)}{(Account.WrittenDown ? "・付箋あり" : "")}）" : "未変更（初期パスワード）"));
            var slip = Payroll.Compute(day.dayNumber, result, Session.Evaluation, Database.payroll);
            var previous = Wallet.Record(slip, out bool updated);
            NightClosing.Close(Session, result, slip);
            if (Session.Director.ExtraSpawned > 0)
                result.Stats.Add(new System.Collections.Generic.KeyValuePair<string, string>("追加の仕事", $"{Session.Director.ExtraSpawned} 件（手が空いたときに届いた接続要求）"));
            // サブクエスト: 個別入金（給与とは別）・段階の進行・返済日の返済
            Session.Quests.OnNightEnd();
            if (Session.Quests.Summary.Count > 0)
                result.Stats.Add(new System.Collections.Generic.KeyValuePair<string, string>("サブクエスト", string.Join("／", Session.Quests.Summary)));
            _pendingGameOver = EndingResolver.CheckGameOver(CareerRecord.Current, Session);
            _finalOutcome = EndingResolver.Outcome(Session);
            // 本編のゲームオーバー・夜の終わりに決まった途中終了（破産など）は、その夜からやり直せるよう翌日の保存地点を作らない
            if (_pendingGameOver == null && Session.Quests.PendingTerminal == null)
            {
                SaveProgress(CurrentDay + 1);
                SaveCheckpoint(CurrentDay + 1);
            }
            SetScreen(EvaluationScreen.Build(ScreenLayer, day, result, slip, previous, updated, Wallet.Balance, GoToNextDay, RestartDay, NightReview.Build(Session)), GameState.Evaluation);
        }

        /// <summary>翌日へ。翌日の定義が無ければ COMING SOON。</summary>
        void GoToNextDay()
        {
            if (_pendingGameOver != null) { ShowGameOver(_pendingGameOver); return; }
            // サブクエスト: 夜の終わりに決まった途中終了 → 朝の進路選択（本編のゲームオーバーが優先）
            var quests = Session?.Quests;
            if (quests != null && quests.PendingTerminal != null)
            {
                string id = quests.ConfirmPendingTerminal();
                ShowSubEnding(id, "その夜からやり直す", () => RestartDay());
                return;
            }
            var choice = quests?.PendingMorningChoice();
            if (choice != null)
            {
                SetScreen(MorningChoiceScreen.Build(ScreenLayer, choice, () =>
                {
                    string id = quests.ChooseMorning(choice, true);
                    if (id != null) ShowSubEnding(id, "その朝から続ける", ContinueGame);
                    else ContinueToNextDay();
                }, () => { quests.ChooseMorning(choice, false); ContinueToNextDay(); }), GameState.Complete);
                return;
            }
            ContinueToNextDay();
        }

        void ContinueToNextDay()
        {
            if (CurrentDay >= DefaultDataFactory.LastDay) { ShowEnding(); return; }
            int next = CurrentDay + 1;
            if (Database.FindDay(next) != null)
            {
                CurrentDay = next;
                ShowDayStart();
            }
            else ShowEnding();
        }

        EndingResolver.GameOver _pendingGameOver;
        string _pendingSubEnding;

        /// <summary>途中終了型のサブエンド（業務中に決まったものは、その夜の開始時点からやり直せる）</summary>
        void ShowSubEnding(string id, string retryLabel, System.Action retry)
        {
            CareerRecord.Current.Save();
            DisposeShift();
            SetScreen(SubEndingScreen.Build(ScreenLayer, id, OperatorName, retryLabel, retry, ShowTitle), GameState.Complete);
        }
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

        void SaveWeekSnapshot(int day)
        {
            if (System.Array.IndexOf(WeekStarts, day) < 0) return;
            try
            {
                PlayerPrefs.SetString(WeekKey(day), WeekSnapshot.Pack(CareerRecord.Current.Snapshot(), Wallet.Snapshot(), Account.Snapshot()));
                PlayerPrefs.Save();
            }
            catch { }
        }

        void RetryWeek(int day)
        {
            try
            {
                if (WeekSnapshot.TryUnpack(PlayerPrefs.GetString(WeekKey(day), ""), out var career, out var wallet, out var account))
                {
                    CareerRecord.Restore(career);
                    Wallet.Restore(wallet);
                    // 旧形式（アカウントなし）は、今のアカウントのまま（過去のパスワードを推測して作らない）
                    if (account != null) Account = OperatorAccount.Restore(account);
                }
            }
            catch { }
            CurrentDay = day;
            _snapshotDay = -1;
            SaveCheckpoint(day);
            ShowDayStart();
        }

        // ---------------- Progress ----------------
        const string ProgressKey = "NightShift.ReachedDay";
        const string NameKey = "NightShift.OperatorName";

        // ---------------- セーブ地点（続きから） ----------------
        // 日付・30日間の記録・財布・アカウントを1か所にまとめて保存する（日付だけ先に進む、などの食い違いを防ぐ）
        const string CheckpointKey = "NightShift.Checkpoint";
        const string QuestChoiceKey = "NightShift.QuestChoice";
        const string CheckpointSep = "#CHECKPOINT#";

        /// <summary>「続きから」で始まる日（セーブ地点が無ければ 0）</summary>
        public int ResumeDay
        {
            get
            {
                if (TryReadCheckpoint(out int day, out _)) return day;
                return ReachedDay > 1 ? ReachedDay : 0; // セーブ地点を作る前の古いセーブ
            }
        }

        static bool TryReadCheckpoint(out int day, out string[] parts)
        {
            day = 0; parts = null;
            try
            {
                parts = PlayerPrefs.GetString(CheckpointKey, "").Split(new[] { CheckpointSep }, System.StringSplitOptions.None);
                return parts.Length == 4 && int.TryParse(parts[0], out day) && day > 0;
            }
            catch { return false; }
        }

        void SaveCheckpoint(int day)
        {
            if (Database.FindDay(day) == null) return;
            try
            {
                PlayerPrefs.SetString(CheckpointKey, string.Join(CheckpointSep, new[] { day.ToString(), CareerRecord.Current.Snapshot(), Wallet.Snapshot(), Account.Snapshot() }));
                PlayerPrefs.SetString(NameKey, OperatorName);
                PlayerPrefs.Save();
            }
            catch { }
        }

        /// <summary>処理完了後の二択専用の保存。通常の朝の保存は残し、途中の業務は再開しない。</summary>
        public void SaveQuestChoiceCheckpoint()
        {
            if (CareerRecord.SaveDisabled || Session?.Quests.PendingChoice?.QuestId != "SQ06") return;
            try
            {
                PlayerPrefs.SetString(QuestChoiceKey, string.Join(CheckpointSep, new[] { CurrentDay.ToString(), CareerRecord.Current.Snapshot(), Wallet.Snapshot(), Account.Snapshot() }));
                PlayerPrefs.Save();
            }
            catch { }
        }

        bool ResumeQuestChoice()
        {
            string[] saved;
            try { saved = PlayerPrefs.GetString(QuestChoiceKey, "").Split(new[] { CheckpointSep }, System.StringSplitOptions.None); }
            catch { return false; }
            if (saved.Length != 4 || !int.TryParse(saved[0], out int day) || Database.FindDay(day) == null) return false;
            var record = JsonUtility.FromJson<CareerRecord>(saved[1]);
            if (record == null || !record.Quest("SQ06").IsOpen || !record.Quest("SQ06").Has("N3_DONE")) return false;
            // やり直しは同じ夜の朝へ戻す。二択から通常業務へ抜ける経路は作らない。
            if (!TryReadCheckpoint(out int morning, out var original) || morning != day) return false;
            _careerSnapshot = original[1]; _walletSnapshot = original[2]; _accountSnapshot = original[3]; _snapshotDay = day;
            CareerRecord.Restore(saved[1]); Wallet.Restore(saved[2]); Account = OperatorAccount.Restore(saved[3]);
            CurrentDay = day;
            ClearScreen(); DisposeShift();
            Session = new ShiftSession(Database, Database.GetDay(day), 0, OperatorName);
            Session.Quests.TerminalEnding += id => _pendingSubEnding = id;
            _desktop = ShiftDesktop.Build(ScreenLayer, this, Session);
            Session.Ui = _desktop; _screen = _desktop.gameObject; State = GameState.Shift;
            Session.Quests.Route<Systems.SideQuests.Sq06Route>().ResumeChoice();
            return true;
        }

        /// <summary>新しく始めたら、前のゲームのセーブ地点と週の初日のセーブを消す</summary>
        static void ClearSaves()
        {
            try
            {
                PlayerPrefs.DeleteKey(CheckpointKey);
                PlayerPrefs.DeleteKey(QuestChoiceKey);
                foreach (var w in WeekStarts) PlayerPrefs.DeleteKey(WeekKey(w));
                PlayerPrefs.SetInt(ProgressKey, 1); // 最高到達日も新しいゲームに合わせる
                PlayerPrefs.Save();
            }
            catch { }
        }

        /// <summary>到達した最高の日（記録用）</summary>
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
        public const string PredecessorMonitorFlag = "PREDECESSOR_MONITOR";

        void AddCareerMails(DayDefinition day)
        {
            var career = CareerRecord.Current;
            int n = day.dayNumber;
            if (n < DefaultDataFactory.MainStartDay) return;
            string date = (string.IsNullOrEmpty(day.clockInDateLabel) ? day.dateLabel : day.clockInDateLabel) + " 20:00";
            // 同じ用件でも、夜ごとに言い回しを変える
            string Vary(params string[] options) => options[(n + career.seed) % options.Length];
            void Mail(string id, string from, string subject, string body) => Session.Mail.Add(new MailMessage
            {
                MailId = id, From = MailManager.Sender(from), FromAddress = "morita@" + MailManager.CompanyDomain,
                To = MailManager.Recipient(OperatorName), Subject = subject, DateLabel = date,
                Body = $"{OperatorName} 様\n\n{body}\n" + MailManager.Signature(from), Day = n
            }, false);

            // 本番初日: 前任者の机に残っていた古いモニターを、サブモニターとして使わせてもらえる
            if (!career.HasFlag(PredecessorMonitorFlag))
            {
                career.SetFlag(PredecessorMonitorFlag);
                if (!career.Owns(Items.Monitor1)) career.AddItem(Items.Monitor1);
                Mail("PREDECESSOR-MONITOR", "情報システム部 課長 森田", "前任者の机のモニターについて",
                    "前任者の机に、古い15インチのモニターが残っていました。\n捨てるのも手間なので、サブモニターとして使ってください。\n（備品の申請は不要です。壊したら弁償です）");
            }
            if (Session.SeniorHelp)
                Mail("SENIOR-HELP-" + n, "カスタマーサポート部 中村", Vary("今夜、少し残ります", "問い合わせ、少し手伝います", "今夜は私もいます"),
                    Vary($"最近きつそうなので、今夜は私も少し残って、問い合わせを{ShiftSession.SeniorHelpTickets}件ほど引き受けます。\n無理をしないように。\n（課長には「精勤手当はなし」と言われました。ごめんなさい）",
                         $"昨日も一昨日も大変だったみたいですね。\n今夜は終電まで残れるので、問い合わせを{ShiftSession.SeniorHelpTickets}件ほどこっちで受けます。\n接続の審査に集中してください。\n（手当の件は…ごめんなさい、課長に止められました）",
                         $"夜勤、ひとりで回すのは無理がありますよね。\n今夜は私が問い合わせを{ShiftSession.SeniorHelpTickets}件引き受けます。缶コーヒー、机に置いておきました。\n（精勤手当はなしだそうです。この会社らしいですね）"));
            if (n == DefaultDataFactory.MainStartDay) return;

            if (career.ConsecutiveRank("D", n - 1) == 2)
                Mail("WARN-GO1-" + n, "情報システム部 課長 森田", Vary("【最終警告】業務評価について", "【重要】契約について", "業務評価の件（必読）"),
                    Vary("2夜続けて、業務評価が最低でした。\n<b>次も同じ評価なら、契約は更新しません。</b>",
                         "評価表を見ました。2夜続けて最低です。\n代わりの人はいくらでもいます。\n<b>次も同じなら、契約はここまでです。</b>",
                         "上から言われているので、はっきり書きます。\n2夜続けての最低評価は、うちでは前例がありません。\n<b>今夜も同じなら、契約は打ち切りです。</b>"));
            if (career.accountCompromised >= 1 && !career.HasFlag("MAILED_GO3_" + career.accountCompromised))
            {
                career.SetFlag("MAILED_GO3_" + career.accountCompromised);
                bool last = career.accountCompromised >= 2;
                Mail("WARN-GO3-" + n, "情報システム部 セキュリティ担当", last ? "【最終警告】あなたのアカウントが再び不正に使われました" : "【警告】あなたのアカウントが不正に使われました",
                    Vary("あなたのIDが、攻撃に使われました。パスワードを直ちに変更してください。\n",
                         "昨夜、あなたのIDで不正なログインがありました。心当たりがなければ、すぐにパスワードを変えてください。\n",
                         "監査ログに、あなたのIDから身に覚えのない操作が残っています。パスワードを直ちに変更してください。\n") +
                    (last ? "<b>次に同じことが起きた場合は、契約を解除し、警察に相談します。</b>" : "<b>同じことが続く場合は、相応の処分を検討します。</b>") +
                    "\n付箋にパスワードを書いていないか、確認してください。");
            }
            if (career.ConsecutiveOvertime(n - 1) >= 5)
                Mail("WARN-GO4-" + n, "カスタマーサポート部 中村", Vary("大丈夫ですか？", "ちゃんと寝てますか", "顔色、悪いですよ"),
                    Vary("毎朝、夜勤明けのあなたを見かけますが、顔色が良くないです。\n無理しないでくださいね。缶コーヒーばかり飲んでいると、倒れちゃいますよ。",
                         "今朝、エレベーターで会ったとき、目が真っ赤でした。\n残業代も出ないのに、毎晩残るのはやめてください。体を壊したら元も子もないです。",
                         "前の夜勤の人も、最後はこんな感じでした。\nお願いだから、今夜は時間になったら帰ってください。"));
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
                string comment = avg == "S" || avg == "A" ? Vary("この調子で頼みます。", "上も評価しています。ただし、手当は増えません。", "よくやっています。気を抜かないように。")
                               : avg == "B" || avg == "C" ? Vary("引き続き、緊張感を持って業務にあたってください。", "まだ改善の余地があります。", "可もなく不可もなく、です。")
                               : Vary("このままでは困ります。", "上から説明を求められています。改善してください。", "正直、期待外れです。");
                Mail("WEEKLY-" + n, "情報システム部 課長 森田", "【週間評価】先週の業務について",
                    $"先週（DAY{from}〜{n - 1}）の評価をお知らせします。\n\n平均ランク: <b>{avg}</b>\n不正な接続の許可: {mal} 件\n罰金（業務改善協力金）: {OperatorWallet.Yen(fines)}\n\n{comment}");
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
            if (slip.taskPay > 0) sb.AppendLine($"　出来高手当（{slip.tasksDone}件）　{Y(slip.taskPay)}");
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

        string _careerSnapshot, _walletSnapshot, _accountSnapshot;
        int _snapshotDay = -1;

        void StartNewGame()
        {
            CurrentDay = 1;
            CareerRecord.StartNew();
            ClearSaves();
            _snapshotDay = -1;
            Wallet.ResetAll();
            Account = OperatorAccount.NewGame(OperatorName);
            Account.Save();
            // 新しいゲームのセーブ地点（DAY1）をすぐ作る。最初の夜の前に終了しても、前のゲームの日付で再開しないように
            SaveCheckpoint(1);
            ShowClockIn();
        }

        /// <summary>デバッグ: タイトルから始めるときの状態</summary>
        public static readonly string[] DebugPresets =
        {
            "今の記録のまま", "新規（記録・所持金を初期化）", "ツールを全部持っている＋¥100,000", "会社の信用が低い（30）",
            "前の2夜がD（先輩の手伝い）", "前任者の手がかり3つ"
        };

        /// <summary>デバッグモード: 任意の日から開始（preset: DebugPresets の番号。記録・所持金を書き換える）</summary>
        void StartFromDay(int day, int preset)
        {
            if (Database.FindDay(day) == null) return;
            var career = CareerRecord.Current;
            switch (preset)
            {
                case 1: CareerRecord.StartNew(); Wallet.ResetAll(); break;
                case 2: UI.Shift.DebugPanelView.GrantAllTools(career, Database, day); Wallet.DebugAddMoney(100000); break;
                case 3: career.trust = 30; career.Save(); break;
                case 4: UI.Shift.DebugPanelView.SetPreviousRanks(career, day, "D", 2); break;
                case 5:
                    foreach (var id in new[] { "C1", "C2", "C3" }) if (!career.clues.Contains(id)) career.clues.Add(id);
                    career.Save();
                    break;
            }
            _snapshotDay = -1;
            CurrentDay = day;
            Account = OperatorAccount.Load();
            try { OperatorName = PlayerPrefs.GetString(NameKey, OperatorName); } catch { }
            if (string.IsNullOrEmpty(Account.UserId) || Account.UserId == DefaultOperatorName) Account.UserId = OperatorName;
            ShowDayStart();
        }

        void ContinueGame()
        {
            try { OperatorName = PlayerPrefs.GetString(NameKey, OperatorName); } catch { }
            if (ResumeQuestChoice()) return;
            if (TryReadCheckpoint(out int day, out var parts) && Database.FindDay(day) != null)
            {
                // セーブ地点の日付・記録・財布・アカウントをそろえて戻す
                CareerRecord.Restore(parts[1]);
                Wallet.Restore(parts[2]);
                Account = OperatorAccount.Restore(parts[3]);
                CurrentDay = day;
            }
            else
            {
                CurrentDay = Mathf.Max(1, ReachedDay);
                Account = OperatorAccount.Load();
            }
            _snapshotDay = -1;
            ShowDayStart();
        }

        /// <summary>デバッグ用: 指定日を最初から開始</summary>
        public void StartDay(int day)
        {
            if (Database.FindDay(day) == null) return;
            CurrentDay = day;
            RestartDay();
        }

        /// <summary>デバッグ: ゲームオーバー・エンディングの画面を確認する（本当の記録には残さない）</summary>
        public void DebugPreview(string id)
        {
            DisposeShift();
            ClearScreen();
            var real = CareerRecord.Current;
            bool saveWas = CareerRecord.SaveDisabled;
            var c = new CareerRecord { seed = real.seed, trust = 80 };
            for (int d = DefaultDataFactory.MainStartDay; d <= DefaultDataFactory.LastDay; d++) c.nights.Add(new NightRecord { day = d, rank = "A" });
            CareerRecord.SetCurrentForTests(c);
            CareerRecord.SaveDisabled = true;
            void Back()
            {
                CareerRecord.SetCurrentForTests(real);
                CareerRecord.SaveDisabled = saveWas;
                ShowTitle();
            }
            if (id.StartsWith("SE-"))
            {
                SetScreen(SubEndingScreen.Build(ScreenLayer, id, OperatorName, "戻る", Back, Back), GameState.Complete);
                return;
            }
            if (id.StartsWith("GO"))
            {
                var s = new ShiftSession(Database, Database.GetDay(20), 1, OperatorName);
                switch (id)
                {
                    case "GO1": foreach (var n in c.nights) if (n.day >= 18 && n.day <= 20) n.rank = "D"; break;
                    case "GO2": s.FatalIncident = "全社のファイルが暗号化された（BRIAR ROSE）"; break;
                    case "GO3": c.accountCompromised = 3; break;
                    case "GO4": foreach (var n in c.nights) if (n.day >= 14 && n.day <= 20) n.overtime = true; break;
                }
                var go = EndingResolver.CheckGameOver(c, s);
                if (go == null) { Back(); return; }
                SetScreen(GameOverScreen.Build(ScreenLayer, go, OperatorName, Back, Back, Back), GameState.Complete);
                return;
            }
            var outcome = FinalOutcome.Defended;
            switch (id)
            {
                case "END1": outcome = FinalOutcome.Critical; break;
                case "END2": c.trust = 20; break;
                case "END3": foreach (var n in c.nights) n.rank = "S"; c.clues.AddRange(new[] { "C1", "C2", "C3", "C4" }); break;
                case "END4": foreach (var n in c.nights) { n.rank = "S"; n.correctReports = 1; } break;
                case "END6": foreach (var n in c.nights) n.rank = "C"; outcome = FinalOutcome.PartialDamage; break;
            }
            var ending = EndingResolver.Resolve(c, outcome);
            SetScreen(EndingScreen.Build(ScreenLayer, ending, outcome, OperatorName, Wallet, Back), GameState.Complete);
        }

        public void RestartDay()
        {
            try { PlayerPrefs.DeleteKey(QuestChoiceKey); } catch { }
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
            if (!keepSession)
            {
                Session = null;
                _pendingSubEnding = null;
                _completeTimer = -1; // 前の夜の「業務完了 → 評価画面」のタイマーを取り消す（やり直し・タイトルへ戻ったとき）
            }
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
                if (_completeTimer <= 0)
                {
                    _completeTimer = -1;
                    // 業務を終えた夜のときだけ評価へ進む
                    if (Session != null && Session.Phase == ShiftPhase.Complete) ShowEvaluation();
                }
            }
            if (_pendingSubEnding != null)
            {
                string id = _pendingSubEnding;
                _pendingSubEnding = null;
                ShowSubEnding(id, "その夜からやり直す", () => RestartDay());
            }
            var keys = ShortcutInput.Poll();
            if (_desktop != null) _desktop.HandleShortcuts(keys);
        }
    }

    /// <summary>
    /// 週のやり直し用の保存（記録・財布・アカウント）。旧形式は「記録#WALLET#財布」でアカウントがない。
    /// </summary>
    public static class WeekSnapshot
    {
        public const string WalletSep = "#WALLET#";
        public const string AccountSep = "#ACCOUNT#";

        public static string Pack(string career, string wallet, string account) => career + WalletSep + wallet + AccountSep + account;

        /// <summary>account は旧形式なら null</summary>
        public static bool TryUnpack(string packed, out string career, out string wallet, out string account)
        {
            career = wallet = account = null;
            if (string.IsNullOrEmpty(packed)) return false;
            var parts = packed.Split(new[] { WalletSep }, System.StringSplitOptions.None);
            if (parts.Length != 2) return false;
            career = parts[0];
            var rest = parts[1].Split(new[] { AccountSep }, System.StringSplitOptions.None);
            wallet = rest[0];
            if (rest.Length == 2 && rest[1].Length > 0) account = rest[1];
            return career.Length > 0 && wallet.Length > 0;
        }
    }
}
