using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>コードで描くドット絵の画板（原点は左上）。拡大しても滲まない（Point フィルタ）</summary>
    public class PixelCanvas
    {
        public readonly int W, H;
        readonly Color32[] _px;
        public PixelCanvas(int w, int h) { W = w; H = h; _px = new Color32[w * h]; }

        public void Set(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = (H - 1 - y) * W + x;
            if (c.a >= 0.99f) { _px[i] = c; return; }
            Color b = _px[i];
            _px[i] = Color.Lerp(b, new Color(c.r, c.g, c.b, Mathf.Max(b.a, c.a)), c.a);
        }

        public void Rect(int x, int y, int w, int h, Color c) { for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Set(x + i, y + j, c); }
        public void Frame(int x, int y, int w, int h, Color c) { Rect(x, y, w, 1, c); Rect(x, y + h - 1, w, 1, c); Rect(x, y, 1, h, c); Rect(x + w - 1, y, 1, h, c); }
        public void Disc(int cx, int cy, int r, Color c) { for (int j = -r; j <= r; j++) for (int i = -r; i <= r; i++) if (i * i + j * j <= r * r + r / 2) Set(cx + i, cy + j, c); }

        public void Line(int x0, int y0, int x1, int y1, Color c, int t = 1)
        {
            int n = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int k = 0; k <= n; k++)
            {
                float f = n == 0 ? 0 : k / (float)n;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, f)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, f));
                Rect(x - t / 2, y - t / 2, t, t, c);
            }
        }

        /// <summary>縦のグラデーション（上 → 下）</summary>
        public void Gradient(int x, int y, int w, int h, Color top, Color bottom)
        {
            for (int j = 0; j < h; j++)
            {
                // 階調を4段に（ドット絵らしく）
                float k = Mathf.Floor(j / (float)Math.Max(1, h - 1) * 4f) / 4f;
                Rect(x, y + j, w, 1, Color.Lerp(top, bottom, k));
            }
        }

        public void Speckle(int x, int y, int w, int h, Color c, float density, System.Random r)
        {
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) if (r.NextDouble() < density) Set(x + i, y + j, c);
        }

        public Sprite ToSprite()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(_px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            s.hideFlags = HideFlags.DontSave;
            return s;
        }
    }

    /// <summary>
    /// 怪異（SQ03）の姿: 長い黒髪で顔が常に隠れた、白い服の女。顔は一切描かない。
    /// 控えめの設定では、手や輪郭を省いた静かな絵にする（判断に必要な情報は変えない）。
    /// </summary>
    public static class HorrorArt
    {
        static readonly Color Hair = new Color(0.03f, 0.03f, 0.04f), HairLight = new Color(0.13f, 0.13f, 0.16f);
        static readonly Color Dress = new Color(0.86f, 0.86f, 0.82f), DressShade = new Color(0.62f, 0.63f, 0.62f), Dirt = new Color(0.45f, 0.42f, 0.38f);
        static readonly Color Skin = new Color(0.72f, 0.76f, 0.78f), SkinShade = new Color(0.5f, 0.55f, 0.6f);
        static Sprite _photo, _crawler, _standing;

        /// <summary>長い髪の束（上から下へ垂れる）。顔の位置をすべて覆う</summary>
        static void HairMass(PixelCanvas p, int cx, int top, int halfTop, int halfBottom, int bottom, System.Random r)
        {
            int dome = Math.Max(3, (bottom - top) / 4);   // 頭の丸み
            for (int y = top; y <= bottom; y++)
            {
                int dy = y - top;
                float half;
                if (dy < dome)
                {
                    float k = 1f - (dome - dy) / (float)dome;
                    half = halfTop * Mathf.Sqrt(Mathf.Clamp01(1f - (1f - k) * (1f - k)));
                }
                else
                {
                    // 頭から下は、肩へ向かって広がりながら真っすぐ垂れる
                    float k = (dy - dome) / (float)Math.Max(1, bottom - top - dome);
                    half = Mathf.Lerp(halfTop, halfBottom, Mathf.Sqrt(k));
                }
                int h = Mathf.RoundToInt(half);
                p.Rect(cx - h, y, h * 2 + 1, 1, Hair);
                // 頭の輪郭のつや
                if (dy > 1 && dy < dome + 2) { p.Set(cx - h + 1, y, HairLight); p.Set(cx + h - 1, y, HairLight); }
            }
            // 分け目
            p.Line(cx, top + 1, cx, top + dome, HairLight);
            // 毛先は不揃いに垂れる・ところどころに光る筋
            for (int x = cx - halfBottom; x <= cx + halfBottom; x++)
            {
                int len = r.Next(0, 9);
                p.Rect(x, bottom, 1, len, Hair);
                if (r.Next(4) == 0) p.Line(x, top + dome + r.Next(4), x + r.Next(-1, 2), bottom - r.Next(4), HairLight);
            }
        }

        /// <summary>社員証の写真（顔は髪で見えない）</summary>
        public static Sprite IdPhoto
        {
            get
            {
                if (_photo != null) return _photo;
                var p = new PixelCanvas(48, 60);
                var r = new System.Random(4);
                p.Gradient(0, 0, 48, 60, new Color(0.5f, 0.55f, 0.6f), new Color(0.36f, 0.4f, 0.46f));
                // 白い服の肩
                p.Rect(4, 46, 40, 14, Dress);
                p.Rect(4, 46, 4, 14, DressShade); p.Rect(40, 46, 4, 14, DressShade);
                p.Speckle(4, 50, 40, 10, Dirt, 0.04f, r);
                HairMass(p, 24, 5, 11, 15, 56, r);
                _photo = p.ToSprite();
                return _photo;
            }
        }

        /// <summary>画面のこちらへ這い出てくる姿（長い髪が前に垂れ、白い腕が伸びる）</summary>
        public static Sprite Crawler
        {
            get
            {
                if (_crawler != null) return _crawler;
                var p = new PixelCanvas(96, 96);
                var r = new System.Random(9);
                // 背中と肩（白い服）
                p.Rect(22, 44, 52, 30, Dress);
                p.Rect(22, 44, 6, 30, DressShade); p.Rect(68, 44, 6, 30, DressShade);
                p.Speckle(22, 44, 52, 30, Dirt, 0.05f, r);
                // 腕: 肩から画面の手前（下の角）へ
                p.Line(26, 52, 8, 84, Skin, 5);
                p.Line(70, 52, 88, 84, Skin, 5);
                p.Line(27, 54, 10, 86, SkinShade, 1);
                p.Line(69, 54, 86, 86, SkinShade, 1);
                // 指（細く長い）
                foreach (var (hx, hy, dir) in new[] { (8, 86, -1), (88, 86, 1) })
                    for (int f = 0; f < 4; f++) p.Line(hx, hy, hx + dir * (2 + f * 2), hy + 7 - f, Skin, 1);
                // 頭と髪（顔の位置をすべて覆い、床まで垂れる）
                HairMass(p, 48, 18, 13, 22, 76, r);
                _crawler = p.ToSprite();
                return _crawler;
            }
        }

        /// <summary>遠くに立っている姿（小さな絵・エンディング用）</summary>
        public static Sprite Standing
        {
            get
            {
                if (_standing != null) return _standing;
                var p = new PixelCanvas(24, 56);
                var r = new System.Random(2);
                p.Rect(6, 16, 12, 38, Dress);
                p.Rect(6, 16, 2, 38, DressShade);
                p.Line(6, 20, 3, 40, Skin, 2); p.Line(17, 20, 20, 40, Skin, 2);
                HairMass(p, 12, 2, 5, 8, 34, r);
                _standing = p.ToSprite();
                return _standing;
            }
        }

        /// <summary>エンディングの絵に、怪異を描き込む</summary>
        public static void DrawStanding(PixelCanvas p, int x, int y, System.Random r, int scale = 1)
        {
            p.Rect(x + 3 * scale, y + 8 * scale, 6 * scale, 18 * scale, Dress);
            p.Rect(x + 3 * scale, y + 8 * scale, scale, 18 * scale, DressShade);
            HairMass(p, x + 6 * scale, y, 3 * scale, 4 * scale, y + 16 * scale, r);
        }
    }
}
