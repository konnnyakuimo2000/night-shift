using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift.UI
{
    public class WindowSpec
    {
        public string Id;
        public string Title;
        public string Icon = "pc";
        public float Width = 480;
        public float Height = 360;
        public bool Closable = true;
        public bool Minimizable = true;
        public bool Maximizable = true;
        public bool Resizable;
        public List<KeyValuePair<string, List<MenuItem>>> Menus;
        public int StatusCells;           // 0 = ステータスバーなし
        public float[] StatusWidths;      // 右側セルの幅（先頭セルは可変）
        public Vector2? Position;
        public bool ShowInTaskbar = true;
    }

    /// <summary>灰色のWindow＋濃い青のタイトルバー＋メニューバー＋ステータスバー。</summary>
    public class RetroWindow : MonoBehaviour
    {
        public string Id;
        public string Title { get; private set; }
        public string IconName;
        public RectTransform Rect => (RectTransform)transform;
        public RectTransform Content { get; private set; }
        public WindowManager Manager;
        public bool IsMinimized { get; private set; }
        public bool IsMaximized { get; private set; }
        public bool Closable = true;
        public bool ShowInTaskbar = true;
        public float LastFocusTime;

        public event Action Closed;
        public event Action Focused;
        public event Action<bool> ActiveChanged;
        /// <summary>このWindowがアクティブな時のキー処理。true を返すと消費。</summary>
        public Func<KeyCode, bool, bool, bool> KeyHandler; // key, ctrl, alt
        public readonly Dictionary<KeyCode, Action> Accelerators = new Dictionary<KeyCode, Action>();
        /// <summary>閉じる直前の確認。false で閉じない。</summary>
        public Func<bool> CanClose;

        Image _titleBar;
        Text _titleText;
        readonly List<Text> _statusCells = new List<Text>();
        Vector2 _restorePos, _restoreSize;
        RetroButton _maxButton;

        public static RetroWindow Build(WindowManager mgr, WindowSpec spec)
        {
            var frame = UIFactory.Image(mgr.Container, RetroTheme.Frame, Color.white, "Window:" + spec.Title);
            var w = frame.gameObject.AddComponent<RetroWindow>();
            w.Manager = mgr;
            w.Id = spec.Id;
            w.IconName = spec.Icon;
            w.Closable = spec.Closable;
            w.ShowInTaskbar = spec.ShowInTaskbar;
            var rt = frame.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(spec.Width, spec.Height);

            // タイトルバー
            var tb = UIFactory.Image(frame.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "TitleBar");
            tb.type = Image.Type.Simple;
            tb.rectTransform.StretchH(3, RetroTheme.TitleBarHeight, 3, 3);
            w._titleBar = tb;
            var drag = tb.gameObject.AddComponent<WindowDrag>();
            drag.Window = w;
            var dbl = tb.gameObject.AddComponent<ClickHandler>();
            dbl.OnDoubleClick = () => { if (spec.Maximizable) w.ToggleMaximize(); };

            float iconX = 3;
            if (!string.IsNullOrEmpty(spec.Icon))
            {
                var ic = UIFactory.IconImage(tb.transform, spec.Icon, 2, 1, 16);
                iconX = 21;
            }
            w._titleText = UIFactory.Label(tb.transform, spec.Title, RetroTheme.FontSize, TextAnchor.MiddleLeft, Color.white);
            w._titleText.fontStyle = FontStyle.Bold;
            w._titleText.rectTransform.Stretch(iconX, 0, 60, 0);
            w._titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            w.Title = spec.Title;

            float bx = 2;
            if (spec.Closable)
            {
                var close = TitleButton(tb.transform, "×", () => w.RequestClose());
                close.Rect.AnchorTopRight(bx, 2, 16, 14);
                bx += 18;
            }
            else bx += 0;
            if (spec.Maximizable)
            {
                w._maxButton = TitleButton(tb.transform, "□", w.ToggleMaximize);
                w._maxButton.Rect.AnchorTopRight(bx, 2, 16, 14);
                bx += 16;
            }
            if (spec.Minimizable)
            {
                var min = TitleButton(tb.transform, "_", w.Minimize);
                min.Rect.AnchorTopRight(bx, 2, 16, 14);
            }

            float top = 3 + RetroTheme.TitleBarHeight + 1;

            // メニューバー
            if (spec.Menus != null && spec.Menus.Count > 0)
            {
                var mb = UIFactory.Rect("MenuBar", frame.transform).StretchH(top, RetroTheme.MenuBarHeight, 3, 3);
                float mx = 2;
                foreach (var kv in spec.Menus)
                {
                    var items = kv.Value;
                    var b = UIFactory.FlatButton(mb, kv.Key, null);
                    b.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    float tw = b.Label.preferredWidth + 14;
                    b.Rect.Place(mx, 1, tw, RetroTheme.MenuBarHeight - 2);
                    var bt = b;
                    b.Button.onClick.AddListener(() =>
                    {
                        w.Focus();
                        var corners = new Vector3[4];
                        bt.Rect.GetWorldCorners(corners);
                        PopupMenu.Show(items, RectTransformUtility.WorldToScreenPoint(null, corners[0]));
                    });
                    mx += tw;
                }
                top += RetroTheme.MenuBarHeight + 1;
            }

            float bottom = 3;
            if (spec.StatusCells > 0)
            {
                var sb = UIFactory.Rect("StatusBar", frame.transform);
                sb.anchorMin = new Vector2(0, 0); sb.anchorMax = new Vector2(1, 0); sb.pivot = new Vector2(0, 0);
                sb.offsetMin = new Vector2(3, 3); sb.offsetMax = new Vector2(-3, 3 + RetroTheme.StatusBarHeight);
                float right = 0;
                var widths = spec.StatusWidths ?? new float[0];
                for (int i = spec.StatusCells - 1; i >= 1; i--)
                {
                    float cw = i - 1 < widths.Length ? widths[i - 1] : 100;
                    var cell = UIFactory.ThinValue(sb, "");
                    var crt = (RectTransform)cell.transform.parent;
                    crt.anchorMin = new Vector2(1, 0); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 0.5f);
                    crt.offsetMin = new Vector2(-right - cw, 1); crt.offsetMax = new Vector2(-right, -1);
                    right += cw + 2;
                    w._statusCells.Insert(0, cell);
                }
                var first = UIFactory.ThinValue(sb, "");
                var frt = (RectTransform)first.transform.parent;
                frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 1);
                frt.offsetMin = new Vector2(0, 1); frt.offsetMax = new Vector2(-right, -1);
                w._statusCells.Insert(0, first);
                bottom += RetroTheme.StatusBarHeight + 2;
            }

            w.Content = UIFactory.Rect("Content", frame.transform).Stretch(5, top + 2, 5, bottom + 2);
            return w;
        }

        static RetroButton TitleButton(Transform parent, string label, Action onClick)
        {
            var b = UIFactory.Button(parent, label, onClick);
            b.Label.fontSize = 11;
            b.Label.fontStyle = FontStyle.Bold;
            b.Label.rectTransform.Stretch(0, -2, 0, 0);
            return b;
        }

        public void SetTitle(string t)
        {
            Title = t;
            _titleText.text = t;
            Manager?.NotifyChanged();
        }

        public void SetStatus(int cell, string text)
        {
            if (cell >= 0 && cell < _statusCells.Count) _statusCells[cell].text = text;
        }

        public void SetActiveLook(bool active)
        {
            _titleBar.sprite = active
                ? RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB)
                : RetroTheme.GradientSprite(RetroTheme.TitleInactiveA, RetroTheme.TitleInactiveB);
            _titleText.color = active ? Color.white : RetroTheme.Face;
            ActiveChanged?.Invoke(active);
        }

        public void Focus() => Manager?.Focus(this);
        internal void RaiseFocused() => Focused?.Invoke();

        public void RequestClose()
        {
            if (!Closable) { Minimize(); return; }
            if (CanClose != null && !CanClose()) return;
            Close();
        }

        public void Close()
        {
            Closed?.Invoke();
            Manager?.Remove(this);
            Destroy(gameObject);
        }

        public void Minimize()
        {
            IsMinimized = true;
            gameObject.SetActive(false);
            Manager?.OnMinimized(this);
        }

        public void Restore()
        {
            IsMinimized = false;
            gameObject.SetActive(true);
        }

        public void ToggleMaximize()
        {
            if (!IsMaximized)
            {
                _restorePos = Rect.anchoredPosition;
                _restoreSize = Rect.sizeDelta;
                var size = Manager.Container.rect.size;
                Rect.anchoredPosition = Vector2.zero;
                Rect.sizeDelta = size;
                IsMaximized = true;
            }
            else
            {
                Rect.anchoredPosition = _restorePos;
                Rect.sizeDelta = _restoreSize;
                IsMaximized = false;
            }
        }

        public void SetPosition(float x, float y) => Rect.anchoredPosition = new Vector2(x, -y);
        public Vector2 Position => new Vector2(Rect.anchoredPosition.x, -Rect.anchoredPosition.y);
    }

    public class WindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
    {
        public RetroWindow Window;
        Vector2 _startPointer, _startPos;

        public void OnPointerDown(PointerEventData e) => Window.Focus();

        public void OnBeginDrag(PointerEventData e)
        {
            if (Window.IsMaximized) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Window.Manager.Container, e.position, null, out _startPointer);
            _startPos = Window.Rect.anchoredPosition;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Window.IsMaximized) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Window.Manager.Container, e.position, null, out var p);
            var pos = _startPos + (p - _startPointer);
            var size = Window.Manager.Container.rect.size;
            pos.x = Mathf.Clamp(pos.x, -Window.Rect.sizeDelta.x + 60, size.x - 60);
            pos.y = Mathf.Clamp(pos.y, -size.y + 20, 0);
            Window.Rect.anchoredPosition = pos;
        }
    }
}
