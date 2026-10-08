using NightShift.Core;
using NightShift.Data;

namespace NightShift.Systems.SideQuests
{
    /// <summary>
    /// SQ05「FX」（GAMBLING_FX_SPEC.md §3）。「夜間相場通信」の案内 → 返信で MIDNIGHT FX が店頭に並ぶ → 最初の手動注文で Active。
    /// 架空通貨ペア NEX/CRD。相場は周回の種と日付で決まり、注文の向きを見て書き換えない。見通しメールは方向を約束しない。
    /// 急変夜は初回注文から2作業夜以降に予告。FX戦士: 取引した夜 3夜以上・純利益 ¥20,000 以上・借入残 0・急変夜を勝ち越し → 朝に「専業になる」で SE-F01。
    /// </summary>
    public class Sq05Route : QuestRoute
    {
        public Sq05Route(SideQuestManager q) : base(q) { }
        public override string Id => "SQ05";
        public override string Title => "FX";
        public override int EarliestDay => 13;
        public const string InvitedFlag = "SQ05_INVITED";
        const string From = "夜間相場通信";
        const string Address = "news@midnight-market.example";

        public override bool Ready() => Unlocked(Features.Store);

        LeisureAccount A => Q.Leisure;
        LeisureState L => Career.leisure;

        public bool Qualified => A.FxNights >= LeisureAccount.NightsGoal && A.FxNet >= LeisureAccount.ProfitGoal && L.debt == 0 && L.spikeCleared;
        public override string Memo() => $"取引した夜 {A.FxNights} / 純利益 {Yen(A.FxNet)}{(L.spikeCleared ? " / 急変夜 勝ち越し" : "")}";

        public override void Offer()
        {
            State.stage = "INVITE";
            Mail("INVITE", From, Address, ChainMail.Subject(ChainMail.Style.Market, "あなたが働いている間にも、相場は動いています"),
                ChainMail.Body(ChainMail.Style.Market, "夜間相場通信　創刊号",
                    "↑↑↑ 上がる？？　↓↓↓ 下がる？？\n" +
                    "読者の皆様、こんばんは。編集部です。\n" +
                    "街が眠っても、架空通貨 NEX/CRD は眠りません。夜勤のアナタこそ、夜の相場の主役です！！\n\n" +
                    "当通信では、今夜の架空経済指標の発表予定と、取引端末のご案内をお届けします。\n" +
                    "買いからでも売りからでも入れるのが、この市場の妙味です。\n\n" +
                    "※ 当通信の見通しは、上昇・下落を約束するものではありません。",
                    "配信の停止は、このメールへの返信で受け付けます。"),
                replies: new[] { "案内を受け取る", "配信を停止する" });
        }

        public override void OnReply(QuestMailRecord r, int index, string text)
        {
            if (r.mailId != "SQ05-INVITE") return;
            if (index == 0)
            {
                State.Mark("REPLIED");
                Career.SetFlag(InvitedFlag);
                Mail("GUIDE", From, Address, ChainMail.Subject(ChainMail.Style.Market, "取引端末のご案内"),
                    ChainMail.Body(ChainMail.Style.Market, "MIDNIGHT FX　ご案内",
                        "ご購読ありがとうございます。編集部がおすすめする取引端末のご案内です。\n" +
                        "社内購買に「MIDNIGHT FX」（¥2,000）が並んでいます！！\n\n" +
                        "→ 遊興口座に入金し、NEX/CRD を買い・売りで取引\n" +
                        "→ 証拠金 ¥500〜¥5,000・倍率 1／5／10 倍\n" +
                        "→ 残りの証拠金が 20% を切ると強制決済（損失は証拠金まで）\n" +
                        "→ 終業時には自動で決済されます"), time: Now + 1);
                Q.Note("MIDNIGHT FX が社内購買に並びました（業務開始前に購入できます）。");
            }
            else Q.Close(this, QuestResult.Declined, "配信を停止しました。");
        }

        public override bool? OnReported(QuestMailRecord r) => null;
        public override bool Touched() => State.Has("REPLIED");

        public override void OnShiftStart()
        {
            if (!Career.Owns(Items.FxTerminal)) return;
            // 今夜の見通し（発表時刻と不確実性だけを伝える）
            var rel = A.Releases;
            A.Price(0);
            string times = string.Join("、", rel.ConvertAll(t => T(t)));
            Mail("OUTLOOK-" + Day, From, Address, ChainMail.Subject(ChainMail.Style.Market, A.SpikeNight ? "【急変夜】今夜の見通し" : "今夜の見通し"),
                ChainMail.Body(ChainMail.Style.Market, $"夜間相場通信　DAY{Day} 号",
                    "【本日のポイント】\n" +
                    $"・架空経済指標の発表予定: <b>{times}</b>\n" +
                    $"・注目度: {(A.SpikeNight ? "★★★★★" : "★★☆☆☆")}\n\n" +
                    "【編集部の見立て】\n" +
                    (A.SpikeNight ? "<b>今夜は大きな値動きが予想されます！！</b>\n上か下か――それは編集部にも分かりません。" : "発表の前後は値が飛びやすい局面。\n方向は、神のみぞ知る、といったところでしょう。")));
        }

        public void OnOrderPlaced()
        {
            if (State.status == QuestStatus.Offered) { State.status = QuestStatus.Active; State.stage = "TRADING"; }
        }

        public void OnPositionClosed(long pnl)
        {
            if (pnl < 0 && !State.Has("FIRST_LOSS"))
            {
                State.Mark("FIRST_LOSS");
                Q.Route<Sq04Route>()?.LoanOffer();
            }
            if (Qualified && !State.Has("WARRIOR_MAIL"))
            {
                State.Mark("WARRIOR_MAIL");
                Mail("WARRIOR", From, Address, ChainMail.Subject(ChainMail.Style.Market, "夜明けまで生き残った方へ"),
                    ChainMail.Body(ChainMail.Style.Market, "祝・急変夜 突破！！",
                        "編集部より、心からの賛辞を。\n" +
                        "急変の夜を越えて、利益を残しました。\n" +
                        "専業になるなら、朝の精算のあとでお選びください。\n\n" +
                        "※ これまでの成績は、今後の利益を保証するものではありません。"), time: Now + 1);
            }
        }

        public override void OnAction(QuestMailRecord r, string key)
        {
            if (key == "LOAN") Q.RequestUi("LOAN");
        }

        public override void OnNightEnd()
        {
            // 急変夜の結果
            if (L.spikeDay == Day)
            {
                L.spikeCleared = L.spikeCleared || (L.spikeClosed > 0 && L.spikePnl > 0);
                if (!L.spikeCleared) { L.spikeDay = 0; State.Mark("SPIKE_RETRY_" + Day); }
                L.spikeClosed = 0; L.spikePnl = 0;
                return;   // 同じ夜に再予告しない
            }
            // 初回注文から2作業夜以降に、翌夜の急変を予告する
            if (L.spikeDay == 0 && !L.spikeCleared && L.fxFirstDay > 0 && Day >= L.fxFirstDay + 1 && Day + 1 <= SideQuestManager.LastTaskDay
                && !State.Has("SPIKE_RETRY_" + Day) && !Q.TaskDayReserved(Day + 1, this))
            {
                L.spikeDay = Day + 1;
                Mail("SPIKE-" + (Day + 1), From, Address, ChainMail.Subject(ChainMail.Style.Market, "【予告】明日の夜は急変が予想されます"),
                    ChainMail.Body(ChainMail.Style.Market, "緊急特報！！　明日の夜は急変",
                        "当局筋の話では――といっても架空の話ですが。\n" +
                        $"DAY{Day + 1} の夜、架空経済指標の大きな発表があります。\n" +
                        "値動きの方向は分かりません。\n" +
                        "取引される場合は、証拠金と倍率にご注意ください。"),
                    nextMorning: true);
            }
        }

        public override void OnFinalDay()
        {
            if (L.debt > 0) return;
            if (State.status == QuestStatus.Offered) Q.Close(this, QuestResult.Abandoned);
        }

        int LastTradedDay => L.fxNights.Count == 0 ? 0 : L.fxNights[L.fxNights.Count - 1];

        public override MorningChoice MorningChoice()
        {
            if (!Qualified || L.warriorDeclinedDay >= LastTradedDay) return null;
            var c = new MorningChoice
            {
                QuestId = Id, Key = "WARRIOR", Title = "夜明けまで生き残った方へ",
                Text = $"取引した夜: {A.FxNights} 夜 / FX純利益: {Yen(A.FxNet)} / 借入残: なし / 急変夜: 勝ち越し\n\n" +
                       "専業になりますか？\n（専業になると、この周回はここで終わります）",
                Accept = "専業になる", Decline = "夜勤を続ける"
            };
            if (Q.Route<Sq03Route>()?.State.IsOpen == true) c.Blocked = "画面の不調が続いていて、今は決められない。（在籍確認の件が片付くまで保留）";
            return c;
        }

        public override void OnMorningChoice(MorningChoice c, bool accept)
        {
            if (accept && c.Blocked == null && Qualified) Q.Resolve(this, QuestResult.Completed, "SE-F01");
            else L.warriorDeclinedDay = Day;
        }
    }
}
