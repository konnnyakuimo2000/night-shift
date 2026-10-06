using System.Collections.Generic;
using NightShift.Data;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>
    /// 社員証写真風のドット絵を社員番号から決定的に生成する（オリジナル描画・外部素材なし）。
    /// 画風: 入国審査ゲーム風の、くすんだ色・大きなドット・黒い縁取り・強い影・無表情。
    /// 32×40 ドットを3倍で表示。光は左から。
    /// 顔立ち（輪郭・目・眉・鼻・口・耳）、髪型と髪の色、服、眼鏡、ひげ、小物を組み合わせて、似た顔が並ばないようにする。
    /// 年齢（白髪・しわ・生え際）と、名前から推定した性別で変える。EmployeeData.portrait が設定されていればそちらを優先する。
    /// </summary>
    public static class PortraitPainter
    {
        const int W = 32, H = 40, Scale = 3;
        const int cx = 16;
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        static Color Mix(Color a, Color b, float t) { var c = Color.Lerp(a, b, t); c.a = 1; return c; }

        static readonly Color[] Skins = { C(222, 190, 160), C(214, 178, 148), C(204, 166, 134), C(194, 156, 124), C(182, 144, 112), C(228, 198, 172), C(200, 160, 136) };
        // 黒・こげ茶・茶・明るい茶（90年代の茶髪）・脱色した金茶・赤茶
        static readonly Color[] Hairs = { C(30, 26, 26), C(46, 38, 34), C(64, 48, 36), C(92, 66, 44), C(126, 86, 48), C(170, 132, 84), C(104, 56, 40) };
        static readonly int[] HairWeights = { 30, 22, 16, 10, 8, 4, 4 };
        static readonly Color[] Coats = { C(62, 66, 72), C(78, 74, 66), C(48, 52, 50), C(86, 72, 58), C(64, 74, 64), C(70, 64, 76), C(92, 88, 80), C(54, 58, 74) };
        static readonly Color[] Ties = { C(120, 40, 40), C(46, 60, 100), C(52, 84, 58), C(112, 90, 46), C(84, 50, 86), C(70, 70, 76) };
        static readonly Color[] Shirts = { C(196, 196, 186), C(186, 176, 172), C(170, 180, 186), C(192, 186, 164), C(176, 186, 170) };
        static readonly Color[] Knits = { C(120, 56, 56), C(66, 80, 110), C(98, 104, 80), C(150, 128, 96), C(84, 66, 92), C(170, 166, 150) };
        static readonly Color[] Backs = { C(148, 156, 140), C(156, 152, 136), C(140, 148, 150), C(160, 150, 150), C(136, 144, 132), C(150, 146, 160) };
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

        static bool InEllipse(int x, int y, float ex, float ey, float rx, float ry) =>
            ((x + 0.5f - ex) * (x + 0.5f - ex)) / (rx * rx) + ((y + 0.5f - ey) * (y + 0.5f - ey)) / (ry * ry) <= 1f;

        static int Pick(System.Random rng, int[] weights)
        {
            int total = 0; foreach (int w in weights) total += w;
            int r = rng.Next(total);
            for (int i = 0; i < weights.Length; i++) { if (r < weights[i]) return i; r -= weights[i]; }
            return weights.Length - 1;
        }

        static Sprite Generate(EmployeeData e)
        {
            int seed = 17;
            foreach (char ch in e.employeeId ?? "") seed = seed * 31 + ch;
            var rng = new System.Random(seed);
            var cv = new Canvas();
            bool female = IsFemale(e);
            int age = e.age;
            float F(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // ================= 顔立ちの決定 =================
            float faceW = female ? F(5.4f, 6.6f) : F(5.8f, 7.4f);   // 頬の幅（半分）
            int faceTop = rng.Next(7, 10);
            int chinY = rng.Next(26, 30);
            int jaw = rng.Next(3);                                   // 0 とがった 1 四角 2 丸
            int eyeType = rng.Next(6);                               // 0 点 1 丸い 2 たれ目 3 つり目 4 眠そう 5 細い
            int browType = rng.Next(5);                              // 0 細い 1 太い 2 つり上がり 3 下がり 4 つながり気味
            int noseType = rng.Next(4);                              // 0 小さい 1 長い 2 わし鼻 3 幅広
            int mouthType = rng.Next(5);                             // 0 薄い 1 厚い 2 片側が上がる 3 への字 4 少し開く
            int eyeGap = rng.Next(3, 5);                             // 目の離れ具合
            bool bigEars = rng.Next(4) == 0;
            bool mole = rng.Next(4) == 0;
            bool freckles = rng.Next(10) == 0;
            bool bags = age >= 33 && rng.Next(5) < 2;
            bool cheekbones = rng.Next(3) == 0;

            int hairColorIdx = Pick(rng, HairWeights);
            if (age >= 45 && hairColorIdx >= 4) hairColorIdx = 1;   // 年配の人は明るく染めていない
            int hairStyle = female ? rng.Next(9) : rng.Next(10);
            bool receding = !female && age >= 44 && rng.Next(2) == 0;
            if (!female && hairStyle == 9 && age < 40) hairStyle = 0; // はげは年配だけ
            int glasses = rng.Next(100) < (age >= 40 ? 50 : 30) ? 1 + rng.Next(5) : 0; // 1 丸 2 四角 3 太い黒縁 4 色付き 5 縁なし
            int facial = !female && age >= 26 ? Pick(rng, new[] { 50, 10, 6, 14, 8, 6 }) : 0; // 1 口ひげ 2 あごひげ全体 3 無精ひげ 4 ヤギひげ 5 もみあげ
            int clothes = female ? rng.Next(5) : Pick(rng, new[] { 40, 16, 10, 14, 14, 6 });
            bool earrings = female ? rng.Next(5) < 2 : rng.Next(20) == 0;
            bool headband = female && rng.Next(7) == 0;
            bool hairpin = female && !headband && rng.Next(5) == 0;
            bool pen = rng.Next(5) == 0;
            int expression = rng.Next(4);                            // 0 無表情 1 眉間にしわ 2 疲れ 3 わずかな笑み

            // ================= 色 =================
            Color back = Backs[rng.Next(Backs.Length)];
            Color skin = Skins[rng.Next(Skins.Length)];
            Color skinHi = Mix(skin, C(236, 224, 200), 0.3f);
            Color skinSh = Mul(skin, 0.78f);
            Color skinDeep = Mul(skin, 0.62f);
            Color baseHair = Hairs[hairColorIdx];
            Color hair = age >= 50 ? Mix(baseHair, C(156, 154, 146), Mathf.Clamp01((age - 46) / 12f))
                      : age >= 40 && rng.Next(3) == 0 ? Mix(baseHair, C(130, 128, 122), 0.3f) : baseHair;
            Color hairHi = Mix(hair, C(210, 200, 176), 0.24f);
            Color hairSh = Mul(hair, 0.68f);

            // ================= 背景 =================
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                cv.Set(x, y, y > 30 ? Mul(back, 0.88f) : back, false);

            // 顔の幅（行ごと）
            float HalfAt(int y)
            {
                float t = (y - faceTop) / (float)(chinY - faceTop);
                if (t < 0) return -1;
                if (t < 0.15f) return faceW * (0.78f + t / 0.15f * 0.22f);
                if (t < 0.62f) return faceW;
                float u = Mathf.Clamp01((t - 0.62f) / 0.38f);
                switch (jaw)
                {
                    case 0: return faceW * (1 - u * 0.8f);
                    case 1: return u < 0.7f ? faceW * (1 - u * 0.12f) : faceW * (0.92f - (u - 0.7f) / 0.3f * 0.5f);
                    default: return faceW * Mathf.Sqrt(Mathf.Max(0.05f, 1 - u * u * 0.85f));
                }
            }
            int span = chinY - faceTop;
            int eyeY = faceTop + Mathf.RoundToInt(span * 0.42f);
            int browY = eyeY - 2;
            int noseBottom = eyeY + Mathf.RoundToInt(span * 0.24f) + (noseType == 1 || noseType == 2 ? 1 : 0);
            int mouthY = Mathf.Min(chinY - 2, noseBottom + 3);
            int lx = cx - eyeGap - 1, rxE = cx + eyeGap;     // 左右の目の x（左の点）

            // ================= 後ろ髪 =================
            void BackHair(float ey, float rx, float ry, int y0, int y1)
            {
                for (int y = y0; y < y1; y++) for (int x = 2; x < 30; x++) if (InEllipse(x, y, cx, ey, rx, ry)) cv.Set(x, y, x > cx + 3 ? hairSh : hair);
            }
            if (female)
            {
                if (hairStyle == 0 || hairStyle == 3 || hairStyle == 7 || hairStyle == 8) BackHair(19, 10.5f, 14.5f, 5, 34);     // ロング
                if (hairStyle == 1) BackHair(16, 9.8f, 10.5f, 5, 27);                                                            // ボブ
                if (hairStyle == 6) BackHair(18, 12f, 13.5f, 4, 31);                                                             // ソバージュ（大きく広がる）
                if (hairStyle == 2) for (int y = 1; y < 9; y++) for (int x = 11; x < 21; x++) if (InEllipse(x, y, cx, 5, 4.4f, 3.6f)) cv.Set(x, y, x < cx ? hairHi : hair); // まとめ髪
                if (hairStyle == 4) for (int y = 10; y < 30; y++) for (int x = 22; x < 28; x++) if (InEllipse(x, y, 25, 20, 2.6f, 9f)) cv.Set(x, y, hairSh); // ポニーテール（横から見える）
            }
            else if (hairStyle == 7) BackHair(17, 9.6f, 12.5f, 6, 31);                                                          // ロン毛・襟足が長い

            // ================= 服 =================
            Color coat = Coats[rng.Next(Coats.Length)];
            Color knit = Knits[rng.Next(Knits.Length)];
            Color shirt = Shirts[rng.Next(Shirts.Length)];
            Color tie = Ties[rng.Next(Ties.Length)];
            bool workJacket = (!female && clothes == 4) || (female && clothes == 4);
            if (workJacket) coat = Mix(C(150, 150, 130), coat, 0.3f);           // 作業着は明るい灰緑
            Color outer = (female && clothes == 1) ? knit : (clothes == 2 ? knit : coat);
            for (int y = 29; y < H; y++)
            for (int x = 0; x < W; x++)
                if (InEllipse(x, y, cx, 43, 17f, 13f))
                    cv.Set(x, y, x < cx - 6 ? Mix(outer, C(200, 196, 180), 0.12f) : x > cx + 5 ? Mul(outer, 0.72f) : outer);
            // 首（あごの下は濃い影）
            for (int y = chinY - 2; y < 32; y++) for (int x = cx - 3; x < cx + 4; x++) cv.Set(x, y, y < chinY + 1 ? skinDeep : x > cx + 1 ? skinSh : skin);

            void ShirtV(Color c, int widen = 0)
            {
                for (int y = 30; y < H; y++)
                {
                    int half = Mathf.Min(5 + widen, (y - 30) / 2 + 2 + widen);
                    for (int x = cx - half; x < cx + half; x++) cv.Set(x, y, x > cx ? Mul(c, 0.85f) : c);
                    cv.Set(cx - half - 1, y, Mul(outer, 0.55f)); cv.Set(cx + half, y, Mul(outer, 0.55f));
                }
            }
            if (!female)
            {
                switch (clothes)
                {
                    case 0: // スーツとネクタイ
                        ShirtV(shirt);
                        cv.Set(cx - 1, 31, tie); cv.Set(cx, 31, Mul(tie, 0.8f));
                        for (int y = 32; y < H; y++) { cv.Set(cx - 1, y, tie); cv.Set(cx, y, Mul(tie, 0.75f)); if (y > 35) cv.Set(cx - 2, y, tie); }
                        break;
                    case 1: // ノーネクタイ（襟を開けている）
                        ShirtV(shirt);
                        for (int y = 30; y < 34; y++) cv.Set(cx, y, skinSh);
                        cv.Set(cx - 1, 30, skinSh);
                        break;
                    case 2: // タートルネック
                        for (int y = 28; y < 32; y++) for (int x = cx - 4; x < cx + 5; x++) cv.Set(x, y, x > cx + 1 ? Mul(knit, 0.75f) : knit);
                        for (int x = cx - 4; x < cx + 5; x++) cv.Set(x, 30, Mul(knit, 0.8f));
                        break;
                    case 3: // ベストとネクタイ
                        ShirtV(shirt, 1);
                        for (int y = 33; y < H; y++) { int half = 2 + (y - 33) / 3; for (int x = cx - half; x < cx + half; x++) if (Mathf.Abs(x - cx) > 0) cv.Set(x, y, Mix(knit, coat, 0.3f)); }
                        for (int y = 31; y < H; y++) cv.Set(cx, y, tie);
                        break;
                    case 4: // 作業着（ファスナー）
                        for (int y = 30; y < 34; y++) for (int x = cx - 3; x < cx + 3; x++) cv.Set(x, y, Mul(outer, 0.85f));
                        for (int y = 31; y < H; y++) cv.Set(cx, y, C(150, 146, 130));
                        break;
                    default: // ボウタイ
                        ShirtV(shirt);
                        cv.Set(cx - 2, 31, tie); cv.Set(cx - 1, 31, Mul(tie, 0.8f)); cv.Set(cx, 31, tie); cv.Set(cx + 1, 31, Mul(tie, 0.8f));
                        break;
                }
            }
            else
            {
                switch (clothes)
                {
                    case 0: ShirtV(shirt); for (int x = cx - 3; x <= cx + 2; x++) cv.Set(x, 31, Mul(shirt, 0.7f)); break;   // ジャケットとブラウス
                    case 1: ShirtV(shirt); for (int y = 33; y < H; y += 2) cv.Set(cx - 6, y, Mul(knit, 0.6f)); break;        // カーディガン（ボタン）
                    case 2: for (int y = 28; y < 32; y++) for (int x = cx - 4; x < cx + 5; x++) cv.Set(x, y, x > cx + 1 ? Mul(knit, 0.75f) : knit); break; // タートル
                    case 3: // リボンのブラウス
                        ShirtV(shirt);
                        Color rib = Mix(tie, C(170, 60, 70), 0.5f);
                        cv.Set(cx - 2, 31, rib); cv.Set(cx - 1, 32, rib); cv.Set(cx, 31, Mul(rib, 0.8f)); cv.Set(cx + 1, 32, rib); cv.Set(cx - 1, 33, rib); cv.Set(cx, 34, rib);
                        break;
                    default: for (int y = 31; y < H; y++) cv.Set(cx, y, C(150, 146, 130)); break; // 作業着
                }
            }
            if (pen) { cv.Set(cx + 7, 33, C(40, 60, 120)); cv.Set(cx + 7, 34, C(40, 60, 120)); cv.Set(cx + 8, 34, C(160, 160, 150)); }

            // ================= 顔 =================
            for (int y = faceTop; y <= chinY; y++)
            {
                float half = HalfAt(y);
                if (half <= 0) continue;
                for (int x = 0; x < W; x++)
                {
                    float dx = x + 0.5f - cx;
                    if (dx < -half || dx > half) continue;
                    Color c = dx < -2.5f ? skinHi : dx > 2.2f ? skinSh : skin;
                    if (y >= chinY - 1) c = skinSh;
                    cv.Set(x, y, c);
                }
            }
            // 頬骨の影
            if (cheekbones) for (int y = eyeY + 2; y < eyeY + 5; y++) { cv.Set(cx + Mathf.RoundToInt(faceW) - 1, y, skinDeep); cv.Set(cx - Mathf.RoundToInt(faceW) + 1, y, skinSh); }
            // 耳
            int earL = cx - Mathf.RoundToInt(faceW) - 1, earR = cx + Mathf.RoundToInt(faceW);
            for (int y = eyeY - 1; y < eyeY + (bigEars ? 5 : 3); y++) { cv.Set(earL, y, skinSh); cv.Set(earR, y, skinDeep); if (bigEars) { cv.Set(earL - 1, y, skinSh); cv.Set(earR + 1, y, skinDeep); } }
            cv.Set(earL, eyeY + 1, skinDeep);
            if (earrings)
            {
                Color gold = C(200, 180, 110);
                cv.Set(earL, eyeY + (bigEars ? 5 : 3), gold); cv.Set(earR, eyeY + (bigEars ? 5 : 3), Mul(gold, 0.8f));
            }

            // 目元のくぼみ
            for (int x = cx - eyeGap - 3; x <= cx + eyeGap + 3; x++)
                if (cv.IsFigure(x, eyeY - 1) && x != cx) cv.Set(x, eyeY - 1, Mix(cv.Get(x, eyeY - 1), skinDeep, expression == 2 ? 0.7f : 0.5f));

            // 眉
            Color brow = Mul(hair, 0.7f);
            for (int i = 0; i < 4; i++)
            {
                int ly = browY, ry = browY;
                if (browType == 2) { ly = browY + (i == 3 ? 1 : 0); ry = browY + (i == 0 ? 1 : 0); }          // つり上がり（内側が下がる）
                if (browType == 3) { ly = browY + (i == 0 ? 1 : 0); ry = browY + (i == 3 ? 1 : 0); }          // 下がり眉
                cv.Set(lx - 1 + i, ly, brow); cv.Set(rxE - 1 + i, ry, brow);
                if (browType == 1) { cv.Set(lx - 1 + i, ly - 1, brow); cv.Set(rxE - 1 + i, ry - 1, brow); }
            }
            if (browType == 0) { cv.Set(lx - 1, browY, Mix(brow, skin, 0.4f)); cv.Set(rxE + 2, browY, Mix(brow, skin, 0.4f)); }
            if (browType == 4) for (int x = lx + 3; x < rxE; x++) cv.Set(x, browY, Mix(brow, skin, 0.35f));
            if (expression == 1) { cv.Set(cx - 1, browY + 1, skinDeep); cv.Set(cx + 1, browY + 1, skinDeep); }

            // 目
            void Eye(int ex, bool left)
            {
                int outer = left ? ex - 1 : ex + 2;
                switch (eyeType)
                {
                    case 0: cv.Set(ex + (left ? 0 : 1), eyeY, Ink); break;                                        // 点
                    case 1: cv.Set(ex, eyeY, Ink); cv.Set(ex + 1, eyeY, Ink); cv.Set(left ? ex - 1 : ex + 2, eyeY, C(210, 206, 196)); break; // 丸い（白目つき）
                    case 2: cv.Set(ex, eyeY, Ink); cv.Set(ex + 1, eyeY, Mul(skinDeep, 0.8f)); cv.Set(outer, eyeY + 1, skinDeep); break; // たれ目
                    case 3: cv.Set(ex, eyeY, Ink); cv.Set(ex + 1, eyeY, Ink); cv.Set(outer, eyeY - 1, Ink); break;                    // つり目
                    case 4: cv.Set(ex, eyeY, Mul(skinDeep, 0.7f)); cv.Set(ex + 1, eyeY, Mul(skinDeep, 0.7f)); cv.Set(ex, eyeY + 1, Ink); cv.Set(ex + 1, eyeY + 1, Mul(skinDeep, 0.8f)); break; // 眠そう（まぶたが重い）
                    default: cv.Set(ex, eyeY, Mul(Ink, 1.6f)); cv.Set(ex + 1, eyeY, Mul(Ink, 1.6f)); break;           // 細い
                }
                if (female) cv.Set(outer, eyeY - 1, Ink);
                cv.Set(ex, eyeY + 1, cv.Get(ex, eyeY + 1) == Ink ? Ink : skinSh);
                if (bags || expression == 2) { cv.Set(ex, eyeY + 2, skinSh); cv.Set(ex + 1, eyeY + 2, skinSh); }
            }
            Eye(lx, true); Eye(rxE, false);

            // 鼻
            for (int y = eyeY + 1; y < noseBottom; y++) cv.Set(cx + 1, y, skinSh);
            switch (noseType)
            {
                case 0: cv.Set(cx, noseBottom, skinSh); cv.Set(cx + 1, noseBottom, skinDeep); break;
                case 1: cv.Set(cx - 1, noseBottom, skinDeep); cv.Set(cx, noseBottom, skinSh); cv.Set(cx + 1, noseBottom, skinDeep); break;
                case 2: cv.Set(cx + 2, noseBottom - 2, skinDeep); cv.Set(cx + 2, noseBottom - 1, skinSh); cv.Set(cx - 1, noseBottom, skinDeep); cv.Set(cx + 1, noseBottom, skinDeep); break; // わし鼻
                default: for (int x = cx - 2; x <= cx + 3; x++) cv.Set(x, noseBottom, x == cx || x == cx + 1 ? skinSh : skinDeep); break; // 幅広
            }
            cv.Set(cx - 1, noseBottom - 2, skinHi);

            // 口
            Color mouth = Mul(skinDeep, 0.75f);
            Color lip = female ? Mix(skin, C(150, 66, 70), 0.5f) : Mix(skin, C(150, 90, 80), 0.3f);
            int mw = female ? 2 : 2 + rng.Next(2);
            for (int x = cx - mw; x <= cx + mw; x++) cv.Set(x, mouthY, mouth);
            switch (mouthType)
            {
                case 1: for (int x = cx - mw + 1; x <= cx + mw - 1; x++) cv.Set(x, mouthY + 1, lip); break;                              // 厚い唇
                case 2: cv.Set(cx + mw + 1, mouthY - 1, mouth); break;                                                                   // 片側が上がる
                case 3: cv.Set(cx - mw - 1, mouthY + 1, mouth); cv.Set(cx + mw + 1, mouthY + 1, mouth); break;                          // への字
                case 4: cv.Set(cx, mouthY, Ink); cv.Set(cx - 1, mouthY, Mul(Ink, 1.5f)); cv.Set(cx, mouthY + 1, lip); break;           // 少し開く
                default: break;
            }
            if (expression == 3) { cv.Set(cx - mw - 1, mouthY - 1, mouth); cv.Set(cx + mw + 1, mouthY - 1, mouth); }
            if (female && mouthType != 1) for (int x = cx - 1; x <= cx + 1; x++) cv.Set(x, mouthY + 1, lip);

            // 年齢・ほくろ・そばかす
            if (age >= 38) { cv.Set(cx - 4, mouthY - 2, skinSh); cv.Set(cx - 4, mouthY - 1, skinSh); cv.Set(cx + 4, mouthY - 2, skinDeep); cv.Set(cx + 4, mouthY - 1, skinDeep); }
            if (age >= 48) for (int x = cx - 4; x <= cx + 4; x += 2) cv.Set(x, faceTop + 2, skinSh);
            if (mole) { var mx = rng.Next(2) == 0 ? cx - 3 : cx + 3; cv.Set(mx, mouthY - 1 - rng.Next(3), Mul(skinDeep, 0.55f)); }
            if (freckles) foreach (var p in new[] { (cx - 4, eyeY + 2), (cx - 3, eyeY + 3), (cx + 3, eyeY + 2), (cx + 4, eyeY + 3), (cx - 5, eyeY + 3) }) cv.Set(p.Item1, p.Item2, Mix(skin, C(150, 100, 70), 0.45f));

            // ひげ
            switch (facial)
            {
                case 1: for (int x = cx - mw - 1; x <= cx + mw + 1; x++) cv.Set(x, mouthY - 1, x > cx + 1 ? hairSh : hair); break;                        // 口ひげ
                case 2: for (int y = mouthY - 1; y <= chinY; y++) for (int x = 0; x < W; x++) if (cv.IsFigure(x, y) && Mathf.Abs(x - cx) <= HalfAt(y) && !(y == mouthY && Mathf.Abs(x - cx) <= mw)) cv.Set(x, y, (x + y) % 3 == 0 ? hairSh : hair); break; // あごひげ全体
                case 3: for (int y = mouthY - 2; y <= chinY; y++) for (int x = 0; x < W; x++) if (cv.IsFigure(x, y) && Mathf.Abs(x - cx) <= HalfAt(y) && (x + y) % 2 == 0 && !(y == mouthY)) cv.Set(x, y, Mix(cv.Get(x, y), hairSh, 0.45f)); break; // 無精ひげ
                case 4: for (int y = mouthY + 1; y <= chinY + 1; y++) for (int x = cx - 1; x <= cx + 1; x++) cv.Set(x, y, x > cx ? hairSh : hair); break;   // ヤギひげ
                case 5: for (int y = eyeY - 2; y < eyeY + 4; y++) { cv.Set(earL + 1, y, hair); cv.Set(earR - 1, y, hairSh); } break;                     // もみあげ
            }

            // ================= 前髪・頭 =================
            int fw = Mathf.RoundToInt(faceW);
            for (int y = 1; y < 30; y++)
            for (int x = 3; x < 29; x++)
            {
                int dx = Mathf.Abs(x - cx);
                int line = faceTop + 3;                                       // 生え際
                if (receding && dx < 4) line = faceTop + 1;
                if (dx > fw - 1) line += 5;
                bool cap = InEllipse(x, y, cx, faceTop + 3, faceW + 2f, 8f) && y < line;
                bool fringe = false, sides = false;
                if (!female)
                {
                    switch (hairStyle)
                    {
                        case 1: if (InEllipse(x, y, cx - 3, faceTop - 2, 5.5f, 2.5f)) cap = true; break;                    // 七三
                        case 3: cap = cap && InEllipse(x, y, cx, faceTop + 4, faceW + 1.2f, 7.2f); break;                     // 坊主
                        case 4: if (y >= faceTop + 1 && y < faceTop + 5 && dx >= 1 && dx <= 4) fringe = true; break;          // センター分け
                        case 5: cap = InEllipse(x, y, cx, faceTop + 2, faceW + 3f, 8.5f) && y < line + 1; break;            // パーマ（大きく丸い）
                        case 6: cap = cap || (y < faceTop - 3 && y > faceTop - 7 && (x % 3 == 0) && dx < 6); break;           // ツンツン
                        case 8: if (dx > fw - 2 && y > faceTop) cap = false; break;                                           // 刈り上げ（横が短い）
                        case 9: cap = cap && dx > 3 && y > faceTop - 2; break;                                                // はげ（横だけ残る）
                    }
                }
                else
                {
                    switch (hairStyle)
                    {
                        case 3: case 7: fringe = y < faceTop + 5 && InEllipse(x, y, cx, faceTop + 2, faceW + 1.5f, 4.6f); break;  // ぱっつん・姫カット
                        case 1: fringe = y < faceTop + 5 && InEllipse(x, y, cx - 2, faceTop + 1, faceW + 1f, 4f); break;          // ボブの斜め前髪
                        case 5: cap = InEllipse(x, y, cx, faceTop + 3, faceW + 2f, 8f) && y < line + 1; break;                    // ショート
                        case 8: fringe = y < faceTop + 6 && InEllipse(x, y, cx - 4, faceTop + 1, faceW, 4.6f); break;            // 横分け（片方に流す）
                        default: fringe = y < faceTop + 4 && InEllipse(x, y, cx + 2, faceTop + 1, faceW + 1.5f, 3.4f); break;
                    }
                    sides = hairStyle != 2 && hairStyle != 4 && hairStyle != 5 && y < 28 && (x <= cx - fw || x >= cx + fw) && InEllipse(x, y, cx, faceTop + 7, faceW + 3.5f, 12f);   // 横の髪は顔の外側だけ
                    if (hairStyle == 7 && y > eyeY + 2 && y < eyeY + 6 && (x == cx - fw || x == cx + fw)) sides = true;            // 姫カットの横の段
                    if (hairStyle == 1 && y > chinY - 3) sides = false;                                                             // ボブはあごまで
                }
                if (!(cap || fringe || sides)) continue;
                Color c = x > cx + 3 ? hairSh : x < cx - 4 && y < faceTop + 1 ? hairHi : hair;
                if (!female && hairStyle == 2 && (x % 3 == 0) && y < faceTop + 1) c = hairHi;                   // オールバックの筋
                if (!female && hairStyle == 3 && (x + y) % 2 == 0) c = Mix(hair, skin, 0.35f);                   // 坊主: 地肌が透ける
                if ((female && hairStyle == 6) || (!female && hairStyle == 5)) if ((x * 3 + y * 5) % 7 == 0) c = hairSh;   // パーマ・ソバージュのうねり
                cv.Set(x, y, c);
            }
            if (!female && hairStyle == 1) for (int y = faceTop - 3; y < faceTop + 1; y++) cv.Set(cx - 3, y, Mix(hair, skin, 0.45f)); // 分け目
            if (!female && hairStyle == 4) for (int y = faceTop - 3; y < faceTop + 2; y++) cv.Set(cx, y, Mix(hair, skin, 0.45f));     // 真ん中の分け目
            if (age >= 50) for (int i = 0; i < 8; i++) { int gx = 7 + rng.Next(18), gy = faceTop - 4 + rng.Next(6); if (cv.IsFigure(gx, gy)) cv.Set(gx, gy, C(176, 174, 166)); }

            // 髪飾り
            if (headband) for (int x = cx - fw - 1; x <= cx + fw + 1; x++) for (int y = 0; y < H; y++) if (y == faceTop - 1 - (Mathf.Abs(x - cx) < 3 ? 1 : 0) && cv.IsFigure(x, y)) cv.Set(x, y, Mix(tie, C(170, 60, 80), 0.5f));
            if (hairpin) { cv.Set(cx + 3, faceTop + 1, C(200, 180, 110)); cv.Set(cx + 4, faceTop + 1, C(200, 180, 110)); }

            // ================= 眼鏡 =================
            if (glasses > 0)
            {
                Color fr = glasses == 3 ? Ink : glasses == 5 ? C(150, 146, 136) : glasses == 2 ? C(70, 64, 58) : C(110, 104, 92);
                int top = eyeY - 1, bottom = eyeY + 2;
                for (int side = 0; side < 2; side++)
                {
                    int x0 = side == 0 ? lx - 2 : rxE - 1, x1 = x0 + 4;
                    for (int x = x0; x <= x1; x++)
                    {
                        bool corner = glasses == 1 && (x == x0 || x == x1);
                        if (glasses != 5) cv.Set(x, top, corner ? cv.Get(x, top) : fr);
                        cv.Set(x, bottom, corner ? cv.Get(x, bottom) : fr);
                        if (glasses == 3) cv.Set(x, top - 1, Mul(fr, 1.3f));
                        if (glasses == 4) for (int y = top + 1; y < bottom; y++) if (x > x0 && x < x1) cv.Set(x, y, Mix(cv.Get(x, y), C(70, 60, 50), 0.55f)); // 色付き
                    }
                    for (int y = top; y <= bottom; y++) { cv.Set(x0, y, (glasses == 1 && (y == top || y == bottom)) ? cv.Get(x0, y) : fr); cv.Set(x1, y, (glasses == 1 && (y == top || y == bottom)) ? cv.Get(x1, y) : fr); }
                }
                for (int x = lx + 3; x < rxE - 1; x++) cv.Set(x, eyeY, fr);
                cv.Set(lx - 1, eyeY, Mix(cv.Get(lx - 1, eyeY), Color.white, 0.3f));
            }

            // ================= 黒い縁取り（人物の外周） =================
            var edge = new List<int>();
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (cv.IsFigure(x, y) && ((x > 0 && !cv.IsFigure(x - 1, y)) || (x < W - 1 && !cv.IsFigure(x + 1, y)) || (y > 0 && !cv.IsFigure(x, y - 1))))
                    edge.Add(y * W + x);
            foreach (int i in edge) cv.Px[i] = Ink;

            // ================= 色をくすませる（彩度を落とし、少し黄ばませ、平らな色に） =================
            for (int i = 0; i < cv.Px.Length; i++)
            {
                var c = cv.Px[i];
                float l = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                c = Mix(c, new Color(l, l, l), 0.35f);
                c = new Color(c.r * 1.02f, c.g, c.b * 0.9f, 1f);
                c = new Color(Mathf.Round(c.r * 24) / 24f, Mathf.Round(c.g * 24) / 24f, Mathf.Round(c.b * 24) / 24f, 1f);
                cv.Px[i] = c;
            }

            // ================= 拡大してテクスチャに =================
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
