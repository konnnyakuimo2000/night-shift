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
        DebugOverlay _overlay;
        RetroWindow _completeWindow;

        public static ShiftDesktop Build(RectTransform parent, GameManager gm, ShiftSession s)
        {
            var root = UIFactory.Solid(parent, RetroTheme.Desktop, "ShiftDesktop");
            root.rectTransform.Stretch();
            var d = root.gameObject.AddComponent<ShiftDesktop>();
            d.Game = gm;
            d.S = s;

            // 壁紙のロゴ
            var logo = UIFactory.Label(root.transform, $"{gm.Database.companyName}\n{gm.Database.systemName}", 26, TextAnchor.LowerRight, new Color(1, 1, 1, 0.08f));
            logo.rectTransform.Stretch(0, 0, 30, 50);
            logo.fontStyle = FontStyle.Bold;

            // モニターに貼った付箋（ウィンドウの下になる）
            d._noteLayer = UIFactory.Rect("NoteLayer", root.transform).Stretch(0, 0, 0, Taskbar.Height);
            d.RefreshStickyNote();

            d.Area = UIFactory.Rect("WindowArea", root.transform).Stretch(0, 0, 0, Taskbar.Height);
            d.Windows = WindowManager.Create(d.Area);
            d._taskbar = Taskbar.Build(root.transform, d);
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
            StickyNote.Create(_noteLayer, Game.Account, 24, 24, Game.Account.Changed ? -4 : 3);
        }

        // ---------------- Flow ----------------
        public void ShowBriefing(Action onStart)
        {
            MailboxView.Open(this, () =>
            {
                onStart();
            });
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
            UIFactory.Label(box.transform, "04:00  業務終了。お疲れさまでした。\n業務記録を集計しています...", 12, TextAnchor.MiddleCenter).rectTransform.StretchH(88, 44);
        }

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

        void OnShiftEnded() => ShowStatus("04:00 業務終了。新規の受付を停止しました。");

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
            bool debug = Core.GameSettings.Current.debugMode;
            if (debug && keys.Contains(Shortcut.ToggleDebug)) DebugPanelView.Open(this);
            if (keys.Contains(Shortcut.OpenMail) && _pause == null) MailboxView.Open(this);
            if (debug && keys.Contains(Shortcut.ToggleOverlay)) ToggleOverlay();

            if (_pause != null)
            {
                if (keys.Contains(Shortcut.Escape)) ClosePause();
                return;
            }

            // Remote Support がアクティブなら、キー入力はリモートPCへ
            var active = Windows.Active;
            var remote = active != null ? active.GetComponent<RemoteSupportView>() : null;
            if (remote != null && remote.HasKeyboard)
            {
                remote.HandleShortcuts(keys);
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
            box.rectTransform.sizeDelta = new Vector2(300, 232);
            var bar = UIFactory.Image(box.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Title");
            bar.rectTransform.StretchH(3, 18, 3, 3);
            UIFactory.Label(bar.transform, " 一時停止", 12, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            UIFactory.Label(box.transform, "PAUSED  —  時計は停止しています", 20, 30, 260, 20, 12, TextAnchor.MiddleCenter);
            UIFactory.Button(box.transform, "業務に戻る(R)", 50, 60, 200, 26, ClosePause);
            UIFactory.Button(box.transform, "システム設定(S)", 50, 92, 200, 26, () => { ClosePause(); SettingsView.Open(this); });
            UIFactory.Button(box.transform, $"DAY{S.Day.dayNumber} をやり直す", 50, 124, 200, 26, () => { ClosePause(); Game.RestartDay(); });
            UIFactory.Button(box.transform, "タイトルへ戻る", 50, 156, 200, 26, () => { ClosePause(); Game.ShowTitle(); });
            UIFactory.Label(box.transform, $"{Core.GameSettings.KeyLabel(Core.GameSettings.Pause)} で再開", 20, 192, 260, 18, 11, TextAnchor.MiddleCenter).color = RetroTheme.Shadow;
        }

        public void ClosePause()
        {
            if (_pause == null) return;
            Destroy(_pause);
            _pause = null;
            S.Time.Paused = false;
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

        /// <summary>
        /// 業務開始前に読む必要があるもの: 今日の業務連絡メール本文 ＋ 今日新しく届いた／章が追加されたマニュアル。
        /// </summary>
        public List<ReadingItem> RequiredReading()
        {
            var day = S.Day;
            var list = new List<ReadingItem>
            {
                new ReadingItem { Id = BriefingReadId, Label = "業務連絡メール（本文）", Read = Game.IsRead(day.dayNumber, BriefingReadId) }
            };
            var yesterday = Game.Database.FindDay(day.dayNumber - 1);
            foreach (var doc in day.attachments)
            {
                if (!Available(doc, day)) continue;
                bool updated = yesterday == null || !Available(doc, yesterday) || !yesterday.attachments.Contains(doc);
                if (!updated)
                    foreach (var sec in doc.sections)
                        if (sec != null && !string.IsNullOrEmpty(sec.requiredFeature) && day.IsUnlocked(sec.requiredFeature) && !yesterday.IsUnlocked(sec.requiredFeature))
                        { updated = true; break; }
                if (!updated) continue;
                list.Add(new ReadingItem { Id = doc.documentId, Label = doc.fileName, Read = Game.IsRead(day.dayNumber, doc.documentId) });
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

        public bool IsRead(string id) => Game.IsRead(S.Day.dayNumber, id);

        public void MarkRead(string id)
        {
            if (Game.IsRead(S.Day.dayNumber, id)) return;
            Game.MarkRead(S.Day.dayNumber, id);
            ReadingChanged?.Invoke();
        }

        // ---------------- Features ----------------
        public bool Unlocked(string feature) => S.Day.IsUnlocked(feature);

        /// <summary>未解放のシステムは開かせない（メニュー・ボタン・通知・キー操作すべての共通ガード）</summary>
        public bool Require(string feature)
        {
            if (Unlocked(feature)) return true;
            AudioFx.Error();
            MessageBox.Show(this, Data.Features.Label(feature), "このシステムはまだ利用できません。\n（担当範囲が広がると利用できるようになります）", "lock");
            return false;
        }

        // ---------------- Helpers ----------------
        public RetroWindow OpenWindow(WindowSpec spec, out bool created) => Windows.Open(spec, out created);

        public static List<KeyValuePair<string, List<MenuItem>>> StandardMenus(ShiftDesktop d, RetroWindow self = null, List<MenuItem> extraFile = null, List<MenuItem> view = null)
        {
            var file = new List<MenuItem>();
            if (extraFile != null) { file.AddRange(extraFile); file.Add(MenuItem.Sep()); }
            file.Add(new MenuItem("一時停止(P)", d.OpenPause, "Esc"));
            file.Add(new MenuItem("閉じる(C)", () => d.Windows.Active?.RequestClose(), "Alt+F4"));
            var edit = new List<MenuItem>
            {
                new MenuItem("コピー(C)", null, "Ctrl+C", false),
                new MenuItem("貼り付け(P)", null, "Ctrl+V", false),
            };
            var v = view ?? new List<MenuItem>();
            if (v.Count > 0) v.Add(MenuItem.Sep());
            v.Add(new MenuItem("メインメニュー(M)", () => MainMenuView.Open(d)));
            v.Add(new MenuItem("ネットワークマップ(N)", () => NetworkMonitorView.Open(d), null, d.Unlocked(Data.Features.NetworkMap)));
            var help = new List<MenuItem>
            {
                new MenuItem("メール（業務連絡）(B)", () => MailboxView.Open(d)),
                new MenuItem("バージョン情報(A)", () => d.ShowStatus($"{d.Game.Database.systemName} {d.Game.Database.systemVersion}")),
            };
            return new List<KeyValuePair<string, List<MenuItem>>>
            {
                new KeyValuePair<string, List<MenuItem>>("ファイル(F)", file),
                new KeyValuePair<string, List<MenuItem>>("編集(E)", edit),
                new KeyValuePair<string, List<MenuItem>>("表示(V)", v),
                new KeyValuePair<string, List<MenuItem>>("ヘルプ(H)", help),
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

            var mail = UIFactory.Button(bg.transform, "メール", () => MailboxView.Open(d), "mail");
            tb._mail = mail;
            mail.Rect.Place(118, 3, 92, 24);

            tb._buttons = UIFactory.Rect("Buttons", bg.transform).Stretch(216, 3, 330, 3);

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
                b.Rect.Place(x, 0, 150, 24);
                b.SetLatched(_d.Windows.Active == w && !w.IsMinimized);
                _map[w] = b;
                x += 152;
            }
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
            if (_mail != null) _mail.SetText(unread > 0 ? $"メール ({unread})" : "メール");
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

        class Toast { public RectTransform Rt; public float Life; }

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
            var toast = new Toast { Rt = rt, Life = alert ? life + 6f : life };
            ch.OnClick = () =>
            {
                n.OnClick?.Invoke();
                Remove(toast);
            };
            _toasts.Insert(0, toast);
            while (_toasts.Count > 5) Remove(_toasts[_toasts.Count - 1]);
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
