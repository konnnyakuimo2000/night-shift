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
            foreach (var app in ToolApps.All)
            {
                if (!ToolApps.Owns(app)) continue;
                var a = app;
                // 権利の一部だけ持っている場合も、持っている権利のタブで開く
                string launch = CareerRecord.Current.Owns(app.Id) ? app.Id : System.Array.Find(app.Rights, r => CareerRecord.Current.Owns(r));
                var root = UIFactory.Rect("Icon:" + app.Id, _icons).Place(x, y, 86, 66);
                var hit = root.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                var ic = UIFactory.Image(root, RetroTheme.BigIcon(app.Icon), Color.white, "Icon");
                ic.raycastTarget = false;
                ic.rectTransform.Place(27, 2, 32, 32);
                var label = UIFactory.Label(root, app.Name, 11, TextAnchor.UpperCenter, Color.white);
                label.rectTransform.Place(0, 36, 86, 30);
                var ch = root.gameObject.AddComponent<ClickHandler>();
                ch.OnDoubleClick = () => { AudioFx.Click(); Tools.Launch(this, launch); };
                y += 72;
                if (y > 520) { y = 10; x += 92; }
            }

            // 機器保護札（SQ03）: 在庫・貼付中・破れた札
            var career = CareerRecord.Current;
            var charm = career.charm;
            int stock = career.ItemCount(Items.Charm);
            bool posted = charm.postedDay == Desktop.S.Day.dayNumber;
            if (Index == 0 && (stock > 0 || posted || charm.torn))
            {
                var root = UIFactory.Rect("Icon:charm", _icons).Place(x, y, 86, 66);
                var hit = root.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                var ic = UIFactory.Image(root, RetroTheme.BigIcon("shield"), posted ? new Color(1f, 0.95f, 0.8f, charm.consumed ? 0.4f : 1f) : Color.white, "Icon");
                ic.raycastTarget = false;
                ic.rectTransform.Place(27, 2, 32, 32);
                string label = posted ? (charm.consumed ? "札（文字が薄い）" : "札（貼付中）") : charm.torn && stock == 0 ? "破れた札" : $"機器保護札 ×{stock}";
                var lb = UIFactory.Label(root, label, 11, TextAnchor.UpperCenter, Color.white);
                lb.rectTransform.Place(0, 36, 86, 30);
                root.gameObject.AddComponent<ClickHandler>().OnDoubleClick = PostCharm;
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

        /// <summary>機器保護札をモニターの側面に貼る（その夜の次の不調を1回防ぐ。翌朝に必ず破れる。貼り直し不可）</summary>
        void PostCharm()
        {
            var career = CareerRecord.Current;
            var ch = career.charm;
            int day = Desktop.S.Day.dayNumber;
            if (ch.postedDay == day) { MessageBox.Show(Desktop, "機器保護札", ch.consumed ? "札の文字が薄くなっている。今夜はもう効果がない。" : "今夜はもう貼ってある。", "shield"); return; }
            if (career.ItemCount(Items.Charm) <= 0) { MessageBox.Show(Desktop, "機器保護札", "破れた札が残っている。もう使えない。", "shield"); return; }
            Desktop.ShowModal("機器保護札", (ch.torn ? "側面の破片を取り除いて、" : "") + "新しい札をモニターの側面に貼りますか？\n（今夜の次の表示の乱れを1回防ぎます。貼った札は翌朝に破れます）", "warning", "貼る", () =>
            {
                if (!career.UseItem(Items.Charm)) return;
                ch.postedDay = day; ch.consumed = false; ch.torn = false;
                career.Save();
                RebuildIcons();
            }, "やめる", null);
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
            _taskButtons = UIFactory.Rect("Tasks", bar.transform).Stretch(178, 3, 320, 3);
            _switcher = UIFactory.Rect("Monitors", bar.transform);
            _switcher.AnchorTopRight(150, 3, 160, 24);
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
            foreach (var app in ToolApps.All)
            {
                if (!ToolApps.Owns(app)) continue;
                string id = CareerRecord.Current.Owns(app.Id) ? app.Id : System.Array.Find(app.Rights, r => CareerRecord.Current.Owns(r));
                items.Add(new MenuItem(app.Name, () => Tools.Launch(this, id)) { Icon = app.Icon });
            }
            if (items.Count == 0) items.Add(new MenuItem("（ツールがありません）", null, null, false));
            items.Add(MenuItem.Sep());
            items.Add(new MenuItem("メインモニターへ (Ctrl+1)", () => Desktop.ShowMonitor(0)) { Icon = "pc" });
            PopupMenu.Show(items, Input.mousePosition, null, 240, null, true);
        }

        void RebuildTasks()
        {
            foreach (Transform t in _taskButtons) Destroy(t.gameObject);
            int count = 0;
            foreach (var w in Windows.Windows) if (w != null && w.ShowInTaskbar) count++;
            float avail = Mathf.Max(200f, _taskButtons.rect.width);
            float bw = count == 0 ? 140 : Mathf.Clamp((avail - 2 * count) / count, 40f, 140f);
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
                EllipsisText.Attach(b.Label);
                b.Rect.Place(x, 0, bw, 24);
                b.SetLatched(Windows.Active == w && !w.IsMinimized);
                x += bw + 2;
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
                // 見ていないメインモニターに通知があるときは「●」を付ける（色だけに頼らない）
                bool unseen = i == 0 && !current && d.MainHasUnseen;
                string label = i == 0 ? (current ? "■メイン" : unseen ? "□メイン●" : "□メイン") : (current ? $"■{i + 1}" : $"□{i + 1}");
                var b = UIFactory.Button(parent, label, () => d.ShowMonitor(idx));
                b.Rect.Place(x, 0, i == 0 ? 70 : 36, 24);
                b.Label.fontSize = 11;
                b.SetLatched(current);
                if (i == 0 && !current && d.MainHasUnseen) b.Label.color = new Color(0.8f, 0, 0);
                x += i == 0 ? 72 : 38;
            }
        }
    }
}
