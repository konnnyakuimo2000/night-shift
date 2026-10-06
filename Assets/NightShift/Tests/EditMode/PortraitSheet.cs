using System.IO;
using System.Text;
using NightShift.UI;
using NUnit.Framework;
using UnityEngine;

namespace NightShift.Tests
{
    /// <summary>全社員の顔写真（ドット絵）を1枚に並べて書き出す（確認用）。Logs/Screenshots/Portraits.png</summary>
    public class PortraitSheet : NightShiftTest
    {
        [Test, Category("Screenshots"), Explicit("画面の確認用")]
        public void WritePortraitSheet()
        {
            var list = TestData.Db.employees;
            const int cols = 7, cell = 104;
            int rows = (list.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * cell, rows * (cell + 24), TextureFormat.RGBA32, false);
            var fill = new Color[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color(0.83f, 0.82f, 0.78f);
            sheet.SetPixels(fill);
            var names = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                var sp = PortraitPainter.Get(e);
                var src = sp.texture;
                int col = i % cols, row = i / cols;
                int ox = col * cell + (cell - src.width) / 2;
                int oy = sheet.height - (row + 1) * (cell + 24) + 4;
                sheet.SetPixels(ox, oy, src.width, src.height, src.GetPixels());
                names.AppendLine($"{i + 1,2}. {e.employeeId} {e.fullName}（{e.kana}・{e.age}歳）");
            }
            sheet.Apply();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Screenshots"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "Portraits.png"), sheet.EncodeToPNG());
            File.WriteAllText(Path.Combine(dir, "Portraits.txt"), names.ToString(), Encoding.UTF8);
        }

        /// <summary>バリエーションの確認用: 架空の社員 48 人（年齢・性別をばらけさせる）。Logs/Screenshots/PortraitsVariety.png</summary>
        [Test, Category("Screenshots"), Explicit("画面の確認用")]
        public void WriteVarietySheet()
        {
            string[] given = { "タロウ", "ミサキ", "ケンジ", "ユミ", "ヒロシ", "サクラ", "ダイスケ", "アヤ", "マコト", "メグミ", "ショウ", "ヨウコ" };
            const int count = 48, cols = 8, cell = 104;
            int rows = count / cols;
            var sheet = new Texture2D(cols * cell, rows * (cell + 24), TextureFormat.RGBA32, false);
            var fill = new Color[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color(0.83f, 0.82f, 0.78f);
            sheet.SetPixels(fill);
            for (int i = 0; i < count; i++)
            {
                var e = ScriptableObject.CreateInstance<NightShift.Data.EmployeeData>();
                e.employeeId = $"V-{i * 37 + 1001}";
                e.kana = "テスト " + given[i % given.Length];
                e.age = 22 + (i * 7) % 38;
                var src = PortraitPainter.Get(e).texture;
                int col = i % cols, row = i / cols;
                int ox = col * cell + (cell - src.width) / 2;
                int oy = sheet.height - (row + 1) * (cell + 24) + 4;
                sheet.SetPixels(ox, oy, src.width, src.height, src.GetPixels());
                Object.DestroyImmediate(e);
            }
            sheet.Apply();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Screenshots"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "PortraitsVariety.png"), sheet.EncodeToPNG());
        }
    }
}
