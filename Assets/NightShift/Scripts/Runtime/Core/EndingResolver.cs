using NightShift.Systems;

namespace NightShift.Core
{
    public enum FinalOutcome { Defended, PartialDamage, Critical }

    /// <summary>ゲームオーバー（DAY8〜29）と本編エンド（DAY30 の後）の判定。ENDINGS.md</summary>
    public static class EndingResolver
    {
        // ---------------- ゲームオーバー ----------------
        public class GameOver
        {
            public string Id;
            public string Title;
            public string Reason;
        }

        /// <summary>1夜でこれだけ不正な接続を許可すると重大インシデント（GO2）</summary>
        public const int MaliciousLimit = 4;

        public static GameOver CheckGameOver(CareerRecord career, ShiftSession s)
        {
            int day = s.Day.dayNumber;
            if (day < Data.DefaultDataFactory.MainStartDay || day >= Data.DefaultDataFactory.LastDay) return null;
            if (!string.IsNullOrEmpty(s.FatalIncident) || s.Evaluation.MaliciousAllow >= MaliciousLimit)
                return new GameOver
                {
                    Id = "GO2", Title = "重大インシデント",
                    Reason = !string.IsNullOrEmpty(s.FatalIncident) ? $"DAY{day}: {s.FatalIncident}。" : $"DAY{day}: 一晩で不正な接続を {s.Evaluation.MaliciousAllow} 件許可し、社内システムが攻撃者に乗っ取られた。"
                };
            if (career.accountCompromised >= 3)
                return new GameOver
                {
                    Id = "GO3", Title = "踏み台",
                    Reason = $"DAY{day}: 自分のIDが3度にわたって攻撃に使われた。" + (GameManager.Instance != null && GameManager.Instance.Account.WrittenDown ? "モニターに貼った付箋が証拠として押収された。" : "")
                };
            if (career.ConsecutiveRank("D", day) >= 3)
                return new GameOver { Id = "GO1", Title = "契約打ち切り", Reason = $"DAY{day}: 総合評価 D が3夜続いた。「明日から来なくていい」。" };
            if (career.ConsecutiveOvertime(day) >= 7)
                return new GameOver { Id = "GO4", Title = "過労で倒れる", Reason = $"DAY{day}: 7夜続けて残業した。06:30、キーボードに突っ伏したまま朝を迎えた。" };
            return null;
        }

        // ---------------- 本編エンド ----------------
        public class Ending
        {
            public string Id;
            public string Title;
            public string Kind;      // BAD / TRUE / GOOD / NORMAL+ / NORMAL
            public string Headline;  // 新聞・社内報の見出し
            public string Text;
            public bool TrueRoute;   // 前任者との対話がある
        }

        public static FinalOutcome Outcome(ShiftSession s)
        {
            if (!string.IsNullOrEmpty(s.FatalIncident)) return FinalOutcome.Critical;
            if (s.Damage.Count > 0 || s.Evaluation.MaliciousAllow > 0) return FinalOutcome.PartialDamage;
            return FinalOutcome.Defended;
        }

        public static string OutcomeLabel(FinalOutcome o) => o == FinalOutcome.Defended ? "完全防衛" : o == FinalOutcome.PartialDamage ? "一部被害" : "重大インシデント";

        public static Ending Resolve(CareerRecord career, FinalOutcome final)
        {
            float rank = career.AverageRankValue();
            const string C = "ネクサスコーポレーション";
            if (final == FinalOutcome.Critical)
                return new Ending
                {
                    Id = "END1", Title = "全責任", Kind = "BAD",
                    Headline = $"{C}、顧客情報流出 「夜間担当の過失」と発表",
                    Text = "最後の夜、攻撃は会社の中枢まで届いた。会社は「夜間担当者の過失」と発表し、あなたは懲戒解雇された。\n数日後、損害賠償の請求書が届いた。金額は、あなたの所持金の全額だった。"
                };
            if (career.trust <= 30)
                return new Ending
                {
                    Id = "END2", Title = "倒産", Kind = "BAD",
                    Headline = $"{C} 倒産 相次ぐ障害で信用失う",
                    Text = "最終夜の朝、取引先から契約解除の FAX が一斉に届いた。度重なる障害で信用を失った会社は、1週間後に倒産した。\n社長は夜逃げし、最後の給与明細は ¥0 だった。\nあなたは電源の落ちたサーバールームで、私物のサブモニターを抱えて帰った。"
                };
            if (final == FinalOutcome.Defended && rank >= 4f && career.clues.Count >= 4)
                return new Ending
                {
                    Id = "END3", Title = "真夜中の答え合わせ", Kind = "TRUE", TrueRoute = true,
                    Headline = "攻撃元は元IT担当者 夜勤明けの「答え合わせ」",
                    Text = "攻撃元をたどった先にいたのは、この会社に使い潰されて辞めた前任の夜勤担当者だった。"
                };
            if (final == FinalOutcome.Defended && rank >= 4f && TotalReports(career) >= 5)
                return new Ending
                {
                    Id = "END4", Title = "引き抜き", Kind = "GOOD",
                    Headline = "外資系セキュリティ会社、国内の夜間監視要員を採用",
                    Text = "最後の夜の対応の記録が業界で噂になり、外資系のセキュリティ会社から誘いが来た。\nあなたはブラック企業を辞め、新しい職場で朝を迎えることにした。"
                };
            return new Ending
            {
                Id = "END6", Title = "契約更新", Kind = "NORMAL",
                Headline = "【社内報】7月の夜間シフト表",
                Text = "「来月もよろしく」。翌月のシフト表が届いた。\n何も変わらない夜が、また始まる。"
            };
        }

        static int TotalReports(CareerRecord career)
        {
            int n = 0;
            foreach (var r in career.nights) n += r.correctReports;
            return n;
        }
    }
}
