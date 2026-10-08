using NightShift.Core;
using NightShift.Data;

namespace NightShift.Systems.SideQuests
{
    /// <summary>
    /// SQ04「ギャンブル」（GAMBLING_FX_SPEC.md §2）。社員からの私信 → 返信で NIGHT CLUB が店頭に並ぶ → 最初の手動ベットで Active。
    /// 勝率は表示どおり（赤 48% / 黒 48% / ハウス 4%）。勝ちを保証するものは売らない。
    /// ギャンブル王: 賭けた夜 3夜以上・純利益 ¥20,000 以上・借入残 0 → 朝の決済後に「夜勤を辞める」を選ぶと SE-G01。
    /// </summary>
    public class Sq04Route : QuestRoute
    {
        public Sq04Route(SideQuestManager q) : base(q) { }
        public override string Id => "SQ04";
        public override string Title => "ギャンブル";
        public override int EarliestDay => 9;
        public const string InvitedFlag = "SQ04_INVITED";

        public override bool Ready() => Unlocked(Features.Store) && Sender != null;

        EmployeeData Sender
        {
            get
            {
                if (State.employeeId == null) State.employeeId = Pick("sender", e => e.age < 40 && e.department != "情報システム部")?.employeeId;
                return Employee(State.employeeId);
            }
        }
        string From => $"{Sender.department} {Sender.fullName}";
        string Family => Sender.fullName.Split(' ')[0];

        /// <summary>同僚の口調（問い合わせと同じ Voice）で文面を選ぶ。順番: 丁寧・標準・くだけた・不安げ・ぶっきらぼう・ベテラン・明るい</summary>
        string P(params string[] byPersona) => byPersona[(int)Voice.Of(Sender)];
        string Address => Sender.employeeId.Replace("-", "").ToLowerInvariant() + "@" + MailManager.CompanyDomain;
        LeisureAccount A => Q.Leisure;
        LeisureState L => Career.leisure;

        public bool Qualified => A.GambleNights >= LeisureAccount.NightsGoal && A.GambleNet >= LeisureAccount.ProfitGoal && L.debt == 0;
        public override string Memo() => $"賭けた夜 {A.GambleNights} / 純利益 {Yen(A.GambleNet)}";

        public override void Offer()
        {
            State.stage = "INVITE";
            // 社員が、回ってきたチェーンメールを転送してくる
            Mail("INVITE", From, Address, "Fw: " + ChainMail.Subject(ChainMail.Style.Club, "夜だけの会員制クラブ"),
                ChainMail.Forward(P(
                    $"夜分に失礼いたします。{Sender.department}の{Sender.fullName}です。\n機材のご購入で、何かとご負担が多いのではと思いご連絡いたしました。\n先日回ってきたものですが、私は一晩で日当ほどになりました。よろしければご案内をお送りいたします。",
                    $"お疲れさまです。{Sender.fullName}です。\n給料だけで機材を揃えるのは大変ですよね。\n回ってきたメールを転送します。昨夜は一回で日当分になりました。興味があれば案内を送ります。",
                    $"おつかれー！{Family}です\n機材高くない？給料じゃ無理っしょ笑\nこれ回ってきたんだけど、昨日一発で日当分いったｗ　気になったら返信して〜",
                    $"あの、夜分にすみません…{Sender.fullName}です。\n余計なお世話だったらごめんなさい。機材、自腹って聞いて…\n回ってきたメールなんですけど、私これで昨夜、日当分くらい勝てちゃって…。もし興味があったら、案内送ります…",
                    $"{Family}。\n回ってきた。昨夜これで日当分。\n興味があれば返信を。",
                    $"どうも、{Sender.fullName}です。\n若い頃はこういうのに手を出すなと言われたもんですが…機材代、馬鹿にならんでしょう。\n昨夜は一回で日当分になりましてね。転送しておきます。気が向いたら返事をください。",
                    $"お疲れさまです〜！{Sender.fullName}です♪\nいいもの見つけちゃいました！昨夜これで日当分当たったんですよ〜！\n転送しますね。興味あったら返信くださいっ☆"),
                    ChainMail.Body(ChainMail.Style.Club, "夜だけの会員制クラブ NIGHT CLUB",
                        "残業中のアナタ！　退屈していませんか？？\n" +
                        "赤か黒、選ぶだけ！！　当たれば賭け金が<b>2倍</b>になって戻ってきます☆\n\n" +
                        "◇当たる確率◇　赤 48%　黒 48%　ハウス 4%\n" +
                        "（会員規約より。勝ちを保証するものではありません）",
                        "会員番号は、紹介者からの返信でお知らせします。")),
                replies: new[] { "案内を受け取る", "興味ありません" });
        }

        public override void OnReply(QuestMailRecord r, int index, string text)
        {
            if (r.mailId != "SQ04-INVITE") return;
            if (index == 0)
            {
                State.Mark("REPLIED");
                Career.SetFlag(InvitedFlag);
                Mail("GUIDE", From, Address, "Re: Fw: " + ChainMail.Subject(ChainMail.Style.Club, "夜だけの会員制クラブ"),
                    P("ご返信ありがとうございます。社内購買に「NIGHT CLUB」（¥1,500）が掲載されているはずです。\n購入後、遊興口座へ入金してからご利用ください。給与の財布とは別の口座でございます。",
                      "案内です。社内購買に「NIGHT CLUB」（¥1,500）が出ているはずです。\n買ったら遊興口座にお金を入れてから賭けます。給料の財布とは別です。",
                      "おけ！購買に「NIGHT CLUB」ってのあるはず（¥1,500）\n買ったら遊興口座ってとこに入金してから賭けるやつ。給料とは別財布ね",
                      "返信ありがとうございます…！購買に「NIGHT CLUB」（¥1,500）があると思います。\n買ったあと、遊興口座にお金を入れてから…です。給料のお財布とは別なので、気をつけてください…",
                      "購買に「NIGHT CLUB」（¥1,500）。\n遊興口座に入金してから賭ける。給料とは別。",
                      "社内購買に「NIGHT CLUB」というのがあります（¥1,500）。\n遊興口座に入れてから賭ける仕組みです。給料とは分けておきなさい。それが長続きのコツです。",
                      "やった〜！購買に「NIGHT CLUB」（¥1,500）があるはずです♪\n遊興口座にお金を入れてから遊ぶんですよ〜。お給料とは別なので安心です☆") + "\n\n" +
                    ChainMail.Body(ChainMail.Style.Club, "NIGHT CLUB ご利用の手引き",
                        "→ 1回 ゲーム内2分で結果発表！！\n" +
                        "→ 1晩 6回まで遊べます♪\n" +
                        "→ 賭け金 ¥500／¥1,000／¥3,000／¥5,000\n\n" +
                        "◇当たる確率◇　赤 48%　黒 48%　ハウス 4%"), time: Now + 1);
                Q.Note("NIGHT CLUB が社内購買に並びました（業務開始前に購入できます）。");
            }
            else
            {
                Q.Close(this, QuestResult.Declined, "案内を断りました。");
            }
        }

        public override bool? OnReported(QuestMailRecord r) => null;   // 私信の報告は誤報にも正報にもしない

        bool MailedTonight => State.Has("MAIL_" + Day);
        void ProgressMail(string id, string subject, string body, string actionKey = null, string actionLabel = null)
        {
            if (MailedTonight) return;
            State.Mark("MAIL_" + Day);
            Mail(id, From, Address, subject, body, time: Now + 1, actionKey: actionKey, actionLabel: actionLabel);
        }

        public void OnBetPlaced()
        {
            if (State.status == QuestStatus.Offered) { State.status = QuestStatus.Active; State.stage = "PLAYING"; }
        }

        public void OnBetResolved(bool win)
        {
            if (win && !State.Has("FIRST_WIN"))
            {
                State.Mark("FIRST_WIN");
                ProgressMail("WIN1",
                    P("おめでとうございます", "当たりましたね", "当たったじゃんｗ", "当たったんですね…！", "当たり", "まずは一勝", "おめでとうございます〜！！"),
                    P("当たられたとのこと、おめでとうございます。\nただ、次も当たるとは限りませんので、どうかご無理なさいませんよう。",
                      "見てましたよ、当たりましたね。\nこの調子です。……まあ、次も当たるとは限りませんけど。",
                      "え、当たってんじゃん！やるね〜\nまぁ次はわかんないけどｗ",
                      "当たったって見て、なんだかほっとしました…\nでも、次も当たるとは限らないので…ほどほどに、ですよ…",
                      "当たったな。\n次は分からん。",
                      "一勝おめでとう。\n勝った日ほど、やめ時を決めておくもんです。",
                      "当たったんですね〜！すごいすごい♪\n次も当たるといいですね！……って、そう簡単じゃないですけど☆"));
            }
            else if (!win && !State.Has("FIRST_LOSS"))
            {
                State.Mark("FIRST_LOSS");
                ProgressMail("LOSS1",
                    P("残念でしたね", "最初はそんなものです", "どんまい", "大丈夫ですか…？", "外れ", "負けも勉強", "ドンマイです〜！"),
                    P("今回は残念でございました。\n取り返そうとなさるのが一番よくないと聞きます。どうかお気をつけて。",
                      "外れましたか。最初はそんなものです。\n取り返そうとして続けるのが、一番よくないらしいですよ。",
                      "外れたかー、どんまい！\n取り返そうとするとヤバいらしいから気をつけてｗ",
                      "外れちゃったんですね…私が誘ったせいで、すみません…\n取り返そうとすると、もっと減るって聞くので…無理しないでください…",
                      "外れたか。\n追うな。",
                      "負けましたか。誰でも通る道です。\n負けを取り返そうとした者から消えていく。覚えておきなさい。",
                      "外れちゃいましたか〜、ドンマイです！\n取り返そうとしないのがコツらしいですよ♪"));
                LoanOffer();
            }
            if (L.gamblePlays >= LeisureAccount.VipAfterPlays && !State.Has("VIP_MAIL"))
            {
                State.Mark("VIP_MAIL");
                Mail("VIP", "NIGHT CLUB 会員事務局", "members@nightclub.example", ChainMail.Subject(ChainMail.Style.Club, "おめでとうございます！VIP席のご案内"),
                    ChainMail.Body(ChainMail.Style.Club, "選ばれたアナタだけのVIP席",
                        "アナタ様、いつも格別のご愛顧を賜り、誠にありがとうございます。\n" +
                        "5回以上お遊びくださった会員様にだけ、特別なお席をご用意いたしました。\n\n" +
                        "VIP席では、1回 <b>¥10,000</b> までお賭けいただけます。\n" +
                        "※ 当たる確率は通常のお席と同じでございます。"),
                    time: Now + 1, actionKey: "VIP", actionLabel: "VIP席を開く");
            }
            if (Qualified && !State.Has("KING_MAIL"))
            {
                State.Mark("KING_MAIL");
                Mail("KING", "NIGHT CLUB 会員事務局", "members@nightclub.example", ChainMail.Subject(ChainMail.Style.Club, "次の席を用意しました"),
                    ChainMail.Body(ChainMail.Style.Club, "ギャンブル王への招待状",
                        "アナタ様のために、次のお席をご用意いたしました。\n" +
                        "夜勤をお辞めになるのでしたら、朝のご精算のあとにお知らせくださいませ。\n" +
                        "支配人一同、お待ち申し上げております。\n\n" +
                        "※ これまでの成績は、今後の勝ちを保証するものではございません。"), time: Now + 1);
            }
        }

        /// <summary>最初の負けの後に一度だけ（SQ04/05 共通）</summary>
        public void LoanOffer()
        {
            if (L.loanOffered || Day + LeisureAccount.LoanDays > DefaultDataFactory.LastDay) return;
            L.loanOffered = true;
            Mail("LOAN", "ナイトファイナンス", "info@night-finance.example", ChainMail.Subject(ChainMail.Style.Loan, "即日OK！追加資金のご案内"),
                ChainMail.Body(ChainMail.Style.Loan, "審査なし！　即日ご融資！！",
                    "お客様各位\n\n" +
                    "「あと少しで取り返せるのに…」そんなお客様の夜を、ナイトファイナンスが応援します！！\n" +
                    "★ ¥5,000／¥10,000／¥20,000 から選べる！\n" +
                    "★ 利息なし！　返済は3日後の朝でOK！\n\n" +
                    "<size=11>【ご注意】</size>\n" +
                    "<b>返済日に口座と財布から返せない場合は「破産」となり、勤務は終了します。</b>\n" +
                    "購入した機材は返済に充てられません。"),
                time: Now + 2, actionKey: "LOAN", actionLabel: "借入を申し込む");
        }

        public override void OnAction(QuestMailRecord r, string key)
        {
            if (key == "VIP") { A.OpenVip(); Q.Note("VIP席を開きました。"); }
            else if (key == "LOAN") Q.RequestUi("LOAN");
        }

        public override bool Touched() => State.Has("REPLIED");

        public override void OnFinalDay()
        {
            if (L.debt > 0) return;   // 借入が残っている間は、返済日まで締めない
            if (State.status == QuestStatus.Offered) Q.Close(this, QuestResult.Abandoned);
        }

        public override MorningChoice MorningChoice()
        {
            if (!Qualified || L.kingDeclinedDay >= LastPlayedDay) return null;
            var c = new MorningChoice
            {
                QuestId = Id, Key = "KING", Title = "VIP招待",
                Text = $"賭けた夜: {A.GambleNights} 夜 / ギャンブル純利益: {Yen(A.GambleNet)} / 借入残: なし\n\n" +
                       "次の席が用意されています。夜勤を辞めますか？\n（辞めると、この周回はここで終わります）",
                Accept = "夜勤を辞める", Decline = "夜勤を続ける"
            };
            if (Q.Route<Sq03Route>()?.State.IsOpen == true) c.Blocked = "画面の不調が続いていて、今は決められない。（在籍確認の件が片付くまで保留）";
            return c;
        }

        int LastPlayedDay => L.gambleNights.Count == 0 ? 0 : L.gambleNights[L.gambleNights.Count - 1];

        public override void OnMorningChoice(MorningChoice c, bool accept)
        {
            if (accept && c.Blocked == null && Qualified) Q.Resolve(this, QuestResult.Completed, "SE-G01");
            else L.kingDeclinedDay = Day;
        }
    }
}
