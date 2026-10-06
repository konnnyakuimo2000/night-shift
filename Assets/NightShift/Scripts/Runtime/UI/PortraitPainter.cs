using System.Collections.Generic;
using NightShift.Data;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>
    /// 社員証写真風のドット絵を社員番号から決定的に生成する（オリジナル描画・外部素材なし）。
    /// 画風: 入国審査ゲーム風の、くすんだ色・大きなドット・黒い縁取り・強い影・無表情。
    /// 32×40 ドットを3倍で表示。光は左から。年齢（白髪・しわ・生え際）と、名前から推定した性別で髪型・服を変える。
    /// EmployeeData.portrait が設定されていればそちらを優先する。
    /// </summary>
    public static class PortraitPainter
    {
        const int W = 32, H = 40, Scale = 3;
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        static Color Mix(Color a, Color b, float t) { var c = Color.Lerp(a, b, t); c.a = 1; return c; }

        // くすんだ色（最後にさらに彩度を落とす）
        static readonly Color[] Skins = { C(214, 178, 148), C(200, 162, 130), C(186, 148, 118), C(222, 190, 160), C(194, 158, 128) };
        static readonly Color[] Hairs = { C(34, 30, 28), C(60, 46, 36), C(84, 62, 44), C(46, 40, 38), C(102, 78, 54) };
        static readonly Color[] Coats = { C(62, 66, 72), C(78, 74, 66), C(48, 52, 50), C(86, 72, 58), C(64, 74, 64), C(70, 64, 76) };
        static readonly Color[] Ties = { C(120, 40, 40), C(46, 60, 100), C(52, 84, 58), C(112, 90, 46), C(84, 50, 86) };
        static readonly Color[] Blouses = { C(196, 196, 186), C(186, 176, 172), C(170, 180, 186), C(192, 186, 164) };
        static readonly Color[] Backs = { C(148, 156, 140), C(156, 152, 136), C(140, 148, 150) };
        static readonly Color Ink = C(24, 22, 20);

        // 名前（読み）から性別を推定する（データに性別の項目がないため）
        static readonly HashSet<string> FemaleNames = new HashSet<string> { "ユイ", "マユ", "チヒロ", "メグミ", "ミサキ", "サクラ", "アヤ", "ユミ", "ヨウコ", "ケイコ", "アキコ", "ナオミ", "リエ", "エリ" };
        static bool IsFemale(EmployeeData e)
        {
            string kana = e.kana ?? "";
            int sp = kana.LastIndexOf(' ');
            string given = sp >= 0 ? kana.Substring(sp + 1) : kana;
            if (FemaleNames.Contains(given)) return true;
            if (given.Length >= 2)
            {
                char last = given[given.Length - 1];
                if (last == 'コ' || last == 'ミ' || last == 'エ' || last == 'カ' || last == 'ナ' || last == 'ヨ') return true;
            }
            return false;
        }

        public static Sprite Get(EmployeeData e)
        {
            if (e == null) return null;
            if (e.portrait != null) return e.portrait;
            string key = e.employeeId + "|" + e.age + "|" + e.kana;
            if (_cache.TryGetValue(key, out var s)) return s;
            s = Generate(e);
            _cache[key] = s;
            return s;
        }

        // ---------------- 描画の下地 ----------------
        sealed class Canvas
        {
            public readonly Color[] Px = new Color[W * H];
            public readonly byte[] Layer = new byte[W * H];   // 0: 背景 1: 人物
            public void Set(int x, int y, Color c, bool figure = true)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                Px[y * W + x] = c;
                if (figure) Layer[y * W + x] = 1;
            }
            public Color Get(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? Color.clear : Px[y * W + x];
            public bool IsFigure(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && Layer[y * W + x] == 1;
        }

        static bool InEllipse(int x, int y, float cx, float cy, float rx, float ry) =>
            ((x + 0.5f - cx) * (x + 0.5f - cx)) / (rx * rx) + ((y + 0.5f - cy) * (y + 0.5f - cy)) / (ry * ry) <= 1f;

        static Sprite Generate(EmployeeData e)
        {
            int seed = 17;
            foreach (char ch in e.employeeId ?? "") seed = seed * 31 + ch;
            var rng = new System.Random(seed);
            var cv = new Canvas();
            bool female = IsFemale(e);
            int age = e.age;
            const int cx = 16;

            // ---------------- 背景（くすんだ単色・下の方に帯） ----------------
            Color back = Backs[rng.Next(Backs.Length)];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                cv.Set(x, y, y > 30 ? Mul(back, 0.88f) : back, false);

            Color skin = Skins[rng.Next(Skins.Length)];
            Color skinHi = Mix(skin, C(236, 224, 200), 0.3f);
            Color skinSh = Mul(skin, 0.78f);
            Color skinDeep = Mul(skin, 0.62f);
            Color baseHair = Hairs[rng.Next(Hairs.Length)];
            Color hair = age >= 50 ? Mix(baseHair, C(150, 148, 140), Mathf.Clamp01((age - 46) / 12f))
                      : age >= 42 && rng.Next(3) == 0 ? Mix(baseHair, C(128, 126, 120), 0.35f) : baseHair;
            Color hairHi = Mix(hair, C(200, 196, 180), 0.22f);
            Color hairSh = Mul(hair, 0.7f);

            // 髪型（男性: 0 短髪 1 七三 2 オールバック 3 坊主 / 女性: 0 ロング 1 ボブ 2 まとめ髪 3 ぱっつん）
            int style = rng.Next(4);
            bool receding = !female && age >= 46 && rng.Next(2) == 0;
            int glasses = rng.Next(100) < (age >= 40 ? 45 : 25) ? 1 + rng.Next(2) : 0;
            int facial = !female && age >= 28 ? rng.Next(6) : 0; // 4 口ひげ 5 あごひげ
            bool heavyBrow = rng.Next(2) == 0;
            bool frown = rng.Next(3) != 0;

            // ---------------- 後ろ髪 ----------------
            if (female && (style == 0 || style == 3))
                for (int y = 6; y < 34; y++) for (int x = 5; x < 27; x++) if (InEllipse(x, y, cx, 19, 10.5f, 14.5f)) cv.Set(x, y, x > cx + 3 ? hairSh : hair);
            if (female && style == 1)
                for (int y = 6; y < 27; y++) for (int x = 6; x < 26; x++) if (InEllipse(x, y, cx, 16, 9.8f, 10.5f)) cv.Set(x, y, x > cx + 3 ? hairSh : hair);
            if (female && style == 2)
                for (int y = 2; y < 9; y++) for (int x = 11; x < 21; x++) if (InEllipse(x, y, cx, 5, 4.4f, 3.6f)) cv.Set(x, y, x < cx ? hairHi : hair);

            // ---------------- 体（くすんだ上着） ----------------
            Color coat = Coats[rng.Next(Coats.Length)];
            for (int y = 29; y < H; y++)
            for (int x = 0; x < W; x++)
                if (InEllipse(x, y, cx, 43, 17f, 13f))
                    cv.Set(x, y, x < cx - 6 ? Mix(coat, C(200, 196, 180), 0.12f) : x > cx + 5 ? Mul(coat, 0.72f) : coat);
            // 首（あごの下は濃い影）
            for (int y = 26; y < 32; y++) for (int x = 13; x < 20; x++) cv.Set(x, y, y < 29 ? skinDeep : x > 17 ? skinSh : skin);

            if (!female)
            {
                Color shirt = C(190, 188, 178);
                for (int y = 30; y < H; y++)
                {
                    int half = Mathf.Min(5, (y - 30) / 2 + 2);
                    for (int x = cx - half; x < cx + half; x++) cv.Set(x, y, x > cx ? Mul(shirt, 0.85f) : shirt);
                    cv.Set(cx - half - 1, y, Mul(coat, 0.55f)); cv.Set(cx + half, y, Mul(coat, 0.55f)); // 襟の線
                }
                Color tie = Ties[rng.Next(Ties.Length)];
                cv.Set(cx - 1, 31, tie); cv.Set(cx, 31, Mul(tie, 0.8f));
                for (int y = 32; y < H; y++) { cv.Set(cx - 1, y, tie); cv.Set(cx, y, Mul(tie, 0.75f)); if (y > 35) cv.Set(cx - 2, y, tie); }
            }
            else
            {
                Color blouse = Blouses[rng.Next(Blouses.Length)];
                for (int y = 30; y < H; y++)
                {
                    int half = Mathf.Min(5, (y - 30) / 2 + 2);
                    for (int x = cx - half; x < cx + half; x++) cv.Set(x, y, x > cx ? Mul(blouse, 0.85f) : blouse);
                }
                for (int x = cx - 3; x <= cx + 2; x++) cv.Set(x, 31, Mul(blouse, 0.7f)); // 襟ぐり
            }

            // ---------------- 顔（角ばったあご） ----------------
            for (int y = 8; y < 29; y++)
            {
                float half = y < 22 ? 6.6f : 6.6f - (y - 22) * 0.75f;  // あごに向かって細くなる
                if (y < 11) half = 5.6f + (y - 8) * 0.35f;              // 頭の上は少し丸く
                for (int x = 0; x < W; x++)
                {
                    float dx = x + 0.5f - cx;
                    if (dx < -half || dx > half) continue;
                    // 光は左から: 左は明るく、右の 1/3 は影、あごの下は濃い影
                    Color c = dx < -2.5f ? skinHi : dx > 2.2f ? skinSh : skin;
                    if (y >= 27) c = skinSh;
                    cv.Set(x, y, c);
                }
            }
            // 耳
            for (int y = 15; y < 20; y++) { cv.Set(9, y, skinSh); cv.Set(23, y, skinDeep); }
            cv.Set(9, 17, skinDeep);

            // 目元のくぼみ（眉の下の影）→ 眉 → 目（小さな黒い点）
            for (int x = 10; x <= 22; x++) if (cv.IsFigure(x, 15) && x != 16) cv.Set(x, 15, Mix(cv.Get(x, 15), skinDeep, 0.55f));
            Color brow = Mul(hair, 0.7f);
            for (int x = 10; x <= 14; x++) cv.Set(x, 14, brow);
            for (int x = 18; x <= 22; x++) cv.Set(x, 14, brow);
            if (heavyBrow) { cv.Set(11, 13, brow); cv.Set(12, 13, brow); cv.Set(20, 13, brow); cv.Set(21, 13, brow); }
            if (frown) { cv.Set(14, 15, brow); cv.Set(18, 15, brow); } // 眉間に力が入った無表情
            cv.Set(12, 16, Ink); cv.Set(13, 16, Mul(skinDeep, 0.8f));
            cv.Set(19, 16, Ink); cv.Set(20, 16, Mul(skinDeep, 0.8f));
            if (female) { cv.Set(11, 16, Ink); cv.Set(21, 16, Ink); }
            cv.Set(12, 17, skinSh); cv.Set(20, 17, skinSh); // 目の下
            if (age >= 40) { cv.Set(11, 18, skinSh); cv.Set(21, 18, skinSh); }

            // 鼻（長い影の線と小鼻）
            for (int y = 16; y < 21; y++) cv.Set(cx + 1, y, skinSh);
            cv.Set(cx - 1, 21, skinDeep); cv.Set(cx, 21, skinSh); cv.Set(cx + 1, 21, skinDeep); cv.Set(cx + 2, 21, skinDeep);
            cv.Set(cx - 1, 19, skinHi);

            // 口（への字の一本線）
            Color mouth = Mul(skinDeep, 0.75f);
            for (int x = cx - 2; x <= cx + 2; x++) cv.Set(x, 24, mouth);
            if (frown) { cv.Set(cx - 3, 25, mouth); cv.Set(cx + 3, 25, mouth); } else { cv.Set(cx - 3, 24, mouth); cv.Set(cx + 3, 24, mouth); }
            if (female) for (int x = cx - 1; x <= cx + 1; x++) cv.Set(x, 25, Mix(skin, C(150, 70, 70), 0.45f));
            // 年齢のしわ
            if (age >= 40) { cv.Set(cx - 4, 22, skinSh); cv.Set(cx - 4, 23, skinSh); cv.Set(cx + 4, 22, skinDeep); cv.Set(cx + 4, 23, skinDeep); }
            if (age >= 50) for (int x = 12; x <= 20; x += 2) cv.Set(x, 11, skinSh);
            // ひげ
            if (facial == 4) for (int x = cx - 3; x <= cx + 3; x++) cv.Set(x, 23, x > cx + 1 ? hairSh : hair);
            if (facial == 5) for (int y = 23; y < 29; y++) for (int x = 10; x < 23; x++) if (cv.IsFigure(x, y) && (y > 24 || x < cx - 3 || x > cx + 3)) cv.Set(x, y, (x + y) % 2 == 0 ? hair : hairSh);

            // ---------------- 髪（前） ----------------
            for (int y = 3; y < 24; y++)
            for (int x = 6; x < 27; x++)
            {
                int dx = Mathf.Abs(x - cx);
                int line = female ? 11 : (style == 3 ? 10 : 11);
                if (receding && dx < 4) line = 9;
                if (dx > 6) line += 5;
                bool cap = InEllipse(x, y, cx, 11, 8.6f, 8f) && y < line;
                if (!female && style == 3) cap = cap && InEllipse(x, y, cx, 12, 8f, 7.4f);
                if (!female && style == 1 && InEllipse(x, y, cx - 3, 6, 5.5f, 2.5f)) cap = true;   // 七三のふくらみ
                bool fringe = female && style == 3 && y < 13 && InEllipse(x, y, cx, 10, 8f, 4.6f);
                if (female && style == 1) fringe = y < 13 && InEllipse(x, y, cx - 2, 9, 7.5f, 4f);
                if (female && (style == 0 || style == 2)) fringe = y < 12 && InEllipse(x, y, cx + 2, 9, 8f, 3.4f);
                bool sides = female && style != 2 && y < 27 && (x <= 9 || x >= 23) && InEllipse(x, y, cx, 15, 9.5f, 12f);
                if (!(cap || fringe || sides)) continue;
                Color c = x > cx + 3 ? hairSh : x < cx - 4 && y < 9 ? hairHi : hair;
                if (!female && style == 2 && (x % 3 == 0) && y < 9) c = hairHi;      // オールバックの筋
                if (!female && style == 3 && (x + y) % 2 == 0) c = Mix(hair, skin, 0.35f); // 坊主: 地肌が透ける
                cv.Set(x, y, c);
            }
            if (!female && style == 1) for (int y = 4; y < 9; y++) cv.Set(cx - 3, y, Mix(hair, skin, 0.45f)); // 分け目
            if (age >= 50) for (int i = 0; i < 8; i++) { int gx = 8 + rng.Next(16), gy = 4 + rng.Next(6); if (cv.IsFigure(gx, gy)) cv.Set(gx, gy, C(176, 174, 166)); }

            // ---------------- 眼鏡（細い黒縁） ----------------
            if (glasses > 0)
            {
                Color fr = glasses == 1 ? Ink : C(110, 104, 92);
                for (int x = 10; x <= 14; x++) { cv.Set(x, 15, fr); cv.Set(x, 18, fr); }
                for (int x = 18; x <= 22; x++) { cv.Set(x, 15, fr); cv.Set(x, 18, fr); }
                for (int y = 15; y <= 18; y++) { cv.Set(10, y, fr); cv.Set(14, y, fr); cv.Set(18, y, fr); cv.Set(22, y, fr); }
                cv.Set(15, 16, fr); cv.Set(16, 16, fr); cv.Set(17, 16, fr);
                cv.Set(11, 16, Mix(cv.Get(11, 16), Color.white, 0.35f)); // レンズの反射
            }

            // ---------------- 黒い縁取り（人物の外周） ----------------
            var edge = new List<int>();
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (cv.IsFigure(x, y) && ((x > 0 && !cv.IsFigure(x - 1, y)) || (x < W - 1 && !cv.IsFigure(x + 1, y)) || (y > 0 && !cv.IsFigure(x, y - 1))))
                    edge.Add(y * W + x);
            foreach (int i in edge) cv.Px[i] = Ink;

            // ---------------- 色をくすませる（彩度を落とし、少し黄ばませ、平らな色に） ----------------
            for (int i = 0; i < cv.Px.Length; i++)
            {
                var c = cv.Px[i];
                float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                c = Mix(c, new Color(l, l, l), 0.35f);
                c = new Color(c.r * 1.02f, c.g, c.b * 0.9f, 1f);
                c = new Color(Mathf.Round(c.r * 24) / 24f, Mathf.Round(c.g * 24) / 24f, Mathf.Round(c.b * 24) / 24f, 1f);
                cv.Px[i] = c;
            }

            // ---------------- 拡大してテクスチャに ----------------
            var tex = new Texture2D(W * Scale, H * Scale, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.hideFlags = HideFlags.DontSave;
            var big = new Color[W * Scale * H * Scale];
            for (int y = 0; y < H * Scale; y++)
            for (int x = 0; x < W * Scale; x++)
                big[y * W * Scale + x] = cv.Px[(H - 1 - y / Scale) * W + x / Scale];
            tex.SetPixels(big);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
