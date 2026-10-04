using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// 複数Windowの管理（開く/閉じる/前面化/最小化/Alt+Tab）。
    /// 社内システムのデスクトップと、Remote PC のデスクトップの両方で使う（入れ子可能）。
    /// </summary>
    public class WindowManager : MonoBehaviour
    {
        public RectTransform Container { get; private set; }
        readonly List<RetroWindow> _windows = new List<RetroWindow>();
        public IReadOnlyList<RetroWindow> Windows => _windows;
        public RetroWindow Active { get; private set; }
        public event Action Changed;
        public event Action<RetroWindow> ActiveChanged;
        public bool HandlesMouseFocus = true;
        int _cascade;

        // Alt+Tab
        RectTransform _switcher;
        List<RetroWindow> _switchList;
        int _switchIndex;
        public bool Switching => _switchList != null;
        Func<bool> _switchHeld;

        public static WindowManager Create(RectTransform container)
        {
            var wm = container.gameObject.AddComponent<WindowManager>();
            wm.Container = container;
            return wm;
        }

        public RetroWindow Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var w in _windows) if (w != null && w.Id == id) return w;
            return null;
        }

        /// <summary>同じIdが既に開いていれば前面化して返す（created=false）</summary>
        public RetroWindow Open(WindowSpec spec, out bool created)
        {
            var existing = Find(spec.Id);
            if (existing != null)
            {
                created = false;
                if (existing.IsMinimized) existing.Restore();
                Focus(existing);
                return existing;
            }
            created = true;
            var w = RetroWindow.Build(this, spec);
            _windows.Add(w);
            var size = Container.rect.size;
            Vector2 pos;
            if (spec.Position.HasValue) pos = spec.Position.Value;
            else
            {
                pos = new Vector2(40 + (_cascade % 8) * 24, 20 + (_cascade % 8) * 22);
                _cascade++;
            }
            // 初回フレームではCanvasのサイズが未確定のことがあるため、妥当なサイズの時だけ画面内に収める
            // （UI の拡大率を上げて画面より大きくなるウィンドウは、画面の大きさまで縮める）
            float width = spec.Width, height = spec.Height;
            if (size.x > 200 && width > size.x) width = size.x;
            if (size.y > 200 && height > size.y) height = size.y;
            if (width != spec.Width || height != spec.Height) w.Rect.sizeDelta = new Vector2(width, height);
            if (size.x > 200 && pos.x + width > size.x) pos.x = Mathf.Max(0, size.x - width);
            if (size.y > 200 && pos.y + height > size.y) pos.y = Mathf.Max(0, size.y - height);
            w.SetPosition(pos.x, pos.y);
            Focus(w);
            return w;
        }

        public void Focus(RetroWindow w)
        {
            if (w == null) return;
            if (w.IsMinimized) w.Restore();
            w.transform.SetAsLastSibling();
            w.LastFocusTime = Time.unscaledTime;
            if (Active != w)
            {
                if (Active != null) Active.SetActiveLook(false);
                Active = w;
                w.SetActiveLook(true);
                ActiveChanged?.Invoke(w);
            }
            w.RaiseFocused();
            Changed?.Invoke();
        }

        internal void Remove(RetroWindow w)
        {
            _windows.Remove(w);
            if (Active == w)
            {
                Active = null;
                FocusTopmost(w);
                if (Active == null) ActiveChanged?.Invoke(null);
            }
            Changed?.Invoke();
        }

        internal void OnMinimized(RetroWindow w)
        {
            if (Active == w)
            {
                w.SetActiveLook(false);
                Active = null;
                FocusTopmost(w);
                if (Active == null) ActiveChanged?.Invoke(null);
            }
            Changed?.Invoke();
        }

        void FocusTopmost(RetroWindow except)
        {
            RetroWindow best = null;
            foreach (var x in _windows)
                if (x != null && x != except && !x.IsMinimized && (best == null || x.LastFocusTime > best.LastFocusTime)) best = x;
            if (best != null) Focus(best);
        }

        public void NotifyChanged() => Changed?.Invoke();

        public void MinimizeAll()
        {
            foreach (var w in _windows.ToArray()) if (!w.IsMinimized) w.Minimize();
        }

        public void CloseAll()
        {
            foreach (var w in _windows.ToArray()) if (w != null) w.Close();
        }

        void Update()
        {
            if (HandlesMouseFocus && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && EventSystem.current != null && !PopupMenu.IsOpen)
            {
                var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(ped, results);
                if (results.Count > 0)
                {
                    var t = results[0].gameObject.transform;
                    while (t != null)
                    {
                        var w = t.GetComponent<RetroWindow>();
                        if (w != null && w.Manager == this) { if (Active != w) Focus(w); break; }
                        t = t.parent;
                    }
                }
            }

            if (Switching && (_switchHeld == null || !_switchHeld())) CommitSwitch();
        }

        // ---------------- Alt+Tab ----------------
        /// <summary>heldCheck: 修飾キー（Alt / Ctrl）が押されている間 true。離すと確定。</summary>
        public void SwitchNext(bool reverse, Func<bool> heldCheck)
        {
            if (!Switching)
            {
                _switchList = new List<RetroWindow>(_windows);
                _switchList.RemoveAll(w => w == null);
                _switchList.Sort((a, b) =>
                {
                    if (a == Active) return -1;
                    if (b == Active) return 1;
                    return b.LastFocusTime.CompareTo(a.LastFocusTime);
                });
                if (_switchList.Count == 0) { _switchList = null; return; }
                _switchIndex = 0;
                _switchHeld = heldCheck;
            }
            int n = _switchList.Count;
            _switchIndex = ((_switchIndex + (reverse ? -1 : 1)) % n + n) % n;
            ShowSwitcher();
            if (heldCheck == null) CommitSwitch();
        }

        void ShowSwitcher()
        {
            if (_switcher != null) Destroy(_switcher.gameObject);
            float w = Mathf.Min(Container.rect.width - 20, Mathf.Max(260, _switchList.Count * 44 + 30));
            var panel = UIFactory.Image(Container, RetroTheme.Raised, Color.white, "AltTab");
            _switcher = panel.rectTransform;
            _switcher.anchorMin = _switcher.anchorMax = new Vector2(0.5f, 0.5f);
            _switcher.pivot = new Vector2(0.5f, 0.5f);
            _switcher.sizeDelta = new Vector2(w, 96);
            _switcher.anchoredPosition = Vector2.zero;
            float x = (w - _switchList.Count * 44) / 2f;
            for (int i = 0; i < _switchList.Count; i++)
            {
                var item = _switchList[i];
                if (i == _switchIndex)
                {
                    var sel = UIFactory.Image(panel.transform, RetroTheme.Pressed, Color.white, "Sel");
                    sel.rectTransform.Place(x + i * 44 + 2, 10, 40, 40);
                }
                var ic = UIFactory.Image(panel.transform, RetroTheme.BigIcon(item.IconName), Color.white, "Icon");
                ic.rectTransform.Place(x + i * 44 + 6, 14, 32, 32);
            }
            var title = UIFactory.Label(panel.transform, _switchList[_switchIndex].Title, RetroTheme.FontSize, TextAnchor.MiddleCenter);
            var img = UIFactory.Image(panel.transform, RetroTheme.ThinSunken, Color.white, "TitleBox");
            img.rectTransform.Place(10, 60, w - 20, 24);
            title.transform.SetParent(img.transform, false);
            title.rectTransform.Stretch(4, 0, 4, 0);
        }

        void CommitSwitch()
        {
            if (_switchList != null && _switchList.Count > 0)
            {
                var target = _switchList[Mathf.Clamp(_switchIndex, 0, _switchList.Count - 1)];
                if (target != null) Focus(target);
            }
            _switchList = null;
            _switchHeld = null;
            if (_switcher != null) Destroy(_switcher.gameObject);
            _switcher = null;
        }

        public void CancelSwitch()
        {
            _switchList = null;
            if (_switcher != null) Destroy(_switcher.gameObject);
            _switcher = null;
        }
    }
}
