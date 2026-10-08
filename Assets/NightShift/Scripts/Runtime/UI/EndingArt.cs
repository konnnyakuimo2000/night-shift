using System.Collections.Generic;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>
    /// エンディング・ゲームオーバー・サブエンドの1枚絵（128×80 のドット絵をコードで描く）。
    /// 文字は描かない（読ませる情報は画面の文章に任せ、絵は情景だけ）。
    /// </summary>
    public static class EndingArt
    {
        public const int W = 128, H = 80;
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        // ---- 色 ----
        static readonly Color Night = new Color(0.04f, 0.06f, 0.14f), NightLow = new Color(0.1f, 0.12f, 0.24f);
        static readonly Color DawnTop = new Color(0.25f, 0.3f, 0.55f), DawnLow = new Color(0.98f, 0.7f, 0.45f);
        static readonly Color Wall = new Color(0.33f, 0.35f, 0.38f), WallDark = new Color(0.16f, 0.17f, 0.2f), Floor = new Color(0.22f, 0.2f, 0.2f);
        static readonly Color DeskC = new Color(0.45f, 0.36f, 0.27f), DeskShade = new Color(0.3f, 0.24f, 0.18f);
        static readonly Color Beige = new Color(0.78f, 0.75f, 0.66f), BeigeShade = new Color(0.55f, 0.52f, 0.45f);
        static readonly Color Ink = new Color(0.06f, 0.06f, 0.08f), Shirt = new Color(0.82f, 0.84f, 0.88f), SkinC = new Color(0.93f, 0.76f, 0.62f);
        static readonly Color Red = new Color(0.8f, 0.1f, 0.08f), Green = new Color(0.25f, 0.85f, 0.4f), Gold = new Color(0.95f, 0.78f, 0.2f);
        static readonly Color Paper = new Color(0.94f, 0.92f, 0.85f), Card = new Color(0.62f, 0.45f, 0.25f);

        public static Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_cache.TryGetValue(id, out var s) && s != null) return s;
            var p = new PixelCanvas(W, H);
            var r = new System.Random(id.GetHashCode());
            Draw(id, p, r);
            s = p.ToSprite();
            _cache[id] = s;
            return s;
        }

        // ================= 部品 =================
        static void Sky(PixelCanvas p, bool dawn, System.Random r, int x = 0, int y = 0, int w = W, int h = H)
        {
            p.Gradient(x, y, w, h, dawn ? DawnTop : Night, dawn ? DawnLow : NightLow);
            if (!dawn) for (int i = 0; i < w * h / 90; i++) p.Set(x + r.Next(w), y + r.Next(h * 2 / 3), new Color(1, 1, 0.9f, 0.6f + (float)r.NextDouble() * 0.4f));
        }

        static void Skyline(PixelCanvas p, int baseY, System.Random r, bool lit, Color body)
        {
            int x = 0;
            while (x < W)
            {
                int w = 8 + r.Next(14), h = 14 + r.Next(30);
                p.Rect(x, baseY - h, w, h, body);
                if (lit)
                    for (int j = baseY - h + 3; j < baseY - 2; j += 4)
                        for (int i = x + 2; i < x + w - 2; i += 3)
                            if (r.Next(3) == 0) p.Set(i, j, Gold);
                x += w + r.Next(2);
            }
        }

        /// <summary>夜勤の部屋（壁・窓・床・机）。窓の外は夜か夜明け</summary>
        static void Office(PixelCanvas p, bool dawn, System.Random r, bool dark = false)
        {
            p.Rect(0, 0, W, H, dark ? WallDark : Wall);
            Sky(p, dawn, r, 76, 8, 42, 30);
            Skyline(p, 38, new System.Random(7), !dawn, dawn ? new Color(0.35f, 0.3f, 0.4f) : new Color(0.08f, 0.09f, 0.14f));
            p.Frame(75, 7, 44, 32, BeigeShade);
            p.Rect(96, 7, 2, 32, BeigeShade);
            // 窓以外の部分を壁で塗り直す（スカイラインのはみ出し）
            p.Rect(0, 0, 75, 40, dark ? WallDark : Wall); p.Rect(119, 0, 9, 40, dark ? WallDark : Wall);
            p.Rect(0, 0, W, 7, dark ? WallDark : Wall); p.Rect(75, 39, 44, 1, dark ? WallDark : Wall);
            p.Rect(0, 58, W, 22, Floor);
            Desk(p, 6, 52, 90);
        }

        static void Desk(PixelCanvas p, int x, int y, int w)
        {
            p.Rect(x, y, w, 5, DeskC);
            p.Rect(x, y + 5, w, 1, DeskShade);
            p.Rect(x + 2, y + 6, 3, 18, DeskShade); p.Rect(x + w - 5, y + 6, 3, 18, DeskShade);
        }

        /// <summary>CRTモニター（screen: 画面の色）</summary>
        static void Monitor(PixelCanvas p, int x, int y, Color screen, int w = 26, int h = 20)
        {
            p.Rect(x, y, w, h, Beige);
            p.Rect(x + w - 3, y, 3, h, BeigeShade);
            p.Rect(x + 3, y + 3, w - 8, h - 7, screen);
            p.Rect(x + w / 2 - 4, y + h, 8, 2, BeigeShade);
            p.Rect(x + w / 2 - 7, y + h + 2, 14, 2, Beige);
        }

        static void ScreenLines(PixelCanvas p, int x, int y, int w, int h, Color c, System.Random r)
        {
            for (int j = y; j < y + h; j += 2) p.Rect(x, j, 2 + r.Next(w - 2), 1, c);
        }

        static void Chart(PixelCanvas p, int x, int y, int w, int h, System.Random r, bool up)
        {
            int prev = y + h / 2;
            for (int i = 0; i < w; i++)
            {
                int v = Mathf.Clamp(prev + r.Next(-2, 3) + (up ? (i % 4 == 0 ? -1 : 0) : (i % 4 == 0 ? 1 : 0)), y, y + h - 1);
                p.Line(x + i, prev, x + i, v, v <= prev ? Green : Red);
                prev = v;
            }
        }

        /// <summary>座っている人（後ろ姿）</summary>
        static void Sitter(PixelCanvas p, int x, int y, Color shirt, bool slumped = false)
        {
            p.Rect(x - 2, y + 10, 16, 18, new Color(0.1f, 0.1f, 0.12f));          // 椅子の背
            p.Rect(x, y + 6, 12, 14, shirt);                                         // 胴
            if (slumped) { p.Disc(x + 12, y + 8, 4, Ink); p.Rect(x + 12, y + 3, 6, 2, Ink); }
            else p.Disc(x + 6, y + 2, 4, Ink);                                        // 頭（髪）
        }

        /// <summary>立っている人（横向き）</summary>
        static void Stander(PixelCanvas p, int x, int y, Color shirt, bool carryBox = false)
        {
            p.Disc(x + 3, y + 3, 3, SkinC);
            p.Rect(x + 1, y, 5, 2, Ink);
            p.Rect(x, y + 7, 7, 12, shirt);
            p.Rect(x + 1, y + 19, 2, 9, Ink); p.Rect(x + 4, y + 19, 2, 9, Ink);
            if (carryBox) { p.Rect(x + 5, y + 9, 10, 8, Card); p.Rect(x + 5, y + 9, 10, 1, new Color(0.45f, 0.32f, 0.18f)); }
        }

        static void Can(PixelCanvas p, int x, int y, Color c) { p.Rect(x, y, 3, 5, c); p.Rect(x, y, 3, 1, Shirt); }
        static void Papers(PixelCanvas p, int x, int y, int n) { for (int i = 0; i < n; i++) p.Rect(x + (i % 2), y - i * 2, 14, 2, i % 2 == 0 ? Paper : new Color(0.82f, 0.8f, 0.72f)); }
        static void Stamp(PixelCanvas p, int x, int y) { p.Frame(x, y, 8, 8, Red); p.Rect(x + 2, y + 3, 4, 2, Red); }
        static void Chips(PixelCanvas p, int x, int y, int n, Color c) { for (int i = 0; i < n; i++) { p.Rect(x, y - i * 2, 8, 2, c); p.Set(x + 3, y - i * 2, Shirt); } }
        static void Envelope(PixelCanvas p, int x, int y) { p.Rect(x, y, 14, 9, Paper); p.Line(x, y, x + 7, y + 5, BeigeShade); p.Line(x + 13, y, x + 7, y + 5, BeigeShade); }
        static void Box(PixelCanvas p, int x, int y) { p.Rect(x, y, 18, 12, Card); p.Rect(x, y, 18, 2, new Color(0.45f, 0.32f, 0.18f)); p.Rect(x + 3, y - 4, 4, 4, Green); p.Rect(x + 9, y - 3, 3, 3, Beige); }
        static void Building(PixelCanvas p, int x, int y, int w, int h, bool lit, Color body, System.Random r)
        {
            p.Rect(x, y, w, h, body);
            for (int j = y + 3; j < y + h - 4; j += 5)
                for (int i = x + 3; i < x + w - 3; i += 5)
                    p.Rect(i, j, 2, 3, lit && r.Next(3) != 0 ? Gold : new Color(0.1f, 0.1f, 0.13f));
        }

        // ================= 場面 =================
        static void Draw(string id, PixelCanvas p, System.Random r)
        {
            switch (id)
            {
                // ---------- 本編 ----------
                case "END1": // 全責任: 赤い警報灯の下、箱を抱えて出ていく
                    Office(p, false, r, true);
                    Monitor(p, 20, 30, Red); Monitor(p, 50, 30, Red);
                    p.Disc(64, 4, 4, Red); p.Rect(54, 0, 20, 1, new Color(0.9f, 0.2f, 0.1f, 0.5f));
                    Stander(p, 104, 44, Shirt, true);
                    break;
                case "END2": // 倒産: 明かりの消えた本社ビル
                    Sky(p, false, r);
                    Building(p, 40, 14, 48, 52, false, new Color(0.22f, 0.24f, 0.28f), r);
                    p.Rect(58, 54, 12, 12, new Color(0.45f, 0.36f, 0.25f));          // 板でふさいだ入口
                    p.Line(58, 54, 69, 65, DeskShade); p.Line(69, 54, 58, 65, DeskShade);
                    p.Rect(0, 66, W, 14, new Color(0.15f, 0.15f, 0.17f));
                    break;
                case "END3": // 真夜中の答え合わせ: 夜明け、2つの画面に並ぶ会話
                    Office(p, true, r);
                    Monitor(p, 16, 30, new Color(0.05f, 0.1f, 0.2f)); ScreenLines(p, 20, 34, 14, 9, Green, r);
                    Monitor(p, 46, 30, new Color(0.05f, 0.1f, 0.2f)); ScreenLines(p, 50, 34, 14, 9, Shirt, r);
                    Sitter(p, 38, 42, Shirt);
                    break;
                case "END4": // 引き抜き: 明るい新しいビルへ歩いていく
                    Sky(p, true, r);
                    Building(p, 74, 10, 40, 58, true, new Color(0.55f, 0.65f, 0.78f), r);
                    p.Rect(0, 66, W, 14, new Color(0.4f, 0.4f, 0.42f));
                    Stander(p, 40, 40, Shirt);
                    break;
                case "END6": // 契約更新: 夜明けの机と缶コーヒー
                    Office(p, true, r);
                    Monitor(p, 30, 30, new Color(0.1f, 0.2f, 0.4f)); ScreenLines(p, 34, 34, 14, 9, Shirt, r);
                    Can(p, 62, 47, Red); Can(p, 67, 47, new Color(0.2f, 0.3f, 0.7f));
                    Sitter(p, 44, 40, Shirt);
                    break;

                // ---------- ゲームオーバー ----------
                case "GO1": // 契約打ち切り: 私物の箱と、閉まるドア
                    p.Rect(0, 0, W, H, WallDark); p.Rect(0, 58, W, 22, Floor);
                    p.Rect(84, 14, 26, 44, new Color(0.4f, 0.32f, 0.24f)); p.Rect(86, 16, 22, 40, new Color(0.5f, 0.4f, 0.3f)); p.Set(104, 36, Gold);
                    Box(p, 30, 46);
                    break;
                case "GO2": // 重大インシデント: すべての画面が赤い
                    Office(p, false, r, true);
                    Monitor(p, 8, 30, Red); Monitor(p, 36, 30, Red); Monitor(p, 64, 30, Red);
                    for (int i = 0; i < 6; i++) p.Speckle(0, 0, W, 50, new Color(0.9f, 0.1f, 0.1f, 0.15f), 0.02f, r);
                    break;
                case "GO3": // 踏み台: 誰もいない席の画面で、カーソルが勝手に動く
                    Office(p, false, r, true);
                    Monitor(p, 34, 28, new Color(0.05f, 0.1f, 0.2f), 30, 22);
                    p.Line(46, 34, 46, 40, Shirt); p.Line(46, 34, 50, 38, Shirt); p.Line(52, 36, 52, 42, Shirt); p.Line(52, 36, 56, 40, Shirt);
                    p.Rect(36, 64, 22, 3, new Color(0.1f, 0.1f, 0.12f));
                    break;
                case "GO4": // 過労で倒れる: 机に突っ伏した背中と、空き缶の山
                    Office(p, false, r);
                    Monitor(p, 26, 30, new Color(0.1f, 0.2f, 0.4f));
                    for (int i = 0; i < 7; i++) Can(p, 58 + (i % 4) * 4, 47 - (i / 4) * 5, i % 2 == 0 ? Red : new Color(0.2f, 0.3f, 0.7f));
                    Sitter(p, 34, 42, Shirt, true);
                    break;

                // ---------- SQ01 ----------
                case "SE-E01": // 共犯の席: 暗い机の上で、封筒が手渡される
                    Office(p, false, r, true);
                    Monitor(p, 30, 30, new Color(0.1f, 0.2f, 0.4f));
                    Envelope(p, 62, 44);
                    Stander(p, 96, 30, new Color(0.25f, 0.27f, 0.35f));
                    Sitter(p, 46, 40, Shirt);
                    break;
                case "SE-E02": // ここまでです: 何も届かない受信トレイ
                    Office(p, true, r);
                    Monitor(p, 34, 28, Shirt, 30, 22);
                    for (int j = 0; j < 4; j++) p.Rect(40, 33 + j * 3, 14, 1, new Color(0.7f, 0.7f, 0.75f));
                    break;
                case "SE-E03": // 記録に残す: 書類の山と赤い印
                    Office(p, false, r);
                    Papers(p, 30, 50, 8); Papers(p, 50, 50, 5);
                    Stamp(p, 70, 42);
                    break;

                // ---------- SQ02 ----------
                case "SE-R01": // 転職祝い: 新しい職場の明るい机と、見覚えのある書類
                    Sky(p, true, r, 0, 0, W, 40);
                    p.Rect(0, 30, W, 50, new Color(0.85f, 0.86f, 0.88f));
                    Desk(p, 14, 52, 100);
                    Monitor(p, 30, 30, new Color(0.3f, 0.5f, 0.8f));
                    Papers(p, 70, 51, 6);
                    p.Disc(110, 40, 6, new Color(0.3f, 0.65f, 0.3f));            // 観葉植物
                    p.Rect(108, 44, 5, 8, Card);
                    break;
                case "SE-R02": // 買い切り: 最後の入金と、消えた連絡先
                    Office(p, false, r);
                    Monitor(p, 30, 30, new Color(0.1f, 0.2f, 0.4f));
                    Envelope(p, 62, 44);
                    break;
                case "SE-R03": // 切断: 抜かれたケーブル
                    p.Rect(0, 0, W, H, WallDark);
                    p.Rect(20, 30, 30, 20, Beige); p.Rect(24, 36, 6, 4, Ink); p.Rect(34, 36, 6, 4, Ink);
                    p.Line(80, 60, 100, 40, Ink, 2); p.Rect(98, 36, 8, 6, Beige);
                    p.Speckle(84, 30, 20, 14, Gold, 0.05f, r);
                    break;
                case "SE-R04": // 提供者の記録: 調査報告書と、自分の名前の付箋
                    Office(p, false, r);
                    Papers(p, 40, 50, 10);
                    p.Rect(44, 28, 8, 6, new Color(1f, 0.92f, 0.45f));
                    Stamp(p, 62, 40);
                    break;

                // ---------- SQ03 ----------
                case "SE-HG01": // 返信先なし: 朝の光と、空の受信トレイ
                    Office(p, true, r);
                    Monitor(p, 34, 28, Shirt, 30, 22);
                    p.Rect(40, 34, 18, 2, new Color(0.7f, 0.7f, 0.75f));
                    break;
                case "SE-HB01": // 受領済み: 無人の椅子と、画面の中の長い髪の女
                    Office(p, false, r, true);
                    Monitor(p, 24, 12, new Color(0.02f, 0.02f, 0.03f), 48, 38);
                    HorrorArt.DrawStanding(p, 34, 15, r, 2);
                    p.Rect(24, 12, 48, 3, Beige);
                    p.Rect(78, 50, 14, 22, new Color(0.1f, 0.1f, 0.12f));            // 引かれた椅子
                    p.Speckle(0, 0, W, H, new Color(0.6f, 0.05f, 0.05f, 0.25f), 0.08f, r);
                    break;
                case "SE-HT01": // 次の担当者: 隣の席の画面に、同じメールが届く
                    Office(p, false, r);
                    Monitor(p, 14, 30, new Color(0.1f, 0.2f, 0.4f));
                    Monitor(p, 54, 30, new Color(0.1f, 0.2f, 0.4f));
                    Envelope(p, 60, 33);
                    p.Line(36, 36, 52, 36, Gold); p.Line(49, 33, 52, 36, Gold); p.Line(49, 39, 52, 36, Gold);
                    HorrorArt.DrawStanding(p, 100, 34, r);
                    break;

                // ---------- SQ04/05 ----------
                case "SE-M01": // 破産: 空の財布と赤字の給与明細
                    p.Rect(0, 0, W, H, WallDark);
                    p.Rect(20, 20, 40, 50, Paper); for (int j = 0; j < 6; j++) p.Rect(24, 26 + j * 6, 30, 1, BeigeShade);
                    p.Rect(40, 58, 16, 3, Red);
                    p.Rect(70, 44, 30, 18, new Color(0.35f, 0.22f, 0.12f)); p.Rect(70, 44, 30, 4, new Color(0.25f, 0.15f, 0.08f));
                    break;
                case "SE-G01": // ギャンブル王: 会員用テーブルとチップの山
                    p.Rect(0, 0, W, H, new Color(0.12f, 0.03f, 0.06f));
                    p.Rect(0, 46, W, 34, new Color(0.05f, 0.35f, 0.15f)); p.Rect(0, 46, W, 2, Gold);
                    Chips(p, 30, 60, 8, Red); Chips(p, 42, 60, 6, new Color(0.2f, 0.3f, 0.8f)); Chips(p, 54, 60, 10, Gold);
                    p.Rect(84, 18, 26, 30, new Color(0.6f, 0.6f, 0.65f)); p.Rect(88, 24, 18, 10, Shirt);   // スロット台
                    p.Set(92, 29, Red); p.Set(97, 29, Red); p.Set(102, 29, Red);
                    break;
                case "SE-F01": // FX戦士: 夜の部屋に並ぶ相場の画面
                    p.Rect(0, 0, W, H, new Color(0.05f, 0.06f, 0.1f));
                    Desk(p, 4, 56, 120);
                    for (int i = 0; i < 4; i++)
                    {
                        Monitor(p, 6 + i * 30, 34, new Color(0.02f, 0.06f, 0.05f));
                        Chart(p, 9 + i * 30, 37, 16, 9, new System.Random(i * 7 + 1), i % 2 == 0);
                    }
                    break;

                // ---------- SQ06 ----------
                case "SE-N01": // 第零班: 知らない企業の端末がずらりと並ぶ
                    p.Rect(0, 0, W, H, new Color(0.02f, 0.03f, 0.04f));
                    for (int j = 0; j < 4; j++)
                        for (int i = 0; i < 7; i++)
                        {
                            p.Rect(6 + i * 17, 6 + j * 17, 14, 12, new Color(0.03f, 0.12f, 0.06f));
                            ScreenLines(p, 7 + i * 17, 7 + j * 17, 12, 10, Green, r);
                        }
                    break;
                case "SE-N02": // 切り捨て: 暗い画面と、調査の通知の封筒
                    Office(p, false, r, true);
                    Monitor(p, 30, 30, new Color(0.02f, 0.02f, 0.03f));
                    Envelope(p, 62, 44); Stamp(p, 66, 44);
                    break;
                case "SE-N03": // 夜明けの証言: 朝日と、普段どおりに動く画面
                    Sky(p, true, r, 0, 0, W, 50);
                    Skyline(p, 50, new System.Random(3), false, new Color(0.3f, 0.28f, 0.38f));
                    p.Disc(100, 44, 7, new Color(1f, 0.85f, 0.5f));
                    p.Rect(0, 50, W, 30, Wall);
                    Desk(p, 10, 62, 100);
                    Monitor(p, 30, 40, new Color(0.1f, 0.3f, 0.5f)); ScreenLines(p, 34, 44, 14, 9, Green, r);
                    break;

                default:
                    Office(p, true, r);
                    break;
            }
        }
    }
}
