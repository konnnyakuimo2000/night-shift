using System;
using System.Collections.Generic;
using NightShift.Core;
using NightShift.Data;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Screens
{
    /// <summary>
    /// 業務評価（リザルト）画面。演出付き:
    /// SHIFT COMPLETE のタイプ表示 → 評価バーとカウントアップ → ★ → ランクの判子 → 業務記録 → 給与明細の印字。
    /// クリック / Enter / Space でスキップ。演出が終わるとボタンが押せる。
    /// </summary>
    public static class EvaluationScreen
    {
        static readonly Color Ink = new Color(0.12f, 0.12f, 0.16f);
        static readonly Color Paper = new Color(0.99f, 0.98f, 0.94f);
        static readonly Color StampRed = new Color(0.78f, 0.1f, 0.12f);

        public static Color RankColor(string rank)
        {
            switch (rank)
            {
                case "S": return new Color(0.85f, 0.62f, 0.05f);
                case "A": return new Color(0.1f, 0.3f, 0.75f);
                case "B": return new Color(0.1f, 0.5f, 0.25f);
                case "C": return new Color(0.75f, 0.4f, 0.05f);
                default: return new Color(0.4f, 0.4f, 0.45f);
            }
        }

        static int Stars(string rank)
        {
            switch (rank) { case "S": return 5; case "A": return 4; case "B": return 3; case "C": return 2; default: return 1; }
        }

        static string RankComment(string rank)
        {
            switch (rank)
            {
                case "S": return "非の打ちどころがない夜でした。";
                case "A": return "見事な働きです。";
                case "B": return "概ね問題なし。確認の手順を固めましょう。";
                case "C": return "いくつか見落としがありました。";
                default: return "明日は、まず調べてから判断を。";
            }
        }

        public static GameObject Build(RectTransform parent, DayDefinition day, EvaluationResult r, PaySlip slip, PaySlip previous, bool updated, int balance, Action onNext, Action onRestart, string review = null)
        {
            var anim = new Timeline();

            // ---------------- 背景 ----------------
            var root = UIFactory.Solid(parent, new Color(0.03f, 0.05f, 0.1f), "Evaluation");
            root.rectTransform.Stretch();
            var water = UIFactory.Label(root.transform, $"DAY {day.dayNumber}", 260, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.035f), true);
            water.fontStyle = FontStyle.Bold;
            water.rectTransform.Stretch();
            water.horizontalOverflow = HorizontalWrapMode.Overflow;
            for (int i = 0; i < 50; i++)
            {
                var l = UIFactory.Solid(root.transform, new Color(1, 1, 1, 0.02f), "Scan");
                l.raycastTarget = false;
                l.rectTransform.StretchH(i * 16, 1);
            }

            // ---------------- 本体ウィンドウ ----------------
            var host = UIFactory.Rect("Host", root.transform).Stretch();
            var wm = WindowManager.Create(host);
            var w = wm.Open(new WindowSpec
            {
                Id = "eval", Title = $"業務評価 - DAY {day.dayNumber} {day.title}", Icon = "nexus", Width = 980, Height = 740,
                Closable = false, Minimizable = false, Maximizable = false, Centered = true, StatusCells = 1
            }, out _);
            w.SetStatus(0, "クリック / Enter で演出をスキップ");
            var c = w.Content;

            // ヘッダー（SHIFT COMPLETE をタイプ表示）
            var head = UIFactory.Image(c, RetroTheme.GradientSprite(new Color(0.02f, 0.08f, 0.3f), new Color(0.15f, 0.3f, 0.6f)), Color.white, "Head");
            head.rectTransform.StretchH(0, 64);
            var title = UIFactory.Label(head.transform, "", 30, TextAnchor.MiddleLeft, Color.white, true);
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.Stretch(18, 0, 300, 0);
            var sub = UIFactory.Label(head.transform, $"{day.dateLabel}  {day.ShiftHoursLabel}\n担当: {GameManager.Instance?.OperatorName}", 12, TextAnchor.MiddleRight, new Color(0.8f, 0.85f, 1f));
            sub.rectTransform.Stretch(0, 0, 16, 0);
            const string titleText = "SHIFT COMPLETE";
            int typed = -1;
            anim.Add(0.1f, 0.7f, p =>
            {
                int n = Mathf.RoundToInt(p * titleText.Length);
                if (n != typed) { typed = n; title.text = titleText.Substring(0, n) + (n < titleText.Length ? "_" : ""); if (n > 0 && n < titleText.Length) AudioFx.Type(); }
            });

            // ---------------- 評価（バー＋カウントアップ） ----------------
            var g = UIFactory.Group(c, "評価", 0, 74, 600, 300);
            float y = 0, t = 0.9f;
            foreach (var cat in r.Categories)
            {
                var name = UIFactory.Label(g, cat.Label, 0, y, 130, 22, 13);
                name.fontStyle = FontStyle.Bold;
                var barBg = UIFactory.Image(g, RetroTheme.Field, Color.white, "Bar");
                barBg.rectTransform.Place(132, y + 2, 330, 20);
                var fill = UIFactory.Image(barBg.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Fill");
                fill.rectTransform.Place(2, 2, 0, 16);
                var score = UIFactory.Label(g, "0", 470, y, 60, 22, 18, TextAnchor.MiddleRight, null, true);
                score.fontStyle = FontStyle.Bold;
                var comment = UIFactory.Label(g, cat.Comment, 132, y + 24, 440, 18, 11);
                comment.color = new Color(0.4f, 0.4f, 0.45f, 0);
                int target = cat.Score;
                int lastShown = -1;
                anim.Add(t, 0.45f, p =>
                {
                    float e = 1 - (1 - p) * (1 - p);
                    fill.rectTransform.sizeDelta = new Vector2(326 * target / 100f * e, 16);
                    int v = Mathf.RoundToInt(target * e);
                    if (v != lastShown) { lastShown = v; score.text = v.ToString(); if (v % 7 == 0) AudioFx.Tick(); }
                    comment.color = new Color(0.4f, 0.4f, 0.45f, p);
                });
                y += 54;
                t += 0.45f;
            }

            // ---------------- 総合（★ と ランクの判子） ----------------
            var rank = UIFactory.Group(c, "総合評価", 610, 74, 350, 300);
            var total = UIFactory.Label(rank, "0", 0, 0, 334, 40, 34, TextAnchor.MiddleCenter, Ink, true);
            total.fontStyle = FontStyle.Bold;
            UIFactory.Label(rank, "/ 100", 0, 38, 334, 16, 11, TextAnchor.MiddleCenter).color = RetroTheme.Shadow;
            int overall = r.Overall, shownTotal = -1;
            anim.Add(t, 0.6f, p =>
            {
                int v = Mathf.RoundToInt(overall * p);
                if (v != shownTotal) { shownTotal = v; total.text = v.ToString(); if (v % 9 == 0) AudioFx.Tick(); }
            });
            t += 0.65f;

            int stars = Stars(r.Rank);
            for (int i = 0; i < 5; i++)
            {
                var star = UIFactory.Label(rank, "★", 62 + i * 44, 60, 40, 40, 30, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
                bool lit = i < stars;
                var sRt = star.rectTransform;
                anim.Add(t + i * 0.12f, 0.15f, p =>
                {
                    if (!lit) return;
                    star.color = Color.Lerp(new Color(0.8f, 0.8f, 0.8f), RankColor(r.Rank), p);
                    float s = 1f + 0.5f * Mathf.Sin(p * Mathf.PI);
                    sRt.localScale = new Vector3(s, s, 1);
                }, () => { if (lit) AudioFx.Tick(); });
            }
            t += 0.8f;

            // 判子（ランク）
            var stamp = UIFactory.Rect("Stamp", rank).Place(107, 104, 120, 120);
            var stampBorder = UIFactory.Solid(stamp, StampRed, "Border");
            stampBorder.rectTransform.Stretch();
            var stampInner = UIFactory.Solid(stampBorder.transform, new Color(1f, 0.97f, 0.95f), "Inner");
            stampInner.rectTransform.Stretch(5, 5, 5, 5);
            var stampLetter = UIFactory.Label(stampInner.transform, r.Rank, 76, TextAnchor.MiddleCenter, StampRed, true);
            stampLetter.fontStyle = FontStyle.Bold;
            stampLetter.rectTransform.Stretch(0, -6, 0, 14);
            var stampWord = UIFactory.Label(stampInner.transform, "評　価", 12, TextAnchor.LowerCenter, StampRed);
            stampWord.fontStyle = FontStyle.Bold;
            stampWord.rectTransform.Stretch(0, 0, 0, 6);
            stamp.localRotation = Quaternion.Euler(0, 0, -8);
            var stampGroup = stamp.gameObject.AddComponent<CanvasGroup>();
            stampGroup.alpha = 0;
            var comment2 = UIFactory.Label(rank, RankComment(r.Rank), 0, 236, 334, 36, 12, TextAnchor.MiddleCenter);
            comment2.color = new Color(0.3f, 0.3f, 0.35f, 0);

            // 画面のフラッシュ（判子を押した瞬間）
            var flash = UIFactory.Solid(root.transform, new Color(1, 1, 1, 0), "Flash");
            flash.raycastTarget = false;
            flash.rectTransform.Stretch();
            bool good = r.Rank == "S" || r.Rank == "A";
            var winRt = w.Rect;
            Vector2 basePos = winRt.anchoredPosition;
            anim.Add(t, 0.22f, p =>
            {
                float s = Mathf.Lerp(3.2f, 1f, p * p);
                stamp.localScale = new Vector3(s, s, 1);
                stampGroup.alpha = Mathf.Clamp01(p * 2f);
            }, () => { });
            anim.Add(t + 0.22f, 0.35f, p =>
            {
                // ドン: 揺れとフラッシュ
                bool calm = Core.GameSettings.Current.reduceFlashing;
                float shake = calm ? 0f : (1 - p) * 6f;
                winRt.anchoredPosition = basePos + new Vector2(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake));
                flash.color = new Color(1, 1, 1, calm ? 0f : good ? 0.35f * (1 - p) : 0.15f * (1 - p));
                comment2.color = new Color(0.3f, 0.3f, 0.35f, p);
                if (p >= 1) winRt.anchoredPosition = basePos;
            }, () => { AudioFx.BigStamp(); if (good) AudioFx.Fanfare(); });
            t += 0.8f;

            // ---------------- 業務記録 ----------------
            var sg = UIFactory.Group(c, "業務記録", 0, 382, 600, 220);
            var sgFade = sg.gameObject.AddComponent<CanvasGroup>();
            sgFade.alpha = 0;
            // 1行に1項目（項目名と、その中身を文章で）
            int row = 0;
            foreach (var kv in r.Stats)
            {
                if (row >= 9) break;
                UIFactory.Label(sg, kv.Key, 0, row * 22, 128, 20, 12);
                UIFactory.ValueBox(sg, 130, row * 22, 452, 20, kv.Value);
                row++;
            }
            anim.Add(t, 0.4f, p => sgFade.alpha = p, AudioFx.Reveal);
            t += 0.4f;

            // ---------------- 給与明細（伝票） ----------------
            var pg = UIFactory.Group(c, "給与明細", 610, 382, 350, 220);
            var paper = UIFactory.Solid(pg, Paper, "Slip");
            paper.rectTransform.Stretch(0, 0, 0, 0);
            if (slip != null)
            {
                string Y(int v) => OperatorWallet.Yen(v);
                var lines = new List<(string label, int amount, bool minus)>
                {
                    ("日当（深夜手当込み）", slip.basePay, false),
                    ($"評価手当（{slip.score}点）", slip.performancePay, false),
                    ($"精勤手当（{slip.rank}）", slip.rankBonus, false),
                    ($"出来高手当（{slip.tasksDone}件）", slip.taskPay, false),
                    ("罰金・控除", slip.deduction, true),
                };
                float ly = 6;
                foreach (var (label, amount, minus) in lines)
                {
                    var lt = UIFactory.Label(paper.transform, label, 10, ly, 180, 20, 12, TextAnchor.MiddleLeft, new Color(Ink.r, Ink.g, Ink.b, 0));
                    var vt = UIFactory.Label(paper.transform, "", 180, ly, 140, 20, 13, TextAnchor.MiddleRight, minus && amount > 0 ? StampRed : Ink, true);
                    int a = amount;
                    anim.Add(t, 0.25f, p =>
                    {
                        lt.color = new Color(Ink.r, Ink.g, Ink.b, p);
                        int v = Mathf.RoundToInt(a * p);
                        vt.text = (minus && a > 0 ? "−" : "") + Y(v);
                    }, AudioFx.Tick);
                    ly += 22;
                    t += 0.25f;
                }
                UIFactory.Label(paper.transform, "－－－－－－－－－－－－－－－－－－－", 10, ly, 320, 14, 11, TextAnchor.MiddleCenter).color = RetroTheme.Shadow;
                ly += 16;
                UIFactory.Label(paper.transform, "本日の支給額", 10, ly, 140, 30, 13, TextAnchor.MiddleLeft, Ink).fontStyle = FontStyle.Bold;
                var totalPay = UIFactory.Label(paper.transform, Y(0), 140, ly, 180, 30, 22, TextAnchor.MiddleRight, RetroTheme.Navy, true);
                totalPay.fontStyle = FontStyle.Bold;
                int shownPay = -1;
                anim.Add(t, 0.9f, p =>
                {
                    float e = 1 - (1 - p) * (1 - p);
                    int v = Mathf.RoundToInt(slip.total * e) / 10 * 10;
                    if (p >= 1) v = slip.total;
                    if (v != shownPay) { shownPay = v; totalPay.text = Y(v); if ((v / 10) % 15 == 0) AudioFx.Coin(); }
                }, AudioFx.Coin);
                t += 0.95f;
                ly += 34;
                var bal = UIFactory.Label(paper.transform, "", 10, ly, 310, 20, 12, TextAnchor.MiddleRight, Ink);
                string note = "";
                if (previous != null)
                    note = updated ? $"（前回 {Y(previous.total)} から更新）" : $"（このDAYは前回の {Y(previous.total)} で計上）";
                if (slip.deductionNotes.Count > 0) note = "罰金・控除: " + string.Join("、", slip.deductionNotes) + " " + note;
                var noteText = UIFactory.Label(paper.transform, note, 10, ly + 22, 320, 32, 11, TextAnchor.UpperLeft, RetroTheme.Shadow);
                noteText.color = new Color(0.45f, 0.45f, 0.5f, 0);
                anim.Add(t, 0.6f, p =>
                {
                    int startBal = balance - (updated ? slip.total - (previous != null ? previous.total : 0) : 0);
                    int v = Mathf.RoundToInt(Mathf.Lerp(startBal, balance, p));
                    bal.text = $"所持金  {Y(v)}";
                    noteText.color = new Color(0.45f, 0.45f, 0.5f, p);
                }, AudioFx.Coin);
                t += 0.6f;
            }

            // ---------------- ボタン ----------------
            var next = UIFactory.Button(c, "翌日へ(N)", 640, 612, 150, 32, onNext, "clock");
            next.Label.fontStyle = FontStyle.Bold;
            // やり直しは、その夜の記録が消えるので確認する
            Action confirmRestart = () => ScreenConfirm.Show(root.rectTransform, "やり直しの確認", $"DAY{day.dayNumber} を最初からやり直します。\n今夜の業務記録は失われます。よろしいですか？", onRestart);
            var retry = UIFactory.Button(c, $"DAY{day.dayNumber}をやり直す(R)", 798, 612, 162, 32, confirmRestart);
            // 振り返り: どの要求の何を見落としたか（業務中は見せない正誤を、終わった後に読める）
            Action openReview = () => ReviewPanel.Show(root.rectTransform, $"DAY{day.dayNumber} の振り返り", review ?? "");
            var reviewBtn = UIFactory.Button(c, "振り返り(V)", 480, 612, 150, 32, openReview, "doc");
            reviewBtn.Interactable = !string.IsNullOrEmpty(review);
            next.Interactable = retry.Interactable = false;
            anim.Add(t, 0.01f, p => { }, () =>
            {
                next.Interactable = retry.Interactable = true;
                w.SetStatus(0, "SHIFT COMPLETE — 本日の業務記録を集計しました。");
            });

            var player = root.gameObject.AddComponent<TimelinePlayer>();
            player.Timeline = anim;
            var click = root.gameObject.AddComponent<ClickHandler>();
            click.OnClick = player.Skip;
            player.OnKey = () =>
            {
                if (!player.Finished) { player.Skip(); return; }
                if (ScreenConfirm.IsOpen(root.rectTransform) || ReviewPanel.IsOpen(root.rectTransform)) return;
                if (Input.GetKeyDown(KeyCode.V) && !string.IsNullOrEmpty(review)) openReview();
                else if (Input.GetKeyDown(KeyCode.N)) onNext();
                else if (Input.GetKeyDown(KeyCode.R)) confirmRestart();
            };
            return root.gameObject;
        }
    }

    /// <summary>評価画面の上に重ねて出す、振り返りの一覧（スクロールできる）。Esc・V・Enter で閉じる</summary>
    public static class ReviewPanel
    {
        public static void Show(RectTransform parent, string title, string text)
        {
            if (IsOpen(parent)) return;
            var dim = UIFactory.Solid(parent, new Color(0, 0, 0, 0.55f), "ReviewDim");
            dim.rectTransform.Stretch();
            dim.transform.SetAsLastSibling();
            var box = UIFactory.Panel(dim.transform, RetroTheme.Raised, "Review");
            box.rectTransform.anchorMin = new Vector2(0.5f, 0.5f); box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            box.rectTransform.sizeDelta = new Vector2(820, 600);
            var bar = UIFactory.Image(box.transform, RetroTheme.GradientSprite(RetroTheme.TitleActiveA, RetroTheme.TitleActiveB), Color.white, "Title");
            bar.rectTransform.StretchH(3, 18, 3, 3);
            UIFactory.Label(bar.transform, " " + title, 12, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            var body = UIFactory.Rect("Body", box.transform).Stretch(10, 28, 10, 48);
            var txt = UIFactory.ScrollText(body, text, out var scroll);
            txt.fontSize = 14;
            ((RectTransform)scroll.transform).Stretch();
            void Close() { if (dim != null) UnityEngine.Object.Destroy(dim.gameObject); }
            var btn = UIFactory.Button(box.transform, "閉じる(Esc)", 0, 0, 140, 30, Close);
            btn.Rect.anchorMin = btn.Rect.anchorMax = new Vector2(0.5f, 0);
            btn.Rect.pivot = new Vector2(0.5f, 0);
            btn.Rect.anchoredPosition = new Vector2(0, 10);
            foreach (var key in new[] { KeyCode.Escape, KeyCode.V, KeyCode.Return })
            {
                var k = dim.gameObject.AddComponent<KeyAction>();
                k.Key = key;
                k.Action = Close;
            }
        }

        public static bool IsOpen(RectTransform parent) => parent != null && parent.Find("ReviewDim") != null;
    }

    /// <summary>簡易タイムライン（開始時刻・長さ・進捗コールバック）</summary>
    public class Timeline
    {
        public class Step { public float At, Duration; public Action<float> Update; public Action OnStart; public bool Started, Done; }
        public readonly List<Step> Steps = new List<Step>();
        public float End { get; private set; }

        public void Add(float at, float duration, Action<float> update, Action onStart = null)
        {
            Steps.Add(new Step { At = at, Duration = duration, Update = update, OnStart = onStart });
            End = Mathf.Max(End, at + duration);
        }
    }

    public class TimelinePlayer : MonoBehaviour
    {
        public Timeline Timeline;
        public Action OnKey;
        float _t;
        bool _skipping;
        public bool Finished { get; private set; }

        public void Skip()
        {
            if (Finished) return;
            _skipping = true;
        }

        void Update()
        {
            if (Timeline == null) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.R))
                OnKey?.Invoke();
            if (Finished) return;

            bool wasSkipping = _skipping;
            _t = _skipping ? Timeline.End + 1 : _t + Time.unscaledDeltaTime;
            bool quiet = wasSkipping; // スキップ時は効果音を鳴らしすぎない
            foreach (var s in Timeline.Steps)
            {
                if (s.Done || _t < s.At) continue;
                if (!s.Started)
                {
                    s.Started = true;
                    if (!quiet || s == Timeline.Steps[Timeline.Steps.Count - 1]) s.OnStart?.Invoke();
                }
                float p = s.Duration <= 0 ? 1 : Mathf.Clamp01((_t - s.At) / s.Duration);
                s.Update?.Invoke(p);
                if (p >= 1) s.Done = true;
            }
            if (_t >= Timeline.End) Finished = true;
        }
    }
}
