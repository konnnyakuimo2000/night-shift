using System.Collections.Generic;
using NightShift.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// マニュアルのビューア。左に目次（業務ごとのページ）、右にページ本文。
    /// 新しい業務が解放された日は、そのページに NEW が付き、最後まで読むと既読になる。
    /// </summary>
    public class ManualViewer : MonoBehaviour
    {
        ShiftDesktop _d;
        RetroWindow _w;
        DocumentDefinition _doc;
        List<int> _pages = new List<int>();
        int _cursor;                       // _pages 内の位置
        RectTransform _toc, _body;
        Text _pageTitle, _pageNo;
        RetroButton _prev, _next;

        public static void Open(ShiftDesktop d, DocumentDefinition doc)
        {
            var w = d.OpenWindow(new WindowSpec
            {
                Id = "doc:" + doc.documentId, Title = $"{doc.fileName} - マニュアル", Icon = doc.icon, Width = 920, Height = 680,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 1, Position = new Vector2(200, 30)
            }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<ManualViewer>();
            v._d = d; v._w = w; v._doc = doc;
            v.Build();
        }

        void Build()
        {
            var c = _w.Content;
            var head = UIFactory.Solid(c, RetroTheme.Navy, "Head");
            head.rectTransform.StretchH(0, 30);
            UIFactory.Label(head.transform, "  " + _doc.title, 15, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();

            // 目次
            var tocBg = UIFactory.Image(c, RetroTheme.Field, Color.white, "Toc");
            tocBg.rectTransform.anchorMin = new Vector2(0, 0); tocBg.rectTransform.anchorMax = new Vector2(0, 1);
            tocBg.rectTransform.pivot = new Vector2(0, 1);
            tocBg.rectTransform.offsetMin = new Vector2(0, 0); tocBg.rectTransform.offsetMax = new Vector2(220, -34);
            UIFactory.Label(tocBg.transform, "目次", 6, 4, 200, 18, 12).fontStyle = FontStyle.Bold;
            _toc = UIFactory.Rect("TocItems", tocBg.transform).Stretch(2, 26, 2, 2);

            // ページ
            var page = UIFactory.Rect("Page", c).Stretch(228, 34, 0, 0);
            var titleBar = UIFactory.Image(page, RetroTheme.GradientSprite(new Color(0.85f, 0.9f, 1f), new Color(0.97f, 0.98f, 1f)), Color.white, "PageTitle");
            titleBar.rectTransform.StretchH(0, 30);
            _pageTitle = UIFactory.Label(titleBar.transform, "", 16, TextAnchor.MiddleLeft, RetroTheme.Navy);
            _pageTitle.fontStyle = FontStyle.Bold;
            _pageTitle.rectTransform.Stretch(10, 0, 120, 0);
            _pageNo = UIFactory.Label(titleBar.transform, "", 12, TextAnchor.MiddleRight, RetroTheme.Shadow);
            _pageNo.rectTransform.Stretch(0, 0, 10, 0);
            _body = UIFactory.Rect("Body", page).Stretch(0, 34, 0, 34);

            _prev = UIFactory.Button(page, "◀ 前のページ", Prev);
            _prev.Rect.anchorMin = _prev.Rect.anchorMax = new Vector2(0, 0);
            _prev.Rect.pivot = new Vector2(0, 0);
            _prev.Rect.sizeDelta = new Vector2(130, 26);
            _prev.Rect.anchoredPosition = new Vector2(0, 2);
            _next = UIFactory.Button(page, "次のページ ▶", Next);
            _next.Rect.anchorMin = _next.Rect.anchorMax = new Vector2(1, 0);
            _next.Rect.pivot = new Vector2(1, 0);
            _next.Rect.sizeDelta = new Vector2(130, 26);
            _next.Rect.anchoredPosition = new Vector2(0, 2);

            _w.Accelerators[KeyCode.LeftArrow] = Prev;
            _w.Accelerators[KeyCode.RightArrow] = Next;
            _w.Accelerators[KeyCode.PageUp] = Prev;
            _w.Accelerators[KeyCode.PageDown] = Next;
            _w.Accelerators[KeyCode.C] = _w.RequestClose;

            _pages = _d.VisiblePages(_doc);
            // 最初に開くのは、まだ読んでいない新しいページ（無ければ先頭）
            _cursor = 0;
            for (int i = 0; i < _pages.Count; i++)
                if (_d.IsNewPage(_doc, _pages[i]) && !_d.IsPageRead(_doc, _pages[i])) { _cursor = i; break; }
            _d.ReadingChanged += RebuildToc;
            ShowPage();
        }

        void OnDestroy()
        {
            if (_d != null) _d.ReadingChanged -= RebuildToc;
        }

        void RebuildToc()
        {
            if (_toc == null) return;
            foreach (Transform t in _toc) Destroy(t.gameObject);
            float y = 0;
            for (int i = 0; i < _pages.Count; i++)
            {
                int idx = i;
                int page = _pages[i];
                var sec = _doc.sections[page];
                bool isNew = _d.IsNewPage(_doc, page);
                bool read = _d.IsPageRead(_doc, page);
                string mark = isNew ? (read ? "  <color=#0030A0>既読</color>" : "  <color=#D00000><b>NEW</b></color>") : "";
                var b = UIFactory.FlatButton(_toc, $"{i + 1}. {sec.title}{mark}", () => { _cursor = idx; ShowPage(); });
                b.Label.alignment = TextAnchor.MiddleLeft;
                b.Label.fontSize = 12;
                b.Rect.Place(0, y, 214, 24);
                if (i == _cursor)
                {
                    b.SetLatched(true);
                    b.Background.color = Color.white;
                    b.Label.fontStyle = FontStyle.Bold;
                }
                y += 25;
            }
        }

        void ShowPage()
        {
            foreach (Transform t in _body) Destroy(t.gameObject);
            if (_pages.Count == 0)
            {
                _pageTitle.text = "（表示できるページはありません）";
                RebuildToc();
                return;
            }
            _cursor = Mathf.Clamp(_cursor, 0, _pages.Count - 1);
            int page = _pages[_cursor];
            var sec = _doc.sections[page];
            _pageTitle.text = sec.title;
            _pageNo.text = $"{_cursor + 1} / {_pages.Count} ページ";
            string text = (_cursor == 0 && !string.IsNullOrEmpty(_doc.body) ? _doc.body + "\n\n" : "") + sec.text;
            var sr = DocumentRenderer.Render(_body, DocumentView.Expand(_d, text), (id, holder) => ManualFigures.Build(id, holder, _d.FigureContext));
            ((RectTransform)sr.transform).Stretch();
            _prev.Interactable = _cursor > 0;
            _next.Interactable = _cursor < _pages.Count - 1;

            bool isNew = _d.IsNewPage(_doc, page);
            if (isNew && !_d.IsPageRead(_doc, page))
            {
                _w.SetStatus(0, "NEW: このページを最後までスクロールすると「既読」になります。");
                var d = _d; var doc = _doc; var w = _w;
                ReadTracker.Attach(sr, () =>
                {
                    d.MarkPageRead(doc, page);
                    if (w != null) w.SetStatus(0, RemainingText());
                });
            }
            else _w.SetStatus(0, RemainingText());
            RebuildToc();
        }

        string RemainingText()
        {
            int left = 0;
            foreach (var p in _d.NewPages(_doc)) if (!_d.IsPageRead(_doc, p)) left++;
            return left > 0 ? $"未読の新しいページがあと {left} ページあります。" : "このマニュアルの新しいページはすべて読みました。";
        }

        void Prev() { if (_cursor > 0) { _cursor--; ShowPage(); } }
        void Next() { if (_cursor < _pages.Count - 1) { _cursor++; ShowPage(); } }
    }
}
