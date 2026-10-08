using System;
using System.Collections.Generic;
using NightShift.Controls;
using NightShift.Core;
using NightShift.Events;
using NightShift.Systems;
using NightShift.UI.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>シフト中の社内システム デスクトップ。各業務Windowの起点。</summary>
    public class ShiftDesktop : MonoBehaviour, IShiftUi
    {
        public GameManager Game { get; private set; }
        public ShiftSession S { get; private set; }
        public WindowManager Windows { get; private set; }
        public RectTransform Area { get; private set; }
        public bool ShiftStarted { get; private set; }

        Taskbar _taskbar;
        NotificationToaster _toaster;
        RectTransform _modalLayer;
        GameObject _pause;
        QuestUi _questUi;
        DebugOverlay _overlay;

        public static ShiftDesktop Build(RectTransform parent, GameManager gm, ShiftSession s)
        {
            var root = UIFactory.Solid(parent, RetroTheme.Desktop, "ShiftDesktop");
            root.rectTransform.Stretch();
            var d = root.gameObject.AddComponent<ShiftDesktop>();
            d.Game = gm;
            d.S = s;

            // 壁紙のロゴ
            var wall = NexusLogo.Create(root.transform, 0, 0, 72, Color.white, true, 0.16f);
            wall.anchorMin = wall.anchorMax = new Vector2(1, 0); wall.pivot = new Vector2(1, 0);
            wall.anchoredPosition = new Vector2(-40, 60);
            var logo = UIFactory.Label(root.transform, gm.Database.systemName, 18, TextAnchor.LowerRight, new Color(1, 1, 1, 0.12f));
            logo.rectTransform.Stretch(0, 0, 44, 38);
            logo.fontStyle = FontStyle.Bold;

            // モニターに貼った付箋（ウィンドウの下になる）
            d._noteLayer = UIFactory.Rect("NoteLayer", root.transform).Stretch(0, 0, 0, Taskbar.Height);
            d.RefreshStickyNote();

            d.Area = UIFactory.Rect("WindowArea", root.transform).Stretch(0, 0, 0, Taskbar.Height);
            d.Windows = WindowManager.Create(d.Area);
            d._taskbar = Taskbar.Build(root.transform, d);
            d._monitorLayer = UIFactory.Rect("Monitors", root.transform).Stretch();
            d.RebuildMonitors();
            d.InventoryChanged += d.RebuildMonitors;
            HorrorFx.Attach(d, UIFactory.Rect("Fx", root.transform).Stretch());
            d._questUi = QuestUi.Attach(d, UIFactory.Rect("QuestFx", root.transform).Stretch());
            d._toaster = NotificationToaster.Build(root.transform, d);
            d._modalLayer = UIFactory.Rect("Modal", root.transform).Stretch();
            d._overlay = DebugOverlay.Build(gm.DebugLayer, d);

            s.Notifications.Raised += d.OnNotification;
            s.EndWarning += d.OnEndWarning;
            s.ShiftEnded += d.OnShiftEnded;

            MainMenuView.Open(d);
            return d;
        }

        public void Dispose()
        {
            if (S != null)
            {
                S.Notifications.Raised -= OnNotification;
                S.EndWarning -= OnEndWarning;
                S.ShiftEnded -= OnShiftEnded;
            }
            if (_overlay != null) Destroy(_overlay.gameObject);
            S.Time.Paused = false;
        }

        RectTransform _noteLayer;

        /// <summary>付箋を貼り直す（パスワード変更後など）</summary>
        public void RefreshStickyNote()
        {
            if (_noteLayer == null) return;
            foreach (Transform t in _noteLayer) Destroy(t.gameObject);
            var note = StickyNote.Create(_noteLayer, Game.Account, 24, 24, Game.Account.Changed ? -4 : 3);
            // 自分で貼った付箋は、クリックではがせる
            if (note != null && Game.Account.Changed)
            {
                var hit = note.gameObject.AddComponent<UnityEngine.UI.Image>();
                hit.color = new Color(0, 0, 0, 0);
                note.gameObject.AddComponent<ClickHandler>().OnClick = ToggleStickyNote;
            }
        }

        /// <summary>新しいパスワードの付箋を貼る／はがす（貼ると、IDを悪用されやすくなる）</summary>
        public void ToggleStickyNote()
        {
            var acc = Game.Account;
            if (!acc.Changed)
            {
                MessageBox.Show(this, "付箋", "まだ初期パスワードのままです。\n先にパスワードを変更してください。", "lock");
                return;
            }
            if (acc.WrittenDown)
                ShowModal("付箋", "モニターに貼ったパスワードの付箋をはがしますか？", "lock", "はがす", () => { acc.SetWrittenDown(false); RefreshStickyNote(); }, "やめる", null);
            else
                ShowModal("付箋", "新しいパスワードを付箋に書いて、モニターに貼りますか？\n（忘れにくくなりますが、見られると ID を悪用されやすくなります）", "warning", "貼る", () => { acc.SetWrittenDown(true); RefreshStickyNote(); }, "やめる", null);
        }

        // ---------------- Flow ----------------
        /// <summary>業務開始前の「業務開始」処理（メールを閉じて開き直しても使えるよう保持）</summary>
        public Action PendingStart { get; private set; }

        public void ShowBriefing(Action onStart)
        {
            PendingStart = () =>
            {
                PendingStart = null;
                onStart();
            };
            MailboxView.Open(this, PendingStart);
        }

        public void OnShiftStarted()
        {
            ShiftStarted = true;
            ShowStatus("業務を開始しました。");
            var mm = Windows.Find(MainMenuView.Id);
            if (mm != null) Windows.Focus(mm);
        }

        public void ShowShiftComplete()
        {
            PopupMenu.CloseAll();
            var host = UIFactory.Solid(_modalLayer, new Color(0, 0, 0, 0.35f), "CompleteDim");
            host.rectTransform.Stretch();
            var box = UIFactory.Panel(host.transform, RetroTheme.Raised, "Complete");
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            box.rectTransform.sizeDelta = new Vector2(420, 150);
            var bar = UIFactory.Image(box.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Title");
            bar.rectTransform.StretchH(3, 18, 3, 3);
            UIFactory.Label(bar.transform, " 業務終了", 12, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            var t = UIFactory.Label(box.transform, "SHIFT COMPLETE", 30, TextAnchor.MiddleCenter, RetroTheme.Navy, true);
            t.rectTransform.StretchH(34, 50);
            UIFactory.Label(box.transform, $"{S.Time.Now}  業務終了。お疲れさまでした。\n業務記録を集計しています...", 12, TextAnchor.MiddleCenter).rectTransform.StretchH(88, 44);
        }

        // ---------------- サブモニター ----------------
        RectTransform _monitorLayer;
        readonly List<WorkstationScreen> _stations = new List<WorkstationScreen>();
        public int CurrentMonitor { get; private set; }
        /// <summary>メインモニターに、見ていない間の通知がある</summary>
        public bool MainHasUnseen { get; private set; }
        public IReadOnlyList<WorkstationScreen> Stations => _stations;

        public int MonitorCount
        {
            get
            {
                var c = CareerRecord.Current;
                int n = 1;
                if (c.Owns(Data.Items.Monitor1)) n++;
                if (c.Owns(Data.Items.Monitor1) && c.Owns(Data.Items.Monitor2)) n++;
                return n;
            }
        }

        public void RebuildMonitors()
        {
            int need = MonitorCount - 1;
            while (_stations.Count < need)
            {
                var ws = WorkstationScreen.Build(_monitorLayer, this, _stations.Count + 1);
                ws.gameObject.SetActive(false);
                _stations.Add(ws);
            }
            foreach (var ws in _stations) { ws.RebuildIcons(); ws.RefreshSwitcher(); }
            _taskbar?.RefreshMonitors();
        }

        /// <summary>見るモニターを切り替える（0 = メイン）</summary>
        public void ShowMonitor(int index)
        {
            index = Mathf.Clamp(index, 0, MonitorCount - 1);
            if (index == CurrentMonitor) return;
            PopupMenu.CloseAll();
            CurrentMonitor = index;
            for (int i = 0; i < _stations.Count; i++) _stations[i].gameObject.SetActive(i + 1 == index);
            if (index == 0) MainHasUnseen = false;
            foreach (var ws in _stations) ws.RefreshSwitcher();
            _taskbar?.RefreshMonitors();
            AudioFx.Click();
        }

        public T FindTool<T>(string id) where T : Component
        {
            foreach (var ws in _stations)
            {
                var w = ws.Windows.Find("tool:" + id);
                if (w != null) return w.GetComponent<T>();
            }
            return null;
        }

        // ---------------- 前任者の手がかり ----------------
        readonly HashSet<string> _cluesOffered = new HashSet<string>();

        /// <summary>手がかりを見つけた。個人フォルダに保存すると数えられる（END3 の条件）</summary>
        public void OfferClue(string id, string title, string text)
        {
            var career = CareerRecord.Current;
            if (career.clues.Contains(id) || _cluesOffered.Contains(id)) return;
            _cluesOffered.Add(id);
            S.Notifications.Notify(NotificationType.Info, "気になる記録", title, S.Time.CurrentMinute, () => ShowClue(id, text));
        }

        /// <summary>手がかりの内容を見せ、個人フォルダに保存するか尋ねる</summary>
        public void ShowClue(string id, string text)
        {
            var career = CareerRecord.Current;
            bool saved = career.clues.Contains(id);
            MessageBox.Ask(this, "気になる記録", text + (saved ? "\n\n（個人フォルダに保存済み）" : "\n\n個人フォルダに保存しますか？"),
                saved ? "閉じる" : "個人フォルダに保存", () => { if (!career.clues.Contains(id)) { career.clues.Add(id); career.Save(); ShowStatus("個人フォルダに保存しました。"); } },
                saved ? "OK" : "保存しない", null, "doc");
        }

        /// <summary>画面の端が一瞬だけ化ける演出（HorrorFx が受け取る）</summary>
        public event Action GlitchRequested;
        public void RequestGlitch() => GlitchRequested?.Invoke();

        // ---- メインモニターで選んでいるもの（ツールが参照する） ----
        public Events.EventInstance SelectedRequest { get; private set; }
        public LogEntry SelectedLog { get; private set; }
        public MailMessage SelectedMail { get; private set; }
        public event Action SelectionChanged;
        public void SelectRequest(Events.EventInstance e) { SelectedRequest = e; SelectionChanged?.Invoke(); }
        public void SelectLog(LogEntry e) { SelectedLog = e; SelectionChanged?.Invoke(); }
        public void SelectMail(MailMessage m) { SelectedMail = m; SelectionChanged?.Invoke(); }

        // ---------------- IShiftUi ----------------
        public void OpenConnectionMonitor(EventInstance focus) => ConnectionMonitorView.Open(this, focus);
        public void OpenHelpDesk(Ticket focus) => HelpDeskView.Open(this, focus);
        public void OpenLogViewer() => LogViewerView.Open(this, null);
        public void OpenPasswordChange() => PasswordChangeView.Open(this);
        public void OpenMailbox(MailMessage focus) => MailboxView.Open(this, null, focus);

        /// <summary>マニュアルの図に渡す情報</summary>
        public FigureContext FigureContext => new FigureContext
        {
            Day = S.Day, Unlocked = Unlocked, Nodes = S.Network.Nodes, Permissions = Game.Database.permissions
        };

        public void ShowStatus(string message)
        {
            var mm = Windows.Find(MainMenuView.Id);
            if (mm != null) mm.SetStatus(0, message);
        }

        // ---------------- Events ----------------
        void OnNotification(Notification n)
        {
            if (CurrentMonitor != 0) { MainHasUnseen = true; foreach (var ws in _stations) ws.RefreshSwitcher(); }
            _toaster.Show(n);
            _taskbar.Flash();
            switch (n.Type)
            {
                case NotificationType.SecurityAlert: AudioFx.Alert(); break;
                case NotificationType.HelpDeskTicket: AudioFx.Ticket(); break;
                case NotificationType.ShiftNotice: AudioFx.Chime(); break;
                default: AudioFx.Notify(); break;
            }
        }

        void OnEndWarning() => ShowStatus("業務終了まで残り10分です。");

        void OnShiftEnded()
        {
            ShowStatus($"{S.Day.EndLabel} 業務終了。新規の受付を停止しました。");
            if (S.OvertimeOffered)
            {
                int coffee = CareerRecord.Current.ItemCount(Data.Items.Coffee);
                MessageBox.Ask(this, "業務終了", $"{S.Day.EndLabel} になりました。仕事が残っています。\n缶コーヒーを1本飲んで、30分だけ残業しますか？（残り {coffee} 本・残業代は出ません）",
                    "残業する（缶コーヒー）", () => { S.StartOvertime(); ShowStatus("残業中 — 新しい仕事は来ません。残りを片付けてください。"); },
                    "退勤する", S.DeclineOvertime, "clock");
            }
        }

        // ---------------- Update ----------------
        public void Tick()
        {
            _taskbar.Tick();
            _toaster.Tick();
            _overlay.Tick();
        }

        public void HandleShortcuts(List<Shortcut> keys)
        {
            if (ShortcutInput.Capturing) return; // キー設定の入力待ち中
            // デバッグは一時停止（Esc）のメニューから開く（デバッグのキーでは開かない）

            // 一時停止キー（既定 Esc）は最優先。リモート操作中・サブモニター表示中・演出中でも必ず効く
            if (keys.Contains(Shortcut.Escape))
            {
                if (ModalOpen) { CancelModal(); return; }
                if (_pause != null) { ClosePause(); return; }
                if (PopupMenu.IsOpen) { PopupMenu.CloseAll(); return; }
                if (ShiftStarted && S.Phase != ShiftPhase.Complete) { OpenPause(); return; }
            }
            if (_pause != null || ModalOpen) return;
            if (keys.Contains(Shortcut.OpenMail)) MailboxView.Open(this);

            // Remote Support がアクティブなら、キー入力はリモートPCへ
            var active = Windows.Active;
            var remote = active != null ? active.GetComponent<RemoteSupportView>() : null;
            if (remote != null && remote.HasKeyboard)
            {
                remote.HandleShortcuts(keys);
                return;
            }

            // モニターの切り替え（キー設定で変更可。既定 Ctrl+←/→）と Ctrl+1〜3
            if (!ShortcutInput.TextFieldFocused && MonitorCount > 1)
            {
                if (Core.GameSettings.Pressed(Core.GameSettings.MonitorPrev)) { ShowMonitor((CurrentMonitor + MonitorCount - 1) % MonitorCount); return; }
                if (Core.GameSettings.Pressed(Core.GameSettings.MonitorNext)) { ShowMonitor((CurrentMonitor + 1) % MonitorCount); return; }
                if (ShortcutInput.Ctrl)
                    for (int i = 0; i < MonitorCount; i++)
                        if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { ShowMonitor(i); return; }
            }
            if (CurrentMonitor != 0)
            {
                // サブモニターを見ている間は、そのモニターのウィンドウにキーを送る
                var ws = _stations[CurrentMonitor - 1];
                var wa = ws.Windows.Active;
                if (wa != null && !PopupMenu.IsOpen)
                    foreach (var kv in wa.Accelerators)
                        if (Input.GetKeyDown(kv.Key) && !ShortcutInput.TextFieldFocused) { kv.Value?.Invoke(); break; }
                return;
            }

            if (Windows.Switching) { /* 切替中は確定待ち */ }
            foreach (var k in keys)
            {
                switch (k)
                {
                    case Shortcut.SwitchNext: Windows.SwitchNext(false, () => ShortcutInput.SwitchModifierHeld); break;
                    case Shortcut.SwitchPrev: Windows.SwitchNext(true, () => ShortcutInput.SwitchModifierHeld); break;
                    case Shortcut.CloseWindow: if (active != null) active.RequestClose(); break;
                    case Shortcut.ShowDesktop: Windows.MinimizeAll(); break;
                    case Shortcut.Escape:
                        if (PopupMenu.IsOpen) PopupMenu.CloseAll();
                        else if (ShiftStarted && S.Phase != ShiftPhase.Complete) OpenPause();
                        break;
                }
            }

            // Window ごとのアクセラレータ（Alt+文字 / 文字のみ）
            if (active != null && !PopupMenu.IsOpen && !Windows.Switching)
            {
                bool text = ShortcutInput.TextFieldFocused;
                bool alt = ShortcutInput.Alt, ctrl = ShortcutInput.Ctrl;
                // Alt＋文字 でメニューバーを開く（同じ文字のボタン操作がないときだけ）
                if (alt && !ctrl)
                    foreach (var kv in active.MenuMnemonics)
                        if (Input.GetKeyDown(kv.Key) && !active.Accelerators.ContainsKey(kv.Key)) { kv.Value?.Invoke(); return; }
                foreach (var kv in active.Accelerators)
                {
                    if (!Input.GetKeyDown(kv.Key)) continue;
                    bool isLetter = (kv.Key >= KeyCode.A && kv.Key <= KeyCode.Z) || (kv.Key >= KeyCode.Alpha0 && kv.Key <= KeyCode.Alpha9);
                    if (isLetter && (ctrl || (text && !alt))) continue;
                    if (isLetter && !alt && !Core.GameSettings.Current.letterAccelerators) continue;
                    if (!isLetter && text && kv.Key != KeyCode.Return && kv.Key != KeyCode.KeypadEnter) continue;
                    kv.Value?.Invoke();
                    break;
                }
            }
        }

        public void ToggleOverlay() => _overlay.Toggle();

        // ---------------- 入力の受け付け ----------------
        /// <summary>ブルースクリーン・停電などの演出で、メインの操作ができない</summary>
        public bool EffectBlocking { get; set; }
        /// <summary>サブクエストの全面演出・その場の二択の間は、承認などの入力を受け付けない（HorrorFx の EffectBlocking とは別に持つ）</summary>
        public bool QuestBlocking { get; set; }
        public bool PauseOpen => _pause != null;
        /// <summary>一時停止メニュー以外の理由で時計を止めている（サブクエストの二択・全面演出）。一時停止を閉じても再開しない</summary>
        public bool ClockHeld => (S?.Quests?.PendingChoice != null) || (_questUi != null && _questUi.HoldsClock);

        /// <summary>メインモニターの業務画面への判定キー（許可・拒否など）を受け付けない状態</summary>
        public bool InputBlocked => _pause != null || ModalOpen || CurrentMonitor != 0 || EffectBlocking || QuestBlocking;

        // ---------------- モーダル（他を操作できない確認） ----------------
        readonly List<Action> _modalCancels = new List<Action>();
        public bool ModalOpen => _modalCancels.Count > 0;

        void CancelModal()
        {
            if (_modalCancels.Count == 0) return;
            _modalCancels[_modalCancels.Count - 1]?.Invoke();
        }

        /// <summary>
        /// 画面全体を暗くして最前面に出す確認。どちらかを選ぶまで他の操作はできない。
        /// Enter = 1つ目のボタン、Esc（一時停止キー）= 2つ目のボタン。
        /// </summary>
        public void ShowModal(string title, string message, string icon, string yesLabel, Action onYes, string noLabel, Action onNo)
        {
            PopupMenu.CloseAll();
            var dim = UIFactory.Solid(_modalLayer, new Color(0, 0, 0, 0.45f), "ModalDim");
            dim.rectTransform.Stretch();
            dim.transform.SetAsLastSibling();
            var box = UIFactory.Panel(dim.transform, RetroTheme.Raised, "Modal");
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            box.rectTransform.sizeDelta = new Vector2(440, 190);
            var bar = UIFactory.Image(box.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Title");
            bar.rectTransform.StretchH(3, 18, 3, 3);
            UIFactory.Label(bar.transform, " " + title, 12, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            var ic = UIFactory.Image(box.transform, RetroTheme.BigIcon(icon ?? "info"), Color.white, "Icon");
            ic.rectTransform.Place(14, 34, 32, 32);
            var msg = UIFactory.Label(box.transform, message, 58, 28, 370, 104, 12, TextAnchor.MiddleLeft);
            msg.verticalOverflow = VerticalWrapMode.Overflow;

            Action cancel = null;
            bool done = false;
            void Close(Action then)
            {
                if (done) return;
                done = true;
                _modalCancels.Remove(cancel);
                if (dim != null) Destroy(dim.gameObject);
                then?.Invoke();
            }
            cancel = () => Close(onNo);
            _modalCancels.Add(cancel);

            bool two = !string.IsNullOrEmpty(noLabel);
            var yes = UIFactory.Button(box.transform, yesLabel, two ? 50 : 140, 146, 160, 28, () => Close(onYes));
            yes.Label.fontStyle = FontStyle.Bold;
            if (two) UIFactory.Button(box.transform, noLabel, 230, 146, 160, 28, () => Close(onNo));
            var enter = dim.gameObject.AddComponent<Screens.KeyAction>();
            enter.Key = KeyCode.Return;
            enter.Action = () => Close(onYes);
            AudioFx.Notify();
        }

        // ---------------- Pause ----------------
        public void OpenPause()
        {
            if (_pause != null) return;
            PopupMenu.CloseAll();
            S.Time.Paused = true;
            var dim = UIFactory.Solid(_modalLayer, new Color(0, 0, 0, 0.5f), "Pause");
            dim.rectTransform.Stretch();
            _pause = dim.gameObject;
            var box = UIFactory.Panel(dim.transform, RetroTheme.Raised, "PauseBox");
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            bool debug = Core.GameSettings.Current.debugMode;
            box.rectTransform.sizeDelta = new Vector2(300, debug ? 264 : 232);
            var bar = UIFactory.Image(box.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Title");
            bar.rectTransform.StretchH(3, 18, 3, 3);
            UIFactory.Label(bar.transform, " 一時停止", 12, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            UIFactory.Label(box.transform, "PAUSED  —  時計は停止しています", 20, 30, 260, 20, 12, TextAnchor.MiddleCenter);
            Action resume = ClosePause;
            Action settings = () => { ClosePause(); SettingsView.Open(this); };
            UIFactory.Button(box.transform, "業務に戻る(R)", 50, 60, 200, 26, resume);
            UIFactory.Button(box.transform, "システム設定(S)", 50, 92, 200, 26, settings);
            // やり直し・タイトルへは、今夜の進み具合が失われるので確認する
            UIFactory.Button(box.transform, $"DAY{S.Day.dayNumber} をやり直す", 50, 124, 200, 26, () =>
                ShowModal("やり直しの確認", $"DAY{S.Day.dayNumber} を最初からやり直します。\n今夜の進み具合は失われます。よろしいですか？", "warning",
                    "やり直す", () => { ClosePause(); Game.RestartDay(); }, "やめる", null));
            UIFactory.Button(box.transform, "タイトルへ戻る", 50, 156, 200, 26, () =>
                ShowModal("タイトルへ戻る", "タイトルへ戻ります。\n今夜の進み具合は失われます。よろしいですか？", "warning",
                    "タイトルへ戻る", () => { ClosePause(); Game.ShowTitle(); }, "やめる", null));
            Action debugPanel = () => { ClosePause(); DebugPanelView.Open(this); };
            if (debug) UIFactory.Button(box.transform, "デバッグ(D)", 50, 188, 200, 26, debugPanel, "debug");
            var pauseKeys = new List<(KeyCode, Action)> { (KeyCode.R, resume), (KeyCode.S, settings) };
            if (debug) pauseKeys.Add((KeyCode.D, debugPanel));
            foreach (var (key, act) in pauseKeys)
            {
                var k = dim.gameObject.AddComponent<Screens.KeyAction>();
                k.Key = key;
                var a = act;
                k.Action = () => { if (!ModalOpen) a(); };
            }
            UIFactory.Label(box.transform, $"{Core.GameSettings.KeyLabel(Core.GameSettings.Pause)} で再開", 20, debug ? 224 : 192, 260, 18, 11, TextAnchor.MiddleCenter).color = RetroTheme.Shadow;
        }

        public void ClosePause()
        {
            if (_pause == null) return;
            Destroy(_pause);
            _pause = null;
            S.Time.Paused = ClockHeld;
        }

        // ---------------- 業務開始前に読むもの ----------------
        public const string BriefingReadId = "MAIL";
        public event Action ReadingChanged;

        public class ReadingItem
        {
            public string Id;
            public string Label;
            public bool Read;
        }

        static bool Available(Data.DocumentDefinition doc, Data.DayDefinition day) =>
            doc != null && day != null && (string.IsNullOrEmpty(doc.requiredFeature) || day.IsUnlocked(doc.requiredFeature));

        static bool PageAvailable(Data.DocumentSection sec, Data.DayDefinition day) =>
            sec != null && day != null && (string.IsNullOrEmpty(sec.requiredFeature) || day.IsUnlocked(sec.requiredFeature));

        /// <summary>今日表示されるページ（業務ごと）の番号</summary>
        public List<int> VisiblePages(Data.DocumentDefinition doc)
        {
            var list = new List<int>();
            if (doc == null) return list;
            for (int i = 0; i < doc.sections.Count; i++) if (PageAvailable(doc.sections[i], S.Day)) list.Add(i);
            return list;
        }

        /// <summary>今日新しく増えたページ（文書自体が今日届いた場合は全ページ）</summary>
        public List<int> NewPages(Data.DocumentDefinition doc)
        {
            var day = S.Day;
            var list = new List<int>();
            if (!Available(doc, day) || !day.attachments.Contains(doc)) return list;
            var yesterday = Game.Database.FindDay(day.dayNumber - 1);
            bool docIsNew = yesterday == null || !Available(doc, yesterday) || !yesterday.attachments.Contains(doc);
            for (int i = 0; i < doc.sections.Count; i++)
            {
                var sec = doc.sections[i];
                if (!PageAvailable(sec, day)) continue;
                if (docIsNew || !PageAvailable(sec, yesterday)) list.Add(i);
            }
            return list;
        }

        static string PageKey(Data.DocumentDefinition doc, int page) => doc.documentId + "#" + page;

        public bool IsNewPage(Data.DocumentDefinition doc, int page) => NewPages(doc).Contains(page);
        public bool IsPageRead(Data.DocumentDefinition doc, int page) => Game.IsRead(S.Day.dayNumber, PageKey(doc, page));

        public void MarkPageRead(Data.DocumentDefinition doc, int page)
        {
            if (IsPageRead(doc, page)) return;
            Game.MarkRead(S.Day.dayNumber, PageKey(doc, page));
            ReadingChanged?.Invoke();
        }

        Data.DocumentDefinition FindDoc(string id)
        {
            foreach (var d in S.Day.attachments) if (d != null && d.documentId == id) return d;
            return null;
        }

        /// <summary>
        /// 業務開始前に読む必要があるもの: 今日の業務連絡メール本文 ＋ マニュアルの「今日新しく増えたページ」。
        /// </summary>
        public List<ReadingItem> RequiredReading()
        {
            var day = S.Day;
            var list = new List<ReadingItem>
            {
                new ReadingItem { Id = BriefingReadId, Label = "業務連絡メール（本文）", Read = Game.IsRead(day.dayNumber, BriefingReadId) }
            };
            foreach (var doc in day.attachments)
            {
                var pages = NewPages(doc);
                if (pages.Count == 0) continue;
                int read = 0;
                foreach (var p in pages) if (IsPageRead(doc, p)) read++;
                list.Add(new ReadingItem
                {
                    Id = doc.documentId,
                    Label = $"{doc.fileName}（新しいページ {read}/{pages.Count}）",
                    Read = read == pages.Count
                });
            }
            return list;
        }

        public bool IsRequiredReading(string id)
        {
            foreach (var r in RequiredReading()) if (r.Id == id) return true;
            return false;
        }

        public bool ReadingComplete
        {
            get { foreach (var r in RequiredReading()) if (!r.Read) return false; return true; }
        }

        /// <summary>メール本文、または文書（新しいページをすべて読んだか）</summary>
        public bool IsRead(string id)
        {
            if (id == BriefingReadId) return Game.IsRead(S.Day.dayNumber, id);
            var doc = FindDoc(id);
            if (doc == null) return true;
            foreach (var p in NewPages(doc)) if (!IsPageRead(doc, p)) return false;
            return true;
        }

        public void MarkRead(string id)
        {
            if (Game.IsRead(S.Day.dayNumber, id)) return;
            Game.MarkRead(S.Day.dayNumber, id);
            ReadingChanged?.Invoke();
        }

        // ---------------- Features ----------------
        public bool Unlocked(string feature) => S.Day.IsUnlocked(feature) || HasTool(feature);

        /// <summary>購入・ツールの有効/無効が変わった</summary>
        public event Action InventoryChanged;
        public void OnInventoryChanged() => InventoryChanged?.Invoke();

        /// <summary>購入したツール・機材が使えるか（ツールはサブモニターが前提）</summary>
        public bool HasTool(string id)
        {
            var c = CareerRecord.Current;
            if (!c.ToolActive(id)) return false;
            if (id == Data.Items.Monitor1 || id == Data.Items.Monitor2) return true;
            var item = Game.Database.FindItem(id);
            return item == null || item.category == Data.StoreCategory.Consumable || c.Owns(Data.Items.Monitor1);
        }

        /// <summary>未解放のシステムは開かせない（メニュー・ボタン・通知・キー操作すべての共通ガード）</summary>
        public bool Require(string feature)
        {
            if (Unlocked(feature)) return true;
            AudioFx.Error();
            MessageBox.Show(this, Data.Features.Label(feature), "このシステムはまだ利用できません。\n（担当範囲が広がると利用できるようになります）", "lock");
            return false;
        }

        // ---------------- Helpers ----------------
        public RetroWindow OpenWindow(WindowSpec spec, out bool created)
        {
            ShowMonitor(0); // メインの業務画面はメインモニターに開く
            return Windows.Open(spec, out created);
        }

        public static List<KeyValuePair<string, List<MenuItem>>> StandardMenus(ShiftDesktop d, RetroWindow self = null, List<MenuItem> extraFile = null, List<MenuItem> view = null)
        {
            var file = new List<MenuItem>();
            if (extraFile != null) { file.AddRange(extraFile); file.Add(MenuItem.Sep()); }
            file.Add(new MenuItem("一時停止(P)", d.OpenPause, Core.GameSettings.KeyLabel(Core.GameSettings.Pause)));
            file.Add(new MenuItem("閉じる(C)", () => d.Windows.Active?.RequestClose(), Core.GameSettings.KeyLabel(Core.GameSettings.CloseWindow)));
            var v = view ?? new List<MenuItem>();
            if (v.Count > 0) v.Add(MenuItem.Sep());
            v.Add(new MenuItem("メインメニュー(M)", () => MainMenuView.Open(d)));
            v.Add(new MenuItem("ネットワークマップ(N)", () => NetworkMonitorView.Open(d), null, d.Unlocked(Data.Features.NetworkMap)));
            // 押せない「編集」や、中身の薄い「ヘルプ」は置かない（使える項目だけ）
            return new List<KeyValuePair<string, List<MenuItem>>>
            {
                new KeyValuePair<string, List<MenuItem>>("ファイル(F)", file),
                new KeyValuePair<string, List<MenuItem>>("表示(V)", v),
            };
        }
    }

    // ============================================================
    public class Taskbar : MonoBehaviour
    {
        public const float Height = 30;
        ShiftDesktop _d;
        RectTransform _buttons;
        Text _clock;
        Text _counts;
        readonly Dictionary<RetroWindow, RetroButton> _map = new Dictionary<RetroWindow, RetroButton>();
        float _flash;
        Image _bell;
        RetroButton _mail;
        bool _mailLatched;

        public static Taskbar Build(Transform parent, ShiftDesktop d)
        {
            var bg = UIFactory.Image(parent, RetroTheme.ThinRaised, Color.white, "Taskbar");
            bg.rectTransform.anchorMin = new Vector2(0, 0); bg.rectTransform.anchorMax = new Vector2(1, 0);
            bg.rectTransform.pivot = new Vector2(0, 0);
            bg.rectTransform.offsetMin = Vector2.zero; bg.rectTransform.offsetMax = new Vector2(0, Height);
            var tb = bg.gameObject.AddComponent<Taskbar>();
            tb._d = d;

            var start = UIFactory.Button(bg.transform, "業務メニュー", null, "network");
            start.Label.fontStyle = FontStyle.Bold;
            start.Rect.Place(3, 3, 112, 24);
            start.Button.onClick.AddListener(() => tb.OpenStartMenu(start));

            var mail = UIFactory.Button(bg.transform, "メール", () =>
            {
                // メールウィンドウのタスクバーボタンを兼ねる（表示中なら最小化）
                var win = d.Windows.Find(MailboxView.Id);
                if (win != null && d.Windows.Active == win && !win.IsMinimized) win.Minimize();
                else MailboxView.Open(d);
            }, "mail");
            tb._mail = mail;
            mail.Rect.Place(118, 3, 92, 24);

            tb._buttons = UIFactory.Rect("Buttons", bg.transform).Stretch(216, 3, 494, 3);
            tb._monitors = UIFactory.Rect("Monitors", bg.transform);
            tb._monitors.AnchorTopRight(328, 3, 160, 24);

            var tray = UIFactory.Image(bg.transform, RetroTheme.ThinSunken, Color.white, "Tray");
            tray.rectTransform.AnchorTopRight(3, 3, 320, 24);
            tb._bell = UIFactory.IconImage(tray.transform, "info", 6, 4);
            tb._counts = UIFactory.Label(tray.transform, "", 11);
            tb._counts.rectTransform.Place(26, 2, 200, 20);
            tb._clock = UIFactory.Label(tray.transform, "00:00", 12, TextAnchor.MiddleRight);
            tb._clock.rectTransform.Stretch(0, 0, 8, 0);
            var trayClick = tray.gameObject.AddComponent<ClickHandler>();
            trayClick.OnClick = () => MainMenuView.Open(d);

            d.Windows.Changed += tb.Rebuild;
            return tb;
        }

        void OnDestroy()
        {
            if (_d != null && _d.Windows != null) _d.Windows.Changed -= Rebuild;
        }

        void OpenStartMenu(RetroButton start)
        {
            var corners = new Vector3[4];
            start.Rect.GetWorldCorners(corners);
            PopupMenu.Show(MainMenuView.Items(_d), RectTransformUtility.WorldToScreenPoint(null, corners[1]), null, 220, null, true);
        }

        void Rebuild()
        {
            foreach (Transform t in _buttons) Destroy(t.gameObject);
            _map.Clear();
            // ウィンドウが多いときは、ボタンを細くして並びきるようにする（あふれて他と重ならない）
            int count = 0;
            foreach (var w in _d.Windows.Windows) if (w != null && w.ShowInTaskbar) count++;
            float avail = Mathf.Max(200f, _buttons.rect.width);
            float bw = count == 0 ? 150 : Mathf.Clamp((avail - 2 * count) / count, 40f, 150f);
            float x = 0;
            foreach (var w in _d.Windows.Windows)
            {
                if (w == null || !w.ShowInTaskbar) continue;
                var win = w;
                var b = UIFactory.Button(_buttons, w.Title, () =>
                {
                    if (_d.Windows.Active == win && !win.IsMinimized) win.Minimize();
                    else _d.Windows.Focus(win);
                }, w.IconName);
                b.Label.alignment = TextAnchor.MiddleLeft;
                EllipsisText.Attach(b.Label);
                b.Rect.Place(x, 0, bw, 24);
                b.SetLatched(_d.Windows.Active == w && !w.IsMinimized);
                _map[w] = b;
                x += bw + 2;
            }
        }

        RectTransform _monitors;

        public void RefreshMonitors()
        {
            if (_monitors == null) return;
            foreach (Transform t in _monitors) Destroy(t.gameObject);
            MonitorSwitcher.Build(_monitors, _d);
        }

        public void Flash() => _flash = 3f;

        public void Tick()
        {
            var s = _d.S;
            _clock.text = $"{_d.Game.Database.GetDay(s.Day.dayNumber).dateLabel.Split(' ')[0]}  {s.Time.Now}";
            _counts.text = _d.Unlocked(Data.Features.HelpDesk)
                ? $"接続要求 {s.Requests.Pending.Count}   受付 {s.HelpDesk.OpenCount}"
                : $"接続要求 {s.Requests.Pending.Count}";
            int unread = s.Mail.UnreadCount;
            if (_mail != null)
            {
                _mail.SetText(unread > 0 ? $"メール ({unread})" : "メール");
                var mw = _d.Windows.Find(MailboxView.Id);
                bool latched = mw != null && _d.Windows.Active == mw && !mw.IsMinimized;
                if (latched != _mailLatched) { _mailLatched = latched; _mail.SetLatched(latched); }
            }
            if (_flash > 0)
            {
                _flash -= Time.unscaledDeltaTime;
                _bell.enabled = _flash <= 0 || Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.3f;
            }
        }
    }

    // ============================================================
    /// <summary>画面右下のバルーン通知。クリックで該当システムへ。</summary>
    public class NotificationToaster : MonoBehaviour
    {
        ShiftDesktop _d;
        RectTransform _root;
        readonly List<Toast> _toasts = new List<Toast>();

        class Toast { public RectTransform Rt; public float Life; public NotificationType Type; public int Count = 1; public Text Body; public Notification Last; }
        public const int MaxToasts = 3;

        public static NotificationToaster Build(Transform parent, ShiftDesktop d)
        {
            var rt = UIFactory.Rect("Toasts", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-6, Taskbar.Height + 6);
            rt.sizeDelta = new Vector2(290, 400);
            var t = rt.gameObject.AddComponent<NotificationToaster>();
            t._d = d; t._root = rt;
            return t;
        }

        public void Show(Notification n)
        {
            // 新しい接続要求は1枚にまとめる（一覧は接続監視で見る）
            if (n.Type == NotificationType.ConnectionRequest)
            {
                var same = _toasts.Find(x => x.Type == NotificationType.ConnectionRequest);
                if (same != null && same.Body != null)
                {
                    same.Count++;
                    same.Last = n;
                    same.Body.text = $"新しい接続要求が {same.Count} 件あります。\n（最新: {n.Body}）";
                    same.Life = Core.GameSettings.Current.notificationSeconds;
                    return;
                }
            }
            var panel = UIFactory.Panel(_root, RetroTheme.Raised, "Toast");
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(290, 66);
            bool alert = n.Type == NotificationType.SecurityAlert;
            var bar = UIFactory.Image(panel.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Bar");
            bar.rectTransform.StretchH(3, 16, 3, 3);
            UIFactory.Label(bar.transform, " " + n.Title + "  " + TimeManager.Format(n.GameTime), 11, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            string icon = alert ? "warning" : n.Type == NotificationType.HelpDeskTicket ? "helpdesk" : n.Type == NotificationType.ConnectionRequest ? "network" : "info";
            var ic = UIFactory.Image(panel.transform, RetroTheme.BigIcon(icon), Color.white, "Icon");
            ic.rectTransform.Place(8, 24, 32, 32);
            var body = UIFactory.Label(panel.transform, n.Body, 12, TextAnchor.UpperLeft);
            body.rectTransform.Stretch(46, 23, 6, 4);
            var ch = panel.gameObject.AddComponent<ClickHandler>();
            float life = Core.GameSettings.Current.notificationSeconds;
            var toast = new Toast { Rt = rt, Life = alert ? life + 6f : life, Type = n.Type, Body = body, Last = n };
            ch.OnClick = () =>
            {
                toast.Last?.OnClick?.Invoke();
                Remove(toast);
            };
            _toasts.Insert(0, toast);
            while (_toasts.Count > MaxToasts) Remove(_toasts[_toasts.Count - 1]);
            Layout();
        }

        void Remove(Toast t)
        {
            if (!_toasts.Remove(t)) return;
            if (t.Rt != null) Destroy(t.Rt.gameObject);
            Layout();
        }

        void Layout()
        {
            float y = 0;
            foreach (var t in _toasts)
            {
                t.Rt.anchoredPosition = new Vector2(0, y);
                y += 70;
            }
        }

        public void Tick()
        {
            if (_d.S.Time.Paused) return;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                _toasts[i].Life -= Time.unscaledDeltaTime;
                if (_toasts[i].Life <= 0) Remove(_toasts[i]);
            }
        }
    }
}
