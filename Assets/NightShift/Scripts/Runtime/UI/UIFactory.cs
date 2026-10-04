using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>旧式業務ソフト風ウィジェットの生成ヘルパー。座標は親の左上原点・ピクセル指定（VBフォーム風）。</summary>
    public static class UIFactory
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>親いっぱいに広げる（左・上・右・下のインセット）</summary>
        public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0, 1);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>横方向に広げ、上から y の位置に高さ h で置く</summary>
        public static RectTransform StretchH(this RectTransform rt, float y, float h, float left = 0, float right = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.offsetMin = new Vector2(left, -y - h);
            rt.offsetMax = new Vector2(-right, -y);
            return rt;
        }

        public static RectTransform AnchorBottomRight(this RectTransform rt, float right, float bottom, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-right, bottom);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform AnchorTopRight(this RectTransform rt, float right, float top, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-right, -top);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static Image Image(Transform parent, Sprite sprite, Color color, string name = "Image")
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image Panel(Transform parent, Sprite sprite = null, string name = "Panel")
        {
            return Image(parent, sprite ?? RetroTheme.Raised, Color.white, name);
        }

        public static Image Solid(Transform parent, Color c, string name = "Solid")
        {
            var img = Image(parent, RetroTheme.White, c, name);
            return img;
        }

        public static Text Label(Transform parent, string text, int size = RetroTheme.FontSize, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null, bool mono = false)
        {
            var rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = mono ? RetroTheme.MonoFont : RetroTheme.UIFont;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? RetroTheme.Text;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Text Label(Transform parent, string text, float x, float y, float w, float h, int size = RetroTheme.FontSize, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var t = Label(parent, text, size, anchor);
            t.rectTransform.Place(x, y, w, h);
            return t;
        }

        public static Text Label(Transform parent, string text, float x, float y, float w, float h, int size, TextAnchor anchor, Color? color, bool mono = false)
        {
            var t = Label(parent, text, size, anchor, color, mono);
            t.rectTransform.Place(x, y, w, h);
            return t;
        }

        // ---------------- Button ----------------
        public static RetroButton Button(Transform parent, string label, Action onClick, string icon = null)
        {
            var img = Image(parent, RetroTheme.Raised, Color.white, "Button");
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.SpriteSwap;
            btn.spriteState = new SpriteState { pressedSprite = RetroTheme.Pressed, selectedSprite = RetroTheme.Raised, highlightedSprite = RetroTheme.Raised, disabledSprite = RetroTheme.Raised };
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            btn.targetGraphic = img;
            var rb = img.gameObject.AddComponent<RetroButton>();
            rb.Button = btn;
            rb.Background = img;

            var t = Label(img.transform, label, RetroTheme.FontSize, TextAnchor.MiddleCenter);
            t.rectTransform.Stretch(2, 2, 2, 2);
            rb.Label = t;
            if (!string.IsNullOrEmpty(icon))
            {
                var ic = Image(img.transform, RetroTheme.Icon(icon), Color.white, "Icon");
                ic.raycastTarget = false;
                ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0, 0.5f);
                ic.rectTransform.pivot = new Vector2(0, 0.5f);
                ic.rectTransform.anchoredPosition = new Vector2(6, 0);
                ic.rectTransform.sizeDelta = new Vector2(16, 16);
                rb.Icon = ic;
                t.rectTransform.Stretch(24, 2, 4, 2);
            }
            if (onClick != null) btn.onClick.AddListener(() => { AudioFx.Click(); onClick(); });
            return rb;
        }

        public static RetroButton Button(Transform parent, string label, float x, float y, float w, float h, Action onClick, string icon = null)
        {
            var b = Button(parent, label, onClick, icon);
            b.Rect.Place(x, y, w, h);
            return b;
        }

        /// <summary>平らなツールバー／リスト用ボタン（ホバーで浮き上がる）</summary>
        public static RetroButton FlatButton(Transform parent, string label, Action onClick, string icon = null)
        {
            var b = Button(parent, label, onClick, icon);
            b.Background.color = new Color(1, 1, 1, 0);
            b.gameObject.AddComponent<FlatHover>().Target = b.Background;
            return b;
        }

        // ---------------- Input ----------------
        public static InputField Input(Transform parent, string initial = "", bool password = false, bool mono = false)
        {
            var img = Image(parent, RetroTheme.Field, Color.white, "Input");
            var field = img.gameObject.AddComponent<InputField>();
            field.targetGraphic = img;
            field.transition = Selectable.Transition.None;
            var nav = field.navigation; nav.mode = Navigation.Mode.None; field.navigation = nav;
            var text = Label(img.transform, "", RetroTheme.FontSize, TextAnchor.MiddleLeft, null, mono);
            text.rectTransform.Stretch(4, 2, 4, 2);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            field.textComponent = text;
            field.caretColor = Color.black;
            field.customCaretColor = true;
            field.selectionColor = new Color(RetroTheme.Selection.r, RetroTheme.Selection.g, RetroTheme.Selection.b, 0.45f);
            field.caretWidth = 1;
            field.caretBlinkRate = 1.06f;
            if (password) field.contentType = InputField.ContentType.Password;
            field.text = initial;
            return field;
        }

        public static InputField Input(Transform parent, float x, float y, float w, float h = 20, string initial = "")
        {
            var f = Input(parent, initial);
            ((RectTransform)f.transform).Place(x, y, w, h);
            return f;
        }

        /// <summary>読み取り専用の値表示欄（凹んだ灰色の箱）</summary>
        public static Text ValueBox(Transform parent, float x, float y, float w, float h = 20, string text = "", bool white = true)
        {
            var img = Image(parent, RetroTheme.Field, white ? Color.white : RetroTheme.FieldReadOnly, "Value");
            img.rectTransform.Place(x, y, w, h);
            var t = Label(img.transform, text, RetroTheme.FontSize, TextAnchor.MiddleLeft);
            t.rectTransform.Stretch(4, 2, 4, 2);
            if (h <= 26) EllipsisText.Attach(t); // 1行の欄は、はみ出したら「…」で省略して全文はツールチップ
            return t;
        }

        public static Text ThinValue(Transform parent, string text = "")
        {
            var img = Image(parent, RetroTheme.ThinSunken, Color.white, "Cell");
            var t = Label(img.transform, text, RetroTheme.FontSize, TextAnchor.MiddleLeft);
            t.rectTransform.Stretch(4, 1, 4, 1);
            return t;
        }

        // ---------------- Group ----------------
        public static RectTransform Group(Transform parent, string title, float x, float y, float w, float h)
        {
            var root = Rect("Group:" + title, parent).Place(x, y, w, h);
            var box = Image(root, RetroTheme.Etched, Color.white, "Etched");
            box.raycastTarget = false;
            box.rectTransform.Stretch(0, 7, 0, 0);
            if (!string.IsNullOrEmpty(title))
            {
                var bg = Solid(root, RetroTheme.Face, "TitleBg");
                bg.raycastTarget = false;
                var t = Label(bg.transform, " " + title + " ", RetroTheme.FontSize, TextAnchor.MiddleLeft, RetroTheme.TextBlue);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                float tw = t.preferredWidth + 2;
                bg.rectTransform.Place(8, 0, tw, 14);
                t.rectTransform.Stretch();
            }
            var content = Rect("Content", root).Stretch(8, 18, 8, 8);
            return content;
        }

        // ---------------- Check / Radio ----------------
        public static RetroToggle Check(Transform parent, string label, bool value, Action<bool> onChanged, bool radio = false)
        {
            var root = Rect(radio ? "Radio" : "Check", parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var box = Image(root, RetroTheme.Field, Color.white, "Box");
            box.raycastTarget = false;
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0, 0.5f);
            box.rectTransform.pivot = new Vector2(0, 0.5f);
            box.rectTransform.anchoredPosition = new Vector2(0, 0);
            box.rectTransform.sizeDelta = new Vector2(13, 13);
            var mark = Image(box.transform, radio ? null : RetroTheme.Icon("check"), radio ? Color.black : Color.white, "Mark");
            mark.raycastTarget = false;
            if (radio) { mark.sprite = RetroTheme.White; mark.rectTransform.Stretch(4, 4, 4, 4); }
            else mark.rectTransform.Stretch(-1, -1, -1, -1);
            var t = Label(root, label);
            t.rectTransform.Stretch(18, 0, 0, 0);
            var tg = root.gameObject.AddComponent<RetroToggle>();
            tg.Mark = mark;
            tg.LabelText = t;
            tg.Changed = onChanged;
            tg.SetValue(value, false);
            return tg;
        }

        // ---------------- Scroll ----------------
        public static ScrollRect Scroll(Transform parent, out RectTransform content, bool white = true, bool horizontal = false)
        {
            var bg = Image(parent, RetroTheme.Field, white ? Color.white : RetroTheme.Face, "Scroll");
            var sr = bg.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = horizontal;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 18f;
            sr.inertia = false;

            var viewport = Rect("Viewport", bg.transform).Stretch(2, 2, 18, 2);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);
            sr.viewport = viewport;

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 0);
            sr.content = content;

            var sb = VScrollbar(bg.transform);
            ((RectTransform)sb.transform).anchorMin = new Vector2(1, 0);
            ((RectTransform)sb.transform).anchorMax = new Vector2(1, 1);
            ((RectTransform)sb.transform).pivot = new Vector2(1, 1);
            ((RectTransform)sb.transform).offsetMin = new Vector2(-18, 2);
            ((RectTransform)sb.transform).offsetMax = new Vector2(-2, -2);
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return sr;
        }

        public static Scrollbar VScrollbar(Transform parent)
        {
            var bg = Image(parent, RetroTheme.Checker, Color.white, "Scrollbar");
            bg.type = UnityEngine.UI.Image.Type.Tiled;
            var sb = bg.gameObject.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            var area = Rect("Sliding", bg.transform).Stretch(0, 0, 0, 0);
            var handle = Image(area, RetroTheme.Raised, Color.white, "Handle");
            handle.rectTransform.offsetMin = Vector2.zero;
            handle.rectTransform.offsetMax = Vector2.zero;
            sb.handleRect = handle.rectTransform;
            sb.targetGraphic = handle;
            sb.transition = Selectable.Transition.None;
            var nav = sb.navigation; nav.mode = Navigation.Mode.None; sb.navigation = nav;
            return sb;
        }

        /// <summary>
        /// 長文表示用のスクロール領域。本文の高さはレイアウトが自動計算するので、文字が詰まったり見切れたりしない。
        /// </summary>
        public static Text ScrollText(Transform parent, string text, out ScrollRect scroll)
        {
            scroll = Scroll(parent, out var content, true);
            var vl = VerticalList(content, 0, 12);
            vl.padding = new RectOffset(14, 14, 10, 16);
            var t = Label(content, text, RetroTheme.ReadingFontSize, TextAnchor.UpperLeft, new Color(0.1f, 0.1f, 0.12f));
            t.font = RetroTheme.ReadingFont;
            t.lineSpacing = 1.15f;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        /// <summary>コンテンツ高さを子要素に合わせる縦並びレイアウト</summary>
        public static VerticalLayoutGroup VerticalList(RectTransform content, float spacing = 0, int padding = 0)
        {
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childControlHeight = true;
            vl.childControlWidth = true;
            vl.childForceExpandHeight = false;
            vl.childForceExpandWidth = true;
            vl.spacing = spacing;
            vl.padding = new RectOffset(padding, padding, padding, padding);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return vl;
        }

        public static void SetHeight(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minHeight = h;
            le.preferredHeight = h;
        }

        /// <summary>水平の区切り線（凹み）</summary>
        public static void Separator(Transform parent, float x, float y, float w)
        {
            var a = Solid(parent, RetroTheme.Shadow, "Sep");
            a.rectTransform.Place(x, y, w, 1);
            var b = Solid(parent, RetroTheme.Highlight, "Sep2");
            b.rectTransform.Place(x, y + 1, w, 1);
        }

        public static Image IconImage(Transform parent, string icon, float x, float y, float size = 16)
        {
            var img = Image(parent, RetroTheme.Icon(icon), Color.white, "Icon:" + icon);
            img.rectTransform.Place(x, y, size, size);
            img.raycastTarget = false;
            return img;
        }
    }

    public class RetroButton : MonoBehaviour
    {
        public Button Button;
        public Image Background;
        public Text Label;
        public Image Icon;
        public RectTransform Rect => (RectTransform)transform;

        public bool Interactable
        {
            get => Button.interactable;
            set
            {
                Button.interactable = value;
                Label.color = value ? RetroTheme.Text : RetroTheme.TextDisabled;
                if (Icon != null) Icon.color = value ? Color.white : new Color(1, 1, 1, 0.35f);
            }
        }

        public void SetText(string s) => Label.text = s;
        public void Click() { if (Button.interactable) Button.onClick.Invoke(); }

        /// <summary>押されたままの見た目（タスクバー等）</summary>
        public void SetLatched(bool latched)
        {
            Background.sprite = latched ? RetroTheme.Pressed : RetroTheme.Raised;
            var st = Button.spriteState;
            st.highlightedSprite = st.selectedSprite = Background.sprite;
            Button.spriteState = st;
        }
    }

    public class RetroToggle : MonoBehaviour, IPointerClickHandler
    {
        public Image Mark;
        public Text LabelText;
        public Action<bool> Changed;
        public bool Value { get; private set; }
        public bool Interactable = true;
        public RetroToggle[] Group;

        public void SetValue(bool v, bool notify = true)
        {
            Value = v;
            Mark.enabled = v;
            if (notify) Changed?.Invoke(v);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactable) return;
            AudioFx.Click();
            if (Group != null)
            {
                foreach (var g in Group) if (g != this && g.Value) g.SetValue(false);
                if (!Value) SetValue(true);
            }
            else SetValue(!Value);
        }
    }

    public class FlatHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Target;
        public void OnPointerEnter(PointerEventData e) { if (Target) Target.color = Color.white; }
        public void OnPointerExit(PointerEventData e) { if (Target) Target.color = new Color(1, 1, 1, 0); }
    }

    /// <summary>クリック・ダブルクリック・右クリックを拾う汎用ハンドラー</summary>
    public class ClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public Action OnClick;
        public Action OnDoubleClick;
        public Action<Vector2> OnRightClick;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) { OnRightClick?.Invoke(e.position); return; }
            if (e.button != PointerEventData.InputButton.Left) return;
            OnClick?.Invoke();
            if (e.clickCount >= 2) OnDoubleClick?.Invoke();
        }
    }
}
