using System;
using System.Collections.Generic;
using NightShift.Controls;
using NightShift.Core;
using NightShift.RemotePC;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Remote
{
    /// <summary>リモートPCのアプリ共通インターフェース（F5 で再表示）</summary>
    public interface IRemoteApp
    {
        void Refresh();
    }

    /// <summary>
    /// 簡略化した実物PCのデスクトップ。専用ミニゲームではなく、
    /// Desktop / Start Menu / 各アプリから VirtualPCState を調査・操作する。
    /// </summary>
    public class RemoteDesktop : MonoBehaviour
    {
        public VirtualPCState PC { get; private set; }
        public ShiftSession Session { get; private set; }
        public WindowManager Windows { get; private set; }
        public System.Random Rng { get; private set; }

        RectTransform _area;
        RectTransform _taskButtons;
        Text _clock;
        Image _netIcon;
        Image _printerIcon;
        RetroButton _start;
        public const float TaskbarHeight = 28;

        public static RemoteDesktop Build(RectTransform parent, ShiftSession session, VirtualPCState pc)
        {
            var bg = UIFactory.Solid(parent, RetroTheme.RemoteDesktop, "RemoteDesktop");
            bg.rectTransform.Stretch();
            bg.gameObject.AddComponent<RectMask2D>();
            var d = bg.gameObject.AddComponent<RemoteDesktop>();
            d.PC = pc;
            d.Session = session;
            d.Rng = new System.Random(pc.Hostname.GetHashCode());

            var wallText = UIFactory.Label(bg.transform, $"{pc.OsName}\n{pc.Hostname}", 18, TextAnchor.LowerRight, new Color(1, 1, 1, 0.18f));
            wallText.rectTransform.Stretch(0, 0, 16, TaskbarHeight + 10);

            d._area = UIFactory.Rect("Area", bg.transform).Stretch(0, 0, 0, TaskbarHeight);
            d.BuildIcons();
            d.Windows = WindowManager.Create(d._area);
            d.BuildTaskbar(bg.transform);
            d.Windows.Changed += d.RebuildTaskButtons;
            pc.Changed += d.OnPcChanged;
            d.OnPcChanged();
            return d;
        }

        void OnDestroy()
        {
            if (PC != null) PC.Changed -= OnPcChanged;
        }

        // ---------------- Desktop icons ----------------
        void BuildIcons()
        {
            var icons = new (string label, string icon, Action open)[]
            {
                ("マイ コンピュータ", "pc", () => RemoteApps.Explorer(this, "マイ コンピュータ")),
                ("マイ ドキュメント", "folder", () => RemoteApps.Explorer(this, "マイ ドキュメント")),
                ("ブラウザー", "globe", () => RemoteApps.Browser(this)),
                ("プリンター", "printer", () => RemoteApps.Settings(this, 1)),
                ("コマンド プロンプト", "terminal", () => RemoteApps.Terminal(this)),
            };
            float y = 10;
            foreach (var it in icons)
            {
                var root = UIFactory.Rect("DesktopIcon", _area).Place(10, y, 76, 62);
                var hit = root.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                var ic = UIFactory.Image(root, RetroTheme.BigIcon(it.icon), Color.white, "Icon");
                ic.raycastTarget = false;
                ic.rectTransform.Place(22, 2, 32, 32);
                var t = UIFactory.Label(root, it.label, 11, TextAnchor.UpperCenter, Color.white);
                t.rectTransform.Place(0, 36, 76, 26);
                var ch = root.gameObject.AddComponent<ClickHandler>();
                var open = it.open;
                ch.OnDoubleClick = () => { AudioFx.Click(); open(); };
                y += 68;
            }
        }

        // ---------------- Taskbar ----------------
        void BuildTaskbar(Transform parent)
        {
            var bar = UIFactory.Image(parent, RetroTheme.ThinRaised, Color.white, "RemoteTaskbar");
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.pivot = new Vector2(0, 0);
            bar.rectTransform.offsetMin = Vector2.zero; bar.rectTransform.offsetMax = new Vector2(0, TaskbarHeight);

            _start = UIFactory.Button(bar.transform, "スタート", ToggleStartMenu, "start");
            _start.Label.fontStyle = FontStyle.Bold;
            _start.Rect.Place(2, 3, 84, 22);

            _taskButtons = UIFactory.Rect("Tasks", bar.transform).Stretch(92, 3, 150, 3);

            var tray = UIFactory.Image(bar.transform, RetroTheme.ThinSunken, Color.white, "Tray");
            tray.rectTransform.AnchorTopRight(2, 3, 140, 22);
            _netIcon = UIFactory.IconImage(tray.transform, "wifi", 4, 3);
            _netIcon.raycastTarget = true;
            var nc = _netIcon.gameObject.AddComponent<ClickHandler>();
            nc.OnClick = () => RemoteApps.Settings(this, 0);
            nc.OnDoubleClick = () => RemoteApps.Settings(this, 0);
            _printerIcon = UIFactory.IconImage(tray.transform, "printer", 24, 3);
            _printerIcon.raycastTarget = true;
            var pc = _printerIcon.gameObject.AddComponent<ClickHandler>();
            pc.OnDoubleClick = () => RemoteApps.PrintQueue(this);
            pc.OnClick = () => RemoteApps.PrintQueue(this);
            _clock = UIFactory.Label(tray.transform, "", 12, TextAnchor.MiddleRight);
            _clock.rectTransform.Stretch(0, 0, 6, 0);
        }

        void RebuildTaskButtons()
        {
            if (_taskButtons == null) return;
            foreach (Transform t in _taskButtons) Destroy(t.gameObject);
            float x = 0;
            foreach (var w in Windows.Windows)
            {
                if (w == null || !w.ShowInTaskbar) continue;
                var win = w;
                var b = UIFactory.Button(_taskButtons, w.Title, () =>
                {
                    if (Windows.Active == win && !win.IsMinimized) win.Minimize();
                    else Windows.Focus(win);
                }, w.IconName);
                b.Label.alignment = TextAnchor.MiddleLeft;
                b.Rect.Place(x, 0, 130, 22);
                b.SetLatched(Windows.Active == w && !w.IsMinimized);
                x += 132;
            }
        }

        void OnPcChanged()
        {
            if (_netIcon != null) _netIcon.sprite = RetroTheme.Icon(PC.InternetConnectivity ? "wifi" : "wifi_off");
            if (_printerIcon != null) _printerIcon.enabled = PC.PrintQueue.Count > 0;
        }

        void Update()
        {
            if (Session != null && _clock != null) _clock.text = Session.Time.Now;
        }

        // ---------------- Start menu ----------------
        public void ToggleStartMenu()
        {
            if (PopupMenu.IsOpen) { PopupMenu.CloseAll(); return; }
            var items = new List<MenuItem>
            {
                new MenuItem("プログラム(P)", null) { Icon = "folder", Children = new List<MenuItem>
                {
                    new MenuItem("ブラウザー", () => RemoteApps.Browser(this)) { Icon = "globe" },
                    new MenuItem("コマンド プロンプト", () => RemoteApps.Terminal(this)) { Icon = "terminal" },
                    new MenuItem("エクスプローラー", () => RemoteApps.Explorer(this, "マイ コンピュータ")) { Icon = "folder" },
                    new MenuItem("タスク マネージャー", () => RemoteApps.TaskManager(this)) { Icon = "taskmgr" },
                } },
                new MenuItem("マイ ドキュメント(D)", () => RemoteApps.Explorer(this, "マイ ドキュメント")) { Icon = "folder" },
                new MenuItem("設定(S)", () => RemoteApps.Settings(this, 0)) { Icon = "gear" },
                new MenuItem("プリンター(I)", () => RemoteApps.Settings(this, 1)) { Icon = "printer" },
                new MenuItem("ネットワーク接続(N)", () => RemoteApps.NetworkConnections(this)) { Icon = "network" },
                MenuItem.Sep(),
                new MenuItem("ファイル名を指定して実行(R)...", () => RemoteApps.Run(this)) { Icon = "run" },
                MenuItem.Sep(),
                new MenuItem("シャットダウン(U)...", null, null, false) { Icon = "exit" },
            };
            var corners = new Vector3[4];
            _start.Rect.GetWorldCorners(corners);
            PopupMenu.Show(items, RectTransformUtility.WorldToScreenPoint(null, corners[1]), null, 200, null, true);
        }

        // ---------------- Shortcuts ----------------
        public void HandleShortcuts(List<Shortcut> keys)
        {
            foreach (var k in keys)
            {
                switch (k)
                {
                    case Shortcut.StartMenu: ToggleStartMenu(); break;
                    case Shortcut.Run: PopupMenu.CloseAll(); RemoteApps.Run(this); break;
                    case Shortcut.Explorer: PopupMenu.CloseAll(); RemoteApps.Explorer(this, "マイ コンピュータ"); break;
                    case Shortcut.Settings: PopupMenu.CloseAll(); RemoteApps.Settings(this, 0); break;
                    case Shortcut.TaskManager: PopupMenu.CloseAll(); RemoteApps.TaskManager(this); break;
                    case Shortcut.ShowDesktop: Windows.MinimizeAll(); break;
                    case Shortcut.SwitchNext: Windows.SwitchNext(false, () => ShortcutInput.SwitchModifierHeld); break;
                    case Shortcut.SwitchPrev: Windows.SwitchNext(true, () => ShortcutInput.SwitchModifierHeld); break;
                    case Shortcut.CloseWindow: if (Windows.Active != null) Windows.Active.RequestClose(); break;
                    case Shortcut.Escape: if (PopupMenu.IsOpen) PopupMenu.CloseAll(); break;
                    case Shortcut.Refresh:
                        var app = Windows.Active != null ? Windows.Active.GetComponent<IRemoteApp>() : null;
                        app?.Refresh();
                        break;
                }
            }

            // アクティブなリモートWindowのアクセラレータ
            var active = Windows.Active;
            if (active != null && !PopupMenu.IsOpen)
            {
                bool text = ShortcutInput.TextFieldFocused;
                foreach (var kv in active.Accelerators)
                {
                    if (!Input.GetKeyDown(kv.Key)) continue;
                    bool isLetter = (kv.Key >= KeyCode.A && kv.Key <= KeyCode.Z) || (kv.Key >= KeyCode.Alpha0 && kv.Key <= KeyCode.Alpha9);
                    if (isLetter && (ShortcutInput.Ctrl || (text && !ShortcutInput.Alt))) continue;
                    if (isLetter && !ShortcutInput.Alt && !Core.GameSettings.Current.letterAccelerators) continue;
                    if (!isLetter && text && kv.Key != KeyCode.Return && kv.Key != KeyCode.KeypadEnter) continue;
                    kv.Value?.Invoke();
                    break;
                }
            }
        }

        // ---------------- Helpers ----------------
        public RetroWindow Open(WindowSpec spec, out bool created, string processName = null)
        {
            var w = Windows.Open(spec, out created);
            if (created && !string.IsNullOrEmpty(processName))
            {
                PC.StartProcess(processName);
                w.Closed += () => PC.StopProcessByName(processName);
            }
            return w;
        }

        public void Message(string title, string message, string icon = "info")
        {
            var w = Windows.Open(new WindowSpec
            {
                Id = "msg:" + title + message.GetHashCode(), Title = title, Icon = icon, Width = 320, Height = 140,
                Minimizable = false, Maximizable = false, ShowInTaskbar = false, Position = new Vector2(240, 160)
            }, out bool created);
            if (!created) return;
            var c = w.Content;
            var ic = UIFactory.Image(c, RetroTheme.BigIcon(icon), Color.white, "Icon");
            ic.rectTransform.Place(8, 8, 32, 32);
            UIFactory.Label(c, message, 50, 4, 250, 56, 12, TextAnchor.MiddleLeft);
            UIFactory.Button(c, "OK", 112, 70, 80, 22, w.Close);
            w.Accelerators[KeyCode.Return] = w.Close;
            if (icon == "warning") AudioFx.Error();
        }
    }
}
