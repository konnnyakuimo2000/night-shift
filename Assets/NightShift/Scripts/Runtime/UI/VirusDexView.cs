using System.Text;
using NightShift.Core;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// 感染記録（ウイルス図鑑）。VACCINE 99 の「感染記録」タブと、マニュアルの {FIG:VIRUSDEX} で同じものを表示する。
    /// 閲覧しても感染・駆除・評価・クエストは何も変わらない（記録は ShiftSession.Discover からだけ増える）。
    /// 発見段階ごとに公開する: 未遭遇=番号だけ / 観測=症状画像・観測記録・初遭遇日 / 検出=名称・危険度・経路・拡大条件・定義
    /// / 駆除=対処・残る被害 / 復旧=対応済みの印と履歴。ゲームに出ない後続候補は載せない。
    /// </summary>
    public static class VirusDexView
    {
        const float CardH = 156;

        /// <summary>図鑑を holder に並べる。戻り値は高さ。session があれば「今夜の感染」も表示する</summary>
        public static float Build(RectTransform holder, float width, ShiftSession session)
        {
            var career = CareerRecord.Current;
            float y = 0;
            int no = 0;
            foreach (var v in VirusCatalog.All)
            {
                if (!v.Implemented) continue;
                no++;
                Card(holder, width, y, no, v, career, session);
                y += CardH + 6;
            }
            // 分類不能の観測記録（雨宮リカ）: 出会ってからだけ載る。ウイルスとは断定しない
            var sq = career.FindQuest("SQ03");
            if (sq != null)
            {
                var bg = UIFactory.Image(holder, RetroTheme.Field, new Color(0.95f, 0.94f, 0.96f), "Unclassified");
                bg.rectTransform.Place(0, y, width, 110);
                var photo = UIFactory.Image(bg.transform, HorrorArt.IdPhoto, Color.white, "Photo");
                photo.rectTransform.Place(8, 8, 72, 90);
                var sb = new StringBuilder();
                sb.AppendLine("<b>分類不能の観測記録</b>");
                sb.AppendLine("名簿にない社員の証明写真。顔は髪に隠れている。");
                sb.AppendLine("ウイルスとして分類できない。VACCINE 99 の検査・駆除の対象ではない。");
                sb.Append(sq.IsOpen ? "記録は続いている。" : "記録は閉じられている。");
                var t = UIFactory.Label(bg.transform, sb.ToString(), 90, 8, width - 100, 96, 12);
                t.verticalOverflow = VerticalWrapMode.Overflow;
                y += 116;
            }
            return y;
        }

        static void Card(RectTransform holder, float width, float y, int no, VirusEntry v, CareerRecord career, ShiftSession session)
        {
            var e = VirusDex.Find(career, v.Id);
            var stage = (DiscoveryStage)(e?.stage ?? 0);
            bool notVirus = v.Kind != VirusKind.Virus;
            var bg = UIFactory.Image(holder, RetroTheme.Field, Color.white, "Card");
            bg.rectTransform.Place(0, y, width, CardH);
            var pic = UIFactory.Image(bg.transform, stage >= DiscoveryStage.Observed ? VirusArt.Get(v.Id) : VirusArt.Unknown, Color.white, "Symptom");
            pic.rectTransform.Place(8, 8, 128, 96);
            string U = "<color=#8890A0>未確認</color>";
            var sb = new StringBuilder();
            string name = stage >= DiscoveryStage.Detected ? v.Name : "<color=#8890A0>名称未確認</color>";
            string mark = stage >= DiscoveryStage.Recovered ? "　<color=#1F7F40>■ 対応済み</color>" : "";
            sb.AppendLine($"<b>No.{no:00}　{name}</b>{mark}　<size=11><color=#666E7C>{(stage >= DiscoveryStage.Recovered ? "" : Label(stage, notVirus))}</color></size>");
            if (stage == DiscoveryStage.None)
            {
                sb.AppendLine(U);
            }
            else
            {
                sb.AppendLine($"観測記録: {v.Symptom}（初遭遇 DAY{e.observedDay}）{(stage < DiscoveryStage.Detected ? "　<color=#B07000>原因は未確定</color>" : "")}");
                sb.AppendLine($"危険度: {(stage >= DiscoveryStage.Detected ? new string('★', v.Danger) + new string('☆', 3 - v.Danger) : U)}　感染経路: {(stage >= DiscoveryStage.Detected ? v.Route : U)}");
                sb.AppendLine($"拡大条件: {(stage >= DiscoveryStage.Detected ? v.Spread : U)}");
                string defs = notVirus ? "定義ファイルでは検出できない" : $"DAY{v.DefinitionDay} 版の定義ファイルから";
                sb.AppendLine($"定義: {(stage >= DiscoveryStage.Detected ? defs : U)}");
                sb.AppendLine($"対処: {(stage >= DiscoveryStage.Removed ? v.Fix : U)}");
                sb.AppendLine($"復旧方法: {(stage >= DiscoveryStage.Removed ? v.Recovery : U)}");
                if (stage >= DiscoveryStage.Recovered && e.history.Count > 0) sb.AppendLine($"履歴: {string.Join("、", e.history)}");
            }
            // 今夜の感染（過去に対応済みかとは別に表示）
            if (session != null && stage >= DiscoveryStage.Observed)
            {
                bool now = false;
                foreach (var kv in session.Security.Infections) if (kv.Value == v.Id) now = true;
                if (now) sb.AppendLine("<color=#B02020>今夜: 感染が続いている（未解決）</color>");
                else if (e != null && e.lastHandledDay == session.Day.dayNumber) sb.AppendLine("<color=#1F7F40>今夜: 解決済み</color>");
            }
            var t = UIFactory.Label(bg.transform, sb.ToString().TrimEnd(), 146, 6, width - 154, CardH - 10, 11);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        /// <summary>段階の名前。ウイルスではない事象は「駆除」と書かない</summary>
        public static string Label(DiscoveryStage s, bool notVirus)
        {
            if (!notVirus) return VirusDex.Label(s);
            switch (s)
            {
                case DiscoveryStage.Detected: return "特定";
                case DiscoveryStage.Removed: return "封じ込め";
                case DiscoveryStage.Recovered: return "対応済み";
                default: return VirusDex.Label(s);
            }
        }
    }

    /// <summary>図鑑の症状画像（ゲーム内の症状を描いた静止画。閲覧しても演出は起きない）</summary>
    public static class VirusArt
    {
        static readonly System.Collections.Generic.Dictionary<string, Sprite> _cache = new System.Collections.Generic.Dictionary<string, Sprite>();
        static Sprite _unknown;

        public static Sprite Unknown
        {
            get
            {
                if (_unknown != null) return _unknown;
                var c = new PixelCanvas(64, 48);
                c.Rect(0, 0, 64, 48, new Color(0.78f, 0.79f, 0.82f));
                c.Frame(0, 0, 64, 48, new Color(0.55f, 0.56f, 0.6f));
                // ？の形
                var q = new Color(0.5f, 0.52f, 0.58f);
                c.Rect(26, 30, 12, 3, q); c.Rect(35, 24, 3, 8, q); c.Rect(29, 20, 9, 3, q); c.Rect(29, 14, 3, 7, q); c.Rect(29, 8, 3, 3, q);
                return _unknown = c.ToSprite();
            }
        }

        public static Sprite Get(string id)
        {
            if (_cache.TryGetValue(id, out var s)) return s;
            var c = new PixelCanvas(64, 48);
            var desk = new Color(0.1f, 0.35f, 0.4f);
            var black = new Color(0.03f, 0.03f, 0.05f);
            var white = Color.white;
            switch (id)
            {
                case "V1": // アイコンが中央へ寄っていく
                    c.Rect(0, 0, 64, 48, desk);
                    for (int i = 0; i < 6; i++) c.Rect(8 + i * 7, 6 + i * 5, 6, 5, new Color(0.9f, 0.9f, 0.6f));
                    break;
                case "V2": // 文字化けの行
                    c.Rect(0, 0, 64, 48, new Color(0.95f, 0.95f, 0.92f));
                    for (int j = 0; j < 7; j++) for (int i = 0; i < 14; i++) if ((i * 7 + j * 3) % 5 != 0) c.Rect(3 + i * 4, 4 + j * 6, 3, 3, new Color(0.2f, 0.2f, 0.3f));
                    break;
                case "V3": // 赤く脈打つノード
                    c.Rect(0, 0, 64, 48, new Color(0.05f, 0.08f, 0.12f));
                    c.Line(12, 24, 32, 12, new Color(0.6f, 0.1f, 0.1f)); c.Line(32, 12, 52, 30, new Color(0.6f, 0.1f, 0.1f));
                    c.Disc(12, 24, 4, new Color(0.9f, 0.15f, 0.15f)); c.Disc(32, 12, 4, new Color(0.9f, 0.15f, 0.15f)); c.Disc(52, 30, 4, new Color(0.5f, 0.6f, 0.7f));
                    break;
                case "V4": // ブルースクリーン
                    c.Rect(0, 0, 64, 48, new Color(0f, 0f, 0.62f));
                    for (int j = 0; j < 5; j++) c.Rect(5, 6 + j * 8, 30 + (j * 9) % 24, 2, white);
                    break;
                case "V5": // 停電と非常灯
                    c.Rect(0, 0, 64, 48, black);
                    c.Rect(4, 4, 12, 5, new Color(0.1f, 0.9f, 0.3f));
                    c.Rect(18, 14, 28, 22, new Color(0.15f, 0.18f, 0.2f));
                    break;
                case "V6": // .briar に変わるファイル
                    c.Rect(0, 0, 64, 48, new Color(0.92f, 0.9f, 0.95f));
                    for (int i = 0; i < 4; i++) { c.Rect(6 + i * 14, 10, 10, 13, new Color(0.6f, 0.4f, 0.6f)); c.Rect(6 + i * 14, 26, 10, 2, new Color(0.4f, 0.2f, 0.4f)); }
                    c.Disc(48, 38, 5, new Color(0.7f, 0.2f, 0.4f));
                    break;
                case "V7": // もう1つのカーソル
                    c.Rect(0, 0, 64, 48, desk);
                    Cursor(c, 18, 12, white); Cursor(c, 38, 22, new Color(0.7f, 0.75f, 0.8f));
                    break;
                case "V8": // 範囲を変えて来る通信
                    c.Rect(0, 0, 64, 48, new Color(0.05f, 0.05f, 0.1f));
                    for (int i = 0; i < 5; i++) c.Line(4, 6 + i * 9, 58, 24, new Color(0.3f + i * 0.12f, 0.3f, 0.8f));
                    break;
                case "V9": // 亀裂とずれたタイル
                    c.Rect(0, 0, 64, 48, new Color(0.1f, 0.35f, 0.4f));
                    c.Rect(10, 8, 30, 16, new Color(0.75f, 0.75f, 0.75f)); c.Rect(14, 12, 30, 16, new Color(0.6f, 0.66f, 0.72f));
                    c.Line(32, 24, 4, 4, white); c.Line(32, 24, 60, 10, white); c.Line(32, 24, 50, 46, white); c.Line(32, 24, 10, 44, white);
                    break;
                case "V10": // 残像が重なる
                    c.Rect(0, 0, 64, 48, new Color(0.1f, 0.35f, 0.4f));
                    for (int i = 0; i < 3; i++) { c.Rect(6 + i * 8, 6 + i * 8, 34, 18, new Color(0.75f, 0.75f, 0.78f, 1f - i * 0.25f)); c.Rect(6 + i * 8, 6 + i * 8, 34, 3, new Color(0.2f, 0.25f, 0.6f, 1f - i * 0.25f)); }
                    break;
                case "V13": // 剥がれて別のデスクトップが見える
                    c.Rect(0, 0, 64, 48, new Color(0.75f, 0.75f, 0.75f));
                    c.Rect(18, 12, 34, 26, new Color(0f, 0.5f, 0.5f));
                    c.Rect(18, 12, 34, 3, new Color(0.88f, 0.85f, 0.78f));
                    c.Rect(24, 20, 8, 7, white);
                    break;
                case "V11": // 黒い染み
                    c.Rect(0, 0, 64, 48, new Color(0.85f, 0.88f, 0.9f));
                    c.Disc(6, 6, 10, black); c.Disc(58, 42, 12, black); c.Disc(18, 40, 6, black); c.Rect(57, 0, 2, 30, black);
                    break;
                case "V12": // 画面の中の画面
                    for (int i = 0; i < 4; i++) { c.Rect(i * 6, i * 5, 64 - i * 12, 48 - i * 10, new Color(0.1f + i * 0.15f, 0.35f, 0.45f)); c.Frame(i * 6, i * 5, 64 - i * 12, 48 - i * 10, white); }
                    break;
                case "V14": // 白く焼き付く
                    c.Gradient(0, 0, 64, 48, new Color(0.95f, 0.95f, 0.95f), new Color(0.7f, 0.75f, 0.8f));
                    for (int j = 0; j < 4; j++) c.Rect(6, 8 + j * 9, 40, 2, new Color(0.85f, 0.85f, 0.85f));
                    break;
                default:
                    return Unknown;
            }
            return _cache[id] = c.ToSprite();
        }

        static void Cursor(PixelCanvas c, int x, int y, Color col)
        {
            for (int i = 0; i < 10; i++) c.Rect(x, y + i, Mathf.Max(1, 7 - i / 2), 1, col);
        }
    }
}
