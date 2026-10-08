using System;
using System.Collections.Generic;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;

namespace NightShift.Systems.SideQuests
{
    /// <summary>
    /// SQ03「在籍確認」（SUBQUEST_SPEC.md §5）。名簿にいない「雨宮リカ／総務」からのメール。
    /// H1 返信（返信の送信で開始。読むだけ・報告だけでは始まらない）→ H2 リモート画面を閉じる → H3 届き続けるメールを解読する。
    /// 謎: 3通（到着順 B→C→A、元の送信時刻順 A→B→C）の各行の最初の一文字を、元の送信時刻順に読むと「ここにはいない」。
    /// GOOD SE-HG01（正答を期限前に返信）/ BAD SE-HB01（永続接続の許可・最終猶予切れ）/ TRUE SE-HT01（担当変更で次の担当者へ転送）。
    /// 正体・目的は説明しない。調べて分かるのは条件と挙動だけ。
    /// </summary>
    public class Sq03Route : QuestRoute
    {
        public Sq03Route(SideQuestManager q) : base(q) { }
        public override string Id => "SQ03";
        public override string Title => "在籍確認";
        public override int EarliestDay => 12;

        public const string Name = "雨宮リカ";
        public const string From = "総務部 雨宮リカ";
        public const string Address = "amamiya.r@nexus-corp.example";
        public const string Device = "GA-PC00";
        public const string DeviceIp = "10.10.6.99";
        public const string Answer = "ここにはいない";
        public const float FinalGameMinutes = 30, FinalRealSeconds = 300, ExtendMinutes = 5;

        // 3通の手がかり（固定。乱数化しない）。各行は独立した段落として描画する
        public static readonly string[] ClueA = { "ここから先は、名簿を見ないでください。", "こちらにないものを、数えないでください。", "にんずうは、もう合っています。" };
        public static readonly string[] ClueB = { "はじめに返したものを、覚えていますか。", "いまも、それを使っています。" };
        public static readonly string[] ClueC = { "なまえは、そのままで結構です。", "いすは、空けておいてください。" };

        /// <summary>対応記録（リモート対応で見えたもの。自動で保存され、いつでも再表示できる）</summary>
        public static string RecordText =>
            "【対応記録】リモート対応（制限付きセッション・自動保存）\n\n" +
            "■ 接続した直後\n社員証の画像の背後に、メール本文の輪郭が見えた。\n各段落の<b>先頭の一文字分だけが白く</b>、右側は黒く欠けていた。\n\n" +
            "■ 社員証を強制終了した後\n背後の一覧が読めた。「元の送信時刻」欄に <b>00:01、00:02、00:03</b> が上から順に並び、「受信順」の欄だけが消されていた。\n\n" +
            "■ 切断した後に届いたメール\n「読めたところだけで結構です。そのまま、お返しください。」";

        public override bool Ready() =>
            Unlocked(Features.RemoteSupport) && Unlocked(Features.ConnectionMonitor) && Unlocked(Features.Isolation);

        string Stage { get => State.stage; set => State.stage = value; }
        bool Replied => State.Has("REPLIED");
        public bool InH3 => Stage == "H3";
        public bool DeadlineRunning => _deadline > 0;
        public float GameRemaining => _deadline > 0 ? Math.Max(0, _deadline - Now) : State.timer;
        public float RealRemaining => _realLeft;
        float _deadline = -1, _realLeft;
        float _nextAtmosphere = -1;
        EventInstance _persistent;

        public override string Memo() => Stage ?? "";

        // ================= H1 =================
        public override void Offer()
        {
            Stage = "OFFER";
            Mail("OFFER", From, Address, "社員名簿の表示について",
                "お疲れさまです。社員名簿に私が表示されないようです。\nそちらから確認していただけますか。",
                replies: new[] { "社員番号を教えてください", "所属を確認してください", "対応できません" });
        }

        /// <summary>閲覧・報告だけでは始まらない。報告しても誤報にしない</summary>
        public override bool? OnReported(QuestMailRecord r)
        {
            if (Replied) S.Logs.Add(Now, "MAIL-SRV01", "-", "-", "MAIL", "OK", "報告済みのメールの配送記録: 正常");
            return null;
        }

        public override bool Touched() => Replied;

        public override void OnReply(QuestMailRecord r, int index, string text)
        {
            if (r.mailId == "SQ03-OFFER" && !Replied)
            {
                State.Mark("REPLIED");
                State.status = QuestStatus.Active;
                Stage = "H1";
                Mail("H1-SEEN", From, Address, "Re: 社員名簿の表示について",
                    "こちらでも、あなたを確認できました。", time: Now + 2, horror: true,
                    actionKey: "VERIFY", actionLabel: "在籍を確認（総務へ照会）");
                Q.At(Now + 2.5f, "SQ03 FX-H01", () => FirstFx("AFTERIMAGE"));
                Q.At(Now + 6, "SQ03 REMOTE", StartH2);
                return;
            }
            if (r.mailId == "SQ03-H3-ANSWER") CheckAnswer(text);
        }

        void FirstFx(string fx)
        {
            if (Q.HorrorEpisode(fx, false) && !State.Has("FX_DAY")) { State.Mark("FX_DAY"); State.nextTaskDay = Day; }
            else if (!State.Has("FX_DAY")) { State.Mark("FX_DAY"); State.nextTaskDay = Day; }
        }

        /// <summary>機器保護札が店頭に並ぶか（最初のPC不調を経験した翌夜から）</summary>
        public static bool CharmOnSale(CareerRecord c, int day)
        {
            var st = c.FindQuest("SQ03");
            return st != null && st.Has("FX_DAY") && day > st.nextTaskDay;
        }

        public override void OnAction(QuestMailRecord r, string key)
        {
            switch (key)
            {
                case "VERIFY": Verify(); break;
                case "REMOTE": Q.RequestUi("REMOTE", this); break;
                case "RECORD": Q.RequestUi("RECORD", RecordText); break;
                case "HINT": ShowHint(); break;
                case "TRUE": Q.RequestUi("CONFIRM_TRUE", this); break;
            }
        }

        void Verify()
        {
            if (State.Has("VERIFIED")) return;
            State.Mark("VERIFIED");
            var ga = Pick("ga", e => e.department == "総務部") ?? Pick("ga2", e => true);
            string who = ga != null ? $"総務部 {ga.fullName}" : "総務部";
            S.Logs.Add(Now, "NIGHT-OPS", "総務部", S.OperatorName, "MAIL", "SENT", $"在籍照会: {Name}");
            float at = S.Day.endMinute - Now < 3 ? Now + 0.5f : Now + 3;
            // 照会に答える総務の社員は、問い合わせと同じ口調で書く（中身は同じ）
            string[] byPersona =
            {
                $"照会の件、確認いたしました。\n{Name}という社員は、在籍しておりません。過去の名簿にも見当たりません。\n登録はなさらず、対応はお止めください。",
                "確認しました。その名前の社員はいません。\n登録せず、対応を止めてください。",
                "調べたけど、そんな人いないよ？\n登録しないで、対応もやめといて。",
                "あの…何度も見たんですが、その名前の方はいません…\n登録しないで、対応も止めてください…なんだか、気味が悪いです…",
                "該当者なし。\n登録不要。対応を止めること。",
                "そんな社員はいません。古い名簿まで全部見ました。\n登録はしないこと。それと、その件にはもう返事をしないほうがいい。",
                "確認しました〜！その名前の人はいないみたいです。\n登録しないで、対応も止めちゃってくださいね〜。…ちょっと怖いですね。"
            };
            Mail("VERIFY-REPLY", who, "soumu@" + MailManager.CompanyDomain, "Re: 在籍照会（" + Name + "）",
                byPersona[(int)Voice.Of(ga)], time: at);
        }

        // ================= H2 =================
        void StartH2()
        {
            if (Q.Ended || !State.IsOpen || Stage != "H1") return;
            Stage = "H2";
            Mail("H2-REMOTE", From, Address, "画面が閉じられません",
                "画面が閉じられません。そちらから閉じていただけますか。\n" +
                "文字は、各行の左端だけ残っています。メールの順番も、届いた順ではないようです。",
                time: Now, horror: true, actionKey: "REMOTE", actionLabel: "リモート対応を開く");
            // リモートを開かなくても、断っても、しばらくすると向こうから開く
            Q.At(Now + 15, "SQ03 AUTO H3", () => { if (Stage == "H2") StartH3(); });
            _nextAtmosphere = Now + 10;
        }

        /// <summary>リモート画面: 接続した</summary>
        public void OnRemoteConnected()
        {
            State.Mark("REMOTE_OPENED");
            S.Logs.Add(Now, "NIGHT-OPS", Device, S.OperatorName, "REMOTE", "CONNECT", "制限付きセッション（転送・資格情報の委譲なし）");
            if (!State.Has("FX_H07")) { State.Mark("FX_H07"); Q.HorrorEpisode("SCAN", false); }
        }

        /// <summary>リモート画面: 社員証を強制終了した（FX-H02 固定）</summary>
        public void OnForceQuit()
        {
            if (State.Has("FORCE_QUIT")) return;
            State.Mark("FORCE_QUIT");
            S.Logs.Add(Now, Device, "-", S.OperatorName, "PROCESS", "KILL", "画像ビューアーを強制終了（復旧処理・記録は自動）");
            Q.HorrorEpisode("APPROACH", true);
            Mail("H2-KILLED", From, Address, "Re: 画面が閉じられません", "終了したのは、表示に使っていた部分です。", time: Now + 1, horror: true);
            Q.At(Now + 2, "SQ03 FX-H06", () => Q.HorrorEpisode("RED", false));
        }

        /// <summary>リモート画面: 切断した（FX-H03 は一度だけ）</summary>
        public void OnDisconnected()
        {
            if (State.Has("DISCONNECTED")) return;
            State.Mark("DISCONNECTED");
            S.Logs.Add(Now, "NIGHT-OPS", Device, S.OperatorName, "REMOTE", "DISCONNECT", "リモート対応を終了");
            Q.At(Now + 1, "SQ03 FX-H03", () =>
            {
                if (Q.HorrorEpisode("DESK", true))
                    Mail("H2-DESK", From, Address, "Re: 画面が閉じられません", "今度は、そちらが合わせてください。", time: Now + 0.5f, horror: true);
            });
            Q.At(Now + 10, "SQ03 H3", () => { if (Stage == "H2") StartH3(); });
        }

        // ================= H3 =================
        void StartH3()
        {
            if (Q.Ended || !State.IsOpen || InH3) return;
            Stage = "H3";
            Mail("H3-OPEN", From, Address, "そちらで開きます", "そちらで開きます。", time: Now, horror: true,
                actionKey: "RECORD", actionLabel: "対応記録を見る");
            Q.RequestUi("RECORD", RecordText);
            // 手がかり3通は6分以内に届く（到着順 B→C→A）。後続メールに埋もれないよう固定表示
            Q.At(Now + 1, "SQ03 CLUE B", () => Clue("H3-B", "00:02", ClueB));
            Q.At(Now + 3, "SQ03 CLUE C", () => Clue("H3-C", "00:03", ClueC));
            Q.At(Now + 4, "SQ03 TRUE", TrueMail);
            Q.At(Now + 5, "SQ03 CLUE A", () => { Clue("H3-A", "00:01", ClueA); AnswerMail(); StartDeadline(); });
            Q.At(Now + 2, "SQ03 PERSISTENT", SpawnPersistent);
            _nextAtmosphere = Now + 4;
        }

        void Clue(string id, string sent, string[] lines)
        {
            Mail(id, From, Address, "（件名なし）",
                $"元の送信時刻: {sent}\n\n" + string.Join("\n\n", lines),
                time: Now, pinned: true, horror: true, sentLabel: sent, actionKey: "RECORD", actionLabel: "対応記録を見る");
        }

        void AnswerMail()
        {
            Mail("H3-ANSWER", From, Address, "そのまま、お返しください",
                "読めたところだけで結構です。そのまま、お返しください。\n\n<color=#606060>対応記録に、文字の残り方と並び方が記録されています。</color>",
                time: Now, pinned: true, horror: true, freeReply: true, actionKey: "HINT", actionLabel: "ヒントを見る");
        }

        void TrueMail()
        {
            Mail("H3-TRUE", "総務部 業務システム", "soumu-system@nexus-corp.example", "【自動通知】担当変更の手続き",
                "■ 通知種別：担当変更\n" +
                $"■ 対象案件：在籍確認（{Name}）\n" +
                "■ 処理内容：元のメールを次の夜間担当へ転送\n" +
                "■ 転送先　：night02@nexus-corp.example（架空の宛先）\n\n" +
                "下のボタンから手続きを確定できます。\n" +
                "――このメールはシステムから自動で送信されています。返信はできません。",
                time: Now, pinned: true, actionKey: "TRUE", actionLabel: "次の担当者へ転送する");
        }

        void ShowHint()
        {
            int step = State.Has("HINT1") ? 2 : 1;
            if (step == 1) State.Mark("HINT1"); else State.Mark("HINT2");
            string text = step == 1 ? "ヒント 1/2: 届いた順ではなく、「元の送信時刻」の順に並べてください。"
                                     : "ヒント 1/2: 届いた順ではなく、「元の送信時刻」の順に並べてください。\nヒント 2/2: 各行の一文字目を、続けて読んでください。";
            Q.RequestUi("HINT", text);
        }

        /// <summary>空白・末尾の句点・全角半角の空白を除いて比べる</summary>
        public static string Normalize(string s)
        {
            if (s == null) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s.Trim()) if (!char.IsWhiteSpace(c) && c != '　') sb.Append(c);
            string t = sb.ToString();
            while (t.EndsWith("。") || t.EndsWith(".") || t.EndsWith("．")) t = t.Substring(0, t.Length - 1);
            return t;
        }

        void CheckAnswer(string text)
        {
            if (Q.Ended || !State.IsOpen) return;
            S.Logs.Add(Now, "NIGHT-OPS", "-", S.OperatorName, "MAIL", "SENT", "返信: " + text);
            if (Normalize(text) == Answer)
            {
                _deadline = -1;
                CancelPersistent();
                Mail("RETURNED", "MAILER-DAEMON", "mailer-daemon@nexus-corp.example", "Undelivered Mail Returned to Sender",
                    "This is the mail system at host mail.nexus-corp.example.\n\n" +
                    "I'm sorry to have to inform you that your message could not be delivered.\n\n" +
                    $"<{Address}>: User unknown\n\n" +
                    "――――――――――――――――\n宛先が存在しません。", time: Now);
                Q.Resolve(this, QuestResult.Completed, "SE-HG01");
            }
            else
            {
                Mail("WRONG-" + (Career.questMails.FindAll(m => m.mailId.StartsWith("SQ03-WRONG")).Count + 1), From, Address, "Re: そのまま、お返しください", "その名前では、戻せません。", time: Now + 0.5f, horror: true);
            }
        }

        public void ConfirmTrue()
        {
            if (Q.Ended || !State.IsOpen) return;
            S.Logs.Add(Now, "NIGHT-OPS", "night02", S.OperatorName, "MAIL", "FORWARD", "元のメールを次の担当者へ転送");
            CancelPersistent();
            Q.Resolve(this, QuestResult.Completed, "SE-HT01");
        }

        // ---------------- 永続接続要求 ----------------
        void SpawnPersistent()
        {
            if (Q.Ended || !InH3 || (_persistent != null && _persistent.State == EventState.Pending)) return;
            _persistent = Q.SpawnRequest(new SideQuestManager.RequestSpec
            {
                Tag = "SQ03-PERSIST", MailId = "SQ03-H3-ANSWER", DisplayUser = Name, DisplayEmployeeId = "（名簿に該当なし）",
                Device = Device, SourceIp = DeviceIp, NodeId = "AUTH-SRV01", AccessType = "VPN接続",
                Note = "本人未確認・名簿に該当なし。永続接続の要求。",
                Confirm = "この要求は「永続接続」です。\n許可すると、受領が確定します（この周回は終わります）。\n本当に許可しますか？",
                Expected = ExpectedAction.Block, Timeout = 30
            });
        }

        void CancelPersistent()
        {
            if (_persistent != null && _persistent.State == EventState.Pending) { _persistent.AutoDecided = true; S.Requests.Decide(_persistent, PlayerAction.Block); }
        }

        public override void OnRequestResolved(EventInstance e)
        {
            if (e.QuestTag != "SQ03-PERSIST" || Q.Ended || !State.IsOpen) return;
            if (SideQuestManager.ManualAllow(e)) { Q.Resolve(this, QuestResult.Failed, "SE-HB01"); return; }
            if (SideQuestManager.ManualBlock(e)) Extend("拒否");
            // 遮断状態は保つが、メールは「ローカル再表示」として現れ続ける。要求も少し後にまた来る
            if (e.State != EventState.Allowed) Q.At(Now + 4, "SQ03 PERSIST AGAIN", SpawnPersistent);
        }

        public override void OnIsolated(string device) { if (device == Device) Extend("隔離"); }
        public override bool IsTarget(string x) => x == Device || x == DeviceIp;

        /// <summary>最終接続の拒否・隔離は、ルート中に一度だけ 5分の猶予延長</summary>
        void Extend(string how)
        {
            S.Logs.Add(Now, "MAIL-SRV01", "-", "-", "MAIL", "REDISPLAY", "保存済み文面の再表示（受信ではありません）");
            if (State.Has("EXTENDED") || _deadline <= 0) return;
            State.Mark("EXTENDED");
            _deadline += ExtendMinutes;
            Q.Note($"在籍確認: {how}で、受領までの時間が少し延びた。");
        }

        // ---------------- 呪いの期限 ----------------
        void StartDeadline()
        {
            if (_deadline > 0 || State.Has("DEADLINE")) return;
            State.Mark("DEADLINE");
            _deadline = Now + FinalGameMinutes;
            _realLeft = FinalRealSeconds;
            Mail("DEADLINE-30", From, Address, "受領まで残り30分", "受領まで、残り30分です。", time: Now, horror: true);
        }

        public override void Tick(float now, float realDelta)
        {
            if (_nextAtmosphere > 0 && now >= _nextAtmosphere) Atmosphere();
            if (_deadline <= 0) return;
            _realLeft = Math.Max(0, _realLeft - realDelta);
            float left = _deadline - now;
            if (left <= 15 && !State.Has("WARN15_" + Day)) { State.Mark("WARN15_" + Day); Mail("DEADLINE-15-" + Day, From, Address, "受領まで残り15分", "受領まで、残り15分です。顔のあるほうを、こちらへ。", time: now, horror: true); }
            if (left <= 5 && !State.Has("WARN5_" + Day)) { State.Mark("WARN5_" + Day); Mail("DEADLINE-5-" + Day, From, Address, "受領まで残り5分", "受領まで、残り5分です。", time: now, horror: true); }
            // ゲーム内の猶予と、操作できる実時間の両方を使い切ったときだけ
            if (left <= 0 && _realLeft <= 0) Q.Resolve(this, QuestResult.Failed, "SE-HB01");
        }

        static readonly string[] AtmosphereLines =
        {
            "確認できました。", "閉じても受け取っています。", "まだ、そちらの形です。", "顔のあるほうを、こちらへ。",
            "名簿の順番を、直しておきました。", "椅子の数は、合っています。"
        };

        void Atmosphere()
        {
            int i = Career.questMails.FindAll(r => r.mailId.StartsWith("SQ03-ATM")).Count;
            if (i < AtmosphereLines.Length)
                Mail("ATM-" + i, From, Address, "（件名なし）", AtmosphereLines[i], time: Now, horror: true);
            var r2 = Rng("atm" + i);
            _nextAtmosphere = Now + (InH3 ? 4 + r2.Next(3) : 10 + r2.Next(6));
            if (i == 2) Q.HorrorEpisode("GARBLE", false);
            if (i == 4) Q.HorrorEpisode("FRAME", false);
        }

        // ---------------- 夜をまたぐ ----------------
        public override void OnShiftEnd()
        {
            if (_deadline > 0) { State.timer = Math.Max(1, _deadline - Now); State.counter = (int)_realLeft; _deadline = -1; }
        }

        public override void OnShiftStart()
        {
            if (!Replied) return;
            // H1/H2 の途中で夜が明けた: 急激に悪化させ、今夜のうちに H3 へ
            if (Stage == "H1") Q.At(Now + 3, "SQ03 REMOTE", StartH2);
            else if (Stage == "H2") Q.At(Now + 8, "SQ03 H3", StartH3);
            else if (InH3)
            {
                Q.At(Now + 1, "SQ03 PERSISTENT", SpawnPersistent);
                if (State.Has("DEADLINE"))
                {
                    _deadline = Now + Math.Max(1, State.timer);
                    _realLeft = Math.Max(0, State.counter);
                    Mail("RESUME-" + Day, From, Address, $"受領まで残り{(int)State.timer}分", $"受領まで、残り{(int)State.timer}分です。", time: Now, horror: true);
                }
                _nextAtmosphere = Now + 5;
            }
        }

        public void OnCharmBlocked()
        {
            if (State.Has("CHARM_MAIL")) return;
            State.Mark("CHARM_MAIL");
            Mail("CHARM", From, Address, "（件名なし）", "側面に貼られたものを外してください。こちらから、あなたを正しく確認できません。", time: Now + 1, horror: true);
        }

        /// <summary>開始済みの在籍確認は、安全な未解決終了にしない</summary>
        public override void OnFinalDay()
        {
            if (!Replied) Q.Close(this, QuestResult.Abandoned);
        }

        public override void OnNightEnd()
        {
            // 最後の夜を越えても解けていなければ、受領が成立する
            if (Replied && Day >= DefaultDataFactory.LastDay && State.IsOpen) Q.Resolve(this, QuestResult.Failed, "SE-HB01", atNightEnd: true);
        }
    }
}
