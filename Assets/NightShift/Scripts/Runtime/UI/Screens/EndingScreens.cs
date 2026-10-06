using System;
using System.Collections.Generic;
using System.Text;
using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Screens
{
    /// <summary>ゲームオーバー画面（社内報の「人事異動のお知らせ」風）</summary>
    public static class GameOverScreen
    {
        public static GameObject Build(RectTransform parent, EndingResolver.GameOver go, string operatorName, Action retryNight, Action retryWeek, Action toTitle)
        {
            var root = UIFactory.Solid(parent, new Color(0.08f, 0.08f, 0.09f), "GameOver");
            root.rectTransform.Stretch();
            var paper = UIFactory.Solid(root.transform, new Color(0.95f, 0.94f, 0.9f), "Paper");
            paper.rectTransform.anchorMin = paper.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            paper.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            paper.rectTransform.sizeDelta = new Vector2(620, 460);
            var ink = new Color(0.1f, 0.1f, 0.1f);
            UIFactory.Label(paper.transform, "社 内 報", 20, 16, 580, 30, 22, TextAnchor.MiddleCenter, ink, true).fontStyle = FontStyle.Bold;
            UIFactory.Solid(paper.transform, ink, "Rule").rectTransform.Place(20, 52, 580, 2);
            UIFactory.Label(paper.transform, "人事異動のお知らせ", 20, 64, 580, 26, 18, TextAnchor.MiddleCenter, ink).fontStyle = FontStyle.Bold;
            UIFactory.Label(paper.transform, $"情報システム部　夜間IT運用担当　{operatorName}\n契約終了", 20, 100, 580, 50, 16, TextAnchor.MiddleCenter, ink);
            var stamp = UIFactory.Label(paper.transform, "GAME OVER", 20, 160, 580, 40, 28, TextAnchor.MiddleCenter, new Color(0.7f, 0.05f, 0.05f), true);
            stamp.fontStyle = FontStyle.Bold;
            UIFactory.Label(paper.transform, $"【{go.Id} {go.Title}】", 20, 210, 580, 24, 15, TextAnchor.MiddleCenter, ink).fontStyle = FontStyle.Bold;
            UIFactory.Label(paper.transform, go.Reason, 40, 240, 540, 80, 13, TextAnchor.UpperCenter, ink);
            var b1 = UIFactory.Button(paper.transform, "その夜からやり直す(R)", 40, 340, 170, 30, retryNight, "clock");
            var b2 = UIFactory.Button(paper.transform, "週の初日からやり直す(W)", 220, 340, 180, 30, retryWeek, "clock");
            b2.Interactable = retryWeek != null;
            UIFactory.Button(paper.transform, "タイトルへ(T)", 410, 340, 170, 30, toTitle, "exit");
            var k1 = root.gameObject.AddComponent<KeyAction>(); k1.Key = KeyCode.R; k1.Action = retryNight;
            var k3 = root.gameObject.AddComponent<KeyAction>(); k3.Key = KeyCode.T; k3.Action = toTitle;
            if (retryWeek != null) { var k2 = root.gameObject.AddComponent<KeyAction>(); k2.Key = KeyCode.W; k2.Action = retryWeek; }
            AudioFx.Error();
            return root.gameObject;
        }
    }

    /// <summary>本編エンド。TRUE エンドは前任者との対話のあと、新聞の1枚絵・成績表・エンディング一覧。</summary>
    public static class EndingScreen
    {
        public static readonly string[][] AllEndings =
        {
            new[] { "END1", "全責任" }, new[] { "END2", "倒産" }, new[] { "END3", "真夜中の答え合わせ" },
            new[] { "END4", "引き抜き" }, new[] { "END6", "契約更新" }
        };
        public static readonly string[][] AllGameOvers =
        {
            new[] { "GO1", "契約打ち切り" }, new[] { "GO2", "重大インシデント" }, new[] { "GO3", "踏み台" }, new[] { "GO4", "過労で倒れる" }
        };

        public static GameObject Build(RectTransform parent, EndingResolver.Ending ending, FinalOutcome outcome, string operatorName, OperatorWallet wallet, Action toTitle)
        {
            var root = UIFactory.Solid(parent, Color.black, "Ending");
            root.rectTransform.Stretch();
            if (ending.TrueRoute) BuildChat(root.rectTransform, ending, operatorName, choice =>
            {
                ending.Text += choice == 0
                    ? "\n\nあなたは通報した。前任者は逮捕されたが、裁判で会社の不正もすべて明るみに出た。あなたは夜勤を辞めた。"
                    : "\n\nあなたは前任者の集めた証拠と、自分の給与明細を持って労働基準監督署へ行った。会社に是正勧告が出た。前任者は、それきり姿を消した。";
                ending.Text += "\n\nそして、夜が明けた。";
                foreach (Transform t in root.transform) UnityEngine.Object.Destroy(t.gameObject);
                BuildNewspaper(root.rectTransform, ending, outcome, operatorName, wallet, toTitle);
            });
            else BuildNewspaper(root.rectTransform, ending, outcome, operatorName, wallet, toTitle);
            return root.gameObject;
        }

        // ---------------- 前任者との対話（END3） ----------------
        static void BuildChat(RectTransform root, EndingResolver.Ending ending, string operatorName, Action<int> onChoice)
        {
            var career = CareerRecord.Current;
            int fines = 0, overtime = 0, ai = 0;
            foreach (var n in career.nights) { fines += n.fines; if (n.overtime) overtime++; ai += n.aiHandled; }
            int tools = 0;
            foreach (var it in career.items) if (it.id.StartsWith("TOOL_") || it.id.StartsWith("AI_")) tools++;

            var lines = new List<string>
            {
                "e0004: …つながったね。",
                "e0004: この夜勤のこと、ここまで続けられた人は初めてだよ。",
                fines > 3000 ? $"e0004: 罰金、合わせて ¥{fines:N0} 取られたでしょう。私のときと同じ。"
                    : "e0004: 罰金、ほとんど取られなかったんだね。すごいな。",
                overtime > 0 ? $"e0004: 缶コーヒーで {overtime} 回も残業して。残業代、出なかったでしょう。"
                    : "e0004: 残業しなかったんだ。それでいい。倒れたら終わりだから。",
                ai > 0 ? "e0004: AI にも頼ったね。でも最後は、自分の目で確かめてた。"
                    : (tools > 0 ? $"e0004: ツールを {tools} 個も自腹で買って。会社は1円も出さないのにね。" : "e0004: ツールも使わずに、全部自分の目で確かめたんだね。"),
                "e0004: 私が攻撃していたのは、あなたじゃない。この会社の記録だよ。",
                "e0004: 改ざんされた勤務記録、払われなかった深夜割増、罰金という名の天引き。全部、外に出したかった。",
                "e0004: でも、あなたは最後まで守り切った。だから、決めるのはあなた。",
            };
            var win = UIFactory.Panel(root, RetroTheme.Raised, "Chat");
            win.rectTransform.anchorMin = win.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            win.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            win.rectTransform.sizeDelta = new Vector2(720, 520);
            var bar = UIFactory.Image(win.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Title");
            bar.rectTransform.StretchH(3, 18, 3, 3);
            UIFactory.Label(bar.transform, " チャット - e0004", 12, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            var bg = UIFactory.Solid(win.transform, RetroTheme.Terminal, "Log");
            bg.rectTransform.Stretch(6, 26, 6, 60);
            var text = UIFactory.Label(bg.transform, "", 14, TextAnchor.UpperLeft, RetroTheme.TerminalText, true);
            text.rectTransform.Stretch(10, 8, 10, 8);
            var b1 = UIFactory.Button(win.transform, "通報する", 100, 470, 220, 30, () => onChoice(0));
            var b2 = UIFactory.Button(win.transform, "証拠を労基署に持っていく", 400, 470, 220, 30, () => onChoice(1));
            b1.gameObject.SetActive(false); b2.gameObject.SetActive(false);
            var sb = new StringBuilder();
            float t = 0.6f;
            foreach (var l in lines)
            {
                var line = l;
                Delay.Run(win.gameObject, t, () => { sb.AppendLine(line); sb.AppendLine(); if (text) text.text = sb.ToString(); AudioFx.Tick(); });
                t += 2.2f;
            }
            Delay.Run(win.gameObject, t, () => { b1.gameObject.SetActive(true); b2.gameObject.SetActive(true); });
        }

        // ---------------- 新聞・成績表・エンディング一覧 ----------------
        static void BuildNewspaper(RectTransform root, EndingResolver.Ending ending, FinalOutcome outcome, string operatorName, OperatorWallet wallet, Action toTitle)
        {
            var career = CareerRecord.Current;
            var ink = new Color(0.1f, 0.1f, 0.1f);
            var paper = UIFactory.Solid(root, new Color(0.94f, 0.92f, 0.86f), "Paper");
            paper.rectTransform.Place(30, 20, 760, 740);
            UIFactory.Label(paper.transform, "夜 明 新 報", 20, 10, 720, 46, 34, TextAnchor.MiddleCenter, ink, true).fontStyle = FontStyle.Bold;
            UIFactory.Label(paper.transform, "1998年（平成10年）6月27日　土曜日　朝刊", 20, 56, 720, 18, 12, TextAnchor.MiddleCenter, ink);
            UIFactory.Solid(paper.transform, ink, "Rule").rectTransform.Place(20, 78, 720, 3);
            var head = UIFactory.Label(paper.transform, ending.Headline, 20, 90, 720, 70, 24, TextAnchor.MiddleCenter, ink);
            head.fontStyle = FontStyle.Bold;
            UIFactory.Label(paper.transform, ending.Text, 40, 170, 680, 260, 14, TextAnchor.UpperLeft, ink);
            UIFactory.Label(paper.transform, Epilogue(career), 40, 440, 680, 60, 13, TextAnchor.UpperLeft, new Color(0.35f, 0.3f, 0.25f));
            var stamp = UIFactory.Label(paper.transform, $"{ending.Id}「{ending.Title}」 — {ending.Kind}", 20, 520, 720, 30, 18, TextAnchor.MiddleCenter, new Color(0.6f, 0.05f, 0.05f));
            stamp.fontStyle = FontStyle.Bold;

            // 成績表
            var card = UIFactory.Panel(root, RetroTheme.Raised, "Score");
            card.rectTransform.Place(810, 20, 440, 420);
            UIFactory.Label(card.transform, " 30日間の成績表", 6, 6, 420, 22, 14).fontStyle = FontStyle.Bold;
            int total = 0, mal = 0, fines = 0, overtime = 0;
            foreach (var n in career.nights) { total += n.totalRequests; mal += n.maliciousAllowed; fines += n.fines; if (n.overtime) overtime++; }
            var items = new List<string>();
            foreach (var it in career.items)
            {
                var def = GameManager.Instance != null ? GameManager.Instance.Database.FindItem(it.id) : null;
                if (def != null && !def.consumable) items.Add(def.displayName);
            }
            string[] ranks = { "", "D", "C", "B", "A", "S" };
            float avg = career.AverageRankValue();
            var sb = new StringBuilder();
            sb.AppendLine($"担当: {operatorName}");
            sb.AppendLine($"最終夜の結果: {EndingResolver.OutcomeLabel(outcome)}");
            sb.AppendLine($"平均ランク: {(avg <= 0 ? "-" : ranks[Mathf.Clamp(Mathf.RoundToInt(avg), 1, 5)])}（{avg:0.0}）");
            sb.AppendLine($"会社の信用: {career.trust} / 100");
            sb.AppendLine($"処理した接続要求: {total} 件");
            sb.AppendLine($"不正な接続の許可: {mal} 件");
            sb.AppendLine($"所持金: {OperatorWallet.Yen(wallet.Balance)}");
            sb.AppendLine($"罰金の合計: {OperatorWallet.Yen(fines)}");
            sb.AppendLine($"残業: {overtime} 回");
            sb.AppendLine($"前任者の手がかり: {career.clues.Count} / 4");
            sb.AppendLine("購入した機材・ツール:");
            sb.AppendLine(items.Count == 0 ? "　なし" : "　" + string.Join("、", items));
            UIFactory.Label(card.transform, sb.ToString(), 12, 32, 416, 380, 12, TextAnchor.UpperLeft);

            // エンディング一覧
            if (!career.endingsSeen.Contains(ending.Id)) career.endingsSeen.Add(ending.Id);
            career.Save();
            var list = UIFactory.Panel(root, RetroTheme.Raised, "List");
            list.rectTransform.Place(810, 450, 440, 260);
            var lb = new StringBuilder(" エンディング一覧\n");
            foreach (var e in AllEndings) lb.AppendLine(career.endingsSeen.Contains(e[0]) ? $"　{e[0]}　{e[1]}" : $"　{e[0]}　？？？");
            lb.AppendLine(" ゲームオーバー記録");
            foreach (var g in AllGameOvers) lb.AppendLine(career.gameOversSeen.Contains(g[0]) ? $"　{g[0]}　{g[1]}" : $"　{g[0]}　？？？");
            UIFactory.Label(list.transform, lb.ToString(), 8, 6, 424, 250, 12, TextAnchor.UpperLeft);
            UIFactory.Button(root, "タイトルへ(T)", 1060, 720, 190, 30, toTitle, "exit");
            var k = root.gameObject.AddComponent<KeyAction>(); k.Key = KeyCode.T; k.Action = toTitle;
        }

        static string Epilogue(CareerRecord career)
        {
            var acc = GameManager.Instance != null ? GameManager.Instance.Account : null;
            if (acc != null && acc.WrittenDown) return "※ パスワードの付箋は、まだ机に貼ってある。";
            if (career.Owns(Data.Items.SocAi)) return "※ SOC-AI のライセンスは、次の夜勤担当に引き継がれた。";
            if (career.Owns(Data.Items.Monitor1)) return "※ あなたのサブモニターは、次の夜勤担当に払い下げられた。";
            return "※ 机の上には、前任者の付箋だけが残っていた。";
        }
    }
}
