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
        public static GameObject Build(RectTransform parent, Action onClockIn, Action onContinue, int reachedDay, Action onQuit, int dayCount = 0, Action<int> onStartDay = null)
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

            float by = -90;
            RetroButton Centered(string label, Action a, string icon, float h)
            {
                var b = UIFactory.Button(root.transform, label, a, icon);
                b.Rect.anchorMin = b.Rect.anchorMax = new Vector2(0.5f, 0.5f);
                b.Rect.pivot = new Vector2(0.5f, 0.5f);
                b.Rect.sizeDelta = new Vector2(200, h);
                b.Rect.anchoredPosition = new Vector2(0, by);
                by -= h + 8;
                return b;
            }
            var clockIn = Centered(onContinue != null ? "CLOCK IN（DAY 1 から）" : "CLOCK IN", onClockIn, "clock", 32);
            clockIn.Label.fontStyle = FontStyle.Bold;
            if (onContinue != null)
                Centered($"続きから（DAY {reachedDay}）", onContinue, "clock", 32).Label.fontStyle = FontStyle.Bold;
            // 設定（タイトル画面上にウィンドウとして開く）
            var host = UIFactory.Rect("WindowHost", root.transform).Stretch();
            var wm = WindowManager.Create(host);
            Centered("設定(O)", () =>
            {
                var w = wm.Open(new WindowSpec
                {
                    Id = "settings", Title = "設定", Icon = "gear", Width = 560, Height = 440,
                    Minimizable = false, Maximizable = false, Position = new Vector2(360, 160)
                }, out bool created);
                if (created) SettingsPanel.Build(w.Content);
            }, "gear", 28);
            Centered("終了(X)", onQuit, "exit", 28);

            // デバッグモード: 任意の日から開始
            if (onStartDay != null && dayCount > 0)
            {
                var row = UIFactory.Rect("DebugDays", root.transform);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(dayCount * 66 + 120, 26);
                row.anchoredPosition = new Vector2(0, by - 8);
                var lbl = UIFactory.Label(row, "DEBUG 開始日:", 0, 0, 110, 24, 12, TextAnchor.MiddleRight);
                lbl.color = new Color(1f, 0.6f, 0.4f);
                for (int i = 1; i <= dayCount; i++)
                {
                    int day = i;
                    UIFactory.Button(row, $"DAY{day}", 116 + (i - 1) * 66, 0, 62, 24, () => onStartDay(day));
                }
                var toggle = row.gameObject.AddComponent<SettingVisibility>();
                toggle.Refresh();
            }
            host.SetAsLastSibling();

            var foot = UIFactory.Label(root.transform, $"DAY 1-7 Tutorial  —  マウス＋キーボードでプレイ / {Core.GameSettings.KeyLabel(Core.GameSettings.Pause)}: 一時停止", 11, TextAnchor.MiddleCenter, new Color(0.4f, 0.45f, 0.5f));
            foot.rectTransform.anchorMin = new Vector2(0, 0); foot.rectTransform.anchorMax = new Vector2(1, 0);
            foot.rectTransform.pivot = new Vector2(0.5f, 0);
            foot.rectTransform.offsetMin = new Vector2(0, 14); foot.rectTransform.offsetMax = new Vector2(0, 32);
            return root.gameObject;
        }
    }

    // ============================================================
    public static class ClockInScreen
    {
        public const int MaxNameLength = 16;

        /// <summary>入力されたユーザー名を検証する。null = OK、それ以外はエラーメッセージ。</summary>
        public static string Validate(string name)
        {
            if (string.IsNullOrEmpty(name)) return "ユーザー名を入力してください。";
            if (name.Length > MaxNameLength) return $"ユーザー名は {MaxNameLength} 文字以内で入力してください。";
            foreach (char ch in name)
                if (char.IsWhiteSpace(ch) || char.IsControl(ch) || ch == '\\' || ch == '/') return "空白や記号（\\ /）は使用できません。";
            return null;
        }

        public static GameObject Build(RectTransform parent, DayDefinition day, string currentName, Action<string> onDone)
        {
            var root = UIFactory.Solid(parent, new Color(0.08f, 0.08f, 0.09f), "ClockIn");
            root.rectTransform.Stretch();

            var hint = UIFactory.Label(root.transform, "通用口  —  夜間入館", 14, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.6f));
            hint.rectTransform.StretchH(90, 24);

            var machine = UIFactory.Panel(root.transform, RetroTheme.Raised, "TimeRecorder");
            machine.rectTransform.anchorMin = machine.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            machine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            machine.rectTransform.sizeDelta = new Vector2(360, 380);
            machine.rectTransform.anchoredPosition = new Vector2(0, 0);

            UIFactory.Label(machine.transform, "TIME RECORDER  TR-500", 10, 10, 340, 18, 12, TextAnchor.MiddleLeft);
            var lcdBg = UIFactory.Image(machine.transform, RetroTheme.Field, new Color(0.15f, 0.22f, 0.15f), "LCD");
            lcdBg.rectTransform.Place(30, 36, 300, 70);
            var lcd = UIFactory.Label(lcdBg.transform, "23:58", 44, TextAnchor.MiddleCenter, new Color(0.55f, 1f, 0.55f), true);
            lcd.rectTransform.Stretch();
            string clockDate = day != null && !string.IsNullOrEmpty(day.clockInDateLabel) ? day.clockInDateLabel : "1998/05/19 (火)";
            UIFactory.Label(machine.transform, clockDate, 30, 110, 300, 18, 12, TextAnchor.MiddleCenter);
            // 打刻印字用の "M/D"
            string stampDate = "5/19";
            var parts = clockDate.Split(' ')[0].Split('/');
            if (parts.Length == 3 && int.TryParse(parts[1], out int mm) && int.TryParse(parts[2], out int dd)) stampDate = $"{mm}/{dd}";

            var slot = UIFactory.Solid(machine.transform, Color.black, "Slot");
            slot.rectTransform.Place(80, 138, 200, 6);

            // タイムカード（氏名欄に記入する）
            var card = UIFactory.Image(machine.transform, RetroTheme.Field, new Color(0.96f, 0.93f, 0.82f), "Card");
            card.rectTransform.Place(70, 148, 220, 130);
            UIFactory.Label(card.transform, "タイムカード　　夜間IT運用", 6, 4, 208, 18, 11, TextAnchor.MiddleCenter);
            UIFactory.Solid(card.transform, new Color(0.6f, 0.45f, 0.4f), "Rule").rectTransform.Place(6, 23, 208, 1);
            UIFactory.Label(card.transform, "氏名:", 8, 30, 40, 22, 12);
            // 氏名の記入は初日だけ。2日目以降は記入済みのカードに打刻するだけ
            bool firstDay = day == null || day.dayNumber <= 1 || string.IsNullOrEmpty(currentName) || currentName == Core.GameManager.DefaultOperatorName;
            string initial = currentName == Core.GameManager.DefaultOperatorName ? "" : currentName;
            var nameField = UIFactory.Input(card.transform, 48, 30, 164, 22, initial);
            nameField.characterLimit = MaxNameLength;
            if (!firstDay)
            {
                nameField.interactable = false;
                nameField.GetComponent<Image>().color = new Color(0.96f, 0.93f, 0.82f);
            }
            var stamp = UIFactory.Label(card.transform, "", 6, 60, 208, 62, 13, TextAnchor.MiddleCenter);
            stamp.color = new Color(0.75f, 0.1f, 0.1f);

            var msg = UIFactory.Label(machine.transform, firstDay ? "氏名を記入して打刻してください（ログインIDになります）" : "カードを差し込んで打刻してください", 10, 284, 340, 18, 11, TextAnchor.MiddleCenter);
            msg.color = RetroTheme.Shadow;

            RetroButton btn = null;
            bool done = false;
            void Punch()
            {
                if (done) return;
                string name = nameField.text.Trim();
                string error = Validate(name);
                if (error != null)
                {
                    AudioFx.Error();
                    msg.text = error;
                    msg.color = new Color(0.7f, 0, 0);
                    nameField.ActivateInputField();
                    return;
                }
                done = true;
                btn.Interactable = false;
                nameField.interactable = false;
                AudioFx.Stamp();
                stamp.text = $"{stampDate}  23:58  出 勤\n{name}";
                msg.color = RetroTheme.Shadow;
                msg.text = $"ログインID: {name}";
                Delay.Run(root.gameObject, 1.3f, () => onDone(name));
            }
            btn = UIFactory.Button(machine.transform, "出勤を打刻する", 100, 310, 160, 30, Punch, "clock");
            if (firstDay)
            {
                nameField.onEndEdit.AddListener(_ => { if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)) Punch(); });
                Delay.Run(root.gameObject, 0.05f, () => { if (nameField) nameField.ActivateInputField(); });
            }
            else
            {
                var enter = root.gameObject.AddComponent<KeyAction>();
                enter.Key = KeyCode.Return;
                enter.Action = Punch;
            }
            return root.gameObject;
        }
    }

    /// <summary>デバッグモードの設定に合わせて表示を切り替える</summary>
    public class SettingVisibility : MonoBehaviour
    {
        bool _subscribed;
        public void Refresh()
        {
            if (!_subscribed) { Core.GameSettings.Changed += Refresh; _subscribed = true; }
            if (this != null) gameObject.SetActive(Core.GameSettings.Current.debugMode);
        }
        void OnDestroy() { Core.GameSettings.Changed -= Refresh; }
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
        public static GameObject Build(RectTransform parent, GameDatabase db, Core.OperatorAccount account, Action onSuccess, Action onCancel)
        {
            var root = UIFactory.Solid(parent, RetroTheme.Desktop, "Login");
            root.rectTransform.Stretch();

            var host = UIFactory.Rect("Host", root.transform).Stretch();
            var wm = WindowManager.Create(host);
            var w = wm.Open(new WindowSpec
            {
                Id = "login", Title = $"{db.systemName} - ログイン", Icon = "lock", Width = 400, Height = 250,
                Closable = true, Minimizable = false, Maximizable = false, Position = new Vector2(440, 250)
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
            var id = UIFactory.Input(c, 120, 60, 200, 20, account.UserId);
            UIFactory.Label(c, "パスワード(P):", 20, 88, 100, 20);
            var pw = UIFactory.Input(c, "", true);
            ((RectTransform)pw.transform).Place(120, 88, 200, 20);
            var err = UIFactory.Label(c, "", 20, 114, 360, 18, 12);
            err.color = new Color(0.7f, 0, 0);

            GameObject note = null;
            void BuildNote()
            {
                if (note != null) UnityEngine.Object.Destroy(note);
                note = StickyNote(root.transform, account);
            }

            bool busy = false;
            void Submit()
            {
                if (busy) return;
                if (account.Matches(id.text, pw.text))
                {
                    busy = true;
                    err.color = RetroTheme.TextBlue;
                    err.text = "ログインしています...";
                    AudioFx.Click();
                    Delay.Run(root.gameObject, 0.8f, onSuccess);
                }
                else
                {
                    AudioFx.Error();
                    err.color = new Color(0.7f, 0, 0);
                    err.text = "ユーザーIDまたはパスワードが正しくありません。";
                    pw.text = "";
                    pw.ActivateInputField();
                }
            }
            UIFactory.Button(c, "OK", 150, 142, 90, 24, Submit);
            UIFactory.Button(c, "キャンセル", 250, 142, 90, 24, onCancel);
            var forgot = UIFactory.FlatButton(c, "パスワードを忘れた場合(F)", () =>
            {
                // 情報システム部に連絡して初期化してもらう（回数は記録され、後の日の評価・脆弱性に影響し得る）
                account.ResetToInitial();
                AudioFx.Notify();
                err.color = RetroTheme.TextBlue;
                err.text = "情報システム部に連絡し、パスワードを初期化しました。";
                BuildNote();
                pw.text = "";
                pw.ActivateInputField();
            });
            forgot.Label.color = RetroTheme.TextBlue;
            forgot.Rect.Place(20, 174, 200, 20);
            pw.onEndEdit.AddListener(_ => { if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)) Submit(); });
            Delay.Run(root.gameObject, 0.05f, () => pw.ActivateInputField());

            BuildNote();
            return root.gameObject;
        }

        /// <summary>
        /// モニターに貼られた付箋。初期パスワードの間は前任者のメモ。
        /// 変更後は、プレイヤーが「付箋に書いた」場合だけ新しいパスワードが貼られている（脆弱性の種）。
        /// </summary>
        static GameObject StickyNote(Transform parent, Core.OperatorAccount account)
        {
            var note = NightShift.UI.StickyNote.Create(parent, account, 70, 430, account.Changed ? -3 : 4);
            return note != null ? note.gameObject : null;
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
        public static GameObject Build(RectTransform parent, DayDefinition day, EvaluationResult r, Core.PaySlip slip, Core.PaySlip previous, bool updated, int balance, Action onNext, Action onRestart)
        {
            var root = UIFactory.Solid(parent, RetroTheme.Desktop, "Evaluation");
            root.rectTransform.Stretch();
            var host = UIFactory.Rect("Host", root.transform).Stretch();
            var wm = WindowManager.Create(host);
            var w = wm.Open(new WindowSpec
            {
                Id = "eval", Title = $"業務評価 - DAY {day.dayNumber} {day.title}", Icon = "doc", Width = 760, Height = 690,
                Closable = false, Minimizable = false, Maximizable = false, Position = new Vector2(260, 40), StatusCells = 1
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

            // 給与明細（スコアに応じて支給。所持金は後のストアで使う）
            var pg = UIFactory.Group(c, "給与明細", 0, 472, 750, 118);
            if (slip != null)
            {
                string Y(int v) => Core.OperatorWallet.Yen(v);
                float px = 0;
                void Item(string label, string value, float w)
                {
                    UIFactory.Label(pg, label, px, 0, w, 18, 12);
                    UIFactory.ValueBox(pg, px, 20, w - 8, 22, value);
                    px += w;
                }
                Item("基本給（夜勤）", Y(slip.basePay), 150);
                Item($"評価手当（{slip.score}点）", Y(slip.performancePay), 150);
                Item($"ランク賞与（{slip.rank}）", Y(slip.rankBonus), 150);
                Item("減額", slip.deduction > 0 ? "−" + Y(slip.deduction) : Y(0), 150);
                var total = UIFactory.Label(pg, $"本日の支給額  {Y(slip.total)}", 0, 50, 360, 28, 18);
                total.fontStyle = FontStyle.Bold;
                total.color = RetroTheme.Navy;
                UIFactory.Label(pg, $"所持金  {Y(balance)}", 600, 50, 140, 28, 14, TextAnchor.MiddleRight).fontStyle = FontStyle.Bold;
                string note = slip.deductionNotes.Count > 0 ? "減額理由: " + string.Join("、", slip.deductionNotes) + "　" : "";
                if (previous != null)
                    note += updated ? $"（前回の支給額 {Y(previous.total)} から更新しました）" : $"（このDAYは前回の支給額 {Y(previous.total)} のまま計上されます）";
                UIFactory.Label(pg, note, 0, 78, 740, 18, 11).color = RetroTheme.Shadow;
            }

            UIFactory.Button(c, "翌日へ(N)", 520, 598, 110, 26, onNext);
            UIFactory.Button(c, $"DAY{day.dayNumber}をやり直す(R)", 636, 598, 114, 26, onRestart);
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
        public static GameObject Build(RectTransform parent, int finishedDay, int nextDay, Action onRestart, Action onTitle)
        {
            var root = UIFactory.Solid(parent, Color.black, "ComingSoon");
            root.rectTransform.Stretch();
            var t1 = UIFactory.Label(root.transform, $"DAY {nextDay} — COMING SOON", 40, TextAnchor.MiddleCenter, Color.white, true);
            t1.rectTransform.StretchH(250, 60);
            var t2 = UIFactory.Label(root.transform, "明日は担当範囲がさらに広がる。", 16, TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.9f));
            t2.rectTransform.StretchH(330, 26);
            var t3 = UIFactory.Label(root.transform, "・サーバー運用管理  解禁予定\n・より手口の巧妙な、人間による攻撃", 13, TextAnchor.UpperCenter, new Color(0.5f, 0.55f, 0.6f));
            t3.rectTransform.StretchH(372, 60);

            var b1 = UIFactory.Button(root.transform, $"DAY{finishedDay} をもう一度(R)", onRestart, "clock");
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
