using System;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// ネクサスコーポレーションのロゴ。六角形の紋章に、端点がネットワークの節点になった白い「N」。
    /// 横に「NEXUS／CORPORATION」の文字を並べる（文字はフォントで描く）。
    /// </summary>
    public static class NexusLogo
    {
        static readonly Color NavyTop = new Color(0.12f, 0.32f, 0.72f), NavyBottom = new Color(0.02f, 0.08f, 0.32f);
        static readonly Color Edge = new Color(0.55f, 0.8f, 1f), Node = new Color(0.4f, 0.95f, 1f), Gold = new Color(1f, 0.82f, 0.25f);
        static Sprite _emblem;

        /// <summary>紋章（32×32 のドット絵）</summary>
        public static Sprite Emblem
        {
            get
            {
                if (_emblem != null) return _emblem;
                var p = new PixelCanvas(32, 32);
                // 六角形（上下がとがった形）を、上が明るい紺のグラデーションで塗る
                for (int y = 1; y <= 30; y++)
                {
                    float dy = Mathf.Abs(y - 15.5f);
                    int half = Mathf.Min(13, Mathf.RoundToInt((15f - dy) * 1.73f));
                    if (half <= 0) continue;
                    float k = Mathf.Floor((y - 1) / 29f * 4f) / 4f;
                    p.Rect(16 - half, y, half * 2, 1, Color.Lerp(NavyTop, NavyBottom, k));
                    p.Set(16 - half, y, Edge); p.Set(16 + half - 1, y, Edge);
                }
                // 白い N（2本の縦線と斜めの線）
                p.Rect(9, 9, 3, 15, Color.white);
                p.Rect(20, 9, 3, 15, Color.white);
                p.Line(10, 9, 21, 23, Color.white, 3);
                // 端点は、つながった節点
                foreach (var (x, y) in new[] { (10, 8), (21, 8), (10, 24), (21, 24) }) p.Disc(x, y, 2, Node);
                p.Set(10, 7, Color.white); p.Set(21, 7, Color.white);
                // 右上の小さな金の点（夜明けの星）
                p.Disc(25, 6, 1, Gold);
                _emblem = p.ToSprite();
                return _emblem;
            }
        }

        /// <summary>紋章と社名を並べたロゴを置く（左上が x, y。高さ h）。戻り値はロゴ全体の枠</summary>
        public static RectTransform Create(Transform parent, float x, float y, float h, Color text, bool withName = true, float alpha = 1f)
        {
            float w = withName ? h * 4.4f : h;
            var root = UIFactory.Rect("NexusLogo", parent).Place(x, y, w, h);
            AddTo(root, h, text, withName, alpha);
            return root;
        }

        /// <summary>中央寄せのロゴ（親の上端から top の位置。高さ h）</summary>
        public static RectTransform CreateCentered(Transform parent, float top, float h, Color text, bool withName = true, float alpha = 1f)
        {
            float w = withName ? h * 4.4f : h;
            var root = UIFactory.Rect("NexusLogo", parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(w, h);
            root.anchoredPosition = new Vector2(0, -top);
            AddTo(root, h, text, withName, alpha);
            return root;
        }

        static void AddTo(RectTransform root, float h, Color text, bool withName, float alpha)
        {
            var em = UIFactory.Image(root, Emblem, new Color(1, 1, 1, alpha), "Emblem");
            em.raycastTarget = false;
            em.rectTransform.Place(0, 0, h, h);
            if (!withName) return;
            var c = new Color(text.r, text.g, text.b, text.a * alpha);
            var name = UIFactory.Label(root, "N E X U S", h * 0.12f + h, 0, h * 3.4f, h * 0.62f, Mathf.RoundToInt(h * 0.5f), TextAnchor.LowerLeft, c, true);
            name.fontStyle = FontStyle.Bold;
            name.raycastTarget = false;
            var corp = UIFactory.Label(root, "C O R P O R A T I O N", h * 0.12f + h, h * 0.62f, h * 3.4f, h * 0.38f, Mathf.Max(8, Mathf.RoundToInt(h * 0.22f)), TextAnchor.UpperLeft, c);
            corp.raycastTarget = false;
        }
    }

    /// <summary>NIGHT CLUB のスロットの絵柄（32×32 のドット絵）。７・BAR・ベル・ﾁｪﾘｰ・ﾘﾝｺﾞ</summary>
    public static class SlotArt
    {
        static readonly Sprite[] _symbols = new Sprite[5];
        static Sprite _reelBg;
        static readonly Color Gold = new Color(1f, 0.8f, 0.2f), GoldDark = new Color(0.65f, 0.42f, 0.05f), White = Color.white;

        public static Sprite Symbol(int i)
        {
            if (i < 0 || i >= _symbols.Length) return null;
            if (_symbols[i] != null) return _symbols[i];
            var p = new PixelCanvas(32, 32);
            switch (i)
            {
                case 0: Seven(p); break;
                case 1: Bar(p); break;
                case 2: Bell(p); break;
                case 3: Cherry(p); break;
                default: Apple(p); break;
            }
            return _symbols[i] = p.ToSprite();
        }

        /// <summary>リールの窓（深い赤のビロードに金の縁）</summary>
        public static Sprite ReelBackground
        {
            get
            {
                if (_reelBg != null) return _reelBg;
                var p = new PixelCanvas(40, 48);
                p.Gradient(0, 0, 40, 48, new Color(0.98f, 0.96f, 0.9f), new Color(0.85f, 0.82f, 0.74f));
                // 上下の影（円筒のリール）
                p.Rect(0, 0, 40, 3, new Color(0.55f, 0.52f, 0.48f)); p.Rect(0, 45, 40, 3, new Color(0.55f, 0.52f, 0.48f));
                p.Rect(0, 3, 40, 2, new Color(0.75f, 0.72f, 0.66f)); p.Rect(0, 43, 40, 2, new Color(0.75f, 0.72f, 0.66f));
                p.Frame(0, 0, 40, 48, GoldDark);
                return _reelBg = p.ToSprite();
            }
        }

        static void Seven(PixelCanvas p)
        {
            // 金の縁取り → 赤い本体 → 白い光沢
            void Shape(Color c, int grow)
            {
                p.Rect(6 - grow, 5 - grow, 21 + grow * 2, 5 + grow * 2, c);
                p.Line(25, 9, 13, 27, c, 6 + grow * 2);
            }
            Shape(GoldDark, 2);
            Shape(Gold, 1);
            Shape(new Color(0.85f, 0.05f, 0.08f), 0);
            p.Rect(7, 6, 10, 1, new Color(1f, 0.6f, 0.6f));
            p.Line(22, 11, 15, 22, new Color(1f, 0.45f, 0.45f));
            p.Set(8, 6, White); p.Set(9, 6, White);
        }

        static readonly string[] Letters = { "111/101/111/101/111", "111/101/111/101/101", "110/101/110/101/101" };   // B A R（3×5）

        static void Bar(PixelCanvas p)
        {
            p.Rect(2, 9, 28, 14, GoldDark);
            p.Rect(3, 10, 26, 12, Gold);
            p.Rect(4, 11, 24, 10, new Color(0.08f, 0.08f, 0.1f));
            p.Rect(4, 11, 24, 1, new Color(0.3f, 0.3f, 0.35f));
            for (int k = 0; k < 3; k++)
            {
                var rows = Letters[k].Split('/');
                for (int r = 0; r < 5; r++)
                    for (int c = 0; c < 3; c++)
                        if (rows[r][c] == '1') p.Rect(7 + k * 7 + c * 2, 12 + r * 2 - 1, 2, 2, White);
            }
            p.Rect(4, 6, 24, 2, new Color(0.08f, 0.08f, 0.1f)); p.Rect(4, 24, 24, 2, new Color(0.08f, 0.08f, 0.1f));
        }

        static void Bell(PixelCanvas p)
        {
            p.Disc(16, 14, 9, GoldDark);
            p.Rect(6, 14, 21, 9, GoldDark);
            p.Rect(4, 22, 25, 3, GoldDark);
            p.Disc(16, 14, 8, Gold);
            p.Rect(7, 14, 19, 8, Gold);
            p.Rect(5, 22, 23, 2, Gold);
            p.Rect(20, 9, 4, 13, new Color(0.85f, 0.6f, 0.1f));      // 右側の影
            p.Rect(10, 9, 2, 8, White); p.Set(12, 8, White);           // 光沢
            p.Disc(16, 26, 2, GoldDark);                               // 舌
            p.Rect(15, 3, 3, 3, GoldDark);                             // 吊り手
        }

        static void Cherry(PixelCanvas p)
        {
            var stem = new Color(0.15f, 0.5f, 0.15f);
            p.Line(10, 21, 18, 6, stem, 2);
            p.Line(22, 21, 18, 6, stem, 2);
            p.Disc(22, 7, 3, new Color(0.25f, 0.7f, 0.2f));            // 葉
            foreach (var cx in new[] { 10, 22 })
            {
                p.Disc(cx, 23, 6, new Color(0.45f, 0f, 0.05f));
                p.Disc(cx, 23, 5, new Color(0.85f, 0.05f, 0.15f));
                p.Rect(cx - 3, 20, 2, 2, White);
            }
        }

        static void Apple(PixelCanvas p)
        {
            p.Disc(16, 18, 10, new Color(0.1f, 0.4f, 0.08f));
            p.Disc(16, 18, 9, new Color(0.35f, 0.75f, 0.2f));
            p.Disc(12, 15, 3, new Color(0.6f, 0.9f, 0.4f));
            p.Rect(11, 13, 2, 2, White);
            p.Line(16, 9, 18, 3, new Color(0.35f, 0.2f, 0.1f), 2);
            p.Disc(21, 5, 2, new Color(0.25f, 0.6f, 0.15f));
        }
    }
}
