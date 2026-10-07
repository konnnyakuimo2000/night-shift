using System;
using System.Collections.Generic;
using NightShift.Core;
using NightShift.Data;

namespace NightShift.Systems.SideQuests
{
    /// <summary>
    /// SQ04/05 共通の「遊興口座」と、NIGHT CLUB（賭け）・MIDNIGHT FX（架空通貨の売買）の計算（GAMBLING_FX_SPEC.md）。
    /// すべて画面の中の架空のサービス・通貨・価格。給与の財布とは別の口座で、入出金は財布の台帳に記録する。
    /// 抽選は周回の種＋ルート＋受け付けた順番で決まり、やり直しても同じ結果になる。相場はプレイヤーの注文を見て書き換えない。
    /// </summary>
    public class LeisureAccount
    {
        public static readonly int[] Stakes = { 500, 1000, 3000, 5000 };
        public const int VipStake = 10000, NightlyLimit = 6, VipAfterPlays = 5, BetMinutes = 2;
        public static readonly int[] Loans = { 5000, 10000, 20000 };
        public const int LoanDays = 3;
        public static readonly int[] Leverages = { 1, 5, 10 };
        public const decimal FeeRate = 0.002m;
        public const int GambleToolPrice = 1500, FxToolPrice = 2000;
        public const int ProfitGoal = 20000, NightsGoal = 3;

        readonly SideQuestManager _q;
        public LeisureAccount(SideQuestManager q) { _q = q; }
        LeisureState L => CareerRecord.Current.leisure;
        ShiftSession S => _q.S;
        int Day => _q.Day;
        float Now => S.Time.CurrentMinute;

        public event Action Changed;
        void Touch() { Changed?.Invoke(); }

        public int Free => L.free;
        public int Locked => L.locked;
        public int Debt => L.debt;
        public int LoanDue => L.loanDue;
        public IReadOnlyList<string> History => L.history;

        /// <summary>ギャンブル純利益 = 払戻し − 賭け金 − ツール代（給与・入金・借入は含めない）</summary>
        public int GambleNet => L.gamblePaid - L.gambleStaked - (CareerRecord.Current.Owns(Items.GambleTerminal) ? GambleToolPrice : 0);
        /// <summary>FX純利益 = 確定損益 − 手数料 − ツール代（含み益は含めない）</summary>
        public int FxNet => (int)(L.fxPnl - L.fxFees) - (CareerRecord.Current.Owns(Items.FxTerminal) ? FxToolPrice : 0);
        public int GambleNights => L.gambleNights.Count;
        public int FxNights => L.fxNights.Count;

        void Log(string s) => L.Log($"DAY{Day} {TimeManager.Format(Now)}  {s}");
        string NextTx(string kind) => $"LEI-{kind}-{++L.txSeq}";

        // ================= 入出金 =================
        public string Deposit(int amount)
        {
            var w = _q.Wallet;
            if (amount <= 0) return "金額を選んでください。";
            if (w == null || w.Balance < amount) return "財布の所持金が足りません。";
            w.AddEntry(NextTx("DEP"), Day, "LEISURE", "遊興口座へ入金", -amount);
            L.free += amount; L.deposited += amount;
            Log($"入金 {Yen(amount)}");
            Touch();
            return $"{Yen(amount)} を入金しました。";
        }

        public string Withdraw(int amount)
        {
            var w = _q.Wallet;
            if (amount <= 0 || amount > L.free) return "出金できるのは空き資金だけです。";
            w?.AddEntry(NextTx("WD"), Day, "LEISURE", "遊興口座から出金", amount);
            L.free -= amount; L.withdrawn += amount;
            Log($"出金 {Yen(amount)}");
            Touch();
            return $"{Yen(amount)} を財布へ出金しました。";
        }

        // ================= 借入 =================
        /// <summary>借入できない理由（null = できる）</summary>
        public string LoanBlocked(int amount)
        {
            if (Array.IndexOf(Loans, amount) < 0) return "借入額を選んでください。";
            if (L.debt > 0) return "返済が終わっていない借入があります（追加借入・借換えはできません）。";
            if (Day + LoanDays > DefaultDataFactory.LastDay) return "返済日が契約期間を越えるため、借入できません。";
            if (!InUse) return "口座を利用していません。";
            return null;
        }

        public bool InUse
        {
            get
            {
                var c = CareerRecord.Current;
                return c.Owns(Items.GambleTerminal) || c.Owns(Items.FxTerminal);
            }
        }

        public string Borrow(int amount)
        {
            var why = LoanBlocked(amount);
            if (why != null) return why;
            L.debt = amount; L.loanDay = Day; L.loanDue = Day + LoanDays;
            L.free += amount;
            Log($"借入 {Yen(amount)}（返済日 DAY{L.loanDue} の朝）");
            Touch();
            return $"{Yen(amount)} を借り入れました。返済日は DAY{L.loanDue} の朝です。";
        }

        /// <summary>早期返済（口座の空き資金から先に、足りなければ財布から）</summary>
        public string Repay(int amount)
        {
            if (L.debt <= 0) return "借入はありません。";
            amount = Math.Min(amount, L.debt);
            int fromFree = Math.Min(amount, L.free);
            int fromWallet = amount - fromFree;
            var w = _q.Wallet;
            if (fromWallet > 0 && (w == null || w.Balance < fromWallet)) return "返済に使えるお金が足りません。";
            L.free -= fromFree;
            if (fromWallet > 0) w.AddEntry(NextTx("REPAY"), Day, "LOAN", "借入の返済", -fromWallet);
            L.debt -= amount;
            Log($"返済 {Yen(amount)}（残り {Yen(L.debt)}）");
            if (L.debt == 0) { L.loanDue = 0; L.loanDay = 0; }
            Touch();
            return L.debt == 0 ? "完済しました。" : $"{Yen(amount)} 返済しました。残り {Yen(L.debt)}。";
        }

        // ================= NIGHT CLUB（赤／黒） =================
        public class Bet { public int Color, Amount, Ordinal; public float ResolveAt; }
        public Bet PendingBet { get; private set; }
        public int PlaysTonight { get; private set; }
        public int StakedTonight { get; private set; }
        public int PaidTonight { get; private set; }
        public int LossTonight => StakedTonight - PaidTonight;
        public static string ColorLabel(int c) => c == 0 ? "赤" : c == 1 ? "黒" : "ハウス";

        public int MaxStake => L.vip ? VipStake : Stakes[Stakes.Length - 1];
        public bool Vip => L.vip;
        public void OpenVip() { L.vip = true; Log("VIP席を開きました（上限 ¥10,000・確率は同じ）"); Touch(); }
        public int StopLoss { get => L.gambleStopLoss; set { L.gambleStopLoss = Math.Max(0, value); Touch(); } }
        public int NightCap { get => L.gambleNightCap; set { L.gambleNightCap = Math.Max(0, value); Touch(); } }

        /// <summary>賭けられない理由（null = 賭けられる）</summary>
        public string BetBlocked(int amount)
        {
            if (!CareerRecord.Current.Owns(Items.GambleTerminal)) return "NIGHT CLUB を持っていません。";
            if (S.Phase != ShiftPhase.Running) return "受付時間外です（業務時間中のみ）。";
            if (PendingBet != null) return "結果待ちの賭けがあります（同時に1件まで）。";
            if (PlaysTonight >= NightlyLimit) return $"今夜の上限（{NightlyLimit}回）に達しました。";
            if (S.Day.endMinute - Now < BetMinutes) return "終業まで2分を切ったため、受付を終了しました。";
            if (amount != VipStake && Array.IndexOf(Stakes, amount) < 0) return "賭け金を選んでください。";
            if (amount > MaxStake) return "VIP席でのみ選べる賭け金です。";
            if (amount > L.free) return "口座の空き資金が足りません。";
            if (L.gambleNightCap > 0 && StakedTonight + amount > L.gambleNightCap) return $"今夜の支出上限（{Yen(L.gambleNightCap)}）を超えます。";
            if (L.gambleStopLoss > 0 && LossTonight >= L.gambleStopLoss) return $"今夜の損失が指定額（{Yen(L.gambleStopLoss)}）に達したため、新規の賭けを止めています。";
            return null;
        }

        /// <summary>この順番の賭けの結果（0 = 赤, 1 = 黒, 2 = ハウス）。賭けの色や額では変わらない</summary>
        public static int Outcome(int seed, int ordinal)
        {
            var r = new Random(unchecked(seed * 31 + 0x5104 + ordinal * 7919));
            int v = r.Next(100);
            return v < 48 ? 0 : v < 96 ? 1 : 2;
        }

        public string PlaceBet(int color, int amount)
        {
            var why = BetBlocked(amount);
            if (why != null) return why;
            var b = new Bet { Color = color, Amount = amount, Ordinal = ++L.betOrdinal, ResolveAt = Now + BetMinutes };
            L.free -= amount; L.locked += amount; L.gambleStaked += amount;
            StakedTonight += amount; PlaysTonight++; L.gamblePlays++;
            if (!L.gambleNights.Contains(Day)) L.gambleNights.Add(Day);
            PendingBet = b;
            Log($"賭け #{b.Ordinal} {ColorLabel(color)} に {Yen(amount)}");
            _q.Route<Sq04Route>()?.OnBetPlaced();
            Touch();
            return $"{ColorLabel(color)} に {Yen(amount)} を賭けました。結果は {TimeManager.Format(b.ResolveAt)} に出ます。";
        }

        void ResolveBet()
        {
            var b = PendingBet;
            if (b == null) return;
            PendingBet = null;
            int result = Outcome(CareerRecord.Current.seed, b.Ordinal);
            bool win = result == b.Color;
            int pay = win ? b.Amount * 2 : 0;
            L.locked -= b.Amount;
            L.free += pay; L.gamblePaid += pay; PaidTonight += pay;
            Log($"結果 #{b.Ordinal}: {ColorLabel(result)} — {(win ? "当たり +" + Yen(b.Amount) : "外れ −" + Yen(b.Amount))}");
            S.Notifications.Notify(NotificationType.Info, "NIGHT CLUB", $"結果: {ColorLabel(result)}（{(win ? "当たり" : "外れ")}）", Now);
            if (win) L.gambleFirstWin = true; else L.gambleFirstLoss = true;
            _q.Route<Sq04Route>()?.OnBetResolved(win);
            Touch();
        }

        // ================= MIDNIGHT FX（NEX/CRD） =================
        public class Position
        {
            public int Side, Margin, Leverage, Fee, Ordinal;
            public decimal Entry, Notional, TakeProfit, StopLoss;
            public float Opened;
        }
        public Position Open { get; private set; }
        public int OrdersTonight { get; private set; }
        public decimal Alert { get; set; }
        decimal _alertFrom;
        double[] _prices;
        public readonly List<float> Releases = new List<float>();
        public bool SpikeNight => L.spikeDay == Day;
        public long PnlTonight { get; private set; }

        /// <summary>今夜の相場（1分ごと）。周回の種と日付だけで決まる</summary>
        void BuildPrices()
        {
            if (_prices != null) return;
            int end = (int)Math.Ceiling(S.Day.endMinute) + 2;
            _prices = new double[end + 1];
            var r = new Random(unchecked(CareerRecord.Current.seed * 17 + 0xF0F0 + Day * 104729));
            bool spike = SpikeNight;
            double p = 100.0 + (r.NextDouble() - 0.5) * 4.0;
            double sigma = spike ? 0.0015 : 0.0006;
            Releases.Clear();
            Releases.Add(60 + r.Next(60));
            Releases.Add(180 + r.Next(90));
            if (spike) Releases.Add(130 + r.Next(40));
            Releases.Sort();
            for (int m = 0; m <= end; m++)
            {
                // 正規分布に近い揺れ
                double z = 0;
                for (int k = 0; k < 6; k++) z += r.NextDouble();
                z -= 3.0;
                p *= 1.0 + sigma * z;
                foreach (var t in Releases)
                    if ((int)t == m) p *= 1.0 + (r.Next(2) == 0 ? -1 : 1) * (spike ? 0.012 + r.NextDouble() * 0.012 : 0.003 + r.NextDouble() * 0.003);
                _prices[m] = Math.Round(p, 3);
            }
        }

        public decimal Price(float minute)
        {
            BuildPrices();
            int m = Math.Max(0, Math.Min(_prices.Length - 2, (int)Math.Floor(minute)));
            double f = Math.Max(0, Math.Min(1, minute - m));
            return Math.Round((decimal)(_prices[m] + (_prices[m + 1] - _prices[m]) * f), 3);
        }

        public decimal PriceNow => Price(Now);

        /// <summary>チャート用: 開始から今までの価格</summary>
        public List<decimal> Series(int points)
        {
            var list = new List<decimal>();
            float start = S.Day.startMinute, now = Math.Max(start + 1, Now);
            for (int i = 0; i < points; i++) list.Add(Price(start + (now - start) * i / (points - 1)));
            return list;
        }

        public static decimal Fee(int margin, int leverage) => Math.Ceiling(margin * leverage * FeeRate);

        public string OrderBlocked(int margin, int leverage)
        {
            if (!CareerRecord.Current.Owns(Items.FxTerminal)) return "MIDNIGHT FX を持っていません。";
            if (S.Phase != ShiftPhase.Running || Now >= S.Day.endMinute) return "閉場しています（業務時間中のみ）。";
            if (Open != null) return "建玉は同時に1つまでです。";
            if (OrdersTonight >= NightlyLimit) return $"今夜の新規注文の上限（{NightlyLimit}回）に達しました。";
            if (Array.IndexOf(Stakes, margin) < 0) return "証拠金を選んでください。";
            if (Array.IndexOf(Leverages, leverage) < 0) return "倍率を選んでください。";
            if (margin + (int)Fee(margin, leverage) > L.free) return "口座の空き資金が足りません（証拠金＋手数料）。";
            return null;
        }

        public string PlaceOrder(int side, int margin, int leverage, decimal takeProfit = 0, decimal stopLoss = 0)
        {
            var why = OrderBlocked(margin, leverage);
            if (why != null) return why;
            int fee = (int)Fee(margin, leverage);
            var pos = new Position
            {
                Side = side >= 0 ? 1 : -1, Margin = margin, Leverage = leverage, Fee = fee, Ordinal = ++L.fxOrdinal,
                Entry = PriceNow, Notional = margin * leverage, TakeProfit = takeProfit, StopLoss = stopLoss, Opened = Now
            };
            L.free -= margin + fee; L.locked += margin; L.fxFees += fee;
            OrdersTonight++;
            if (!L.fxNights.Contains(Day)) L.fxNights.Add(Day);
            if (L.fxFirstDay == 0) L.fxFirstDay = Day;
            Open = pos;
            Log($"新規 #{pos.Ordinal} {(pos.Side > 0 ? "買い" : "売り")} 証拠金 {Yen(margin)} ×{leverage} @ {pos.Entry:0.000}（手数料 {Yen(fee)}）");
            _q.Route<Sq05Route>()?.OnOrderPlaced();
            Touch();
            return $"{(pos.Side > 0 ? "買い" : "売り")}で建てました（{pos.Entry:0.000}）。";
        }

        /// <summary>含み損益（円・小数のまま）。損失は証拠金まで</summary>
        public static decimal Unrealized(Position p, decimal price)
        {
            decimal pnl = p.Notional * (price - p.Entry) / p.Entry * p.Side;
            return Math.Max(pnl, -p.Margin);
        }

        public decimal UnrealizedNow => Open == null ? 0 : Unrealized(Open, PriceNow);

        public string CloseNow(string why = "手動決済") => Open == null ? "建玉はありません。" : ClosePosition(PriceNow, why);

        string ClosePosition(decimal price, string why)
        {
            var p = Open;
            Open = null;
            long pnl = (long)Math.Round(Unrealized(p, price), MidpointRounding.AwayFromZero);
            L.locked -= p.Margin;
            L.free += (int)Math.Max(0, p.Margin + pnl);
            L.fxPnl += pnl;
            PnlTonight += pnl;
            if (SpikeNight) { L.spikePnl += pnl; L.spikeClosed++; }
            Log($"決済 #{p.Ordinal}（{why}）@ {price:0.000}  損益 {(pnl >= 0 ? "+" : "−")}{Yen((int)Math.Abs(pnl))}");
            S.Notifications.Notify(NotificationType.Info, "MIDNIGHT FX", $"{why}: 損益 {(pnl >= 0 ? "+" : "−")}{Yen((int)Math.Abs(pnl))}", Now);
            _q.Route<Sq05Route>()?.OnPositionClosed(pnl);
            Touch();
            return $"決済しました（{why}）。損益 {(pnl >= 0 ? "+" : "−")}{Yen((int)Math.Abs(pnl))}";
        }

        void TickFx()
        {
            if (Open == null && Alert <= 0) return;
            decimal price = PriceNow;
            if (Alert > 0)
            {
                if (_alertFrom == 0) _alertFrom = price;
                if ((_alertFrom < Alert && price >= Alert) || (_alertFrom > Alert && price <= Alert))
                {
                    S.Notifications.Notify(NotificationType.Info, "MIDNIGHT FX", $"価格が {Alert:0.000} に到達しました", Now);
                    Alert = 0; _alertFrom = 0;
                }
            }
            if (Open == null) return;
            var p = Open;
            decimal pnl = p.Notional * (price - p.Entry) / p.Entry * p.Side;
            // 残りの証拠金が 20% 以下で強制決済（損失は証拠金まで）
            if (p.Margin + pnl <= p.Margin * 0.2m) { ClosePosition(price, "強制決済"); return; }
            if (p.TakeProfit > 0 && (p.Side > 0 ? price >= p.TakeProfit : price <= p.TakeProfit)) { ClosePosition(price, "利益確定（自動）"); return; }
            if (p.StopLoss > 0 && (p.Side > 0 ? price <= p.StopLoss : price >= p.StopLoss)) ClosePosition(price, "損失確定（自動）");
        }

        // ================= 夜の進行 =================
        public void Tick(float now)
        {
            if (PendingBet != null && now >= PendingBet.ResolveAt) ResolveBet();
            TickFx();
        }

        /// <summary>終業: 結果待ちの賭けを確定し、建玉はその時点の価格で自動決済（夜をまたぐ建玉は作らない）</summary>
        public void OnShiftEnd()
        {
            if (PendingBet != null) ResolveBet();
            if (Open != null) ClosePosition(PriceNow, "終業時の自動決済");
        }

        /// <summary>夜の終わり（給与・個別入金の後）: 返済日の朝なら返済。足りなければ破産</summary>
        public void OnNightEnd()
        {
            OnShiftEnd();
            if (L.debt > 0 && L.loanDue > 0 && Day + 1 >= L.loanDue)
            {
                int debt = L.debt;
                int fromFree = Math.Min(debt, L.free);
                L.free -= fromFree;
                int rest = debt - fromFree;
                var w = _q.Wallet;
                int fromWallet = w == null ? 0 : Math.Max(0, Math.Min(rest, w.Balance));
                if (fromWallet > 0) w.AddEntry(NextTx("DUE"), Day, "LOAN", "借入の返済（返済日）", -fromWallet);
                L.debt = rest - fromWallet;
                Log($"返済日: {Yen(fromFree + fromWallet)} を返済（不足 {Yen(L.debt)}）");
                if (L.debt > 0)
                {
                    QuestRoute q = _q.Route<Sq04Route>();
                    if (q == null || q.State.status == QuestStatus.Locked) q = _q.Route<Sq05Route>();
                    if (q != null) { q.State.status = QuestStatus.Active; _q.Resolve(q, QuestResult.Failed, "SE-M01", atNightEnd: true); }
                }
                else
                {
                    L.loanDue = 0; L.loanDay = 0;
                    _q.Note($"借入を返済しました（{Yen(debt)}）。");
                }
            }
        }

        public static string Yen(int v) => OperatorWallet.Yen(v);
    }
}
