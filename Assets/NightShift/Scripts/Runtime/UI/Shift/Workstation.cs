using System.Collections.Generic;
using NightShift.Core;
using NightShift.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// サブモニター（担当者用ワークステーション）。社員PCと同じ Windows 風の画面で、買ったツールをアプリとして起動する。
    /// ツールは起動している間だけ効果がある。見るモニターは Ctrl+←/→・Ctrl+1〜3・タスクバーの □■ で切り替える。
    /// </summary>
    public class WorkstationScreen : MonoBehaviour
    {
        public ShiftDesktop Desktop { get; private set; }
        public WindowManager Windows { get; private set; }
        public int Index { get; private set; }
        RectTransform _area, _icons, _taskButtons, _switcher;
        Text _clock;

        public static WorkstationScreen Build(Transform parent, ShiftDesktop d, int index)
        {
            var bg = UIFactory.Solid(parent, new Color(0.13f, 0.36f, 0.42f), "Workstation" + index);
            bg.rectTransform.Stretch();
            var ws = bg.gameObject.AddComponent<WorkstationScreen>();
            ws.Desktop = d;
            ws.Index = index;
            var wall = UIFactory.Label(bg.transform, $"NEXUS Workstation 2000\n担当者用ワークステーション  モニター {index + 1}", 18, TextAnchor.LowerRight, new Color(1, 1, 1, 0.18f));
            wall.rectTransform.Stretch(0, 0, 16, Taskbar.Height + 10);
            ws._area = UIFactory.Rect("Area", bg.transform).Stretch(0, 0, 0, Taskbar.Height);
            ws._icons = UIFactory.Rect("Icons", ws._area).Stretch();
            ws.Windows = WindowManager.Create(ws._area);
            ws.BuildTaskbar(bg.transform);
            ws.Windows.Changed += ws.RebuildTasks;
            ws.RebuildIcons();
            return ws;
        }

        // ---------------- アイコン ----------------
        public void RebuildIcons()
        {
            foreach (Transform t in _icons) Destroy(t.gameObject);
            float x = 10, y = 10;
            foreach (var it in Desktop.Game.Database.storeItems)
            {
                if (it == null || it.category == StoreCategory.Hardware || it.category == StoreCategory.Consumable) continue;
                if (!CareerRecord.Current.Owns(it.itemId)) continue;
                var item = it;
                var root = UIFactory.Rect("Icon:" + it.itemId, _icons).Place(x, y, 86, 66);
                var hit = root.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                var ic = UIFactory.Image(root, RetroTheme.BigIcon(it.icon), Color.white, "Icon");
                ic.raycastTarget = false;
                ic.rectTransform.Place(27, 2, 32, 32);
                var label = UIFactory.Label(root, it.displayName, 11, TextAnchor.UpperCenter, Color.white);
                label.rectTransform.Place(0, 36, 86, 30);
                var ch = root.gameObject.AddComponent<ClickHandler>();
                ch.OnDoubleClick = () => { AudioFx.Click(); Tools.Launch(this, item.itemId); };
                y += 72;
                if (y > 520) { y = 10; x += 92; }
            }

            // 前任者の付箋（ワークステーションの裏に貼られていた）— 手がかり1
            if (Index == 1)
            {
                var note = UIFactory.Image(_icons, RetroTheme.White, new Color(1f, 0.93f, 0.45f), "PredecessorNote");
                note.rectTransform.anchorMin = note.rectTransform.anchorMax = new Vector2(1, 1);
                note.rectTransform.pivot = new Vector2(1, 1);
                note.rectTransform.sizeDelta = new Vector2(130, 90);
                note.rectTransform.anchoredPosition = new Vector2(-24, -20);
                note.rectTransform.localRotation = Quaternion.Euler(0, 0, -4f);
                var t = UIFactory.Label(note.transform, "夜は長い。\n全部を一度に\n見ようとしないこと", 12, TextAnchor.MiddleCenter, new Color(0.15f, 0.15f, 0.3f));
                t.rectTransform.Stretch(6, 6, 6, 6);
                var ch2 = note.gameObject.AddComponent<ClickHandler>();
                ch2.OnClick = () => Desktop.ShowClue("C1",
                    "ワークステーションの裏に貼られていた、前任者の付箋。\n「夜は長い。全部を一度に見ようとしないこと」\n「このPCのパスワードは付箋に書かないこと」\n— E-0004");
            }
        }

        // ---------------- タスクバー ----------------
        void BuildTaskbar(Transform parent)
        {
            var bar = UIFactory.Image(parent, RetroTheme.ThinRaised, Color.white, "Taskbar");
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.pivot = new Vector2(0, 0);
            bar.rectTransform.offsetMin = Vector2.zero; bar.rectTransform.offsetMax = new Vector2(0, Taskbar.Height);
            var start = UIFactory.Button(bar.transform, "スタート", ToggleStart, "start");
            start.Label.fontStyle = FontStyle.Bold;
            start.Rect.Place(3, 3, 84, 24);
            // サブモニターからも一時停止できるように（Esc でも可）
            var pause = UIFactory.Button(bar.transform, "一時停止", () => Desktop.OpenPause(), "clock");
            pause.Rect.Place(90, 3, 84, 24);
            _taskButtons = UIFactory.Rect("Tasks", bar.transform).Stretch(178, 3, 300, 3);
            _switcher = UIFactory.Rect("Monitors", bar.transform);
            _switcher.AnchorTopRight(150, 3, 140, 24);
            var tray = UIFactory.Image(bar.transform, RetroTheme.ThinSunken, Color.white, "Tray");
            tray.rectTransform.AnchorTopRight(3, 3, 140, 24);
            _clock = UIFactory.Label(tray.transform, "", 12, TextAnchor.MiddleRight);
            _clock.rectTransform.Stretch(0, 0, 8, 0);
            MonitorSwitcher.Build(_switcher, Desktop);
        }

        void ToggleStart()
        {
            if (PopupMenu.IsOpen) { PopupMenu.CloseAll(); return; }
            var items = new List<MenuItem>();
            foreach (var it in Desktop.Game.Database.storeItems)
            {
                if (it == null || it.category == StoreCategory.Hardware || it.category == StoreCategory.Consumable) continue;
                if (!CareerRecord.Current.Owns(it.itemId)) continue;
                var id = it.itemId;
                items.Add(new MenuItem(it.displayName, () => Tools.Launch(this, id)) { Icon = it.icon });
            }
            if (items.Count == 0) items.Add(new MenuItem("（ツールがありません）", null, null, false));
            items.Add(MenuItem.Sep());
            items.Add(new MenuItem("メインモニターへ (Ctrl+1)", () => Desktop.ShowMonitor(0)) { Icon = "pc" });
            PopupMenu.Show(items, Input.mousePosition, null, 240, null, true);
        }

        void RebuildTasks()
        {
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
                b.Rect.Place(x, 0, 140, 24);
                b.SetLatched(Windows.Active == w && !w.IsMinimized);
                x += 142;
            }
        }

        public void RefreshSwitcher()
        {
            foreach (Transform t in _switcher) Destroy(t.gameObject);
            MonitorSwitcher.Build(_switcher, Desktop);
        }

        void Update()
        {
            if (_clock != null && Desktop != null && Desktop.S != null) _clock.text = Desktop.S.Time.Now;
        }
    }

    /// <summary>タスクバーのモニター切り替え（□■□）。見ていないモニターに通知があると点滅する。</summary>
    public static class MonitorSwitcher
    {
        public static void Build(RectTransform parent, ShiftDesktop d)
        {
            int n = d.MonitorCount;
            if (n <= 1) return;
            float x = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                bool current = d.CurrentMonitor == i;
                string label = i == 0 ? (current ? "■メイン" : "□メイン") : (current ? $"■{i + 1}" : $"□{i + 1}");
                var b = UIFactory.Button(parent, label, () => d.ShowMonitor(idx));
                b.Rect.Place(x, 0, i == 0 ? 62 : 36, 24);
                b.Label.fontSize = 11;
                b.SetLatched(current);
                if (i == 0 && !current && d.MainHasUnseen) b.Label.color = new Color(0.8f, 0, 0);
                x += i == 0 ? 64 : 38;
            }
        }
    }
}
