using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// 1行の欄に収まらない文字を、末尾「…」で省略する。省略したときはマウスを乗せると全文をツールチップで出す。
    /// （はみ出した部分が黙って消えるのを防ぐ）
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class EllipsisText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Text _t;
        string _shown;
        string _full;
        float _width = -1;
        bool _truncated;

        public static void Attach(Text t)
        {
            if (t == null || t.GetComponent<EllipsisText>() != null) return;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = true;
            t.gameObject.AddComponent<EllipsisText>();
        }

        void Awake() => _t = GetComponent<Text>();

        void LateUpdate()
        {
            if (_t == null) return;
            float w = _t.rectTransform.rect.width;
            if (_t.text == _shown && Mathf.Approximately(w, _width)) return;
            if (_t.text != _shown) _full = _t.text;
            _width = w;
            Fit();
        }

        void Fit()
        {
            string s = _full ?? "";
            _truncated = false;
            if (!s.Contains("<")) // リッチテキストは省略しない（タグを壊さないため）
            {
                _t.text = s;
                if (_width > 4 && _t.preferredWidth > _width)
                {
                    int lo = 0, hi = s.Length;
                    while (lo < hi)
                    {
                        int mid = (lo + hi + 1) / 2;
                        _t.text = s.Substring(0, mid) + "…";
                        if (_t.preferredWidth <= _width) lo = mid; else hi = mid - 1;
                    }
                    _t.text = s.Substring(0, Mathf.Max(0, lo)) + "…";
                    _truncated = true;
                }
            }
            else _t.text = s;
            _shown = _t.text;
        }

        public void OnPointerEnter(PointerEventData e) { if (_truncated) Tooltip.Show(_full, e.position); }
        public void OnPointerExit(PointerEventData e) => Tooltip.Hide();
        void OnDisable() { if (_truncated) Tooltip.Hide(); }
    }

    /// <summary>マウスを乗せたときの小さな説明（黄色い枠）</summary>
    public static class Tooltip
    {
        static GameObject _go;
        static Text _text;

        public static void Show(string text, Vector2 screenPos)
        {
            var root = PopupMenu.OverlayRoot;
            if (root == null || string.IsNullOrEmpty(text)) return;
            if (_go == null)
            {
                var bg = UIFactory.Image(root, RetroTheme.White, new Color(1f, 1f, 0.88f), "Tooltip");
                bg.raycastTarget = false;
                var outline = bg.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1, -1);
                bg.rectTransform.pivot = new Vector2(0, 1);
                bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = root.pivot;
                _text = UIFactory.Label(bg.transform, "", RetroTheme.FontSize, TextAnchor.UpperLeft, RetroTheme.Text);
                _text.raycastTarget = false;
                _text.rectTransform.Stretch(4, 2, 4, 2);
                _go = bg.gameObject;
            }
            _go.SetActive(true);
            _go.transform.SetAsLastSibling();
            _text.text = text;
            var rt = (RectTransform)_go.transform;
            float w = Mathf.Min(_text.preferredWidth + 10, 520);
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            rt.sizeDelta = new Vector2(w, 20);
            float h = _text.preferredHeight + 6;
            rt.sizeDelta = new Vector2(w, h);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPos, null, out var local);
            var rect = root.rect;   // root の pivot 基準
            var pos = local + new Vector2(12, -16);
            pos.x = Mathf.Min(pos.x, rect.xMax - w - 4);
            pos.y = Mathf.Max(pos.y, rect.yMin + h + 4);
            rt.anchoredPosition = pos;
        }

        public static void Hide()
        {
            if (_go != null) _go.SetActive(false);
        }
    }
}
