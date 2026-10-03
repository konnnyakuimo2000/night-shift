using System.Collections.Generic;
using NightShift.Data;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>
    /// 社員証写真風のドット絵を社員番号から決定的に生成する（オリジナル描画・外部素材なし）。
    /// EmployeeData.portrait が設定されていればそちらを優先する。
    /// </summary>
    public static class PortraitPainter
    {
        const int W = 24, H = 30, Scale = 4;
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        static readonly Color[] Skins = { C(246, 214, 182), C(236, 196, 158), C(222, 178, 140), C(250, 224, 196) };
        static readonly Color[] Hairs = { C(30, 26, 26), C(58, 40, 30), C(88, 60, 40), C(44, 36, 34) };
        static readonly Color[] Suits = { C(40, 50, 80), C(70, 70, 76), C(30, 30, 34), C(90, 70, 52), C(60, 80, 70) };
        static readonly Color[] Ties = { C(150, 30, 40), C(30, 60, 140), C(40, 100, 60), C(120, 90, 30), C(90, 40, 110) };

        public static Sprite Get(EmployeeData e)
        {
            if (e == null) return null;
            if (e.portrait != null) return e.portrait;
            string key = e.employeeId + "|" + e.age;
            if (_cache.TryGetValue(key, out var s)) return s;
            s = Generate(e);
            _cache[key] = s;
            return s;
        }

        static Sprite Generate(EmployeeData e)
        {
            int seed = 17;
            foreach (char ch in e.employeeId ?? "") seed = seed * 31 + ch;
            var rng = new System.Random(seed);
            var px = new Color[W * H];
            void Set(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) px[(H - 1 - y) * W + x] = c; }
            bool InEllipse(int x, int y, float cx, float cy, float rx, float ry) =>
                ((x + 0.5f - cx) * (x + 0.5f - cx)) / (rx * rx) + ((y + 0.5f - cy) * (y + 0.5f - cy)) / (ry * ry) <= 1f;

            // 背景（証明写真風の淡いグラデーション）
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    Set(x, y, Color.Lerp(C(196, 214, 230), C(160, 184, 206), y / (float)H));

            Color skin = Skins[rng.Next(Skins.Length)];
            Color skinShade = skin * 0.88f; skinShade.a = 1;
            Color hair = e.age >= 50 ? C(150, 148, 146) : e.age >= 44 && rng.Next(3) == 0 ? C(96, 92, 90) : Hairs[rng.Next(Hairs.Length)];
            Color suit = Suits[rng.Next(Suits.Length)];
            Color tie = Ties[rng.Next(Ties.Length)];
            int hairStyle = rng.Next(4);   // 0: 短髪 1: 分け目 2: 長め 3: 前髪
            bool glasses = rng.Next(100) < 35;
            bool longHair = hairStyle == 2;

            const float cx = 12f, cy = 12.5f;

            // 長い髪（顔の後ろ）
            if (longHair)
                for (int y = 6; y < 25; y++)
                    for (int x = 4; x < 20; x++)
                        if (InEllipse(x, y, cx, 14f, 7.6f, 10.5f)) Set(x, y, hair);

            // 肩・スーツ
            for (int y = 22; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (InEllipse(x, y, cx, 31f, 12.5f, 9f)) Set(x, y, suit);
            // シャツとネクタイ
            for (int y = 22; y < H; y++)
            {
                int half = (y - 22) / 2 + 1;
                for (int x = (int)cx - half; x < (int)cx + half; x++) Set(x, y, C(240, 240, 236));
            }
            for (int y = 23; y < H; y++) { Set(11, y, tie); Set(12, y, tie); }
            // 首
            for (int y = 19; y < 23; y++) for (int x = 10; x < 14; x++) Set(x, y, skinShade);

            // 顔
            for (int y = 4; y < 22; y++)
                for (int x = 4; x < 20; x++)
                    if (InEllipse(x, y, cx, cy, 6.2f, 7.6f)) Set(x, y, skin);
            // 耳
            Set(5, 12, skinShade); Set(5, 13, skinShade); Set(18, 12, skinShade); Set(18, 13, skinShade);

            // 髪（上部）
            for (int y = 3; y < 10; y++)
                for (int x = 4; x < 20; x++)
                {
                    bool top = InEllipse(x, y, cx, 9.5f, 6.8f, 6.2f) && y < 8;
                    bool fringe = hairStyle == 3 && y < 10 && InEllipse(x, y, cx, 8f, 6f, 3f);
                    bool sides = (hairStyle == 1 || longHair) && y < 12 && (x <= 5 || x >= 18) && InEllipse(x, y, cx, 10f, 7f, 7f);
                    if (top || fringe || sides) Set(x, y, hair);
                }
            if (hairStyle == 1) for (int y = 4; y < 8; y++) Set(9, y, skin); // 分け目

            // 眉・目・口
            Color dark = C(40, 32, 30);
            Set(8, 10, hair); Set(9, 10, hair); Set(14, 10, hair); Set(15, 10, hair);
            Set(9, 12, dark); Set(14, 12, dark);
            Set(11, 15, skinShade); Set(12, 15, skinShade);
            int mouthW = 1 + rng.Next(2);
            for (int x = 12 - mouthW; x < 12 + mouthW; x++) Set(x, 17, C(170, 90, 80));

            if (glasses)
            {
                Color g = C(50, 50, 60);
                for (int x = 7; x <= 10; x++) { Set(x, 11, g); Set(x, 13, g); }
                for (int x = 13; x <= 16; x++) { Set(x, 11, g); Set(x, 13, g); }
                Set(7, 12, g); Set(10, 12, g); Set(13, 12, g); Set(16, 12, g); Set(11, 11, g); Set(12, 11, g);
            }

            var tex = new Texture2D(W * Scale, H * Scale, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.hideFlags = HideFlags.DontSave;
            var big = new Color[W * Scale * H * Scale];
            for (int y = 0; y < H * Scale; y++)
                for (int x = 0; x < W * Scale; x++)
                    big[y * W * Scale + x] = px[(y / Scale) * W + x / Scale];
            tex.SetPixels(big);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
