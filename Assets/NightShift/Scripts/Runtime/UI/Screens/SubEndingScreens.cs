using System;
using System.Text;
using NightShift.Core;
using NightShift.Systems.SideQuests;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Screens
{
    /// <summary>途中終了型のサブエンド（本編のエンドとは別の図鑑）。終端の前の保存地点からやり直せる</summary>
    public static class SubEndingScreen
    {
        public static GameObject Build(RectTransform parent, string endingId, string operatorName, string retryLabel, Action retry, Action toTitle)
        {
            var def = SubEndings.Get(endingId);
            bool bad = endingId == "SE-HB01" || endingId == "SE-M01";
            var root = UIFactory.Solid(parent, bad ? new Color(0.12f, 0.01f, 0.02f) : new Color(0.03f, 0.03f, 0.05f), "SubEnding");
            root.rectTransform.Stretch();
            var paper = UIFactory.Solid(root.transform, new Color(0.95f, 0.94f, 0.9f), "Paper");
            paper.rectTransform.anchorMin = paper.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            paper.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            paper.rectTransform.sizeDelta = new Vector2(660, 560);
            var ink = new Color(0.1f, 0.1f, 0.1f);
            UIFactory.Label(paper.transform, "SUB ENDING", 20, 16, 620, 30, 22, TextAnchor.MiddleCenter, ink, true).fontStyle = FontStyle.Bold;
            UIFactory.Solid(paper.transform, ink, "Rule").rectTransform.Place(20, 52, 620, 2);
            UIFactory.Label(paper.transform, $"【{endingId}　{def?.Title}】（{def?.Kind}）", 20, 66, 620, 26, 17, TextAnchor.MiddleCenter, bad ? new Color(0.6f, 0.05f, 0.05f) : ink).fontStyle = FontStyle.Bold;
            UIFactory.Label(paper.transform, $"夜間IT運用担当　{operatorName}", 20, 96, 620, 22, 13, TextAnchor.MiddleCenter, ink);
            UIFactory.Label(paper.transform, def?.Text ?? "", 50, 130, 560, 200, 14, TextAnchor.UpperCenter, ink);

            var career = CareerRecord.Current;
            var sb = new StringBuilder("サブエンド図鑑\n");
            int seen = 0;
            foreach (var e in SubEndings.All) if (career.subEndingsSeen.Contains(e.Id)) seen++;
            sb.Append($"　{seen} / {SubEndings.All.Count}　（");
            bool first = true;
            foreach (var e in SubEndings.All)
                if (career.subEndingsSeen.Contains(e.Id)) { sb.Append(first ? "" : "・").Append(e.Title); first = false; }
            sb.Append("）");
            UIFactory.Label(paper.transform, sb.ToString(), 40, 360, 580, 90, 12, TextAnchor.UpperLeft, ink);

            var b1 = UIFactory.Button(paper.transform, retryLabel + "(R)", 120, 480, 200, 32, retry, "clock");
            b1.Interactable = retry != null;
            UIFactory.Button(paper.transform, "タイトルへ(T)", 340, 480, 200, 32, toTitle, "exit");
            if (retry != null) { var k1 = root.gameObject.AddComponent<KeyAction>(); k1.Key = KeyCode.R; k1.Action = retry; }
            var k2 = root.gameObject.AddComponent<KeyAction>(); k2.Key = KeyCode.T; k2.Action = toTitle;
            AudioFx.Chime();
            return root.gameObject;
        }
    }

    /// <summary>朝の進路選択（ギャンブル王・FX戦士など）。朝の精算のあとに一度だけ尋ねる</summary>
    public static class MorningChoiceScreen
    {
        public static GameObject Build(RectTransform parent, MorningChoice c, Action accept, Action decline)
        {
            var root = UIFactory.Solid(parent, new Color(0.05f, 0.06f, 0.09f), "MorningChoice");
            root.rectTransform.Stretch();
            var box = UIFactory.Panel(root.transform, RetroTheme.Raised, "Box");
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            box.rectTransform.sizeDelta = new Vector2(600, 360);
            var bar = UIFactory.Image(box.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Title");
            bar.rectTransform.StretchH(3, 18, 3, 3);
            UIFactory.Label(bar.transform, " " + c.Title, 12, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            UIFactory.Label(box.transform, "朝。精算が終わった。", 20, 32, 560, 24, 14, TextAnchor.MiddleLeft).fontStyle = FontStyle.Bold;
            UIFactory.Label(box.transform, c.Text + (c.Blocked != null ? "\n\n<color=#A00000>" + c.Blocked + "</color>" : ""), 20, 64, 560, 220, 13, TextAnchor.UpperLeft);
            var yes = UIFactory.Button(box.transform, c.Accept, 60, 300, 220, 32, accept, "exit");
            yes.Interactable = c.Blocked == null;
            UIFactory.Button(box.transform, c.Decline, 320, 300, 220, 32, decline, "clock");
            return root.gameObject;
        }
    }
}
