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
    /// 自腹で買うツール・購買システムの見た目。1998年ごろの市販ソフト（ウイルス対策ソフト・ユーティリティ）風:
    /// 製品ロゴ入りのグラデーションの見出し、溝の線で囲んだグループ枠、へこんだ白い一覧、立体のボタン、黄色い注意書き。
    /// 灰色一色の社内業務システムよりも、何をするソフトか・どこを押すかが分かりやすい。
    /// </summary>
    public static class ToolStyle
    {
        public static readonly Color Bg = RetroTheme.Face;
        public static readonly Color CardColor = Color.white;
        public static readonly Color Border = RetroTheme.Shadow;
        public static readonly Color Ink = Color.black;
        public static readonly Color Muted = new Color(0.32f, 0.32f, 0.36f);
        public static readonly Color Disabled = RetroTheme.Face;

        // 製品ごとの色（見出しのグラデーション）
        public static readonly Color Teal = new Color(0.00f, 0.40f, 0.45f);
        public static readonly Color Green = new Color(0.00f, 0.42f, 0.16f);
        public static readonly Color Indigo = new Color(0.16f, 0.20f, 0.55f);
        public static readonly Color Red = new Color(0.58f, 0.06f, 0.05f);
        public static readonly Color Orange = new Color(0.72f, 0.36f, 0.00f);
        public static readonly Color Purple = new Color(0.36f, 0.14f, 0.48f);
        public static readonly Color Slate = new Color(0.14f, 0.24f, 0.36f);
        public static readonly Color Navy = new Color(0.00f, 0.14f, 0.45f);
        public static readonly Color Violet = new Color(0.24f, 0.08f, 0.36f);

        public const float HeaderHeight = 56;
        public const float Pad = 10;

        /// <summary>へこんだ白い枠（一覧・値の欄）</summary>
        public static Sprite Round => RetroTheme.Field;
        /// <summary>溝の線のグループ枠</summary>
        public static Sprite Outline => RetroTheme.Etched;

        public static Color Light(Color c, float k = 0.85f) => Color.Lerp(c, Color.white, k);

        public static Color ToneColor(Tone t)
        {
            switch (t)
            {
                case Tone.Info: return new Color(0.00f, 0.15f, 0.60f);
                case Tone.Ok: return new Color(0.00f, 0.45f, 0.10f);
                case Tone.Warn: return new Color(0.62f, 0.38f, 0.00f);
                case Tone.Bad: return new Color(0.70f, 0.00f, 0.00f);
                default: return new Color(0.25f, 0.25f, 0.28f);
            }
        }

        // ---------------- 文字 ----------------
        public static Text Text(Transform p, string s, int size = 12, Color? color = null, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool mono = false)
        {
            var t = UIFactory.Label(p, s, size, anchor, color ?? Ink, mono);
            if (bold) t.fontStyle = FontStyle.Bold;
            t.lineSpacing = 1.05f;
            return t;
        }

        public static Text Text(Transform p, string s, float x, float y, float w, float h, int size = 12, Color? color = null, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool mono = false)
        {
            var t = Text(p, s, size, color, anchor, bold, mono);
            t.rectTransform.Place(x, y, w, h);
            return t;
        }

        // ---------------- 画面の土台・見出し ----------------
        /// <summary>ウィンドウの中身を灰色の土台にする。中身を置く RectTransform を返す</summary>
        public static RectTransform Page(RetroWindow w)
        {
            var bg = UIFactory.Solid(w.Content, Bg, "ToolPage");
            bg.rectTransform.Stretch(-2, -2, -2, -2);
            return bg.rectTransform;
        }

        /// <summary>製品の見出し（グラデーションの帯・ロゴ・名前・「何をするソフトか」）。下に続く中身の y を返す</summary>
        public static float Header(Transform p, string title, string tagline, string icon, Color accent, bool running = true)
        {
            var band = UIFactory.Image(p, RetroTheme.GradientSprite(accent, Color.Lerp(accent, Color.white, 0.5f)), Color.white, "Header");
            band.rectTransform.StretchH(0, HeaderHeight);
            // ロゴ（立体の枠の中のアイコン）
            var logo = UIFactory.Image(band.transform, RetroTheme.Raised, Color.white, "Logo");
            logo.rectTransform.Place(10, 8, 40, 40);
            var ic = UIFactory.Image(logo.transform, RetroTheme.BigIcon(icon), Color.white, "Icon");
            ic.rectTransform.Place(4, 4, 32, 32);
            var t = Text(band.transform, title, 60, 7, 600, 24, 17, Color.white, TextAnchor.MiddleLeft, true);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.6f);
            sh.effectDistance = new Vector2(1, -1);
            var tag = Text(band.transform, tagline, 60, 31, 660, 18, 11, new Color(1, 1, 1, 0.92f), TextAnchor.MiddleLeft);
            var sh2 = tag.gameObject.AddComponent<Shadow>();
            sh2.effectColor = new Color(0, 0, 0, 0.35f);
            sh2.effectDistance = new Vector2(1, -1);
            // 動作中のランプ（ソフトが常駐して動いている）
            if (running)
            {
                var led = ToolLed.CreateRight(band.transform, 14, 22, 12);
                led.Set(ToolFx.LedGreen, 0.6f);
                var lt = Text(band.transform, "動作中", 11, Color.white, TextAnchor.MiddleRight, true);
                lt.rectTransform.AnchorTopRight(30, 19, 60, 18);
            }
            // 見出しの下の溝
            var a = UIFactory.Solid(p, RetroTheme.Shadow, "HeaderRule");
            a.rectTransform.StretchH(HeaderHeight, 1);
            var b = UIFactory.Solid(p, RetroTheme.Highlight, "HeaderRule2");
            b.rectTransform.StretchH(HeaderHeight + 1, 1);
            return HeaderHeight + Pad;
        }

        // ---------------- グループ枠 ----------------
        /// <summary>グループ枠。中身を置く RectTransform（余白・見出しの分を除いた場所）を返す</summary>
        public static RectTransform Card(Transform p, float x, float y, float w, float h, string title = null)
        {
            var img = UIFactory.Image(p, Outline, Color.white, "Group");
            img.rectTransform.Place(x, y, w, h);
            return CardInner(img.rectTransform, title);
        }

        /// <summary>親に合わせて伸びるグループ枠（左・上・右・下の位置）</summary>
        public static RectTransform CardStretch(Transform p, float left, float top, float right, float bottom, string title = null)
        {
            var img = UIFactory.Image(p, Outline, Color.white, "Group");
            img.rectTransform.Stretch(left, top, right, bottom);
            return CardInner(img.rectTransform, title);
        }

        /// <summary>
        /// 横は親の割合（a0〜a1）、縦は上から top の位置に高さ height のグループ枠。height &lt;= 0 なら下端（bottomInset）まで伸ばす。
        /// </summary>
        public static RectTransform Box(Transform p, float a0, float a1, float top, float height, string title = null, float bottomInset = Pad)
        {
            var img = UIFactory.Image(p, Outline, Color.white, "Group");
            var rt = img.rectTransform;
            float l = a0 <= 0 ? Pad : Pad / 2, r = a1 >= 1 ? Pad : Pad / 2;
            float t = string.IsNullOrEmpty(title) ? 0 : 7; // 見出しの文字が枠線の上にかかる分
            rt.pivot = new Vector2(0, 1);
            if (height > 0)
            {
                rt.anchorMin = new Vector2(a0, 1); rt.anchorMax = new Vector2(a1, 1);
                rt.offsetMin = new Vector2(l, -top - height); rt.offsetMax = new Vector2(-r, -top - t);
            }
            else
            {
                rt.anchorMin = new Vector2(a0, 0); rt.anchorMax = new Vector2(a1, 1);
                rt.offsetMin = new Vector2(l, bottomInset); rt.offsetMax = new Vector2(-r, -top - t);
            }
            return CardInner(rt, title);
        }

        /// <summary>下端から bottom の位置に、高さ height のグループ枠（横は a0〜a1）</summary>
        public static RectTransform BoxBottom(Transform p, float a0, float a1, float bottom, float height, string title = null)
        {
            var img = UIFactory.Image(p, Outline, Color.white, "Group");
            var rt = img.rectTransform;
            float l = a0 <= 0 ? Pad : Pad / 2, r = a1 >= 1 ? Pad : Pad / 2;
            rt.anchorMin = new Vector2(a0, 0); rt.anchorMax = new Vector2(a1, 0);
            rt.pivot = new Vector2(0, 0);
            rt.offsetMin = new Vector2(l, bottom); rt.offsetMax = new Vector2(-r, bottom + height - (string.IsNullOrEmpty(title) ? 0 : 7));
            return CardInner(rt, title);
        }

        static RectTransform CardInner(RectTransform box, string title)
        {
            float top = 10;
            if (!string.IsNullOrEmpty(title))
            {
                // 見出しの文字を枠線の上に重ねる（98 のグループボックス）
                var bg = UIFactory.Solid(box, Bg, "TitleBg");
                bg.raycastTarget = false;
                var t = Text(bg.transform, " " + title + " ", 12, RetroTheme.TextBlue, TextAnchor.MiddleLeft, true);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                bg.rectTransform.Place(8, -7, t.preferredWidth + 2, 14);
                t.rectTransform.Stretch();
                top = 14;
            }
            return UIFactory.Rect("Inner", box).Stretch(10, top, 10, 10);
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

        /// <summary>状態の欄（へこんだ白い枠に、項目・値・バッジ）。値の Text を返す</summary>
        public static Text Tile(Transform p, int i, int n, float y, float h, string label, out RectTransform tile, string icon = null)
        {
            var bg = UIFactory.Image(p, RetroTheme.Field, Color.white, "Tile");
            Cell(bg.rectTransform, i, n, y, h);
            tile = bg.rectTransform;
            Text(bg.transform, label, 8, 5, 400, 16, 11, Muted, TextAnchor.MiddleLeft);
            float left = 8;
            if (icon != null)
            {
                var ic = UIFactory.Image(bg.transform, RetroTheme.Icon(icon), Color.white, "Icon");
                ic.raycastTarget = false;
                ic.rectTransform.Place(8, 27, 16, 16);
                left = 28;
            }
            var v = Text(bg.transform, "", 12, Ink, TextAnchor.MiddleLeft, true);
            v.rectTransform.StretchH(24, 22, left, 6);
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            return v;
        }

        // ---------------- スクロール ----------------
        /// <summary>縦にスクロールする領域（98 のスクロールバー）。sunken = へこんだ白い枠。中身の content を返す</summary>
        public static RectTransform ScrollArea(Transform parent, out ScrollRect sr, bool sunken = false)
        {
            var root = UIFactory.Rect("ScrollArea", parent).Stretch();
            var bg = root.gameObject.AddComponent<Image>();
            if (sunken) { bg.sprite = RetroTheme.Field; bg.type = Image.Type.Sliced; bg.color = Color.white; }
            else bg.color = new Color(0, 0, 0, 0);
            sr = root.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 20f;
            sr.inertia = false;
            float inset = sunken ? 2 : 0;
            var viewport = UIFactory.Rect("Viewport", root).Stretch(inset, inset, 16 + inset, inset);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);
            sr.viewport = viewport;
            var content = UIFactory.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0, 1);
            content.sizeDelta = Vector2.zero;
            sr.content = content;
            var sb = UIFactory.VScrollbar(root);
            var srt = (RectTransform)sb.transform;
            srt.anchorMin = new Vector2(1, 0); srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(1, 1);
            srt.offsetMin = new Vector2(-16 - inset, inset); srt.offsetMax = new Vector2(-inset, -inset);
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return content;
        }

        /// <summary>スクロールする長い文（へこんだ白い枠・リッチテキスト）</summary>
        public static Text ScrollText(Transform parent, int size = 12, bool mono = false)
        {
            var content = ScrollArea(parent, out _, true);
            var vl = UIFactory.VerticalList(content, 0, 0);
            vl.padding = new RectOffset(6, 6, 4, 8);
            var t = Text(content, "", size, Ink, TextAnchor.UpperLeft, false, mono);
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.15f;
            return t;
        }

        // ---------------- バッジ ----------------
        static float TextWidth(string s, int size)
        {
            float w = 0;
            foreach (char ch in s) w += ch < 0x80 ? size * 0.6f : size * 1.0f;
            return w;
        }

        /// <summary>状態の表示（へこんだ小さな枠に色付きの文字）。左上 (x, y) に置く</summary>
        public static RectTransform Badge(Transform p, string text, Tone tone, float x, float y)
        {
            var rt = MakeBadge(p, text, tone);
            rt.Place(x, y, rt.sizeDelta.x, 18);
            return rt;
        }

        /// <summary>右寄せの状態表示</summary>
        public static RectTransform BadgeRight(Transform p, string text, Tone tone, float right, float y)
        {
            var rt = MakeBadge(p, text, tone);
            rt.AnchorTopRight(right, y, rt.sizeDelta.x, 18);
            return rt;
        }

        static RectTransform MakeBadge(Transform p, string text, Tone tone)
        {
            var img = UIFactory.Image(p, RetroTheme.ThinSunken, Color.white, "Badge");
            img.raycastTarget = false;
            var t = Text(img.transform, text, 11, ToneColor(tone), TextAnchor.MiddleCenter, true);
            t.rectTransform.Stretch();
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            img.rectTransform.sizeDelta = new Vector2(TextWidth(text, 11) + 14, 18);
            return img.rectTransform;
        }

        // ---------------- ボタン ----------------
        /// <summary>立体のボタン。primary = 既定のボタン（太字・黒い外枠。そのソフトでいちばん押すボタン）</summary>
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
            f.textComponent.fontSize = 13;
            ((RectTransform)f.transform).Place(x, y, w, h);
            return f;
        }

        // ---------------- 項目と値 ----------------
        /// <summary>「項目:　値」の1行。値の Text を返す</summary>
        public static Text Field(Transform p, string key, float x, float y, float w, float keyWidth = 96, bool mono = false)
        {
            Text(p, key + ":", x, y, keyWidth, 20, 12, Muted, TextAnchor.MiddleLeft);
            var v = Text(p, "", x + keyWidth, y, w - keyWidth, 20, 13, Ink, TextAnchor.MiddleLeft, true, mono);
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            return v;
        }

        /// <summary>大きな数字の欄（例: 該当 12 件）。数字の Text を返す</summary>
        public static Text Stat(Transform p, string label, float x, float y, float w, float h, Color accent)
        {
            var bg = UIFactory.Image(p, RetroTheme.Field, Color.white, "Stat");
            bg.rectTransform.Place(x, y, w, h);
            Text(bg.transform, label, 8, 4, w - 16, 16, 11, Muted, TextAnchor.MiddleLeft);
            return Text(bg.transform, "-", 8, 20, w - 16, h - 24, 20, accent, TextAnchor.MiddleLeft, true);
        }

        /// <summary>進行状況のバー（98 の青いブロック）。塗りの Image（fillAmount で操作）を返す</summary>
        public static Image Progress(Transform p, float x, float y, float w, float h, Color accent)
        {
            var bg = UIFactory.Image(p, RetroTheme.Field, Color.white, "Progress");
            bg.rectTransform.Place(x, y, w, h);
            var fill = UIFactory.Image(bg.transform, Blocks, Color.white, "Fill");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0;
            fill.rectTransform.Stretch(3, 3, 3, 3);
            return fill;
        }

        static Sprite _blocks;
        /// <summary>98 の進行状況バーの、すき間のある青いブロック（横に並べた絵）</summary>
        static Sprite Blocks
        {
            get
            {
                if (_blocks) return _blocks;
                const int n = 40, h = 8, block = 7;
                var t = new Texture2D(n * (block + 2), h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color[t.width * h];
                for (int x = 0; x < t.width; x++)
                for (int y = 0; y < h; y++)
                    px[y * t.width + x] = x % (block + 2) < block ? RetroTheme.Selection : new Color(0, 0, 0, 0);
                t.SetPixels(px);
                t.Apply();
                _blocks = Sprite.Create(t, new Rect(0, 0, t.width, h), new Vector2(0.5f, 0.5f), 100f);
                return _blocks;
            }
        }

        /// <summary>まだ何も選んでいないときの案内（i のアイコンと手順）</summary>
        public static GameObject EmptyState(Transform p, string title, params string[] steps)
        {
            var root = UIFactory.Rect("Empty", p).Stretch();
            var box = UIFactory.Image(root, RetroTheme.Field, Color.white, "Box");
            box.rectTransform.Stretch(Pad, 0, Pad, Pad);
            var ic = UIFactory.Image(box.transform, RetroTheme.BigIcon("info"), Color.white, "Info");
            ic.rectTransform.Place(16, 16, 32, 32);
            Text(box.transform, title, 14, Ink, TextAnchor.MiddleLeft, true).rectTransform.StretchH(20, 24, 60, 10);
            float y = 56;
            for (int i = 0; i < steps.Length; i++)
            {
                var t = Text(box.transform, $"{i + 1}. {steps[i]}", 12, Ink, TextAnchor.MiddleLeft);
                t.rectTransform.StretchH(y, 20, 60, 10);
                y += 24;
            }
            return root.gameObject;
        }

        /// <summary>注意書き（黄色いツールチップ風の枠・アイコン・文字）。文字の親（＝枠）は呼び出し側で広げられる</summary>
        public static Text Banner(Transform p, string text, Tone tone, float x, float y, float w, float h)
        {
            var frame = UIFactory.Solid(p, Color.black, "Banner");
            frame.rectTransform.Place(x, y, w, h);
            var bg = UIFactory.Solid(frame.transform, RetroTheme.Tooltip, "Fill");
            bg.rectTransform.Stretch(1, 1, 1, 1);
            var ic = UIFactory.Image(frame.transform, RetroTheme.Icon(tone == Tone.Info || tone == Tone.Ok ? "info" : "warning"), Color.white, "Icon");
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0, 0.5f);
            ic.rectTransform.pivot = new Vector2(0, 0.5f);
            ic.rectTransform.sizeDelta = new Vector2(16, 16);
            ic.rectTransform.anchoredPosition = new Vector2(7, 0);
            var t = Text(frame.transform, text, 12, tone == Tone.Bad ? ToneColor(Tone.Bad) : Ink, TextAnchor.MiddleLeft);
            t.rectTransform.Stretch(29, 2, 6, 2);
            return t;
        }
    }

    /// <summary>98 の立体ボタン（押すとへこむ）。primary は既定のボタン（太字・黒い外枠）</summary>
    public class FlatButton : MonoBehaviour, IPointerClickHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Image Bg;
        public Text Label;
        public Action OnClick;
        Image _frame;
        Shadow _emboss;
        bool _primary, _down, _latched, _interactable = true;
        public RectTransform Rect => (RectTransform)transform;

        public static FlatButton Create(Transform p, string text, Action onClick, Color accent, bool primary)
        {
            // 外枠（既定のボタンだけ黒く見せる）→ 立体の面
            var frame = UIFactory.Image(p, RetroTheme.White, new Color(0, 0, 0, 0), "Button");
            var b = frame.gameObject.AddComponent<FlatButton>();
            b._frame = frame;
            b.Bg = UIFactory.Image(frame.transform, RetroTheme.Raised, Color.white, "Face");
            b.Bg.rectTransform.Stretch(1, 1, 1, 1);
            b.Bg.raycastTarget = false;
            b._primary = primary;
            b.OnClick = onClick;
            b.Label = ToolStyle.Text(b.Bg.transform, text, 12, Color.black, TextAnchor.MiddleCenter, primary);
            b.Label.rectTransform.Stretch(6, 0, 6, 0);
            b._emboss = b.Label.gameObject.AddComponent<Shadow>();
            b._emboss.effectColor = Color.white;
            b._emboss.effectDistance = new Vector2(1, -1);
            b.Paint();
            return b;
        }

        public bool Interactable
        {
            get => _interactable;
            set { _interactable = value; Paint(); }
        }

        public void SetText(string s) { if (Label) Label.text = s; }

        /// <summary>既定のボタンかどうかを切り替える（色の引数は互換のため。98 のボタンは色を付けない）</summary>
        public void SetAccent(Color c, bool primary) { _primary = primary; Paint(); }

        /// <summary>押し込まれたままの表示（絞り込みの切り替えなど）</summary>
        public void SetLatched(bool v) { _latched = v; Paint(); }

        void Paint()
        {
            if (!Bg) return;
            bool pressed = _down || _latched;
            Bg.sprite = pressed ? RetroTheme.Pressed : RetroTheme.Raised;
            _frame.color = _primary && _interactable ? Color.black : new Color(0, 0, 0, 0);
            Label.fontStyle = _primary || _latched ? FontStyle.Bold : FontStyle.Normal;
            Label.color = _interactable ? Color.black : RetroTheme.TextDisabled;
            _emboss.enabled = !_interactable; // 使えないボタンは文字が彫り込まれたように見せる
            Label.rectTransform.Stretch(pressed ? 7 : 6, pressed ? 1 : 0, pressed ? 5 : 6, pressed ? -1 : 0);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (!_interactable) { AudioFx.Error(); return; }
            AudioFx.Click();
            OnClick?.Invoke();
        }

        public void OnPointerExit(PointerEventData e) { _down = false; Paint(); }
        public void OnPointerDown(PointerEventData e) { if (_interactable) { _down = true; Paint(); } }
        public void OnPointerUp(PointerEventData e) { _down = false; Paint(); }
    }

    /// <summary>
    /// へこんだ白い一覧。1件 2行（主な内容・補足）＋右に状態。クリックで選ぶ（選んだ行は紺色）。
    /// </summary>
    public class ToolList : MonoBehaviour
    {
        public struct Row
        {
            public string Title, Sub, Badge, Icon;
            public Tone Tone;
            public bool Blink;   // 状態の表示を点滅させる（NEW など）
            public Row(string title, string sub, string badge = null, Tone tone = Tone.Neutral, string icon = null)
            { Title = title; Sub = sub; Badge = badge; Tone = tone; Icon = icon; Blink = false; }
        }

        public const float RowHeight = 38;
        public object Selected { get; private set; }
        public event Action<object> SelectionChanged;
        RectTransform _content;
        Text _empty;
        string _signature;
        readonly List<object> _data = new List<object>();
        readonly List<Image> _rowBgs = new List<Image>();
        readonly List<Text> _titles = new List<Text>(), _subs = new List<Text>();

        public static ToolList Create(Transform parent, Color accent, string emptyText)
        {
            var content = ToolStyle.ScrollArea(parent, out var sr, true);
            var root = (RectTransform)sr.transform;
            var list = root.gameObject.AddComponent<ToolList>();
            list._content = content;
            list._empty = ToolStyle.Text(root, emptyText, 12, ToolStyle.Muted, TextAnchor.MiddleCenter);
            list._empty.rectTransform.Stretch(10, 10, 26, 10);
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
            _rowBgs.Clear(); _titles.Clear(); _subs.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                int idx = i;
                var bg = UIFactory.Solid(_content, Color.white, "Row");
                bg.rectTransform.anchorMin = new Vector2(0, 1); bg.rectTransform.anchorMax = new Vector2(1, 1);
                bg.rectTransform.pivot = new Vector2(0, 1);
                bg.rectTransform.offsetMin = new Vector2(0, -(i + 1) * RowHeight);
                bg.rectTransform.offsetMax = new Vector2(0, -i * RowHeight);
                _rowBgs.Add(bg);
                var line = UIFactory.Solid(bg.transform, new Color(0.85f, 0.85f, 0.85f), "Line");
                line.raycastTarget = false;
                line.rectTransform.anchorMin = new Vector2(0, 0); line.rectTransform.anchorMax = new Vector2(1, 0);
                line.rectTransform.offsetMin = new Vector2(0, 0); line.rectTransform.offsetMax = new Vector2(0, 1);
                float left = 6;
                if (!string.IsNullOrEmpty(r.Icon))
                {
                    var ic = UIFactory.Image(bg.transform, RetroTheme.Icon(r.Icon), Color.white, "Icon");
                    ic.raycastTarget = false;
                    ic.rectTransform.Place(6, 11, 16, 16);
                    left = 28;
                }
                var title = ToolStyle.Text(bg.transform, r.Title, 12, Color.black, TextAnchor.MiddleLeft, true);
                title.rectTransform.StretchH(3, 18, left, 110);
                title.raycastTarget = false;
                EllipsisText.Attach(title);
                var sub = ToolStyle.Text(bg.transform, r.Sub ?? "", 11, ToolStyle.Muted, TextAnchor.MiddleLeft);
                sub.rectTransform.StretchH(20, 16, left, 110);
                sub.raycastTarget = false;
                EllipsisText.Attach(sub);
                _titles.Add(title); _subs.Add(sub);
                if (!string.IsNullOrEmpty(r.Badge))
                {
                    var badge = ToolStyle.BadgeRight(bg.transform, r.Badge, r.Tone, 6, 10);
                    if (r.Blink) badge.gameObject.AddComponent<ToolBlink>();
                }
                var click = bg.gameObject.AddComponent<ClickHandler>();
                click.OnClick = () => Select(_data[idx]);
            }
            _content.sizeDelta = new Vector2(0, rows.Count * RowHeight);
            _empty.gameObject.SetActive(rows.Count == 0);
            Paint();
            // 選んでいたものが一覧から消えたら、選択を外したことを知らせる（連動する詳細・ボタンも解除）
            if (Selected != null && !_data.Contains(Selected)) Select(null);
        }

        /// <summary>選ぶ。一覧にないものは選べない（null として扱う）</summary>
        public void Select(object data)
        {
            if (data != null && !_data.Contains(data)) data = null;
            Selected = data;
            Paint();
            SelectionChanged?.Invoke(data);
        }

        void Paint()
        {
            for (int i = 0; i < _rowBgs.Count; i++)
            {
                if (!_rowBgs[i]) continue;
                bool sel = Equals(_data[i], Selected);
                _rowBgs[i].color = sel ? RetroTheme.Selection : Color.white;
                _titles[i].color = sel ? Color.white : Color.black;
                _subs[i].color = sel ? new Color(0.85f, 0.88f, 1f) : ToolStyle.Muted;
            }
        }
    }
}
