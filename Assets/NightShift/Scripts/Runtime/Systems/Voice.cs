using System;
using System.Collections.Generic;
using NightShift.Data;

namespace NightShift.Systems
{
    /// <summary>社員の口調（問い合わせ・返事の文体）</summary>
    public enum Persona { Polite, Standard, Casual, Anxious, Curt, Veteran, Cheerful }

    /// <summary>
    /// 問い合わせの文面を、社員ごとの口調で組み立てる。
    /// 口調は社員番号・年齢・ITの得意さ・部署から決まる（同じ社員はいつも同じ口調）。
    /// 質問の中身（判断に関わる部分）は変えず、あいさつ・名乗り・締め・催促・お礼・件名を変える。
    /// 言い回しの選択には、ゲームの乱数ではなく問い合わせごとの乱数を使う（攻撃や要求の出方に影響しない）。
    /// </summary>
    public static class Voice
    {
        static int Hash(string s)
        {
            int h = 23;
            foreach (char c in s ?? "") h = h * 31 + c;
            return h & 0x7fffffff;
        }

        public static Persona Of(EmployeeData e)
        {
            if (e == null) return Persona.Standard;
            int h = Hash(e.employeeId);
            if (e.age >= 50) return h % 3 != 0 ? Persona.Veteran : Persona.Polite;
            if (e.itLiteracy <= 2 && h % 3 != 0) return Persona.Anxious;
            if (e.age <= 26) return h % 2 == 0 ? Persona.Casual : Persona.Cheerful;
            if (e.department == "営業部" && h % 3 == 0) return Persona.Curt;
            if (e.age >= 42) return h % 3 == 0 ? Persona.Curt : Persona.Polite;
            Persona[] pool = { Persona.Standard, Persona.Standard, Persona.Polite, Persona.Casual, Persona.Curt, Persona.Anxious, Persona.Cheerful };
            return pool[h % pool.Length];
        }

        public static Random RngFor(EmployeeData e, string salt) => new Random(Hash((e?.employeeId ?? "") + "|" + salt));

        static string Pick(Random r, params string[] a) => a[r.Next(a.Length)];

        static string Family(EmployeeData e)
        {
            string n = e?.fullName ?? "";
            int sp = n.IndexOf(' ');
            return sp > 0 ? n.Substring(0, sp) : n;
        }

        /// <summary>最初の問い合わせ文（「{部署}の{名前}です。」で始まる元の文を、口調に合わせて組み立て直す）</summary>
        public static string Open(EmployeeData e, string filled, Random r)
        {
            if (e == null) return filled;
            string intro = $"{e.department}の{e.fullName}です。";
            string core = filled.StartsWith(intro) ? filled.Substring(intro.Length).TrimStart('\n') : filled;
            string fam = Family(e);
            var p = Of(e);
            string greet, name, close;
            switch (p)
            {
                case Persona.Polite:
                    greet = Pick(r, "夜分遅くに大変恐れ入ります。", "夜間のご対応、お疲れさまでございます。", "夜分に失礼いたします。");
                    name = $"{e.department}の{fam}と申します。";
                    close = Pick(r, "お手数をおかけしますが、よろしくお願いいたします。", "ご確認いただけますと幸いです。", "恐れ入りますが、ご教示ください。");
                    break;
                case Persona.Casual:
                    greet = Pick(r, "夜遅くにすみません！", "お疲れさまです〜", "夜勤おつかれさまです！");
                    name = $"{e.department}の{fam}です！";
                    close = Pick(r, "よろしくお願いしまーす！", "すみませんがお願いします！", "助けてください〜");
                    core = Casualize(core);
                    break;
                case Persona.Anxious:
                    greet = Pick(r, "あの、すみません…。", "夜中に本当にすみません…。", "お忙しいところ、すみません…。");
                    name = $"{e.department}の{fam}です…。";
                    close = Pick(r, "本当にすみません、よろしくお願いします…。", "何か壊してしまったんでしょうか…。", "急ぎではないかもしれないんですが…すみません。");
                    core = Anxiousize(core);
                    break;
                case Persona.Curt:
                    greet = "";
                    name = $"{e.department} {fam}。";
                    close = Pick(r, "急ぎで。", "至急お願いします。", "早めに返事ください。");
                    core = Curtize(core);
                    break;
                case Persona.Veteran:
                    greet = Pick(r, "夜勤さん、ちょっといいか。", "夜勤の人、悪いが見てくれ。", "ああ、夜勤さんか。");
                    name = $"{e.department}の{fam}だ。";
                    close = Pick(r, "朝までに何とかしてくれ。", "早めに頼むよ。", "こういうのはどうも苦手でな。頼む。");
                    break;
                case Persona.Cheerful:
                    greet = Pick(r, "こんばんは！", "夜勤おつかれさまです(^^)", "遅くまでおつかれさまです！");
                    name = $"{e.department}の{fam}です♪";
                    close = Pick(r, "助かります〜！よろしくです(^^)", "よろしくお願いします m(_ _)m", "お手すきのときにお願いしまーす(^^;");
                    break;
                default:
                    greet = Pick(r, "お疲れさまです。", "夜分にすみません。", "");
                    name = intro;
                    close = Pick(r, "よろしくお願いします。", "ご確認お願いします。", "");
                    break;
            }
            var lines = new List<string>();
            string head = (greet + name).Trim();
            if (head.Length > 0) lines.Add(head);
            lines.Add(core);
            if (close.Length > 0) lines.Add(close);
            return string.Join("\n", lines);
        }

        /// <summary>件名（せっかちな人は【至急】、丁寧な人は「〜について」など）</summary>
        public static string Subject(EmployeeData e, string subject, Random r)
        {
            switch (Of(e))
            {
                case Persona.Curt: return "【至急】" + subject;
                case Persona.Veteran: return r.Next(2) == 0 ? "【急ぎ】" + subject : subject;
                case Persona.Polite: return "【ご相談】" + subject;
                case Persona.Anxious: return subject + "（すみません）";
                case Persona.Cheerful: return "ちょっと相談です：" + subject;
                case Persona.Casual: return r.Next(2) == 0 ? subject + "！" : subject;
                default: return subject;
            }
        }

        /// <summary>催促（返事が遅いとき）。元の文が決まり文句なら口調ごとの文に、固有の文なら口調の前置きを付ける</summary>
        public static string Reminder(EmployeeData e, string filled, Random r)
        {
            bool generic = filled.StartsWith("すみません、先ほどの件");
            string lead;
            switch (Of(e))
            {
                case Persona.Polite: lead = generic ? Pick(r, "度々失礼いたします。先ほどの件、いかがでしょうか。", "重ねて恐れ入ります。先ほどお問い合わせした件、ご確認いただけましたでしょうか。") : "度々失礼いたします。"; break;
                case Persona.Casual: lead = generic ? Pick(r, "あの〜、さっきの件どうなりました？", "すみません、さっきの件まだですかね？") : "あの〜、"; break;
                case Persona.Anxious: lead = generic ? Pick(r, "何度もすみません…。先ほどの件、まだでしょうか…。", "すみません、やっぱり不安で…さっきの件、どうしたらいいですか…？") : "何度もすみません…。"; break;
                case Persona.Curt: lead = generic ? Pick(r, "さっきの件、まだ？", "返事まだですか。こっちも急いでるんで。") : "まだですか。"; break;
                case Persona.Veteran: lead = generic ? Pick(r, "おい、さっきの件はどうなった。", "夜勤さん、さっきのはまだかね。") : "おい、"; break;
                case Persona.Cheerful: lead = generic ? Pick(r, "さっきの件、どうでしょう〜？(^^;", "お忙しいところすみません、さっきの件どうなりました？") : "たびたびすみません(^^; "; break;
                default: lead = generic ? filled : ""; break;
            }
            return generic ? lead : lead + filled;
        }

        /// <summary>お礼・返事（回答した後、直したとき・直らなかったとき）</summary>
        public static string Reply(EmployeeData e, string filled, Random r)
        {
            if (string.IsNullOrEmpty(filled)) return filled;
            // 元の返事が「了解」「わかりました」などで始まるときは、前置きを重ねない
            bool acknowledged = filled.StartsWith("了解") || filled.StartsWith("わかりました") || filled.StartsWith("ありがとう") || filled.StartsWith("承知");
            switch (Of(e))
            {
                case Persona.Polite: return (acknowledged ? "ご丁寧にありがとうございます。" : Pick(r, "ご丁寧にありがとうございます。", "承知いたしました。", "ありがとうございます。")) + filled;
                case Persona.Casual: return Casualize(filled) + Pick(r, "", " ありがとうございます！", " 助かりました〜");
                case Persona.Anxious: return Anxiousize(filled) + Pick(r, " ありがとうございます…。", " すみませんでした…。", "");
                case Persona.Curt: return (acknowledged ? "" : Pick(r, "了解。", "わかりました。", "")) + Curtize(filled);
                case Persona.Veteran: return (acknowledged ? Pick(r, "そうか。", "ふむ。") : Pick(r, "そうか。", "ふむ。", "わかった。")) + filled + Pick(r, "", " 助かったよ。", " 頼りになるな。");
                case Persona.Cheerful: return filled + Pick(r, "(^^)", " ありがとうございます♪", " 助かりました〜！");
                default: return filled;
            }
        }

        // ---------------- 文末の言い換え（文の意味は変えない、安全なものだけ） ----------------
        static string Casualize(string s)
        {
            s = s.Replace("いいですか？", "いいですかね？").Replace("でしょうか？", "ですかね？");
            int last = s.LastIndexOf('。');
            if (last >= 0 && last == s.Length - 1) s = s.Substring(0, last) + "！";
            return s;
        }

        static string Anxiousize(string s)
        {
            int first = s.IndexOf("。");
            if (first > 0 && (first == 0 || s[first - 1] != '…')) s = s.Substring(0, first) + "…。" + s.Substring(first + 1);
            return s.Replace("いいですか？", "いいんでしょうか…？");
        }

        static string Curtize(string s)
        {
            return s.Replace("てもいいですか？", "ていい？").Replace("ていいですか？", "ていい？").Replace("でもいいですか？", "でいい？")
                    .Replace("もらえますか？", "もらえる？").Replace("でしょうか？", "か？").Replace("いいですか？", "いい？");
        }
    }
}
