namespace NightShift.Systems.SideQuests
{
    /// <summary>
    /// 1998年ごろのチェーンメール・広告メール風の装飾（SQ04 ギャンブル・SQ05 FX の案内）。
    /// 飾るのは見た目だけ。勝率・損失の上限・「保証しない」などの大事な注意書きは、本文の中に必ず残す。
    /// </summary>
    public static class ChainMail
    {
        public enum Style { Club, Market, Loan }

        static string Line(Style s) => s == Style.Club ? "☆★☆★☆★☆★☆★☆★☆★☆★☆★☆★☆★"
                                    : s == Style.Market ? "■□■□■□■□■□■□■□■□■□■□■□■□"
                                    : "◆◇◆◇◆◇◆◇◆◇◆◇◆◇◆◇◆◇◆◇◆◇";

        static string Mark(Style s) => s == Style.Club ? "★" : s == Style.Market ? "■" : "◆";

        /// <summary>件名の飾り（例: ★☆★ VIP席のご案内 ★☆★）</summary>
        public static string Subject(Style s, string subject)
        {
            string a = s == Style.Club ? "★☆★" : s == Style.Market ? "■□■" : "◆◇◆";
            string b = s == Style.Club ? "★☆★" : s == Style.Market ? "■□■" : "◆◇◆";
            return $"{a} {subject} {b}";
        }

        /// <summary>本文の飾り: 見出しの枠・本文・区切り線・追伸</summary>
        public static string Body(Style s, string title, string body, string footer = null)
        {
            string line = Line(s), m = Mark(s);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(line);
            sb.AppendLine($"　{m}{m}　<b>{title}</b>　{m}{m}");
            sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine(body);
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━");
            if (!string.IsNullOrEmpty(footer)) sb.AppendLine(footer);
            sb.Append(s == Style.Loan
                ? "※このメールは送信専用です。お申し込みは画面のボタンから。"
                : "※このメールは転送自由です！！　お友達にも教えてあげてくださいネ♪");
            return sb.ToString();
        }

        /// <summary>社員が転送してきたメール（Fw:）。前置きのあとに、元のメールを引用する</summary>
        public static string Forward(string note, string original)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(note);
            sb.AppendLine();
            sb.AppendLine("-----Original Message-----");
            foreach (var l in original.Split('\n')) sb.Append("> ").AppendLine(l);
            return sb.ToString().TrimEnd();
        }
    }
}
