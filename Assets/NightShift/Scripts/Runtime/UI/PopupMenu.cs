using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift.UI
{
    public class MenuItem
    {
        public string Label;
        public string Shortcut;
        public Action Action;
        public bool Enabled = true;
        public bool Checked;
        public bool Separator;
        public string Icon;
        public List<MenuItem> Children; // サブメニュー

        public static MenuItem Sep() => new MenuItem { Separator = true };
        public MenuItem() { }
        public MenuItem(string label, Action action, string shortcut = null, bool enabled = true)
        {
            Label = label; Action = action; Shortcut = shortcut; Enabled = enabled;
        }
    }

    /// <summary>ドロップダウンメニュー／スタートメニュー等の共通ポップアップ。外側クリックで閉じる。</summary>
    public class PopupMenu : MonoBehaviour
    {
        public static PopupMenu Current;
        public static RectTransform OverlayRoot; // 最前面レイヤー
        RectTransform _panel;
        PopupMenu _child;
        PopupMenu _parentMenu;
        Action _onClosed;

        public static bool IsOpen => Current != null;

        /// <summary>screenPos はスクリーン座標（左上基準に変換して表示）</summary>
        public static PopupMenu Show(List<MenuItem> items, Vector2 screenPos, RectTransform overlay = null, float minWidth = 140, Action onClosed = null, bool openUpwards = false)
        {
            CloseAll();
            var m = Create(items, screenPos, overlay ?? OverlayRoot, minWidth, null, openUpwards);
            m._onClosed = onClosed;
            Current = m;
            return m;
        }

        static PopupMenu Create(List<MenuItem> items, Vector2 screenPos, RectTransform overlay, float minWidth, PopupMenu parent, bool openUpwards)
        {
            var root = UIFactory.Rect("PopupMenu", overlay).Stretch();
            var m = root.gameObject.AddComponent<PopupMenu>();
            m._parentMenu = parent;
            if (parent == null)
            {
                // 外側クリックで閉じるためのブロッカー
                var blocker = root.gameObject.AddComponent<Image>();
                blocker.color = new Color(0, 0, 0, 0);
                var ch = root.gameObject.AddComponent<ClickHandler>();
                ch.OnClick = CloseAll;
                ch.OnRightClick = _ => CloseAll();
            }

            // 幅の計算
            float width = minWidth;
            var probe = UIFactory.Label(root, "");
            foreach (var it in items)
            {
                if (it.Separator) continue;
                probe.text = it.Label + "      " + (it.Shortcut ?? "");
                width = Mathf.Max(width, probe.preferredWidth + 48);
            }
            Destroy(probe.gameObject);

            float height = 4;
            foreach (var it in items) height += it.Separator ? 8 : 20;

            var panel = UIFactory.Image(root, RetroTheme.Raised, Color.white, "Panel");
            m._panel = panel.rectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screenPos, null, out var local);
            // overlay は Stretch なので左上原点に変換
            var size = overlay.rect.size;
            float x = local.x + size.x * overlay.pivot.x;
            float yTop = size.y * (1 - overlay.pivot.y) - local.y;
            if (openUpwards) yTop -= height;
            if (x + width > size.x) x = size.x - width;
            if (yTop + height > size.y) yTop = size.y - height;
            if (yTop < 0) yTop = 0;
            panel.rectTransform.Place(x, yTop, width, height);

            float y = 2;
            foreach (var it in items)
            {
                if (it.Separator)
                {
                    UIFactory.Separator(panel.transform, 3, y + 3, width - 6);
                    y += 8;
                    continue;
                }
                var row = UIFactory.Rect("Item", panel.transform).Place(2, y, width - 4, 20);
                var bg = row.gameObject.AddComponent<Image>();
                bg.sprite = RetroTheme.White;
                bg.color = new Color(0, 0, 0, 0);
                if (!string.IsNullOrEmpty(it.Icon)) UIFactory.IconImage(row, it.Icon, 3, 2);
                if (it.Checked) UIFactory.IconImage(row, "check", 3, 2);
                var t = UIFactory.Label(row, it.Label, RetroTheme.FontSize, TextAnchor.MiddleLeft, it.Enabled ? RetroTheme.Text : RetroTheme.TextDisabled);
                t.rectTransform.Stretch(24, 0, 60, 0);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                Text sc = null;
                if (!string.IsNullOrEmpty(it.Shortcut) || it.Children != null)
                {
                    sc = UIFactory.Label(row, it.Children != null ? "▶" : it.Shortcut, RetroTheme.FontSize, TextAnchor.MiddleRight, it.Enabled ? RetroTheme.Text : RetroTheme.TextDisabled);
                    sc.rectTransform.Stretch(0, 0, 8, 0);
                    sc.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
                var hover = row.gameObject.AddComponent<MenuRowHover>();
                hover.Bg = bg; hover.Label = t; hover.Shortcut = sc; hover.Enabled = it.Enabled;
                var item = it;
                var rowRt = row;
                hover.Entered = () =>
                {
                    if (m._child != null) { m._child.CloseSelf(); m._child = null; }
                    if (item.Children != null && item.Enabled)
                    {
                        var corners = new Vector3[4];
                        rowRt.GetWorldCorners(corners);
                        var sp = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                        m._child = Create(item.Children, sp, overlay, 140, m, false);
                    }
                };
                var ch = row.gameObject.AddComponent<ClickHandler>();
                ch.OnClick = () =>
                {
                    if (!item.Enabled || item.Children != null) return;
                    AudioFx.Click();
                    CloseAll();
                    item.Action?.Invoke();
                };
                y += 20;
            }
            return m;
        }

        void CloseSelf()
        {
            if (_child != null) _child.CloseSelf();
            if (this != null) Destroy(gameObject);
        }

        public static void CloseAll()
        {
            if (Current == null) return;
            var c = Current;
            Current = null;
            var cb = c._onClosed;
            c.CloseSelf();
            cb?.Invoke();
        }
    }

    public class MenuRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Bg;
        public Text Label;
        public Text Shortcut;
        public bool Enabled = true;
        public Action Entered;

        public void OnPointerEnter(PointerEventData e)
        {
            Bg.color = RetroTheme.Selection;
            Label.color = Enabled ? Color.white : RetroTheme.Shadow;
            if (Shortcut) Shortcut.color = Label.color;
            Entered?.Invoke();
        }

        public void OnPointerExit(PointerEventData e)
        {
            Bg.color = new Color(0, 0, 0, 0);
            Label.color = Enabled ? RetroTheme.Text : RetroTheme.TextDisabled;
            if (Shortcut) Shortcut.color = Label.color;
        }
    }

    /// <summary>シンプルなドロップダウン（コンボボックス風）</summary>
    public class RetroDropdown : MonoBehaviour
    {
        public List<string> Options = new List<string>();
        public int Index;
        public Text ValueText;
        public Action<int> Changed;

        public string Value => Index >= 0 && Index < Options.Count ? Options[Index] : "";

        public static RetroDropdown Create(Transform parent, float x, float y, float w, List<string> options, int index = 0, Action<int> changed = null)
        {
            var bg = UIFactory.Image(parent, RetroTheme.Field, Color.white, "Dropdown");
            bg.rectTransform.Place(x, y, w, 20);
            var dd = bg.gameObject.AddComponent<RetroDropdown>();
            dd.Options = options;
            dd.Index = index;
            dd.Changed = changed;
            dd.ValueText = UIFactory.Label(bg.transform, "", RetroTheme.FontSize);
            dd.ValueText.rectTransform.Stretch(4, 2, 20, 2);
            var arrow = UIFactory.Button(bg.transform, "▼", dd.Open);
            arrow.Label.fontSize = 8;
            arrow.Rect.anchorMin = new Vector2(1, 0); arrow.Rect.anchorMax = new Vector2(1, 1);
            arrow.Rect.pivot = new Vector2(1, 0.5f);
            arrow.Rect.offsetMin = new Vector2(-18, 2); arrow.Rect.offsetMax = new Vector2(-2, -2);
            var ch = bg.gameObject.AddComponent<ClickHandler>();
            ch.OnClick = dd.Open;
            dd.Refresh();
            return dd;
        }

        public void SetOptions(List<string> options, int index = 0)
        {
            Options = options;
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));
            Refresh();
        }

        public void Set(int i, bool notify = false)
        {
            Index = i;
            Refresh();
            if (notify) Changed?.Invoke(i);
        }

        void Refresh() => ValueText.text = Value;

        void Open()
        {
            var rt = (RectTransform)transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var sp = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var items = new List<MenuItem>();
            for (int i = 0; i < Options.Count; i++)
            {
                int idx = i;
                items.Add(new MenuItem(Options[i], () => Set(idx, true)) { Checked = i == Index });
            }
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            PopupMenu.Show(items, sp, null, rt.rect.width);
        }
    }
}
