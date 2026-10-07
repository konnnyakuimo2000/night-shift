using System.Collections.Generic;
using System.IO;
using System.Text;
using NightShift.Data;
using NightShift.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NightShift.Tests
{
    /// <summary>問い合わせの口調（Voice）。質問の中身は変えず、社員ごとに文体が変わるか</summary>
    public class VoiceTests : NightShiftTest
    {
        const string Core = "ウイルス対策ソフトに「ウイルスを検出しました」と出ました。とりあえず再起動して様子を見てもいいですか？";

        [Test]
        public void Open_KeepsTheQuestion_AndIsDeterministic()
        {
            foreach (var e in TestData.Db.employees)
            {
                if (e == null || string.IsNullOrEmpty(e.fullName) || e.nameRedacted) continue;
                string filled = $"{e.department}の{e.fullName}です。\n" + Core;
                string a = Voice.Open(e, filled, Voice.RngFor(e, "x"));
                string b = Voice.Open(e, filled, Voice.RngFor(e, "x"));
                Assert.AreEqual(a, b, "同じ社員・同じ問い合わせなら同じ文面");
                Assert.IsTrue(a.Contains("ウイルスを検出しました"), $"{e.fullName}: 質問の中身が残っている");
                Assert.IsTrue(a.Contains(e.fullName.Split(' ')[0]), $"{e.fullName}: 名乗っている");
                Assert.AreEqual(Voice.Of(e), Voice.Of(e));
            }
        }

        [Test]
        public void Personas_AreVaried()
        {
            var seen = new HashSet<Persona>();
            foreach (var e in TestData.Db.employees) if (e != null) seen.Add(Voice.Of(e));
            Assert.GreaterOrEqual(seen.Count, 4, "社員の口調が4種類以上に分かれている");
        }

        /// <summary>確認用: 全社員の問い合わせ文面の見本を書き出す（Logs/VoiceSamples.txt）</summary>
        [Test, Category("Screenshots"), Explicit("確認用")]
        public void WriteSamples()
        {
            var sb = new StringBuilder();
            foreach (var e in TestData.Db.employees)
            {
                if (e == null || string.IsNullOrEmpty(e.fullName) || e.nameRedacted) continue;
                var r = Voice.RngFor(e, "sample");
                sb.AppendLine($"■ {e.fullName}（{e.age}歳・{e.department}）— {Voice.Of(e)}");
                sb.AppendLine("件名: " + Voice.Subject(e, "ウイルス対策ソフトが警告を出した", r));
                sb.AppendLine(Voice.Open(e, $"{e.department}の{e.fullName}です。\n" + Core, r));
                sb.AppendLine("（催促）" + Voice.Reminder(e, "すみません、先ほどの件、どうしたらいいでしょうか…？", r));
                sb.AppendLine("（返事）" + Voice.Reply(e, "了解です、LANケーブルを抜いて待ちます。", r));
                sb.AppendLine();
            }
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "VoiceSamples.txt"));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
    }
}
