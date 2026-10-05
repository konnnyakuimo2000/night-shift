using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>
    /// 1990年代末〜2000年代初頭の日本企業向け業務ソフト風のテーマ。
    /// 素材はすべて実行時に手続き生成（特定OSのアイコン・素材は使用しない）。
    /// </summary>
    public static class RetroTheme
    {
        // ---- colors ----
        public static readonly Color Face = C(212, 208, 200);
        public static readonly Color Highlight = Color.white;
        public static readonly Color Shadow = C(128, 128, 128);
        public static readonly Color DarkShadow = C(64, 64, 64);
        public static readonly Color Text = Color.black;
        /// <summary>補足説明の文字（灰色の背景でも読める濃さ）</summary>
        public static readonly Color HintText = new Color(0.25f, 0.25f, 0.25f);
        public static readonly Color TextDisabled = C(128, 128, 128);
        public static readonly Color TextBlue = C(0, 32, 140);
        public static readonly Color Selection = C(10, 36, 106);
        public static readonly Color SelectionText = Color.white;
        public static readonly Color TitleActiveA = C(10, 36, 106);
        public static readonly Color TitleActiveB = C(110, 150, 210);
        public static readonly Color TitleInactiveA = C(128, 128, 128);
        public static readonly Color TitleInactiveB = C(192, 192, 192);
        public static readonly Color Desktop = C(46, 74, 98);
        public static readonly Color RemoteDesktop = C(0, 112, 112);
        public static readonly Color FieldReadOnly = C(236, 233, 226);
        public static readonly Color Tooltip = C(255, 255, 225);
        public static readonly Color Navy = C(0, 0, 128);
        public static readonly Color Terminal = C(0, 0, 0);
        public static readonly Color TerminalText = C(192, 192, 192);
        public static readonly Color Note = C(255, 244, 140);

        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        // ---- fonts ----
        static Font _ui, _mono, _reading;

        /// <summary>メール・マニュアルなど長文用の読みやすいフォント</summary>
        public static Font ReadingFont
        {
            get
            {
                if (_reading == null)
                    _reading = Font.CreateDynamicFontFromOSFont(new[] { "Meiryo UI", "Meiryo", "Yu Gothic UI", "Yu Gothic", "MS UI Gothic", "Arial" }, 14);
                if (_reading == null) _reading = UIFont;
                return _reading;
            }
        }
        public const int ReadingFontSize = 14;
        public static Font UIFont
        {
            get
            {
                if (_ui == null)
                    _ui = Font.CreateDynamicFontFromOSFont(new[] { "MS UI Gothic", "MS PGothic", "MS Gothic", "Meiryo UI", "Meiryo", "Yu Gothic UI", "Arial" }, 12);
                if (_ui == null) _ui = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _ui;
            }
        }

        public static Font MonoFont
        {
            get
            {
                if (_mono == null)
                    _mono = Font.CreateDynamicFontFromOSFont(new[] { "MS Gothic", "Osaka-Mono", "Consolas", "Courier New" }, 12);
                if (_mono == null) _mono = UIFont;
                return _mono;
            }
        }

        public const int FontSize = 12;
        public const int TitleBarHeight = 18;
        public const int MenuBarHeight = 19;
        public const int StatusBarHeight = 20;
        public const int RowHeight = 17;

        // ---- sprites ----
        static Sprite _raised, _pressed, _field, _thinSunken, _etched, _white, _checker, _frame, _thinRaised;
        public static Sprite Raised => _raised ? _raised : _raised = Bevel(Face, Highlight, Shadow, DarkShadow, Face);
        public static Sprite Frame => _frame ? _frame : _frame = Bevel(Face, Highlight, Shadow, DarkShadow, Face);
        public static Sprite Pressed => _pressed ? _pressed : _pressed = Bevel(DarkShadow, Shadow, Face, Highlight, Face);
        public static Sprite Field => _field ? _field : _field = Bevel(Shadow, DarkShadow, Face, Highlight, Color.white);
        public static Sprite ThinSunken => _thinSunken ? _thinSunken : _thinSunken = Thin(Shadow, Highlight, Face);
        public static Sprite ThinRaised => _thinRaised ? _thinRaised : _thinRaised = Thin(Highlight, Shadow, Face);
        public static Sprite Etched => _etched ? _etched : _etched = EtchedBox();
        public static Sprite White => _white ? _white : _white = Solid();
        public static Sprite Checker => _checker ? _checker : _checker = MakeChecker();

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        /// <summary>2px のベベル（9-slice）。色は白で乗算されるため、そのまま表示する。</summary>
        static Sprite Bevel(Color tlOuter, Color tlInner, Color brInner, Color brOuter, Color fill)
        {
            const int n = 8;
            var t = NewTex(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int ty = n - 1 - y; // 上からの行
                Color c = fill;
                if (x == n - 1 || ty == n - 1) c = brOuter;
                else if (x == 0 || ty == 0) c = tlOuter;
                else if (x == n - 2 || ty == n - 2) c = brInner;
                else if (x == 1 || ty == 1) c = tlInner;
                px[y * n + x] = c;
            }
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
        }

        static Sprite Thin(Color tl, Color br, Color fill)
        {
            const int n = 4;
            var t = NewTex(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int ty = n - 1 - y;
                Color c = fill;
                if (x == n - 1 || ty == n - 1) c = br;
                else if (x == 0 || ty == 0) c = tl;
                px[y * n + x] = c;
            }
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
        }

        static Sprite EtchedBox()
        {
            const int n = 6;
            var t = NewTex(n, n);
            var px = new Color[n * n];
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int ty = n - 1 - y;
                Color c = clear;
                bool outer = x == 0 || ty == 0 || x == n - 1 || ty == n - 1;
                bool inner = !outer && (x == 1 || ty == 1 || x == n - 2 || ty == n - 2);
                if (outer) c = (x == n - 1 || ty == n - 1) ? Highlight : Shadow;
                else if (inner) c = (x == n - 2 || ty == n - 2) ? Shadow : Highlight;
                if (x == n - 1 && ty == 0) c = Highlight;
                px[y * n + x] = c;
            }
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(2, 2, 2, 2));
        }

        static Sprite Solid()
        {
            var t = NewTex(4, 4);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
        }

        static readonly Dictionary<string, Sprite> _gradients = new Dictionary<string, Sprite>();

        /// <summary>左→右の横グラデーション（タイトルバー用）</summary>
        public static Sprite GradientSprite(Color a, Color b)
        {
            string key = ColorUtility.ToHtmlStringRGB(a) + ColorUtility.ToHtmlStringRGB(b);
            if (_gradients.TryGetValue(key, out var sp)) return sp;
            var t = NewTex(128, 1);
            t.filterMode = FilterMode.Bilinear;
            for (int x = 0; x < 128; x++) t.SetPixel(x, 0, Color.Lerp(a, b, x / 127f));
            t.Apply();
            sp = Sprite.Create(t, new Rect(0, 0, 128, 1), new Vector2(0.5f, 0.5f));
            _gradients[key] = sp;
            return sp;
        }

        static Sprite MakeChecker()
        {
            var t = NewTex(2, 2);
            t.wrapMode = TextureWrapMode.Repeat;
            t.SetPixel(0, 0, Highlight); t.SetPixel(1, 1, Highlight);
            t.SetPixel(1, 0, Face); t.SetPixel(0, 1, Face);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---------------- Icons (16x16, procedurally drawn) ----------------
        static readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();

        public static Sprite Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_icons.TryGetValue(name, out var s)) return s;
            var c = new IconCanvas(16);
            IconPainter.Paint(name, c);
            s = c.ToSprite();
            _icons[name] = s;
            return s;
        }

        public static Sprite BigIcon(string name)
        {
            string key = name + "@32";
            if (_icons.TryGetValue(key, out var s)) return s;
            var c = new IconCanvas(16);
            IconPainter.Paint(name, c);
            s = c.ToSprite(2);
            _icons[key] = s;
            return s;
        }

        public class IconCanvas
        {
            public readonly int Size;
            public readonly Color[] Px;
            public IconCanvas(int size) { Size = size; Px = new Color[size * size]; }

            public void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= Size || y >= Size) return;
                Px[(Size - 1 - y) * Size + x] = c;
            }

            public void Rect(int x, int y, int w, int h, Color c)
            {
                for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) Set(i, j, c);
            }

            public void Box(int x, int y, int w, int h, Color fill, Color border)
            {
                Rect(x, y, w, h, border);
                Rect(x + 1, y + 1, w - 2, h - 2, fill);
            }

            public void Circle(float cx, float cy, float r, Color c)
            {
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r) Set(x, y, c);
            }

            public void Line(int x0, int y0, int x1, int y1, Color c)
            {
                int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
                int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
                while (true)
                {
                    Set(x0, y0, c);
                    if (x0 == x1 && y0 == y1) break;
                    int e2 = 2 * err;
                    if (e2 >= dy) { err += dy; x0 += sx; }
                    if (e2 <= dx) { err += dx; y0 += sy; }
                }
            }

            public Sprite ToSprite(int scale = 1)
            {
                int n = Size * scale;
                var t = NewTex(n, n);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = Px[(y / scale) * Size + x / scale];
                t.SetPixels(px); t.Apply();
                return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
