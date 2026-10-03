using System;
using System.Collections.Generic;
using NightShift.Data;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Screens
{
    /// <summary>実時間ベースの簡易タイマー</summary>
    public class Delay : MonoBehaviour
    {
        float _t;
        Action _a;
        public static Delay Run(GameObject host, float seconds, Action a)
        {
            var d = host.AddComponent<Delay>();
            d._t = seconds; d._a = a;
            return d;
        }
        void Update()
        {
            _t -= Time.unscaledDeltaTime;
            if (_t <= 0) { var a = _a; _a = null; Destroy(this); a?.Invoke(); }
        }
    }

    public class Blink : MonoBehaviour
    {
        public Graphic Target;
        public float Period = 1f;
        void Update()
        {
            if (Target) Target.enabled = Mathf.Repeat(Time.unscaledTime, Period) < Period * 0.55f;
        }
    }

    // ============================================================
    public static class TitleScreen
    {
        public static GameObject Build(RectTransform parent, Action onClockIn, Action onQuit)
        {
            var root = UIFactory.Solid(parent, new Color(0.02f, 0.03f, 0.08f), "TitleScreen");
            root.rectTransform.Stretch();

            // 走査線風の帯
            for (int i = 0; i < 40; i++)
            {
                var l = UIFactory.Solid(root.transform, new Color(1, 1, 1, 0.025f), "Scan");
                l.raycastTarget = false;
                l.rectTransform.StretchH(i * 20, 1);
            }

            var title = UIFactory.Label(root.transform, "NIGHT//SHIFT", 72, TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 1f), true);
            title.rectTransform.StretchH(200, 100);
            title.fontStyle = FontStyle.Bold;
            var cursor = UIFactory.Label(root.transform, "_", 72, TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 1f), true);
            cursor.rectTransform.StretchH(200, 100, 560, 0);
            cursor.gameObject.AddComponent<Blink>().Target = cursor;

            var sub = UIFactory.Label(root.transform, "Corporate IT Operations / Night Duty", 16, TextAnchor.MiddleCenter, new Color(0.55f, 0.65f, 0.8f), true);
            sub.rectTransform.StretchH(300, 24);
            var corp = UIFactory.Label(root.transform, "NEXUS CORPORATION  情報システム部  夜間IT運用担当", 13, TextAnchor.MiddleCenter, new Color(0.45f, 0.5f, 0.6f));
            corp.rectTransform.StretchH(330, 20);

            var clockIn = UIFactory.Button(root.transform, "CLOCK IN", onClockIn, "clock");
            clockIn.Rect.anchorMin = clockIn.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            clockIn.Rect.pivot = new Vector2(0.5f, 0.5f);
            clockIn.Rect.sizeDelta = new Vector2(180, 32);
            clockIn.Rect.anchoredPosition = new Vector2(0, -90);
            clockIn.Label.fontStyle = FontStyle.Bold;

            var quit = UIFactory.Button(root.transform, "終了(X)", onQuit, "exit");
            quit.Rect.anchorMin = quit.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            quit.Rect.pivot = new Vector2(0.5f, 0.5f);
            quit.Rect.sizeDelta = new Vector2(180, 28);
            quit.Rect.anchoredPosition = new Vector2(0, -130);

            var foot = UIFactory.Label(root.transform, "DAY 1 Vertical Slice  —  マウス＋キーボードでプレイ / Esc: 一時停止", 11, TextAnchor.MiddleCenter, new Color(0.4f, 0.45f, 0.5f));
            foot.rectTransform.anchorMin = new Vector2(0, 0); foot.rectTransform.anchorMax = new Vector2(1, 0);
            foot.rectTransform.pivot = new Vector2(0.5f, 0);
            foot.rectTransform.offsetMin = new Vector2(0, 14); foot.rectTransform.offsetMax = new Vector2(0, 32);
            return root.gameObject;
        }
    }

    // ============================================================
    public static class ClockInScreen
    {
        public static GameObject Build(RectTransform parent, DayDefinition day, Action onDone)
        {
            var root = UIFactory.Solid(parent, new Color(0.08f, 0.08f, 0.09f), "ClockIn");
            root.rectTransform.Stretch();

            var hint = UIFactory.Label(root.transform, "通用口  —  夜間入館", 14, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.6f));
            hint.rectTransform.StretchH(110, 24);

            var machine = UIFactory.Panel(root.transform, RetroTheme.Raised, "TimeRecorder");
            machine.rectTransform.anchorMin = machine.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            machine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            machine.rectTransform.sizeDelta = new Vector2(360, 300);
            machine.rectTransform.anchoredPosition = new Vector2(0, 10);

            UIFactory.Label(machine.transform, "TIME RECORDER  TR-500", 10, 10, 340, 18, 12, TextAnchor.MiddleLeft);
            var lcdBg = UIFactory.Image(machine.transform, RetroTheme.Field, new Color(0.15f, 0.22f, 0.15f), "LCD");
            lcdBg.rectTransform.Place(30, 36, 300, 70);
            var lcd = UIFactory.Label(lcdBg.transform, "23:58", 44, TextAnchor.MiddleCenter, new Color(0.55f, 1f, 0.55f), true);
            lcd.rectTransform.Stretch();
            var date = UIFactory.Label(machine.transform, day != null ? "1998/05/19 (火)" : "", 30, 110, 300, 18, 12, TextAnchor.MiddleCenter);

            var slot = UIFactory.Solid(machine.transform, Color.black, "Slot");
            slot.rectTransform.Place(110, 140, 140, 6);

            var card = UIFactory.Image(machine.transform, RetroTheme.Field, new Color(0.96f, 0.93f, 0.82f), "Card");
            card.rectTransform.Place(120, 150, 120, 90);
            UIFactory.Label(card.transform, "タイムカード\n夜間IT運用 night01", 4, 4, 112, 34, 11, TextAnchor.UpperCenter);
            var stamp = UIFactory.Label(card.transform, "", 4, 44, 112, 40, 12, TextAnchor.MiddleCenter);
            stamp.color = new Color(0.75f, 0.1f, 0.1f);

            RetroButton btn = null;
            btn = UIFactory.Button(machine.transform, "出勤を打刻する(I)", 100, 254, 160, 28, () =>
            {
                btn.Interactable = false;
                AudioFx.Stamp();
                stamp.text = "5/19  23:58\n出 勤";
                lcd.text = "23:58";
                Delay.Run(root.gameObject, 1.3f, onDone);
            }, "clock");
            var cw = root.gameObject.AddComponent<KeyAction>();
            cw.Key = KeyCode.Return; cw.Action = () => { if (btn.Interactable) btn.Click(); };
            var cw2 = root.gameObject.AddComponent<KeyAction>();
            cw2.Key = KeyCode.I; cw2.Action = () => { if (btn.Interactable) btn.Click(); };
            return root.gameObject;
        }
    }

    public class KeyAction : MonoBehaviour
    {
        public KeyCode Key;
        public Action Action;
        void Update() { if (Input.GetKeyDown(Key)) Action?.Invoke(); }
    }

    // ============================================================
    public static class LoginScreen
    {
        public const string ExpectedId = "night01";
        public const string ExpectedPassword = "nexus98";

        public static GameObject Build(RectTransform parent, GameDatabase db, Action onSuccess, Action onCancel)
        {
            var root = UIFactory.Solid(parent, RetroTheme.Desktop, "Login");
            root.rectTransform.Stretch();

            var host = UIFactory.Rect("Host", root.transform).Stretch();
            var wm = WindowManager.Create(host);
            var w = wm.Open(new WindowSpec
            {
                Id = "login", Title = $"{db.systemName} - ログオン", Icon = "lock", Width = 400, Height = 230,
                Closable = true, Minimizable = false, Maximizable = false, Position = new Vector2(440, 260)
            }, out _);
            w.CanClose = () => { onCancel(); return false; };
            var c = w.Content;

            var banner = UIFactory.Solid(c, RetroTheme.Navy, "Banner");
            banner.rectTransform.StretchH(0, 44);
            var bt = UIFactory.Label(banner.transform, $"{db.companyName}\n{db.systemName} {db.systemVersion}", 12, TextAnchor.MiddleLeft, Color.white);
            bt.rectTransform.Stretch(52, 0, 4, 0);
            var bic = UIFactory.Image(banner.transform, RetroTheme.BigIcon("network"), Color.white, "Logo");
            bic.rectTransform.Place(10, 6, 32, 32);

            UIFactory.Label(c, "ユーザーID(U):", 20, 60, 100, 20);
            var id = UIFactory.Input(c, 120, 60, 200, 20, ExpectedId);
            UIFactory.Label(c, "パスワード(P):", 20, 88, 100, 20);
            var pw = UIFactory.Input(c, "", true);
            ((RectTransform)pw.transform).Place(120, 88, 200, 20);
            var err = UIFactory.Label(c, "", 20, 114, 360, 18, 12);
            err.color = new Color(0.7f, 0, 0);

            void Submit()
            {
                if (id.text.Trim() == ExpectedId && pw.text == ExpectedPassword)
                {
                    err.color = RetroTheme.TextBlue;
                    err.text = "ログオンしています...";
                    AudioFx.Click();
                    Delay.Run(root.gameObject, 0.8f, onSuccess);
                }
                else
                {
                    AudioFx.Error();
                    err.text = "ユーザーIDまたはパスワードが正しくありません。";
                    pw.text = "";
                    pw.ActivateInputField();
                }
            }
            UIFactory.Button(c, "OK", 150, 142, 90, 24, Submit);
            UIFactory.Button(c, "キャンセル", 250, 142, 90, 24, onCancel);
            pw.onEndEdit.AddListener(_ => { if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)) Submit(); });
            Delay.Run(root.gameObject, 0.05f, () => pw.ActivateInputField());

            // モニターに貼られた付箋
            var note = UIFactory.Solid(root.transform, RetroTheme.Note, "StickyNote");
            note.rectTransform.anchorMin = note.rectTransform.anchorMax = new Vector2(1, 0);
            note.rectTransform.pivot = new Vector2(1, 0);
            note.rectTransform.sizeDelta = new Vector2(170, 130);
            note.rectTransform.anchoredPosition = new Vector2(-60, 60);
            note.rectTransform.localRotation = Quaternion.Euler(0, 0, 4);
            var nt = UIFactory.Label(note.transform, "夜間担当の方へ\n\nID : night01\nPW : nexus98\n\n※初日だけ。あとで変えること！\n　　　　　　　— K.", 12, TextAnchor.UpperLeft, new Color(0.15f, 0.15f, 0.3f));
            nt.rectTransform.Stretch(10, 10, 8, 8);
            return root.gameObject;
        }
    }

    // ============================================================
    public static class DayIntroScreen
    {
        public static GameObject Build(RectTransform parent, DayDefinition day, Action onDone)
        {
            var root = UIFactory.Solid(parent, Color.black, "DayIntro");
            root.rectTransform.Stretch();
            var cg = root.gameObject.AddComponent<CanvasGroup>();
            var t1 = UIFactory.Label(root.transform, $"DAY {day.dayNumber}", 56, TextAnchor.MiddleCenter, Color.white, true);
            t1.rectTransform.StretchH(300, 70);
            var t2 = UIFactory.Label(root.transform, $"— {day.title} —", 22, TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.9f), true);
            t2.rectTransform.StretchH(372, 30);
            var t3 = UIFactory.Label(root.transform, $"{day.dateLabel}  00:00 - 04:00", 13, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.55f), true);
            t3.rectTransform.StretchH(410, 20);
            var fade = root.gameObject.AddComponent<Fader>();
            fade.Group = cg;
            fade.Duration = 3.4f;
            fade.Done = onDone;
            var ch = root.gameObject.AddComponent<ClickHandler>();
            ch.OnClick = fade.Skip;
            return root.gameObject;
        }
    }

    public class Fader : MonoBehaviour
    {
        public CanvasGroup Group;
        public float Duration = 3f;
        public Action Done;
        float _t;
        bool _done;
        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float a = Mathf.Min(Mathf.Clamp01(_t / 0.6f), Mathf.Clamp01((Duration - _t) / 0.6f) + (_t < Duration ? 0 : 0));
            Group.alpha = _t < Duration - 0.6f ? Mathf.Clamp01(_t / 0.6f) : Mathf.Clamp01((Duration - _t) / 0.6f);
            if (_t >= Duration || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) Skip();
        }
        public void Skip()
        {
            if (_done) return;
            _done = true;
            Done?.Invoke();
        }
    }

    // ============================================================
    public static class EvaluationScreen
    {
        public static GameObject Build(RectTransform parent, DayDefinition day, EvaluationResult r, Action onNext, Action onRestart)
        {
            var root = UIFactory.Solid(parent, RetroTheme.Desktop, "Evaluation");
            root.rectTransform.Stretch();
            var host = UIFactory.Rect("Host", root.transform).Stretch();
            var wm = WindowManager.Create(host);
            var w = wm.Open(new WindowSpec
            {
                Id = "eval", Title = $"業務評価 - DAY {day.dayNumber} {day.title}", Icon = "doc", Width = 760, Height = 560,
                Closable = false, Minimizable = false, Maximizable = false, Position = new Vector2(260, 110), StatusCells = 1
            }, out _);
            w.SetStatus(0, "SHIFT COMPLETE  04:00   本日の業務記録を集計しました。");
            var c = w.Content;

            var head = UIFactory.Solid(c, RetroTheme.Navy, "Head");
            head.rectTransform.StretchH(0, 40);
            UIFactory.Label(head.transform, $"  SHIFT COMPLETE — DAY {day.dayNumber}  {day.dateLabel}", 16, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();

            var g = UIFactory.Group(c, "評価", 0, 50, 500, 250);
            float y = 0;
            foreach (var cat in r.Categories)
            {
                UIFactory.Label(g, cat.Label, 0, y, 120, 20).fontStyle = FontStyle.Bold;
                var barBg = UIFactory.Image(g, RetroTheme.Field, Color.white, "Bar");
                barBg.rectTransform.Place(122, y + 2, 200, 16);
                int blocks = Mathf.RoundToInt(cat.Score / 5f);
                for (int i = 0; i < blocks; i++)
                {
                    var b = UIFactory.Solid(barBg.transform, RetroTheme.Navy, "B");
                    b.rectTransform.Place(3 + i * 9.7f, 3, 8, 10);
                }
                UIFactory.Label(g, cat.Score.ToString(), 328, y, 40, 20, 12, TextAnchor.MiddleRight);
                UIFactory.Label(g, cat.Comment, 122, y + 20, 360, 18, 11).color = RetroTheme.Shadow;
                y += 44;
            }

            var rg = UIFactory.Group(c, "総合", 510, 50, 240, 250);
            var rank = UIFactory.Label(rg, r.Rank, 0, 10, 224, 120, 96, TextAnchor.MiddleCenter);
            rank.font = RetroTheme.MonoFont;
            rank.color = RetroTheme.Navy;
            UIFactory.Label(rg, $"総合評価  {r.Overall} / 100", 0, 140, 224, 22, 14, TextAnchor.MiddleCenter);
            UIFactory.Label(rg, RankComment(r.Rank), 0, 166, 224, 50, 11, TextAnchor.UpperCenter).color = RetroTheme.Shadow;

            var sg = UIFactory.Group(c, "業務記録（内部）", 0, 306, 750, 160);
            int col = 0, row = 0;
            foreach (var kv in r.Stats)
            {
                float x = col * 370;
                UIFactory.Label(sg, kv.Key, x, row * 19, 230, 18, 12);
                UIFactory.ValueBox(sg, x + 236, row * 19, 110, 18, kv.Value);
                row++;
                if (row >= 6) { row = 0; col++; }
            }

            UIFactory.Button(c, "次へ(N)", 520, 476, 110, 26, onNext);
            UIFactory.Button(c, $"DAY{day.dayNumber}をやり直す(R)", 636, 476, 114, 26, onRestart);
            var k1 = root.gameObject.AddComponent<KeyAction>(); k1.Key = KeyCode.N; k1.Action = onNext;
            var k2 = root.gameObject.AddComponent<KeyAction>(); k2.Key = KeyCode.R; k2.Action = onRestart;
            return root.gameObject;
        }

        static string RankComment(string rank)
        {
            switch (rank)
            {
                case "S": return "非の打ちどころがない夜でした。";
                case "A": return "初日としては十分すぎる働きです。";
                case "B": return "概ね問題なし。確認の手順を固めましょう。";
                case "C": return "いくつか見落としがありました。";
                default: return "明日は、まず調べてから判断を。";
            }
        }
    }

    // ============================================================
    public static class ComingSoonScreen
    {
        public static GameObject Build(RectTransform parent, int nextDay, Action onRestart, Action onTitle)
        {
            var root = UIFactory.Solid(parent, Color.black, "ComingSoon");
            root.rectTransform.Stretch();
            var t1 = UIFactory.Label(root.transform, $"DAY {nextDay} — COMING SOON", 40, TextAnchor.MiddleCenter, Color.white, true);
            t1.rectTransform.StretchH(250, 60);
            var t2 = UIFactory.Label(root.transform, "明日は担当範囲がさらに広がる。", 16, TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.9f));
            t2.rectTransform.StretchH(330, 26);
            var t3 = UIFactory.Label(root.transform, "・サーバー運用管理  解禁予定\n・より手口の巧妙な、人間による攻撃", 13, TextAnchor.UpperCenter, new Color(0.5f, 0.55f, 0.6f));
            t3.rectTransform.StretchH(372, 60);

            var b1 = UIFactory.Button(root.transform, "DAY1 をもう一度(R)", onRestart, "clock");
            b1.Rect.anchorMin = b1.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            b1.Rect.pivot = new Vector2(0.5f, 0.5f);
            b1.Rect.sizeDelta = new Vector2(180, 28);
            b1.Rect.anchoredPosition = new Vector2(-95, -120);
            var b2 = UIFactory.Button(root.transform, "タイトルへ(T)", onTitle, "exit");
            b2.Rect.anchorMin = b2.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            b2.Rect.pivot = new Vector2(0.5f, 0.5f);
            b2.Rect.sizeDelta = new Vector2(180, 28);
            b2.Rect.anchoredPosition = new Vector2(95, -120);
            var k1 = root.gameObject.AddComponent<KeyAction>(); k1.Key = KeyCode.R; k1.Action = onRestart;
            var k2 = root.gameObject.AddComponent<KeyAction>(); k2.Key = KeyCode.T; k2.Action = onTitle;
            return root.gameObject;
        }
    }
}
