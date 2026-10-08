using System.Linq;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using NightShift.Systems.SideQuests;
using NightShift.UI.Shift;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>サブクエスト（SQ03 在籍確認・SQ04 ギャンブル・SQ05 FX）と共通基盤（SUBQUEST_SPEC.md §8・GAMBLING_FX_SPEC.md §7）</summary>
    public class SideQuestTests : NightShiftTest
    {
        OperatorWallet _wallet;

        [SetUp]
        public void TestWallet()
        {
            _wallet = new OperatorWallet();
            SideQuestManager.WalletProvider = () => _wallet;
        }

        static ShiftSession Start(int day)
        {
            var s = TestData.Session(day);
            s.StartShift();
            return s;
        }

        static void Advance(ShiftSession s, float to, float realDelta = 1f)
        {
            for (float t = s.Time.CurrentMinute; t <= to; t += 0.5f)
            {
                s.Time.JumpTo(t);
                s.Quests.Tick(t, realDelta);
            }
        }

        static MailMessage Mail(ShiftSession s, string id) => s.Mail.Find(id);

        // ================= 共通 =================
        [Test]
        public void Offers_OnePerNight_AndQuietEndAfterTwoIgnoredNights()
        {
            var s = Start(20);
            int offered = CareerRecord.Current.quests.Count(q => q.status == QuestStatus.Offered);
            Assert.AreEqual(1, offered, "一夜の新しい案内は1本");
            var q0 = CareerRecord.Current.quests.First(q => q.status == QuestStatus.Offered);
            int before = _wallet.Balance;
            s.Quests.OnNightEnd();
            Assert.AreEqual(QuestStatus.Offered, q0.status);
            var s2 = Start(21);
            s2.Quests.OnNightEnd();
            Assert.AreEqual(QuestStatus.Resolved, q0.status, "反応しないまま2夜で静かに終了");
            Assert.AreEqual(QuestResult.Abandoned, q0.result);
            Assert.AreEqual(before, _wallet.Balance, "未読放置で財布は変わらない");
        }

        [Test]
        public void Offers_AtMostTwoOpen()
        {
            var s = Start(20);
            s.Quests.DebugOffer("SQ03");
            Assert.GreaterOrEqual(s.Quests.OpenCount(), 2);
            var s2 = Start(21);
            Assert.IsNotNull(s2.Quests.OfferBlockedReason(), "進行中が2本なら新しい案内は保留");
        }

        [Test]
        public void QuestMails_PersistAcrossNights_WithReplies()
        {
            var s = Start(20);
            s.Quests.DebugOffer("SQ05");
            var m = Mail(s, "SQ05-INVITE");
            Assert.IsNotNull(m);
            Assert.IsTrue(s.Mail.SendReply(m, 1, m.Replies[1]));
            Assert.IsFalse(s.Mail.SendReply(m, 0, m.Replies[0]), "選択式の返信は一度だけ");
            var s2 = Start(21);
            var m2 = Mail(s2, "SQ05-INVITE");
            Assert.IsNotNull(m2, "翌夜も受信トレイに残る");
            Assert.AreEqual(1, m2.RepliedIndex, "送った返信も残る");
        }

        // ================= SQ04 ギャンブル =================
        [Test]
        public void Gamble_OutcomeDistribution_IsAsDisplayed_AndDeterministic()
        {
            // スロット: 表示している確率どおりに当たり、払戻し率は 100% 未満（長く遊ぶほど減る）
            const int N = 40000;
            int wins = 0, sevens = 0;
            long paid = 0;
            for (int i = 1; i <= N; i++)
            {
                var spin = LeisureAccount.Outcome(42, i);
                bool line = spin.Reels[0] == spin.Reels[1] && spin.Reels[1] == spin.Reels[2];
                Assert.AreEqual(spin.Multiplier > 0, line, "3つ揃ったときだけ当たり");
                if (spin.Multiplier > 0) wins++;
                if (spin.Multiplier == 20) sevens++;
                paid += spin.Multiplier;
            }
            Assert.That(wins / (float)N * 100f, Is.InRange(LeisureAccount.WinChance - 1f, LeisureAccount.WinChance + 1f));
            Assert.That(sevens / (float)N, Is.InRange(0.007f, 0.013f));
            Assert.That(paid / (float)N, Is.InRange(0.9f, 0.99f), "払戻し率は 100% 未満");
            Assert.Less(LeisureAccount.ReturnRate, 100f);
            var a1 = LeisureAccount.Outcome(42, 7); var a2 = LeisureAccount.Outcome(42, 7);
            Assert.AreEqual(a1.Multiplier, a2.Multiplier, "同じ順番なら同じ結果");
            CollectionAssert.AreEqual(a1.Reels, a2.Reels);
        }

        [Test]
        public void Gamble_InviteUnlocksStore_BetPaysOutOnce()
        {
            var s = Start(20);
            s.Quests.DebugOffer("SQ04");
            var item = TestData.Db.FindItem(Items.GambleTerminal);
            Assert.IsFalse(StoreView.OnSale(item, 20), "返信前は店頭に並ばない");
            var m = Mail(s, "SQ04-INVITE");
            s.Mail.SendReply(m, 0, m.Replies[0]);
            Assert.IsTrue(StoreView.OnSale(item, 20), "返信で専用商品が並ぶ");
            Assert.AreEqual(QuestStatus.Offered, CareerRecord.Current.Quest("SQ04").status, "返信・購入だけでは始まらない");

            CareerRecord.Current.AddItem(Items.GambleTerminal);
            _wallet.DebugAddMoney(20000);
            var a = s.Quests.Leisure;
            a.Deposit(10000);
            Assert.AreEqual(10000, _wallet.Balance);
            a.PlaceBet(1000);
            Assert.AreEqual(QuestStatus.Active, CareerRecord.Current.Quest("SQ04").status, "最初の手動ベットで Active");
            Assert.IsNotNull(a.BetBlocked(1000), "結果待ちの間は次を受け付けない");
            Assert.AreEqual(9000, a.Free);
            Assert.AreEqual(1000, a.Locked);
            Advance(s, s.Time.CurrentMinute + 2.5f);
            Assert.IsNull(a.PendingBet);
            Assert.AreEqual(0, a.Locked);
            int mul = LeisureAccount.Outcome(42, 1).Multiplier;
            Assert.AreEqual(9000 + 1000 * mul, a.Free);
            Assert.AreEqual(1000 * mul - 1000 - LeisureAccount.GambleToolPrice, a.GambleNet, "純利益はツール代込み");
            Advance(s, s.Time.CurrentMinute + 3f);
            Assert.AreEqual(9000 + 1000 * mul, a.Free, "同じ回転は二度払い戻さない");
        }

        [Test]
        public void Gamble_NightlyLimit_AndVipStake()
        {
            var s = Start(20);
            CareerRecord.Current.AddItem(Items.GambleTerminal);
            _wallet.DebugAddMoney(50000);
            var a = s.Quests.Leisure;
            a.Deposit(40000);
            Assert.IsNotNull(a.BetBlocked(LeisureAccount.VipStake), "VIP席を開くまで ¥10,000 は選べない");
            for (int i = 0; i < LeisureAccount.NightlyLimit; i++)
            {
                Assert.IsNull(a.BetBlocked(500), $"{i + 1} 回目");
                a.PlaceBet(500);
                Advance(s, s.Time.CurrentMinute + 2.5f);
            }
            Assert.IsNotNull(a.BetBlocked(500), "一夜6回まで");
        }

        // ================= SQ05 FX =================
        [Test]
        public void Fx_LossIsLimitedToMargin_AndFeeIsRoundedUp()
        {
            var p = new LeisureAccount.Position { Side = 1, Margin = 5000, Leverage = 10, Entry = 100m, Notional = 50000 };
            Assert.AreEqual(-5000m, LeisureAccount.Unrealized(p, 80m), "急変で飛び越えても損失は証拠金まで");
            Assert.AreEqual(1000m, LeisureAccount.Unrealized(p, 102m));
            p.Side = -1;
            Assert.AreEqual(-1000m, LeisureAccount.Unrealized(p, 102m), "売りは逆向き");
            Assert.AreEqual(1m, LeisureAccount.Fee(500, 1), "想定元本 ¥500 の 0.2% = ¥1（切り上げ）");
            Assert.AreEqual(100m, LeisureAccount.Fee(5000, 10));
        }

        [Test]
        public void Fx_ShiftEndClosesPosition_AndMoneyIsConserved()
        {
            var s = Start(20);
            CareerRecord.Current.AddItem(Items.FxTerminal);
            _wallet.DebugAddMoney(10000);
            var a = s.Quests.Leisure;
            a.Deposit(10000);
            Assert.IsNull(a.OrderBlocked(1000, 5));
            a.PlaceOrder(1, 1000, 5);
            Assert.IsNotNull(a.Open);
            Assert.IsNotNull(a.OrderBlocked(1000, 5), "建玉は同時に1つまで");
            Advance(s, 40);
            a.OnShiftEnd();
            Assert.IsNull(a.Open, "終業時に自動決済");
            var L = CareerRecord.Current.leisure;
            Assert.AreEqual(0, a.Locked);
            Assert.AreEqual((int)(10000 - L.fxFees + L.fxPnl), a.Free, "口座の資金 = 入金 − 手数料 + 確定損益");
            Assert.AreEqual(a.FxNet, (int)(L.fxPnl - L.fxFees) - LeisureAccount.FxToolPrice);
        }

        [Test]
        public void Fx_PricesAreDeterministicPerNight()
        {
            var s1 = Start(20);
            var s2 = Start(20);
            Assert.AreEqual(s1.Quests.Leisure.Price(100), s2.Quests.Leisure.Price(100));
            var s3 = Start(21);
            Assert.AreNotEqual(s1.Quests.Leisure.Price(100), s3.Quests.Leisure.Price(100), "夜が変われば相場も変わる");
        }

        // ================= 借入・破産 =================
        [Test]
        public void Loan_RepaidOnDueMorning_OrBankruptcy()
        {
            var s = Start(20);
            CareerRecord.Current.AddItem(Items.GambleTerminal);
            CareerRecord.Current.Quest("SQ04").status = QuestStatus.Active;
            var a = s.Quests.Leisure;
            StringAssert.Contains("借り入れました", a.Borrow(10000));
            Assert.AreEqual(10000, a.Debt);
            Assert.IsNotNull(a.LoanBlocked(5000), "追加借入はできない");
            a.Withdraw(10000);   // 財布へ移しておく
            Assert.AreEqual(10000, _wallet.Balance);
            s.Quests.OnNightEnd();
            Assert.AreEqual(10000, a.Debt, "返済日の前は引き落とさない");
            var s2 = Start(22);   // 返済日 DAY23 の朝 = DAY22 の夜の終わり
            _wallet.TrySpend(6000);
            s2.Quests.OnNightEnd();
            Assert.AreEqual("SE-M01", s2.Quests.PendingTerminal, "財布と口座から返しきれなければ破産");
        }

        [Test]
        public void Loan_FullyRepaid_NoBankruptcy_AndZeroBalanceAloneIsNotBankruptcy()
        {
            var s = Start(20);
            CareerRecord.Current.AddItem(Items.FxTerminal);
            s.Quests.OnNightEnd();
            Assert.IsNull(s.Quests.PendingTerminal, "残高0だけでは破産しない");
            var a = s.Quests.Leisure;
            a.Borrow(5000);
            var s2 = Start(22);
            s2.Quests.OnNightEnd();
            Assert.IsNull(s2.Quests.PendingTerminal);
            Assert.AreEqual(0, CareerRecord.Current.leisure.debt);
        }

        // ================= SQ03 在籍確認 =================
        [Test]
        public void Haunt_ReadingOrReportingDoesNotStart_ReplyStartsOnce()
        {
            var s = Start(20);
            s.Quests.DebugOffer("SQ03");
            var m = Mail(s, "SQ03-OFFER");
            s.Quests.OnMailShown(m);
            Assert.IsNull(s.Quests.OnMailReported(m), "報告しても誤報にしない");
            Assert.AreEqual(QuestStatus.Offered, CareerRecord.Current.Quest("SQ03").status, "閲覧・報告だけでは始まらない");
            s.Mail.SendReply(m, 2, m.Replies[2]);
            Assert.AreEqual(QuestStatus.Active, CareerRecord.Current.Quest("SQ03").status, "どの返信でも開始");
            Assert.AreEqual("H1", CareerRecord.Current.Quest("SQ03").stage);
        }

        static Sq03Route BeginH3(ShiftSession s)
        {
            s.Quests.DebugOffer("SQ03");
            var m = s.Mail.Find("SQ03-OFFER");
            s.Mail.SendReply(m, 0, m.Replies[0]);
            Advance(s, s.Time.CurrentMinute + 30);   // リモートを開かなくても、向こうから開いて H3 へ
            return s.Quests.Route<Sq03Route>();
        }

        [Test]
        public void Haunt_CluesArriveBCA_AndInitialsSpellAnswer()
        {
            var s = Start(20);
            var r = BeginH3(s);
            Assert.IsTrue(r.InH3);
            var a = s.Mail.Find("SQ03-H3-A");
            var b = s.Mail.Find("SQ03-H3-B");
            var c = s.Mail.Find("SQ03-H3-C");
            Assert.IsNotNull(a); Assert.IsNotNull(b); Assert.IsNotNull(c);
            Assert.Less(b.ReceivedAt, c.ReceivedAt);
            Assert.Less(c.ReceivedAt, a.ReceivedAt, "到着順は B → C → A");
            Assert.IsTrue(a.Pinned && b.Pinned && c.Pinned, "手がかりは固定表示");
            string initials = string.Concat(Sq03Route.ClueA.Concat(Sq03Route.ClueB).Concat(Sq03Route.ClueC).Select(l => l[0]));
            Assert.AreEqual(Sq03Route.Answer, initials, "元の送信時刻順（A→B→C）に各行の頭を読むと答え");
            string received = string.Concat(Sq03Route.ClueB.Concat(Sq03Route.ClueC).Concat(Sq03Route.ClueA).Select(l => l[0]));
            Assert.AreNotEqual(Sq03Route.Answer, received, "受信順では正しい文にならない");
            Assert.IsTrue(r.DeadlineRunning, "3通と最初のヒントが揃ってから最終猶予が始まる");
        }

        [Test]
        public void Haunt_AnswerNormalization()
        {
            Assert.AreEqual(Sq03Route.Answer, Sq03Route.Normalize("  ここにはいない。 "));
            Assert.AreEqual(Sq03Route.Answer, Sq03Route.Normalize("ここ　には　いない"));
            Assert.AreNotEqual(Sq03Route.Answer, Sq03Route.Normalize("ここにいる"));
        }

        [Test]
        public void Haunt_WrongThenCorrectAnswer_IsGood()
        {
            var s = Start(20);
            BeginH3(s);
            var ans = s.Mail.Find("SQ03-H3-ANSWER");
            Assert.IsTrue(ans.FreeReply);
            s.Mail.SendReply(ans, 0, "わからない");
            Advance(s, s.Time.CurrentMinute + 1);
            Assert.IsNotNull(s.Mail.Find("SQ03-WRONG-1"), "誤答は一通返して再入力可");
            Assert.IsTrue(CareerRecord.Current.Quest("SQ03").IsOpen);
            s.Mail.SendReply(ans, 0, "ここにはいない。");
            var st = CareerRecord.Current.Quest("SQ03");
            Assert.AreEqual("SE-HG01", st.endingId);
            Assert.IsFalse(s.Quests.Ended, "GOOD は本編継続");
            Assert.Contains("SE-HG01", CareerRecord.Current.subEndingsSeen);
        }

        [Test]
        public void Haunt_AllowingPersistentConnection_IsBad()
        {
            var s = Start(20);
            BeginH3(s);
            var req = s.Requests.Pending.FirstOrDefault(e => e.QuestTag == "SQ03-PERSIST");
            Assert.IsNotNull(req, "本人未確認の永続接続要求");
            Assert.IsNotNull(req.QuestConfirm, "許可の前に確認を出す");
            string ended = null;
            s.Quests.TerminalEnding += id => ended = id;
            s.Requests.Decide(req, PlayerAction.Allow);
            Assert.AreEqual("SE-HB01", ended);
            Assert.IsTrue(s.Quests.Ended);
        }

        [Test]
        public void Haunt_DeadlineNeedsBothGameAndRealTime_DenyExtendsOnce()
        {
            var s = Start(20);
            var r = BeginH3(s);
            var req = s.Requests.Pending.First(e => e.QuestTag == "SQ03-PERSIST");
            float before = r.GameRemaining;
            s.Requests.Decide(req, PlayerAction.Block);
            Assert.AreEqual(before + Sq03Route.ExtendMinutes, r.GameRemaining, 0.01f, "拒否で一度だけ5分延長");
            Advance(s, s.Time.CurrentMinute + 40, 0f);
            Assert.IsFalse(s.Quests.Ended, "操作できる実時間が残っている間は受領しない");
            Advance(s, s.Time.CurrentMinute + 1, 400f);
            Assert.IsTrue(s.Quests.Ended);
            Assert.AreEqual("SE-HB01", CareerRecord.Current.Quest("SQ03").endingId);
        }

        [Test]
        public void Haunt_TrueEnding_ByExplicitForward()
        {
            var s = Start(20);
            var r = BeginH3(s);
            Assert.IsNotNull(s.Mail.Find("SQ03-H3-TRUE"));
            r.ConfirmTrue();
            Assert.AreEqual("SE-HT01", CareerRecord.Current.Quest("SQ03").endingId);
            Assert.IsTrue(s.Quests.Ended);
        }

        [Test]
        public void Charm_BlocksOneEpisode_AndTearsNextMorning()
        {
            var s = Start(20);
            var ch = CareerRecord.Current.charm;
            ch.postedDay = 20;
            Assert.IsFalse(s.Quests.HorrorEpisode("RED", false), "札が1エピソードを防ぐ");
            Assert.IsTrue(ch.consumed);
            Assert.IsTrue(s.Quests.HorrorEpisode("RED", false), "効果は一夜1回");
            s.Quests.OnNightEnd();
            Assert.IsTrue(ch.torn, "翌朝に破れる");
            Assert.AreEqual(0, ch.postedDay);
        }

        [Test]
        public void Charm_OnSaleOnlyAfterFirstMalfunction_NextNight()
        {
            var item = TestData.Db.FindItem(Items.Charm);
            Assert.IsFalse(StoreView.OnSale(item, 20));
            var s = Start(20);
            BeginH3(s);
            Assert.IsFalse(StoreView.OnSale(item, 20), "不調を経験した夜はまだ並ばない");
            Assert.IsTrue(StoreView.OnSale(item, 21), "翌夜から並ぶ");
        }

        [Test]
        public void SeenSubEndings_SurviveNightRetry()
        {
            var c = CareerRecord.Current;
            string snap = c.Snapshot();
            c.SeeSubEnding("SE-HG01");
            CareerRecord.Restore(snap);
            Assert.Contains("SE-HG01", CareerRecord.Current.subEndingsSeen, "やり直しで図鑑は消えない");
        }
    }
}
