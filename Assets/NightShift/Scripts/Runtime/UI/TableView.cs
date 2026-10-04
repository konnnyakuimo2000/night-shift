using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    public class TableColumn
    {
        public string Header;
        public float Width;
        public TableColumn(string header, float width) { Header = header; Width = width; }
    }

    /// <summary>表形式の一覧（業務ソフトのグリッド）。行選択・ダブルクリック対応。</summary>
    public class TableView : MonoBehaviour
    {
        public List<TableColumn> Columns = new List<TableColumn>();
        public ScrollRect Scroll;
        RectTransform _content;
        readonly List<Row> _rows = new List<Row>();
        public object Selected { get; private set; }
        public int SelectedIndex { get; private set; } = -1;
        public event Action<object> SelectionChanged;
        public event Action<object> DoubleClicked;
        public bool ShowMarker = true;
        public bool Mono;
        int _count;

        class Row
        {
            public GameObject Go;
            public Image Bg;
            public Text Marker;
            public List<Text> Cells = new List<Text>();
            public object Data;
        }

        public static TableView Create(Transform parent, float x, float y, float w, float h, params TableColumn[] columns)
        {
            var root = UIFactory.Rect("Table", parent).Place(x, y, w, h);
            return Build(root, columns);
        }

        public static TableView CreateStretch(Transform parent, float left, float top, float right, float bottom, params TableColumn[] columns)
        {
            var root = UIFactory.Rect("Table", parent).Stretch(left, top, right, bottom);
            return Build(root, columns);
        }

        static TableView Build(RectTransform root, TableColumn[] columns)
        {
            var tv = root.gameObject.AddComponent<TableView>();
            tv.Columns.AddRange(columns);

            var sr = UIFactory.Scroll(root, out var content, true);
            ((RectTransform)sr.transform).Stretch(0, 0, 0, 0);
            tv.Scroll = sr;

            // ヘッダー（スクロール領域の内側上部に固定）
            var viewport = sr.viewport;
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, -2 - 18);
            var header = UIFactory.Rect("Header", sr.transform).StretchH(2, 18, 2, 18);
            var headerBg = header.gameObject.AddComponent<Image>();
            headerBg.sprite = RetroTheme.White; headerBg.color = RetroTheme.Face;
            float hx = tv.ShowMarker ? 14 : 0;
            if (tv.ShowMarker)
            {
                var m = UIFactory.Image(header, RetroTheme.Raised, Color.white, "HeaderMarker");
                m.rectTransform.Place(0, 0, 14, 18);
            }
            foreach (var c in columns)
            {
                var cell = UIFactory.Image(header, RetroTheme.Raised, Color.white, "H:" + c.Header);
                cell.rectTransform.Place(hx, 0, c.Width, 18);
                var t = UIFactory.Label(cell.transform, c.Header, RetroTheme.FontSize, TextAnchor.MiddleCenter);
                t.rectTransform.Stretch(2, 0, 2, 0);
                hx += c.Width;
            }
            tv._content = content;
            return tv;
        }

        public void SetRows<T>(IList<T> items, Func<T, string[]> toCells)
        {
            _count = items.Count;
            while (_rows.Count < items.Count) _rows.Add(CreateRow(_rows.Count));
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                if (i < items.Count)
                {
                    r.Go.SetActive(true);
                    r.Data = items[i];
                    var cells = toCells(items[i]);
                    for (int c = 0; c < r.Cells.Count; c++) r.Cells[c].text = c < cells.Length ? cells[c] : "";
                }
                else
                {
                    r.Go.SetActive(false);
                    r.Data = null;
                }
            }
            _content.sizeDelta = new Vector2(0, items.Count * RetroTheme.RowHeight);
            // 選択の維持
            int newIndex = -1;
            if (Selected != null)
                for (int i = 0; i < items.Count; i++) if (ReferenceEquals(items[i], Selected)) newIndex = i;
            if (newIndex < 0 && Selected != null) { Selected = null; SelectedIndex = -1; SelectionChanged?.Invoke(null); }
            else SelectedIndex = newIndex;
            RefreshHighlight();
        }

        Row CreateRow(int index)
        {
            var r = new Row();
            var go = UIFactory.Rect("Row", _content);
            go.anchorMin = new Vector2(0, 1); go.anchorMax = new Vector2(1, 1); go.pivot = new Vector2(0, 1);
            go.offsetMin = new Vector2(0, -(index + 1) * RetroTheme.RowHeight);
            go.offsetMax = new Vector2(0, -index * RetroTheme.RowHeight);
            r.Go = go.gameObject;
            r.Bg = go.gameObject.AddComponent<Image>();
            r.Bg.sprite = RetroTheme.White;
            r.Bg.color = Color.white;
            float x = 0;
            if (ShowMarker)
            {
                r.Marker = UIFactory.Label(go, "", 11, TextAnchor.MiddleCenter);
                r.Marker.rectTransform.Place(0, 0, 14, RetroTheme.RowHeight);
                x = 14;
            }
            foreach (var c in Columns)
            {
                var t = UIFactory.Label(go, "", RetroTheme.FontSize, TextAnchor.MiddleLeft, null, Mono);
                t.rectTransform.Place(x + 3, 0, c.Width - 5, RetroTheme.RowHeight);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;
                EllipsisText.Attach(t);
                r.Cells.Add(t);
                // 縦罫線
                var line = UIFactory.Solid(go, new Color(0.85f, 0.85f, 0.85f), "Grid");
                line.raycastTarget = false;
                line.rectTransform.Place(x + c.Width - 1, 0, 1, RetroTheme.RowHeight);
                x += c.Width;
            }
            var bottom = UIFactory.Solid(go, new Color(0.9f, 0.9f, 0.9f), "GridH");
            bottom.raycastTarget = false;
            bottom.rectTransform.anchorMin = new Vector2(0, 0); bottom.rectTransform.anchorMax = new Vector2(1, 0);
            bottom.rectTransform.pivot = new Vector2(0, 0);
            bottom.rectTransform.offsetMin = Vector2.zero; bottom.rectTransform.offsetMax = new Vector2(0, 1);

            var click = go.gameObject.AddComponent<ClickHandler>();
            int idx = index;
            click.OnClick = () => Select(idx);
            click.OnDoubleClick = () => { if (r.Data != null) DoubleClicked?.Invoke(r.Data); };
            return r;
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _count) return;
            SelectedIndex = index;
            Selected = _rows[index].Data;
            RefreshHighlight();
            SelectionChanged?.Invoke(Selected);
        }

        public void SelectData(object data)
        {
            for (int i = 0; i < _count; i++)
                if (ReferenceEquals(_rows[i].Data, data)) { Select(i); EnsureVisible(i); return; }
        }

        public void ClearSelection()
        {
            Selected = null; SelectedIndex = -1;
            RefreshHighlight();
            SelectionChanged?.Invoke(null);
        }

        public void MoveSelection(int delta)
        {
            if (_count == 0) return;
            int i = SelectedIndex < 0 ? 0 : Mathf.Clamp(SelectedIndex + delta, 0, _count - 1);
            Select(i);
            EnsureVisible(i);
        }

        void RefreshHighlight()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                bool sel = i == SelectedIndex && _rows[i].Data != null;
                _rows[i].Bg.color = sel ? RetroTheme.Selection : Color.white;
                foreach (var t in _rows[i].Cells) t.color = sel ? RetroTheme.SelectionText : RetroTheme.Text;
                if (_rows[i].Marker != null) _rows[i].Marker.text = sel ? "▶" : "";
                if (_rows[i].Marker != null) _rows[i].Marker.color = RetroTheme.Text;
            }
        }

        public void ScrollToBottom()
        {
            Canvas.ForceUpdateCanvases();
            Scroll.verticalNormalizedPosition = 0f;
        }

        public bool IsAtBottom => Scroll.verticalNormalizedPosition <= 0.02f || _content.rect.height <= Scroll.viewport.rect.height;

        public void EnsureVisible(int index)
        {
            float viewH = Scroll.viewport.rect.height;
            float contentH = _content.rect.height;
            if (contentH <= viewH) return;
            float y = index * RetroTheme.RowHeight;
            float top = _content.anchoredPosition.y;
            if (y < top) _content.anchoredPosition = new Vector2(0, y);
            else if (y + RetroTheme.RowHeight > top + viewH) _content.anchoredPosition = new Vector2(0, y + RetroTheme.RowHeight - viewH);
        }

        public int Count => _count;
    }
}
