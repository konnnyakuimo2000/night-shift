using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>状態バッジの色の種類（色だけに頼らず、必ず文字も添える）</summary>
    public enum Tone { Neutral, Info, Ok, Warn, Bad }

    /// <summary>
    /// 自腹で買うツール・購買システムの見た目。灰色の業務システムとは違う、市販ソフトらしい分かりやすいデザイン。
    /// 白いカード・ツールごとの色の見出し・大きめの文字・状態バッジ・平らなボタン。
    /// </summary>
    public static class ToolStyle
    {
        public static readonly Color Bg = new Color(0.93f, 0.94f, 0.96f);
        public static readonly Color CardColor = Color.white;
        public static readonly Color Border = new Color(0.82f, 0.84f, 0.88f);
        public static readonly Color Ink = new Color(0.12f, 0.14f, 0.19f);
        public static readonly Color Muted = new Color(0.40f, 0.43f, 0.50f);
        public static readonly Color Disabled = new Color(0.84f, 0.85f, 0.88f);

        // ツールごとの色
        public static readonly Color Teal = new Color(0.05f, 0.47f, 0.55f);
        public static readonly Color Green = new Color(0.13f, 0.53f, 0.27f);
        public static readonly Color Indigo = new Color(0.25f, 0.30f, 0.62f);
        public static readonly Color Red = new Color(0.72f, 0.18f, 0.16f);
        public static readonly Color Orange = new Color(0.82f, 0.45f, 0.08f);
        public static readonly Color Purple = new Color(0.47f, 0.27f, 0.62f);
        public static readonly Color Slate = new Color(0.22f, 0.33f, 0.45f);
        public static readonly Color Navy = new Color(0.12f, 0.25f, 0.50f);
        public static readonly Color Violet = new Color(0.30f, 0.18f, 0.45f);

        public const float HeaderHeight = 56;
        public const float Pad = 10;

        static Sprite _round, _outline;

        /// <summary>角の丸い四角（塗り）</summary>
        public static Sprite Round => _round ? _round : _round = MakeRound(false);
        /// <summary>角の丸い四角（白地に細い枠）</summary>
        public static Sprite Outline => _outline ? _outline : _outline = MakeRound(true);

        static Sprite MakeRound(bool outline)
        {
            const int n = 16, r = 5;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // 角からの距離で丸める（少しだけぼかして滑らかに）
                float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                Color c = Color.white;
                if (outline)
                {
                    bool edge = d > r - 1.2f || x == 0 || y == 0 || x == n - 1 || y == n - 1;
                    if (edge) c = Border;
                }
                c.a = a;
                px[y * n + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r + 1, r + 1, r + 1, r + 1));
        }

        public static Color Light(Color c, float k = 0.85f) => Color.Lerp(c, Color.white, k);

        public static Color ToneColor(Tone t)
        {
            switch (t)
            {
                case Tone.Info: return new Color(0.16f, 0.38f, 0.72f);
                case Tone.Ok: return new Color(0.12f, 0.50f, 0.25f);
                case Tone.Warn: return new Color(0.72f, 0.45f, 0.02f);
                case Tone.Bad: return new Color(0.75f, 0.15f, 0.13f);
                default: return Muted;
            }
        }

        // ---------------- 文字 ----------------
        public static Text Text(Transform p, string s, int size = 13, Color? color = null, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool mono = false)
        {
            var t = UIFactory.Label(p, s, size, anchor, color ?? Ink, mono);
            if (bold) t.fontStyle = FontStyle.Bold;
            t.lineSpacing = 1.1f;
            return t;
        }

        public static Text Text(Transform p, string s, float x, float y, float w, float h, int size = 13, Color? color = null, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool mono = false)
        {
            var t = Text(p, s, size, color, anchor, bold, mono);
            t.rectTransform.Place(x, y, w, h);
            return t;
        }

        // ---------------- 画面の土台・見出し ----------------
        /// <summary>ウィンドウの中身を薄い灰色の土台にする。中身を置く RectTransform を返す</summary>
        public static RectTransform Page(RetroWindow w)
        {
            var bg = UIFactory.Solid(w.Content, Bg, "ToolPage");
            bg.rectTransform.Stretch(-2, -2, -2, -2);
            return bg.rectTransform;
        }

        /// <summary>ツールの色の見出し帯（名前と「何をするツールか」）。下に続く中身の y を返す</summary>
        public static float Header(Transform p, string title, string tagline, string icon, Color accent)
        {
            var band = UIFactory.Solid(p, accent, "Header");
            band.rectTransform.StretchH(0, HeaderHeight);
            var shade = UIFactory.Solid(band.transform, new Color(0, 0, 0, 0.12f), "Shade");
            shade.rectTransform.StretchH(HeaderHeight - 2, 2);
            var ic = UIFactory.Image(band.transform, RetroTheme.BigIcon(icon), Color.white, "Icon");
            ic.rectTransform.Place(14, 12, 32, 32);
            Text(band.transform, title, 56, 9, 600, 22, 16, Color.white, TextAnchor.MiddleLeft, true);
            Text(band.transform, tagline, 56, 31, 640, 18, 11, new Color(1, 1, 1, 0.88f), TextAnchor.MiddleLeft);
            return HeaderHeight + Pad;
        }

        // ---------------- カード ----------------
        /// <summary>白いカード。中身を置く RectTransform（余白・見出しの分を除いた場所）を返す</summary>
        public static RectTransform Card(Transform p, float x, float y, float w, float h, string title = null)
        {
            var img = UIFactory.Image(p, Outline, CardColor, "Card");
            img.rectTransform.Place(x, y, w, h);
            return CardInner(img.rectTransform, title);
        }

        /// <summary>親に合わせて伸びるカード（左・上・右・下の位置）</summary>
        public static RectTransform CardStretch(Transform p, float left, float top, float right, float bottom, string title = null)
        {
            var img = UIFactory.Image(p, Outline, CardColor, "Card");
            img.rectTransform.Stretch(left, top, right, bottom);
            return CardInner(img.rectTransform, title);
        }

        /// <summary>
        /// 横は親の割合（a0〜a1）、縦は上から top の位置に高さ height のカード。height &lt;= 0 なら下端（bottomInset）まで伸ばす。
        /// </summary>
        public static RectTransform Box(Transform p, float a0, float a1, float top, float height, string title = null, float bottomInset = Pad)
        {
            var img = UIFactory.Image(p, Outline, CardColor, "Card");
            var rt = img.rectTransform;
            float l = a0 <= 0 ? Pad : Pad / 2, r = a1 >= 1 ? Pad : Pad / 2;
            rt.pivot = new Vector2(0, 1);
            if (height > 0)
            {
                rt.anchorMin = new Vector2(a0, 1); rt.anchorMax = new Vector2(a1, 1);
                rt.offsetMin = new Vector2(l, -top - height); rt.offsetMax = new Vector2(-r, -top);
            }
            else
            {
                rt.anchorMin = new Vector2(a0, 0); rt.anchorMax = new Vector2(a1, 1);
                rt.offsetMin = new Vector2(l, bottomInset); rt.offsetMax = new Vector2(-r, -top);
            }
            return CardInner(rt, title);
        }

        /// <summary>下端から bottom の位置に、高さ height のカード（横は a0〜a1）</summary>
        public static RectTransform BoxBottom(Transform p, float a0, float a1, float bottom, float height, string title = null)
        {
            var img = UIFactory.Image(p, Outline, CardColor, "Card");
            var rt = img.rectTransform;
            float l = a0 <= 0 ? Pad : Pad / 2, r = a1 >= 1 ? Pad : Pad / 2;
            rt.anchorMin = new Vector2(a0, 0); rt.anchorMax = new Vector2(a1, 0);
            rt.pivot = new Vector2(0, 0);
            rt.offsetMin = new Vector2(l, bottom); rt.offsetMax = new Vector2(-r, bottom + height);
            return CardInner(rt, title);
        }

        /// <summary>親の横幅を n 等分した i 番目に、上から y・高さ h で置く（ボタンやタイルを並べる用）</summary>
        public static RectTransform Cell(RectTransform rt, int i, int n, float y, float h, float gap = 8)
        {
            rt.anchorMin = new Vector2((float)i / n, 1); rt.anchorMax = new Vector2((float)(i + 1) / n, 1);
            rt.pivot = new Vector2(0, 1);
            rt.offsetMin = new Vector2(i == 0 ? 0 : gap / 2, -y - h);
            rt.offsetMax = new Vector2(i == n - 1 ? 0 : -gap / 2, -y);
            return rt;
        }

        /// <summary>状態タイル（項目・値・バッジ）。値の Text を返す</summary>
        public static Text Tile(Transform p, int i, int n, float y, float h, string label, out RectTransform tile)
        {
            var bg = UIFactory.Image(p, Round, new Color(0.95f, 0.96f, 0.98f), "Tile");
            Cell(bg.rectTransform, i, n, y, h);
            tile = bg.rectTransform;
            Text(bg.transform, label, 10, 6, 400, 16, 11, Muted, TextAnchor.MiddleLeft, true);
            var v = Text(bg.transform, "", 13, Ink, TextAnchor.MiddleLeft, true);
            v.rectTransform.StretchH(24, 22, 10, 6);
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            return v;
        }

        /// <summary>縦にスクロールする領域（細いスクロールバー）。中身を置く content を返す（高さは呼び出し側で決める）</summary>
        public static RectTransform ScrollArea(Transform parent, out ScrollRect sr)
        {
            var root = UIFactory.Rect("ScrollArea", parent).Stretch();
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            sr = root.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 24f;
            sr.inertia = false;
            var viewport = UIFactory.Rect("Viewport", root).Stretch(0, 0, 8, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);
            sr.viewport = viewport;
            var content = UIFactory.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0, 1);
            content.sizeDelta = Vector2.zero;
            sr.content = content;
            var sbBg = UIFactory.Image(root, Round, new Color(0, 0, 0, 0.05f), "Scrollbar");
            sbBg.rectTransform.anchorMin = new Vector2(1, 0); sbBg.rectTransform.anchorMax = new Vector2(1, 1);
            sbBg.rectTransform.pivot = new Vector2(1, 1);
            sbBg.rectTransform.offsetMin = new Vector2(-6, 0); sbBg.rectTransform.offsetMax = Vector2.zero;
            var sb = sbBg.gameObject.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            var area = UIFactory.Rect("Area", sbBg.transform).Stretch();
            var handle = UIFactory.Image(area, Round, new Color(0, 0, 0, 0.25f), "Handle");
            handle.rectTransform.Stretch();
            sb.handleRect = handle.rectTransform;
            sb.targetGraphic = handle;
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        /// <summary>スクロールする長い文（リッチテキスト）。高さは文の長さに合わせて自動</summary>
        public static Text ScrollText(Transform parent, int size = 13, bool mono = false)
        {
            var content = ScrollArea(parent, out _);
            var vl = UIFactory.VerticalList(content, 0, 0);
            vl.padding = new RectOffset(2, 6, 2, 8);
            var t = Text(content, "", size, Ink, TextAnchor.UpperLeft, false, mono);
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.2f;
            return t;
        }

        static RectTransform CardInner(RectTransform card, string title)
        {
            float top = 10;
            if (!string.IsNullOrEmpty(title))
            {
                Text(card, title, 12, 8, 600, 18, 12, Muted, TextAnchor.MiddleLeft, true);
                top = 30;
            }
            return UIFactory.Rect("Inner", card).Stretch(12, top, 12, 10);
        }

        // ---------------- バッジ ----------------
        static float TextWidth(string s, int size)
        {
            float w = 0;
            foreach (char ch in s) w += ch < 0x80 ? size * 0.6f : size * 1.0f;
            return w;
        }

        /// <summary>状態バッジ（色＋文字）。左上 (x, y) に置く</summary>
        public static RectTransform Badge(Transform p, string text, Tone tone, float x, float y)
        {
            var rt = MakeBadge(p, text, tone);
            rt.Place(x, y, rt.sizeDelta.x, 20);
            return rt;
        }

        /// <summary>右寄せの状態バッジ</summary>
        public static RectTransform BadgeRight(Transform p, string text, Tone tone, float right, float y)
        {
            var rt = MakeBadge(p, text, tone);
            rt.AnchorTopRight(right, y, rt.sizeDelta.x, 20);
            return rt;
        }

        static RectTransform MakeBadge(Transform p, string text, Tone tone)
        {
            var c = ToneColor(tone);
            var img = UIFactory.Image(p, Round, Light(c, 0.84f), "Badge");
            img.raycastTarget = false;
            var t = Text(img.transform, text, 11, Color.Lerp(c, Color.black, 0.15f), TextAnchor.MiddleCenter, true);
            t.rectTransform.Stretch();
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            img.rectTransform.sizeDelta = new Vector2(TextWidth(text, 11) + 18, 20);
            return img.rectTransform;
        }

        // ---------------- ボタン ----------------
        /// <summary>平らなボタン。primary = 色付き（そのツールでいちばん押すボタン）</summary>
        public static FlatButton Button(Transform p, string text, float x, float y, float w, float h, Action onClick, Color accent, bool primary = true)
        {
            var b = FlatButton.Create(p, text, onClick, accent, primary);
            b.Rect.Place(x, y, w, h);
            return b;
        }

        // ---------------- 入力欄 ----------------
        public static InputField Input(Transform p, float x, float y, float w, float h, string initial = "", bool mono = true)
        {
            var f = UIFactory.Input(p, initial, false, mono);
            var img = f.GetComponent<Image>();
            img.sprite = Outline;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            f.textComponent.fontSize = 14;
            f.textComponent.rectTransform.Stretch(8, 2, 8, 2);
            ((RectTransform)f.transform).Place(x, y, w, h);
            return f;
        }

        // ---------------- 項目と値 ----------------
        /// <summary>「項目　値」の1行。値の Text を返す</summary>
        public static Text Field(Transform p, string key, float x, float y, float w, float keyWidth = 96, bool mono = false)
        {
            Text(p, key, x, y, keyWidth, 20, 12, Muted, TextAnchor.MiddleLeft);
            var v = Text(p, "", x + keyWidth, y, w - keyWidth, 20, 14, Ink, TextAnchor.MiddleLeft, false, mono);
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            return v;
        }

        /// <summary>大きな数字のタイル（例: 該当 12 件）。数字の Text を返す</summary>
        public static Text Stat(Transform p, string label, float x, float y, float w, float h, Color accent)
        {
            var bg = UIFactory.Image(p, Round, Light(accent, 0.9f), "Stat");
            bg.rectTransform.Place(x, y, w, h);
            Text(bg.transform, label, 10, 6, w - 20, 16, 11, Muted, TextAnchor.MiddleLeft, true);
            var v = Text(bg.transform, "-", 10, 22, w - 20, h - 26, 22, Color.Lerp(accent, Color.black, 0.2f), TextAnchor.MiddleLeft, true);
            return v;
        }

        /// <summary>進み具合のバー。塗りの Image（fillAmount で操作）を返す</summary>
        public static Image Progress(Transform p, float x, float y, float w, float h, Color accent)
        {
            var bg = UIFactory.Image(p, Round, Disabled, "Progress");
            bg.rectTransform.Place(x, y, w, h);
            var fill = UIFactory.Image(bg.transform, RetroTheme.White, accent, "Fill");
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillAmount = 0;
            fill.rectTransform.Stretch(2, 2, 2, 2);
            return fill;
        }

        /// <summary>まだ何も選んでいないときの案内（何をすればよいかを手順で示す）</summary>
        public static GameObject EmptyState(Transform p, string title, params string[] steps)
        {
            var root = UIFactory.Rect("Empty", p).Stretch();
            Text(root, title, 0, 18, 10000, 24, 15, Muted, TextAnchor.MiddleLeft, true).rectTransform.StretchH(18, 24, 16, 16);
            float y = 52;
            string[] nums = { "①", "②", "③", "④" };
            for (int i = 0; i < steps.Length; i++)
            {
                var t = Text(root, $"{nums[Mathf.Min(i, nums.Length - 1)]}  {steps[i]}", 13, Ink, TextAnchor.MiddleLeft);
                t.rectTransform.StretchH(y, 22, 20, 16);
                y += 26;
            }
            return root.gameObject;
        }

        /// <summary>注意書きの帯（色＋文字）</summary>
        public static Text Banner(Transform p, string text, Tone tone, float x, float y, float w, float h)
        {
            var c = ToneColor(tone);
            var bg = UIFactory.Image(p, Round, Light(c, 0.86f), "Banner");
            bg.rectTransform.Place(x, y, w, h);
            var bar = UIFactory.Solid(bg.transform, c, "Bar");
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(0, 1);
            bar.rectTransform.pivot = new Vector2(0, 0.5f);
            bar.rectTransform.offsetMin = new Vector2(0, 3); bar.rectTransform.offsetMax = new Vector2(4, -3);
            var t = Text(bg.transform, text, 12, Color.Lerp(c, Color.black, 0.35f), TextAnchor.MiddleLeft, true);
            t.rectTransform.Stretch(14, 2, 8, 2);
            return t;
        }
    }

    /// <summary>平らなボタン（色付き／白）。マウスを乗せると明るく、押すと暗くなる</summary>
    public class FlatButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Image Bg;
        public Text Label;
        public Action OnClick;
        Color _accent;
        bool _primary, _hover, _down, _interactable = true;
        public RectTransform Rect => (RectTransform)transform;

        public static FlatButton Create(Transform p, string text, Action onClick, Color accent, bool primary)
        {
            var img = UIFactory.Image(p, ToolStyle.Round, Color.white, "FlatButton");
            var b = img.gameObject.AddComponent<FlatButton>();
            b.Bg = img;
            b._accent = accent;
            b._primary = primary;
            b.OnClick = onClick;
            b.Label = ToolStyle.Text(img.transform, text, 13, Color.white, TextAnchor.MiddleCenter, true);
            b.Label.rectTransform.Stretch(6, 0, 6, 0);
            b.Paint();
            return b;
        }

        public bool Interactable
        {
            get => _interactable;
            set { _interactable = value; Paint(); }
        }

        public void SetText(string s) { if (Label) Label.text = s; }

        public void SetAccent(Color c, bool primary) { _accent = c; _primary = primary; Paint(); }

        void Paint()
        {
            if (!Bg) return;
            if (!_interactable)
            {
                Bg.color = ToolStyle.Disabled;
                Label.color = ToolStyle.Muted;
                return;
            }
            Color baseColor = _primary ? _accent : ToolStyle.Light(_accent, 0.88f);
            if (_down) baseColor = Color.Lerp(baseColor, Color.black, 0.15f);
            else if (_hover) baseColor = Color.Lerp(baseColor, Color.white, _primary ? 0.12f : 0.3f);
            Bg.color = baseColor;
            Label.color = _primary ? Color.white : Color.Lerp(_accent, Color.black, 0.25f);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (!_interactable) { AudioFx.Error(); return; }
            AudioFx.Click();
            OnClick?.Invoke();
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; Paint(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; Paint(); }
        public void OnPointerDown(PointerEventData e) { _down = true; Paint(); }
        public void OnPointerUp(PointerEventData e) { _down = false; Paint(); }
    }

    /// <summary>
    /// 2行（主な内容・補足）＋右のバッジの一覧。クリックで選ぶ。表の代わりに使う（ツール用）。
    /// </summary>
    public class ToolList : MonoBehaviour
    {
        public struct Row
        {
            public string Title, Sub, Badge;
            public Tone Tone;
            public Row(string title, string sub, string badge = null, Tone tone = Tone.Neutral) { Title = title; Sub = sub; Badge = badge; Tone = tone; }
        }

        public const float RowHeight = 46;
        public object Selected { get; private set; }
        public event Action<object> SelectionChanged;
        RectTransform _content;
        Text _empty;
        Color _accent;
        string _signature;
        readonly List<object> _data = new List<object>();
        readonly List<Image> _rowBgs = new List<Image>();

        public static ToolList Create(Transform parent, Color accent, string emptyText)
        {
            var content = ToolStyle.ScrollArea(parent, out var sr);
            var root = (RectTransform)sr.transform;
            var list = root.gameObject.AddComponent<ToolList>();
            list._content = content;
            list._accent = accent;
            list._empty = ToolStyle.Text(root, emptyText, 13, ToolStyle.Muted, TextAnchor.MiddleCenter);
            list._empty.rectTransform.Stretch(10, 10, 10, 10);
            return list;
        }

        public void SetItems<T>(IList<T> items, Func<T, Row> map)
        {
            var rows = new List<Row>();
            var sig = new System.Text.StringBuilder();
            foreach (var it in items)
            {
                var r = map(it);
                rows.Add(r);
                sig.Append(r.Title).Append('|').Append(r.Sub).Append('|').Append(r.Badge).Append('|').Append((int)r.Tone).Append('\n');
            }
            string s = sig.ToString();
            bool sameData = _data.Count == items.Count;
            if (sameData) for (int i = 0; i < items.Count; i++) if (!Equals(_data[i], items[i])) { sameData = false; break; }
            if (s == _signature && sameData) return; // 変わっていなければ作り直さない
            _signature = s;
            _data.Clear();
            foreach (var it in items) _data.Add(it);

            foreach (Transform t in _content) Destroy(t.gameObject);
            _rowBgs.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                int idx = i;
                var bg = UIFactory.Image(_content, ToolStyle.Round, Color.white, "Row");
                bg.rectTransform.anchorMin = new Vector2(0, 1); bg.rectTransform.anchorMax = new Vector2(1, 1);
                bg.rectTransform.pivot = new Vector2(0, 1);
                bg.rectTransform.offsetMin = new Vector2(0, -(i + 1) * RowHeight + 2);
                bg.rectTransform.offsetMax = new Vector2(0, -i * RowHeight);
                _rowBgs.Add(bg);
                var line = UIFactory.Solid(bg.transform, new Color(0, 0, 0, 0.06f), "Line");
                line.raycastTarget = false;
                line.rectTransform.anchorMin = new Vector2(0, 0); line.rectTransform.anchorMax = new Vector2(1, 0);
                line.rectTransform.offsetMin = new Vector2(8, -2); line.rectTransform.offsetMax = new Vector2(-8, -1);
                var title = ToolStyle.Text(bg.transform, r.Title, 13, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
                title.rectTransform.StretchH(4, 20, 10, 110);
                title.raycastTarget = false;
                EllipsisText.Attach(title);
                var sub = ToolStyle.Text(bg.transform, r.Sub ?? "", 11, ToolStyle.Muted, TextAnchor.MiddleLeft);
                sub.rectTransform.StretchH(24, 18, 10, 110);
                sub.raycastTarget = false;
                EllipsisText.Attach(sub);
                if (!string.IsNullOrEmpty(r.Badge)) ToolStyle.BadgeRight(bg.transform, r.Badge, r.Tone, 8, 13);
                var click = bg.gameObject.AddComponent<ClickHandler>();
                click.OnClick = () => Select(_data[idx]);
            }
            _content.sizeDelta = new Vector2(0, rows.Count * RowHeight);
            _empty.gameObject.SetActive(rows.Count == 0);
            if (Selected != null && !_data.Contains(Selected)) Selected = null;
            Paint();
        }

        public void Select(object data)
        {
            Selected = data;
            Paint();
            SelectionChanged?.Invoke(data);
        }

        void Paint()
        {
            for (int i = 0; i < _rowBgs.Count; i++)
                if (_rowBgs[i]) _rowBgs[i].color = Equals(_data[i], Selected) ? ToolStyle.Light(_accent, 0.82f) : Color.white;
        }
    }
}
