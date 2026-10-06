using System.Collections.Generic;
using NightShift.Data;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>
    /// 社員証写真風のドット絵を社員番号から決定的に生成する（オリジナル描画・外部素材なし）。
    /// 48×60 ドット。光は左上から。年齢（白髪・しわ・生え際）と、名前から推定した性別で髪型・服を変える。
    /// EmployeeData.portrait が設定されていればそちらを優先する。
    /// </summary>
    public static class PortraitPainter
    {
        const int W = 48, H = 60, Scale = 2;
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        static Color Mix(Color a, Color b, float t) { var c = Color.Lerp(a, b, t); c.a = 1; return c; }

        static readonly Color[] Skins = { C(246, 214, 184), C(238, 200, 164), C(226, 184, 146), C(250, 226, 200), C(232, 192, 160) };
        static readonly Color[] Hairs = { C(28, 24, 24), C(52, 38, 30), C(78, 54, 38), C(40, 34, 34), C(96, 66, 44) };
        static readonly Color[] Suits = { C(36, 46, 78), C(64, 66, 74), C(28, 28, 34), C(84, 66, 50), C(52, 72, 64), C(70, 60, 84) };
        static readonly Color[] Ties = { C(150, 30, 40), C(30, 60, 140), C(40, 100, 60), C(130, 96, 30), C(96, 40, 116), C(160, 120, 40) };
        static readonly Color[] Blouses = { C(244, 244, 240), C(236, 222, 228), C(220, 232, 244), C(240, 236, 214) };
        static readonly Color[] Cardigans = { C(150, 60, 70), C(70, 90, 130), C(110, 120, 90), C(180, 150, 110), C(90, 70, 100) };

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

            // ---------------- 背景（証明写真の青。頭の後ろが少し明るい） ----------------
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var c = Mix(C(198, 216, 232), C(150, 176, 202), y / (float)H);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(24, 22)) / 30f;
                c = Mix(Mix(c, Color.white, 0.18f), c, Mathf.Clamp01(d));
                // 当時の写真らしい粗い粒（市松の微妙なむら）
                if (((x + y) & 1) == 0 && y > 40) c = Mul(c, 0.985f);
                cv.Set(x, y, c, false);
            }

            Color skin = Skins[rng.Next(Skins.Length)];
            Color skinLight = Mix(skin, Color.white, 0.25f);
            Color skinShade = Mul(skin, 0.86f);
            Color skinDeep = Mul(skin, 0.72f);
            Color baseHair = Hairs[rng.Next(Hairs.Length)];
            Color hair = age >= 50 ? Mix(baseHair, C(170, 168, 166), Mathf.Clamp01((age - 46) / 14f))
                      : age >= 42 && rng.Next(3) == 0 ? Mix(baseHair, C(150, 148, 146), 0.35f) : baseHair;
            Color hairLight = Mix(hair, Color.white, 0.28f);
            Color hairDark = Mul(hair, 0.65f);
            Color dark = C(36, 28, 28);

            const int cx = 24;
            // 髪型（男性: 0 短髪 1 七三分け 2 オールバック 3 スポーツ刈り / 女性: 0 ロング 1 ボブ 2 まとめ髪 3 ぱっつん前髪）
            int style = rng.Next(4);
            bool receding = !female && age >= 48 && rng.Next(2) == 0;
            int glasses = rng.Next(100) < (age >= 40 ? 50 : 30) ? 1 + rng.Next(3) : 0; // 1 丸 2 四角 3 縁なし
            int brow = rng.Next(3);
            bool smile = rng.Next(3) == 0;
            int facial = !female && age >= 30 ? rng.Next(6) : 0; // 4 口ひげ 5 無精ひげ
            bool lanyard = rng.Next(2) == 0;

            // ---------------- 後ろ髪（顔の後ろ） ----------------
            if (female && (style == 0 || style == 3))
                for (int y = 10; y < 52; y++)
                for (int x = 8; x < 40; x++)
                    if (InEllipse(x, y, cx, 30, 15.5f, 22f)) cv.Set(x, y, x < cx - 4 ? hair : hairDark);
            if (female && style == 1)
                for (int y = 10; y < 42; y++)
                for (int x = 9; x < 39; x++)
                    if (InEllipse(x, y, cx, 26, 14.5f, 16f)) cv.Set(x, y, x < cx - 4 ? hair : hairDark);
            if (female && style == 2)
                for (int y = 4; y < 14; y++)
                for (int x = 17; x < 31; x++)
                    if (InEllipse(x, y, cx, 8, 6.5f, 5.5f)) cv.Set(x, y, InEllipse(x, y, cx - 2, 7, 3, 2.5f) ? hairLight : hair);

            // ---------------- 体（肩・服） ----------------
            Color suit = female ? Cardigans[rng.Next(Cardigans.Length)] : Suits[rng.Next(Suits.Length)];
            bool cardigan = female && rng.Next(2) == 0;
            Color top = female ? (cardigan ? suit : Blouses[rng.Next(Blouses.Length)]) : suit;
            for (int y = 43; y < H; y++)
            for (int x = 0; x < W; x++)
                if (InEllipse(x, y, cx, 64, 25f, 20f))
                {
                    float shade = x < cx - 8 ? 1.08f : x > cx + 9 ? 0.82f : 1f;
                    cv.Set(x, y, Mul(top, shade));
                }

            // 首（あごの下は影）
            for (int y = 37; y < 47; y++)
            for (int x = 19; x < 29; x++)
                cv.Set(x, y, y < 44 ? skinDeep : x > 25 ? skinShade : skin);   // あごの下は濃い影（あごの線をはっきりさせる）

            if (!female)
            {
                // ワイシャツの V と襟、ネクタイ
                Color shirt = C(244, 244, 240);
                for (int y = 45; y < H; y++)
                {
                    int half = Mathf.Min(9, (y - 45) / 2 + 3);
                    for (int x = cx - half; x < cx + half; x++) cv.Set(x, y, x > cx + 2 ? Mul(shirt, 0.9f) : shirt);
                }
                for (int i = 0; i < 4; i++) { cv.Set(cx - 6 + i, 45 + i, Mul(shirt, 0.8f)); cv.Set(cx + 5 - i, 45 + i, Mul(shirt, 0.8f)); }
                // スーツの襟（下に向かって開く）
                for (int y = 47; y < H; y++)
                {
                    int half = Mathf.Min(10, (y - 45) / 2 + 3);
                    cv.Set(cx - half - 1, y, Mul(suit, 0.6f)); cv.Set(cx + half, y, Mul(suit, 0.6f));
                    cv.Set(cx - half - 2, y, Mul(suit, 1.15f));
                }
                Color tie = Ties[rng.Next(Ties.Length)];
                // 結び目
                for (int y = 46; y < 49; y++) for (int x = cx - 2; x < cx + 2; x++) cv.Set(x, y, x == cx - 2 ? Mul(tie, 1.2f) : tie);
                // 本体（下に行くほど広い）・柄の斜線
                for (int y = 49; y < H; y++)
                {
                    int half = 1 + (y - 49) / 4;
                    for (int x = cx - half - 1; x <= cx + half; x++)
                        cv.Set(x, y, (x + y) % 4 == 0 ? Mul(tie, 0.75f) : x <= cx - half ? Mul(tie, 1.15f) : tie);
                }
            }
            else
            {
                // ブラウスの襟ぐり（丸）、カーディガンなら中のブラウスが見える
                Color blouse = cardigan ? Blouses[rng.Next(Blouses.Length)] : top;
                for (int y = 45; y < H; y++)
                {
                    int half = cardigan ? Mathf.Min(8, (y - 45) / 2 + 3) : 0;
                    for (int x = cx - half; x < cx + half; x++) cv.Set(x, y, blouse);
                }
                for (int x = cx - 5; x <= cx + 5; x++) if (InEllipse(x, 46, cx, 45, 6, 2.2f)) cv.Set(x, 46, Mul(blouse, 0.85f));
                if (rng.Next(2) == 0)
                    for (int x = cx - 4; x <= cx + 4; x++) cv.Set(x, 48 + (Mathf.Abs(x - cx) < 3 ? 1 : 0), C(230, 214, 150)); // ネックレス
            }
            // 社員証のストラップ
            if (lanyard)
            {
                Color strap = C(40, 90, 170);
                for (int y = 46; y < H; y++) { cv.Set(cx - 9 + (y - 46) / 5, y, strap); cv.Set(cx + 8 - (y - 46) / 5, y, strap); }
            }

            // ---------------- 顔 ----------------
            for (int y = 12; y < 42; y++)
            for (int x = 10; x < 38; x++)
            {
                // 下に行くほど細くなる（あご）
                float rx = y > 30 ? 11f - (y - 30) * 0.32f : 11f;
                if (!InEllipse(x, y, cx, 26, rx, 14.2f)) continue;
                Color c = skin;
                if (x <= cx - 6 && y < 30) c = skinLight;          // 光の当たる頬・額
                if (x >= cx + 7) c = skinShade;                      // 右側の影
                if (y >= 38) c = skinShade;                          // あごの下
                cv.Set(x, y, c);
            }
            // 耳
            for (int y = 24; y < 32; y++)
            {
                if (InEllipse(12, y, 12.5f, 28, 2.2f, 4f)) cv.Set(12, y, skinShade);
                if (InEllipse(13, y, 12.5f, 28, 2.2f, 4f)) cv.Set(13, y, skin);
                if (InEllipse(35, y, 35.5f, 28, 2.2f, 4f)) cv.Set(35, y, skinShade);
                if (InEllipse(36, y, 35.5f, 28, 2.2f, 4f)) cv.Set(36, y, skinDeep);
            }
            cv.Set(12, 28, skinDeep); cv.Set(36, 28, Mul(skinDeep, 0.9f));

            // 頬の赤み（若い人・女性）
            if (female || age < 28)
            {
                Color blush = Mix(skin, C(236, 140, 130), 0.35f);
                cv.Set(17, 31, blush); cv.Set(18, 31, blush); cv.Set(30, 31, blush); cv.Set(31, 31, blush);
            }

            // ---------------- 目・眉・鼻・口 ----------------
            void Eye(int ex)
            {
                // 上まぶた → 白目 → 瞳（光）→ 下のふち
                for (int x = ex - 2; x <= ex + 2; x++) cv.Set(x, 24, x == ex + 2 ? Mul(dark, 1.6f) : dark);   // 上まぶた
                cv.Set(ex - 1, 25, C(244, 242, 236)); cv.Set(ex + 2, 25, C(232, 228, 222));                     // 白目
                cv.Set(ex - 1, 26, C(236, 234, 228)); cv.Set(ex + 2, 26, C(226, 222, 216));
                for (int y = 25; y <= 27; y++) { cv.Set(ex, y, C(52, 34, 28)); cv.Set(ex + 1, y, C(38, 26, 22)); } // 瞳（2×3）
                cv.Set(ex, 25, Mix(C(52, 34, 28), Color.white, 0.65f));                                         // 光
                cv.Set(ex - 1, 27, skinShade); cv.Set(ex + 2, 27, skinShade);                                   // 下まぶた
                if (female) { cv.Set(ex + 3, 23, dark); cv.Set(ex - 3, 24, dark); }
                if (age >= 45) for (int x = ex - 1; x <= ex + 1; x++) cv.Set(x, 29, skinShade); // 目の下
            }
            Eye(19); Eye(28);
            // 眉（0 まっすぐ 1 山形 2 太い）
            Color browC = Mul(hair, 0.8f);
            for (int i = 0; i < 6; i++)
            {
                int yl = 20 + (brow == 1 && (i == 2 || i == 3) ? -1 : 0);
                cv.Set(16 + i, yl, browC); cv.Set(26 + i, yl, browC);
                if (brow == 2) { cv.Set(16 + i, yl + 1, browC); cv.Set(26 + i, yl + 1, browC); }
            }
            // 鼻（左に光、右に影、小鼻）
            for (int y = 27; y < 33; y++) { cv.Set(cx + 1, y, skinShade); if (y < 31) cv.Set(cx - 1, y, skinLight); }
            cv.Set(cx - 2, 33, skinDeep); cv.Set(cx + 2, 33, skinDeep); cv.Set(cx, 33, skinShade);
            // 口
            Color lip = female ? C(196, 82, 92) : C(178, 104, 96);
            int mw = 3 + rng.Next(2);
            for (int x = cx - mw; x <= cx + mw - 1; x++) cv.Set(x, 36, Mul(lip, 0.75f));
            for (int x = cx - mw + 1; x <= cx + mw - 2; x++) cv.Set(x, 37, x < cx ? Mix(lip, Color.white, 0.2f) : lip);
            if (smile) { cv.Set(cx - mw - 1, 35, Mul(lip, 0.75f)); cv.Set(cx + mw, 35, Mul(lip, 0.75f)); }
            // 年齢のしわ
            if (age >= 45) { cv.Set(18, 33, skinShade); cv.Set(19, 34, skinShade); cv.Set(30, 33, skinShade); cv.Set(29, 34, skinShade); }
            if (age >= 50) for (int x = 19; x < 30; x += 2) cv.Set(x, 18, skinShade);
            // ひげ
            if (facial == 4) for (int x = cx - 4; x <= cx + 3; x++) { cv.Set(x, 34, hairDark); if (x > cx - 4 && x < cx + 3) cv.Set(x, 35, hair); }
            if (facial == 5)
                for (int y = 33; y < 41; y++)
                for (int x = 15; x < 34; x++)
                    if (cv.IsFigure(x, y) && ((x * 7 + y * 3) % 5 == 0) && Vector2.Distance(new Vector2(x, y), new Vector2(cx, 30)) > 6) cv.Set(x, y, Mix(cv.Get(x, y), hairDark, 0.35f));

            // ---------------- 髪（前） ----------------
            // 生え際（x ごとの、髪が下りてくる高さ）。こめかみ・もみあげの方が低い
            int Hairline(int x)
            {
                int dx = Mathf.Abs(x - cx);
                int h = female ? 15 : (style == 3 ? 16 : 17);
                if (receding && dx < 6) h = 13;                       // 生え際の後退（M字）
                if (dx > 9) h += 8; else if (dx > 7) h += 3;
                if (!female && style == 1 && x < cx - 2) h += 1;        // 七三: 分け目の左は少し下りる
                return h;
            }
            for (int y = 6; y < 34; y++)
            for (int x = 9; x < 40; x++)
            {
                bool cap = InEllipse(x, y, cx, 20, 13.8f, 13f) && y < Hairline(x);
                if (!female && style == 1 && InEllipse(x, y, cx - 4, 10, 8f, 3.5f)) cap = true;             // 七三: 分けた側のふくらみ
                bool sideburn = !female && y >= 18 && y < 26 && (x == 12 || x == 13 || x == 35 || x == 36) && InEllipse(x, y, cx, 22, 13f, 13f);
                bool fringe = false;
                if (female && style == 3) fringe = y < 21 && InEllipse(x, y, cx, 17, 12f, 7.5f);                 // ぱっつん
                if (female && style == 1) fringe = y < 20 && InEllipse(x, y, cx - 3, 15, 11f, 6f);                // 斜めの前髪
                if (female && (style == 0 || style == 2)) fringe = y < 19 && InEllipse(x, y, cx + 2, 14, 12f, 5.5f);
                bool sides = female && style != 2 && y < 40 && (x <= 13 || x >= 35) && InEllipse(x, y, cx, 24, 14f, 17f);
                if (!female && style == 3) cap = cap && InEllipse(x, y, cx, 21, 13f, 12f);                        // スポーツ刈り（短く丸い）
                if (!(cap || sideburn || fringe || sides)) continue;
                Color c = hair;
                if (x > cx + 6) c = hairDark;
                // つやの光（左上の弧）
                if (InEllipse(x, y, cx - 4, 13, 6, 3) && !InEllipse(x, y, cx - 4, 14, 5, 2.4f)) c = hairLight;
                // 毛の流れ（細い線）
                if ((x * 3 + y) % 7 == 0 && c == hair) c = Mix(hair, hairDark, 0.5f);
                cv.Set(x, y, c);
            }
            if (!female && style == 1) for (int y = 8; y < 14; y++) cv.Set(cx - 5, y, Mix(skin, hair, 0.35f));   // 七三の分け目
            if (!female && style == 3)                                                                              // スポーツ刈り: 地肌が少し透ける
                for (int y = 8; y < 18; y++) for (int x = 12; x < 37; x++) if (cv.IsFigure(x, y) && (x + y) % 3 == 0 && cv.Get(x, y) == hair) cv.Set(x, y, Mix(hair, skin, 0.3f));
            if (!female && style == 2) for (int x = 12; x < 37; x += 3) for (int y = 10; y < 15; y++) if (cv.IsFigure(x, y) && (x + y) % 2 == 0) cv.Set(x, y, hairLight); // オールバックの筋
            if (age >= 50)
                for (int i = 0; i < 18; i++) { int gx = 11 + rng.Next(26), gy = 9 + rng.Next(10); if (cv.IsFigure(gx, gy)) cv.Set(gx, gy, C(200, 198, 196)); } // 白髪

            // ---------------- 眼鏡 ----------------
            if (glasses > 0)
            {
                Color fr = glasses == 3 ? C(160, 160, 170) : rng.Next(2) == 0 ? C(40, 36, 40) : C(120, 90, 50);
                void Lens(int ex)
                {
                    for (int x = ex - 3; x <= ex + 3; x++)
                    for (int y = 23; y <= 29; y++)
                    {
                        bool edge = glasses == 1 ? InEllipse(x, y, ex + 0.5f, 26.5f, 3.6f, 3.4f) && !InEllipse(x, y, ex + 0.5f, 26.5f, 2.6f, 2.4f)
                                                 : (x == ex - 3 || x == ex + 3 || y == 23 || y == 29);
                        if (glasses == 3 && y != 29 && x != ex - 3 && x != ex + 3) edge = false; // 縁なしは下と横だけ
                        if (edge) cv.Set(x, y, fr);
                    }
                    cv.Set(ex - 2, 24, Mix(cv.Get(ex - 2, 24), Color.white, 0.6f)); // レンズの光
                }
                Lens(19); Lens(28);
                cv.Set(23, 25, fr); cv.Set(24, 25, fr);
                cv.Set(13, 25, fr); cv.Set(14, 25, fr); cv.Set(34, 25, fr); cv.Set(35, 25, fr);
            }

            // ---------------- 輪郭線（人物の外側を1ドット濃く） ----------------
            var outline = new List<int>();
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (!cv.IsFigure(x, y)) continue;
                if (!cv.IsFigure(x - 1, y) && x > 0 || !cv.IsFigure(x + 1, y) && x < W - 1 || !cv.IsFigure(x, y - 1) && y > 0) outline.Add(y * W + x);
            }
            foreach (int i in outline) cv.Px[i] = Mul(cv.Px[i], 0.62f);

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
