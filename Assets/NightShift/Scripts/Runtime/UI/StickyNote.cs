using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// モニターに貼られた付箋（影・テープ・罫線・折れた角まで描く）。
    /// 初期パスワードの間は前任者のメモ、変更後は「付箋に書いた」場合だけ自分のメモが貼られる。
    /// 付箋は後の日の脆弱性要素（パスワードの書き置き）でもある。
    /// </summary>
    public static class StickyNote
    {
        static readonly Color Paper = new Color(1f, 0.93f, 0.45f);
        static readonly Color PaperDark = new Color(0.9f, 0.8f, 0.3f);
        static readonly Color Ink = new Color(0.12f, 0.15f, 0.4f);
        static readonly Color Rule = new Color(0.55f, 0.65f, 0.85f, 0.45f);
        static readonly Color Tape = new Color(1f, 1f, 1f, 0.55f);

        /// <summary>付箋に書かれている内容（貼られていなければ null）</summary>
        public static string TextFor(Core.OperatorAccount account)
        {
            if (account == null) return null;
            if (!account.Changed)
                return $"夜間担当の方へ\n\nID : {account.UserId}\nPW : {Core.OperatorAccount.InitialPassword}\n\n※初日だけ。\n　あとで変えること！\n　　　　　　— K.";
            if (account.WrittenDown)
                return $"自分用メモ\n\nID : {account.UserId}\nPW : {account.Password}";
            return null;
        }

        /// <summary>parent の右上（right, top のオフセット）に付箋を貼る。貼る内容が無ければ null。</summary>
        public static RectTransform Create(Transform parent, Core.OperatorAccount account, float right, float top, float angle)
        {
            string text = TextFor(account);
            if (text == null) return null;

            var root = UIFactory.Rect("StickyNote", parent);
            root.anchorMin = root.anchorMax = new Vector2(1, 1);
            root.pivot = new Vector2(1, 1);
            root.anchoredPosition = new Vector2(-right, -top);
            root.sizeDelta = new Vector2(176, 150);
            root.localRotation = Quaternion.Euler(0, 0, angle);

            // 影
            var shadow = UIFactory.Solid(root, new Color(0, 0, 0, 0.28f), "Shadow");
            shadow.raycastTarget = false;
            shadow.rectTransform.Stretch(5, 6, -5, -6);

            // 紙
            var paper = UIFactory.Solid(root, Paper, "Paper");
            paper.raycastTarget = false;
            paper.rectTransform.Stretch();
            // 上部の糊の部分（少し濃い）
            var glue = UIFactory.Solid(paper.transform, PaperDark, "Glue");
            glue.raycastTarget = false;
            glue.rectTransform.StretchH(0, 16);
            // 罫線
            for (int i = 0; i < 7; i++)
            {
                var line = UIFactory.Solid(paper.transform, Rule, "Rule");
                line.raycastTarget = false;
                line.rectTransform.StretchH(30 + i * 16, 1, 8, 8);
            }
            // 右下の折れた角
            var fold = UIFactory.Solid(paper.transform, PaperDark, "Fold");
            fold.raycastTarget = false;
            fold.rectTransform.anchorMin = fold.rectTransform.anchorMax = new Vector2(1, 0);
            fold.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            fold.rectTransform.sizeDelta = new Vector2(20, 20);
            fold.rectTransform.anchoredPosition = new Vector2(-4, 4);
            fold.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            // 文字（手書き風に少し大きめ・濃紺）
            var t = UIFactory.Label(paper.transform, text, 13, TextAnchor.UpperLeft, Ink);
            t.font = RetroTheme.ReadingFont;
            t.lineSpacing = 1.05f;
            t.rectTransform.Stretch(12, 20, 8, 8);

            // セロハンテープ（画面に貼っている）
            var tape = UIFactory.Solid(root, Tape, "Tape");
            tape.raycastTarget = false;
            tape.rectTransform.anchorMin = tape.rectTransform.anchorMax = new Vector2(0.5f, 1);
            tape.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tape.rectTransform.sizeDelta = new Vector2(70, 18);
            tape.rectTransform.anchoredPosition = new Vector2(0, 2);
            tape.rectTransform.localRotation = Quaternion.Euler(0, 0, -angle * 0.8f + 3);
            return root;
        }
    }
}
