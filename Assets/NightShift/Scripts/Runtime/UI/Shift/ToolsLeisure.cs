using System;
using System.Collections.Generic;
using NightShift.Core;
using NightShift.Systems.SideQuests;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// SQ04/05 の専用ツール（GAMBLING_FX_SPEC.md）。すべて画面の中の架空のサービス・通貨・価格。
    /// 仕事の許可ボタンとは別のウィンドウ。結果待ちの間も業務の時間は進む。毎回の勝ちで派手な全画面演出は出さない。
    /// </summary>
    public static class LeisureUi
    {
        public static readonly Color Club = new Color(0.36f, 0.06f, 0.12f);
        public static readonly Color Fx = new Color(0.02f, 0.26f, 0.22f);
        public static string Yen(int v) => OperatorWallet.Yen(v);
        public static string Signed(long v) => (v >= 0 ? "+" : "−") + OperatorWallet.Yen((int)Math.Abs(v));

        /// <summary>口座の欄（財布・空き資金・拘束・借入を分けて表示。入出金ボタン）</summary>
        public class AccountPanel
        {
            Text _wallet, _free, _locked, _debt;
            readonly ShiftDesktop _d;
            readonly Action<string> _status;

            public AccountPanel(Transform box, ShiftDesktop d, Color accent, Action<string> status)
            {
                _d = d; _status = status;
                _wallet = ToolStyle.Field(box, "財布", 0, 0, 200, 70);
                _free = ToolStyle.Field(box, "空き資金", 200, 0, 200, 70);
                _locked = ToolStyle.Field(box, "拘束", 0, 22, 200, 70);
                _debt = ToolStyle.Field(box, "借入残", 200, 22, 200, 70);
                // 入出金は3つだけ（細かい額は「借入・返済」の口座画面で）
                ToolStyle.Button(box, "入金 ¥5,000", 0, 48, 120, 24, () => _status(_d.S.Quests.Leisure.Deposit(5000)), accent, false);
                ToolStyle.Button(box, "全額出金", 126, 48, 100, 24, () => _status(_d.S.Quests.Leisure.Withdraw(_d.S.Quests.Leisure.Free)), accent, false);
                ToolStyle.Button(box, "借入・返済…", 232, 48, 110, 24, () => LeisureAccountView.Open(_d), accent, false);
            }

            public void Refresh()
            {
                var a = _d.S.Quests.Leisure;
                _wallet.text = Yen((_d.S.Quests.Wallet ?? _d.Game.Wallet).Balance);
                _free.text = Yen(a.Free);
                _locked.text = Yen(a.Locked);
                _debt.text = a.Debt > 0 ? $"{Yen(a.Debt)}（DAY{a.LoanDue} の朝に返済）" : "なし";
                _debt.color = a.Debt > 0 ? ToolStyle.Red : ToolStyle.Ink;
            }
        }

        public static Text History(Transform box)
        {
            var t = ToolStyle.ScrollText(box, 11, true);
            return t;
        }

        public static string HistoryText(LeisureAccount a, int lines = 40)
        {
            var h = a.History;
            var sb = new System.Text.StringBuilder();
            for (int i = h.Count - 1; i >= 0 && i >= h.Count - lines; i--) sb.AppendLine(h[i]);
            return sb.Length == 0 ? "（まだ履歴はありません）" : sb.ToString();
        }
    }

    // ============================================================ NIGHT CLUB（スロット）
    /// <summary>
    /// 簡単なスロット。賭け金を選んで［回す］だけ。3つ揃えば配当表の倍率で払い戻す。
    /// 当たる確率と払戻し率は画面に出したまま（長く遊ぶほど減る）。結果は回した順番で決まり、やり直しても変わらない。
    /// </summary>
    public class NightClubTool : ToolWindow
    {
        LeisureUi.AccountPanel _account;
        readonly List<(int amount, FlatButton b)> _stakes = new List<(int, FlatButton)>();
        int _stake = 500;
        readonly Image[] _reels = new Image[3];
        readonly Image[] _reelBg = new Image[3];
        Image _payline;
        Text _result, _stats, _caps, _hist;
        FlatButton _spin, _capBtn, _stopBtn;
        static readonly int[] CapSteps = { 0, 3000, 5000, 10000, 20000 };
        System.Random _blur = new System.Random();

        RectTransform _more;   // 上限設定と履歴（「詳細」で開く）
        protected override float RefreshInterval => 0.08f;
        LeisureAccount A => S.Quests.Leisure;

        protected override void Build(RectTransform p)
        {
            var accent = LeisureUi.Club;
            float y = ToolStyle.Header(p, "NIGHT CLUB", $"3つ揃えば当たり！　当たる確率 {LeisureAccount.WinChance:0}%・払戻し率 {LeisureAccount.ReturnRate:0}%（長く遊ぶほど減ります）", "globe", accent);
            _account = new LeisureUi.AccountPanel(ToolStyle.Box(p, 0, 1, y, 90, "遊興口座（給料の財布とは別）"), D, accent, s => W.SetStatus(0, s));
            y += 98;

            // リール（金の縁の筐体に、3つの窓）
            var reel = ToolStyle.Box(p, 0, 1, y, 166);
            var cab = UIFactory.Solid(reel, new Color(0.65f, 0.42f, 0.05f), "Cabinet");
            cab.rectTransform.Place(8, 0, 548, 120);
            var cabIn = UIFactory.Solid(cab.transform, new Color(0.25f, 0.02f, 0.06f), "Velvet");
            cabIn.rectTransform.Stretch(4, 4, 4, 4);
            for (int i = 0; i < 3; i++)
            {
                var bg = UIFactory.Image(cabIn.transform, SlotArt.ReelBackground, Color.white, "Reel");
                bg.rectTransform.Place(14 + i * 176, 6, 160, 100);
                _reelBg[i] = bg;
                _reels[i] = UIFactory.Image(bg.transform, SlotArt.Symbol(0), Color.white, "Symbol");
                _reels[i].rectTransform.anchorMin = _reels[i].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _reels[i].rectTransform.sizeDelta = new Vector2(84, 84);
                _reels[i].preserveAspect = true;
            }
            // 当たりの線（中央の横線）
            _payline = UIFactory.Solid(cabIn.transform, new Color(0.9f, 0.1f, 0.1f, 0.55f), "Payline");
            _payline.rectTransform.anchorMin = new Vector2(0, 0.5f); _payline.rectTransform.anchorMax = new Vector2(1, 0.5f);
            _payline.rectTransform.sizeDelta = new Vector2(0, 2);
            _payline.raycastTarget = false;
            _result = ToolStyle.Text(reel, "", 0, 124, 560, 30, 20, accent, TextAnchor.MiddleCenter, true);
            y += 174;

            // 賭け金と［回す］
            var bet = ToolStyle.Box(p, 0, 1, y, 64, "賭け金を選んで、回す");
            float x = 0;
            foreach (int v in new[] { 500, 1000, 3000, 5000, LeisureAccount.VipStake })
            {
                int amount = v;
                var b = ToolStyle.Button(bet, LeisureUi.Yen(v), x, 4, 66, 34, () => { _stake = amount; Refresh(); }, accent, false);
                _stakes.Add((v, b));
                x += 70;
            }
            _spin = ToolStyle.Button(bet, "回す！", 0, 0, 170, 40, () => { W.SetStatus(0, A.PlaceBet(_stake)); Refresh(); }, new Color(0.75f, 0.05f, 0.05f));
            _spin.Rect.AnchorTopRight(0, 2, 170, 40);
            _spin.Label.fontSize = 18;
            y += 72;

            // 配当表
            var pay = ToolStyle.Box(p, 0, 0.5f, y, 112, "配当表");
            int row = 0;
            foreach (var (symbol, mul, per) in LeisureAccount.Paytable)
            {
                for (int k = 0; k < 3; k++)
                {
                    var ic = UIFactory.Image(pay, SlotArt.Symbol(symbol), Color.white, "Pay");
                    ic.rectTransform.Place(k * 22, row * 22, 20, 20);
                }
                ToolStyle.Text(pay, $"×{mul}　（{per / 10f:0.0}%）", 74, row * 22, 190, 20, 13, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
                row++;
            }
            _more = UIFactory.Rect("More", p).Stretch();
            _more.gameObject.SetActive(false);
            var lim = ToolStyle.Box(_more, 0.5f, 1, y, 112, "自分で決める上限");
            _capBtn = ToolStyle.Button(lim, "", 0, 0, 250, 24, () => A.NightCap = Next(CapSteps, A.NightCap), accent, false);
            _stopBtn = ToolStyle.Button(lim, "", 0, 28, 250, 24, () => A.StopLoss = Next(CapSteps, A.StopLoss), accent, false);
            _caps = ToolStyle.Text(lim, "", 0, 56, 260, 30, 11, ToolStyle.Muted, TextAnchor.UpperLeft);
            y += 120;

            var stats = ToolStyle.Box(p, 0, 1, y, 36);
            _stats = ToolStyle.Text(stats, "", 0, 0, 440, 18, 12, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            var toggle = ToolStyle.Button(stats, "詳細 ▼", 0, 0, 100, 20, null, accent, false);
            toggle.Rect.AnchorTopRight(0, -1, 100, 20);
            toggle.OnClick = () => { _more.gameObject.SetActive(!_more.gameObject.activeSelf); toggle.SetText(_more.gameObject.activeSelf ? "詳細 ▲" : "詳細 ▼"); };
            y += 44;
            _hist = LeisureUi.History(ToolStyle.Box(_more, 0, 1, y, 0, "履歴（過去の結果は、次の結果を予測しません）"));
        }

        static int Next(int[] steps, int cur) { int i = Array.IndexOf(steps, cur); return steps[(i + 1) % steps.Length]; }

        void ShowReel(int i, int symbol, bool spinning)
        {
            _reels[i].sprite = SlotArt.Symbol(symbol);
            // 回転中は絵柄が流れてぼやける（上下に揺らし、色を落とす）
            _reels[i].color = spinning ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
            _reels[i].rectTransform.anchoredPosition = spinning ? new Vector2(0, _blur.Next(-24, 25)) : Vector2.zero;
            _reelBg[i].color = spinning ? new Color(0.9f, 0.9f, 0.9f) : Color.white;
        }

        protected override void Refresh()
        {
            if (_result == null || S?.Quests == null) return;
            _account.Refresh();
            foreach (var (amount, b) in _stakes)
            {
                b.gameObject.SetActive(amount != LeisureAccount.VipStake || A.Vip);
                b.SetLatched(amount == _stake);
            }
            string blocked = A.BetBlocked(_stake);
            _spin.Interactable = blocked == null;
            var pb = A.PendingBet;
            if (pb != null)
            {
                // 左から順に止まる（回し終わるのはゲーム内2分後）
                float k = Mathf.Clamp01((S.Time.CurrentMinute - pb.Placed) / LeisureAccount.BetMinutes);
                var final = LeisureAccount.Outcome(Core.CareerRecord.Current.seed, pb.Ordinal);
                for (int i = 0; i < 3; i++)
                {
                    bool stopped = k >= 0.45f + 0.25f * i;
                    ShowReel(i, stopped ? final.Reels[i] : _blur.Next(LeisureAccount.Symbols.Length), !stopped);
                }
                _result.text = "回転中…";
                _result.color = LeisureUi.Club;
            }
            else if (A.LastSpin != null)
            {
                for (int i = 0; i < 3; i++) ShowReel(i, A.LastSpin.Reels[i], false);
                bool win = A.LastSpin.Multiplier > 0;
                _result.text = win ? (A.LastSpin.Multiplier >= 20 ? $"★ JACKPOT ★　{A.LastSpin.Multiplier}倍！　+{LeisureUi.Yen(A.LastPayout)}" : $"{A.LastSpin.Multiplier}倍！　+{LeisureUi.Yen(A.LastPayout)}") : "外れ";
                _result.color = win ? new Color(0.75f, 0.5f, 0f) : ToolStyle.Muted;
                if (_payline) _payline.color = win ? new Color(1f, 0.8f, 0.1f, 0.9f) : new Color(0.9f, 0.1f, 0.1f, 0.55f);
            }
            else
            {
                for (int i = 0; i < 3; i++) ShowReel(i, i == 1 ? LeisureAccount.Bar : LeisureAccount.Seven, false);
                _result.text = blocked ?? "賭け金を選んで［回す！］";
            }
            if (pb == null && blocked != null && A.LastSpin != null) W.SetStatus(0, blocked);
            _capBtn.SetText(A.NightCap > 0 ? $"一夜の支出上限: {LeisureUi.Yen(A.NightCap)}" : "一夜の支出上限: なし");
            _stopBtn.SetText(A.StopLoss > 0 ? $"この損失で新規停止: {LeisureUi.Yen(A.StopLoss)}" : "損失での新規停止: なし");
            _caps.text = $"今夜 {A.PlaysTonight}/{LeisureAccount.NightlyLimit} 回\n賭けた額 {LeisureUi.Yen(A.StakedTonight)}　払戻し {LeisureUi.Yen(A.PaidTonight)}";
            _stats.text = $"ギャンブル純利益（ツール代込み）: {LeisureUi.Signed(A.GambleNet)}　　遊んだ夜: {A.GambleNights} 夜{(A.Vip ? "　　VIP席" : "")}";
            _hist.text = LeisureUi.HistoryText(A);
        }
    }

    // ============================================================ MIDNIGHT FX（上がる／下がる）
    /// <summary>
    /// 簡単な取引画面。金額を選んで［上がる］か［下がる］を押し、好きなときに［確定する］。倍率は ×5 固定。
    /// 損失は入れた金額まで。終業時には自動で確定する。値動きの方向は予測しない。
    /// </summary>
    public class MidnightFxTool : ToolWindow
    {
        public const int Leverage = 5;
        static readonly int[] Amounts = { 1000, 3000, 5000 };
        LeisureUi.AccountPanel _account;
        readonly List<Image> _bars = new List<Image>();
        readonly List<(int v, FlatButton b)> _amounts = new List<(int, FlatButton)>();
        int _amount = 1000;
        Text _price, _releases, _pos, _pnl, _stats, _hist, _note;
        FlatButton _up, _down, _close;
        const int Bars = 72;

        protected override float RefreshInterval => 0.3f;
        LeisureAccount A => S.Quests.Leisure;

        protected override void Build(RectTransform p)
        {
            var accent = LeisureUi.Fx;
            float y = ToolStyle.Header(p, "MIDNIGHT FX", "上がるか、下がるか。選んで、好きなときに確定するだけ。損失は入れた金額まで（終業時に自動で確定）。", "network", accent);
            _account = new LeisureUi.AccountPanel(ToolStyle.Box(p, 0, 1, y, 90, "遊興口座（給料の財布とは別）"), D, accent, s => W.SetStatus(0, s));
            y += 98;

            var chart = ToolStyle.Box(p, 0, 1, y, 170, "NEX/CRD（架空の通貨）");
            var area = UIFactory.Image(chart, RetroTheme.Field, new Color(0.05f, 0.08f, 0.07f), "Chart");
            area.rectTransform.Stretch(0, 0, 190, 0);
            for (int i = 0; i < Bars; i++)
            {
                var b = UIFactory.Solid(area.transform, new Color(0.3f, 0.95f, 0.55f), "Bar");
                var rt = b.rectTransform;
                rt.anchorMin = new Vector2((float)i / Bars, 0); rt.anchorMax = new Vector2((i + 0.8f) / Bars, 0.5f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                b.raycastTarget = false;
                _bars.Add(b);
            }
            _price = ToolStyle.Text(chart, "", 0, 0, 180, 40, 28, accent, TextAnchor.MiddleRight, true, true);
            _price.rectTransform.AnchorTopRight(0, 0, 180, 40);
            _releases = ToolStyle.Text(chart, "", 0, 0, 180, 100, 11, ToolStyle.Muted, TextAnchor.UpperRight);
            _releases.rectTransform.AnchorTopRight(0, 46, 180, 100);
            y += 178;

            var order = ToolStyle.Box(p, 0, 1, y, 104, "金額を選んで、上がるか下がるかを選ぶ");
            float x = 0;
            foreach (int v in Amounts)
            {
                int amount = v;
                var b = ToolStyle.Button(order, LeisureUi.Yen(v), x, 0, 100, 30, () => { _amount = amount; Refresh(); }, accent, false);
                _amounts.Add((v, b)); x += 106;
            }
            _note = ToolStyle.Text(order, "", x + 10, 0, 300, 30, 11, ToolStyle.Muted, TextAnchor.MiddleLeft);
            _up = ToolStyle.Button(order, "▲ 上がる", 0, 38, 300, 44, () => Place(1), new Color(0.05f, 0.45f, 0.15f));
            _down = ToolStyle.Button(order, "▼ 下がる", 320, 38, 300, 44, () => Place(-1), new Color(0.6f, 0.08f, 0.08f));
            _up.Label.fontSize = _down.Label.fontSize = 18;
            y += 112;

            var pos = ToolStyle.Box(p, 0, 1, y, 70, "いまの取引");
            _pos = ToolStyle.Text(pos, "", 0, 0, 380, 22, 13, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            _pnl = ToolStyle.Text(pos, "", 0, 22, 380, 30, 22, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            _close = ToolStyle.Button(pos, "確定する", 0, 0, 200, 46, () => { W.SetStatus(0, A.CloseNow()); Refresh(); }, accent);
            _close.Rect.AnchorTopRight(0, 4, 200, 46);
            _close.Label.fontSize = 16;
            y += 78;

            var stats = ToolStyle.Box(p, 0, 1, y, 36);
            _stats = ToolStyle.Text(stats, "", 0, 0, 540, 18, 12, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            var more = UIFactory.Rect("More", p).Stretch();
            more.gameObject.SetActive(false);
            var toggle = ToolStyle.Button(stats, "履歴 ▼", 0, 0, 100, 20, null, accent, false);
            toggle.Rect.AnchorTopRight(0, -1, 100, 20);
            toggle.OnClick = () => { more.gameObject.SetActive(!more.gameObject.activeSelf); toggle.SetText(more.gameObject.activeSelf ? "履歴 ▲" : "履歴 ▼"); };
            y += 44;
            _hist = LeisureUi.History(ToolStyle.Box(more, 0, 1, y, 0, "履歴"));
        }

        void Place(int side)
        {
            W.SetStatus(0, A.PlaceOrder(side, _amount, Leverage));
            Refresh();
        }

        protected override void Refresh()
        {
            if (_price == null || S?.Quests == null) return;
            _account.Refresh();
            foreach (var (v, b) in _amounts) b.SetLatched(v == _amount);
            var series = A.Series(Bars);
            decimal lo = decimal.MaxValue, hi = decimal.MinValue;
            foreach (var v in series) { if (v < lo) lo = v; if (v > hi) hi = v; }
            if (hi - lo < 0.05m) { hi += 0.025m; lo -= 0.025m; }
            for (int i = 0; i < _bars.Count; i++)
            {
                float k = (float)((series[i] - lo) / (hi - lo));
                var rt = _bars[i].rectTransform;
                rt.anchorMax = new Vector2(rt.anchorMax.x, Mathf.Clamp(0.04f + k * 0.94f, 0.04f, 1f));
                bool up = i == 0 || series[i] >= series[i - 1];
                _bars[i].color = up ? new Color(0.3f, 0.95f, 0.55f) : new Color(0.95f, 0.35f, 0.3f);
            }
            _price.text = A.PriceNow.ToString("0.000");
            var rel = new System.Text.StringBuilder(A.SpikeNight ? "<color=#C03020><b>今夜は急変夜</b></color>\n" : "");
            rel.Append("値が動きやすい時刻:\n");
            foreach (var t in A.Releases) rel.Append(TimeManager.Format(t)).Append('\n');
            _releases.text = rel.ToString();
            _note.text = $"倍率 ×{Leverage}・手数料 {LeisureUi.Yen((int)LeisureAccount.Fee(_amount, Leverage))}\n今夜 {A.OrdersTonight}/{LeisureAccount.NightlyLimit} 回";
            string blocked = A.OrderBlocked(_amount, Leverage);
            _up.Interactable = _down.Interactable = blocked == null;
            var o = A.Open;
            _close.Interactable = o != null;
            if (o == null)
            {
                _pos.text = blocked ?? "金額を選んで［上がる］か［下がる］を押してください。";
                _pnl.text = "";
            }
            else
            {
                long pnl = (long)Math.Round(A.UnrealizedNow);
                _pos.text = $"{(o.Side > 0 ? "▲ 上がる" : "▼ 下がる")} に {LeisureUi.Yen(o.Margin)}（{o.Entry:0.000} から）";
                _pnl.text = $"いま {LeisureUi.Signed(pnl)}";
                _pnl.color = pnl >= 0 ? new Color(0.05f, 0.45f, 0.15f) : new Color(0.7f, 0.08f, 0.08f);
            }
            _stats.text = $"FX純利益（確定分・手数料とツール代込み）: {LeisureUi.Signed(A.FxNet)}　取引した夜: {A.FxNights} 夜　今夜: {LeisureUi.Signed(A.PnlTonight)}";
            _hist.text = LeisureUi.HistoryText(A);
        }
    }

    // ============================================================ 遊興口座（借入・返済）
    /// <summary>メインモニターの「遊興口座」。借入はここからだけ申し込める（読んだだけでは契約しない）</summary>
    public class LeisureAccountView : MonoBehaviour
    {
        public const string Id = "leisure-account";
        ShiftDesktop _d;
        RetroWindow _w;
        LeisureUi.AccountPanel _account;
        Text _loan;
        float _t;

        public static void Open(ShiftDesktop d)
        {
            var w = d.OpenWindow(new WindowSpec { Id = Id, Title = "遊興口座", Icon = "folder", Width = 560, Height = 470, StatusCells = 1 }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<LeisureAccountView>();
            v._d = d; v._w = w;
            v.Build();
        }

        void Build()
        {
            var p = ToolStyle.Page(_w);
            var accent = ToolStyle.Slate;
            float y = ToolStyle.Header(p, "遊興口座", "NIGHT CLUB／MIDNIGHT FX 共通の口座。財布とは別。購入した機材は返済に充てられません。", "folder", accent, false);
            _account = new LeisureUi.AccountPanel(ToolStyle.Box(p, 0, 1, y, 90, "残高"), _d, accent, s => _w.SetStatus(0, s));
            y += 98;
            var loan = ToolStyle.Box(p, 0, 1, y, 190, "借入（利息なし・返済は3日後の朝・同時に1本まで）");
            ToolStyle.Text(loan, "<b>返済日に、口座の空き資金と財布から返せない場合は「破産」となり、勤務は終了します。</b>\n返済日の朝は「給与と個別入金 → 全取引の終了 → 返済」の順に処理します。",
                0, 0, 500, 44, 11, ToolStyle.Red, TextAnchor.UpperLeft);
            float x = 0;
            foreach (int v in LeisureAccount.Loans)
            {
                int amount = v;
                ToolStyle.Button(loan, "借りる " + LeisureUi.Yen(v), x, 52, 150, 28, () => Borrow(amount), accent, false);
                x += 156;
            }
            ToolStyle.Button(loan, "全額を返済する", 0, 90, 150, 28, () => { _w.SetStatus(0, _d.S.Quests.Leisure.Repay(int.MaxValue)); Refresh(); }, accent, false);
            _loan = ToolStyle.Text(loan, "", 0, 124, 500, 40, 12, ToolStyle.Ink, TextAnchor.UpperLeft);
            _w.Accelerators[KeyCode.C] = _w.RequestClose;
            Refresh();
        }

        void Borrow(int amount)
        {
            var a = _d.S.Quests.Leisure;
            string why = a.LoanBlocked(amount);
            if (why != null) { AudioFx.Error(); _w.SetStatus(0, why); return; }
            _d.ShowModal("借入の確認",
                $"{LeisureUi.Yen(amount)} を借ります。\n返済日: DAY{_d.S.Day.dayNumber + LeisureAccount.LoanDays} の朝（口座の空き資金 → 財布の順に引き落とし）\n" +
                "返せなければ「破産」となり、この周回は終わります。\nよろしいですか？", "warning", "借りる", () =>
                {
                    _w.SetStatus(0, a.Borrow(amount));
                    Refresh();
                }, "やめる", null);
        }

        void Refresh()
        {
            if (_d == null || _d.S == null) return;
            _account.Refresh();
            var a = _d.S.Quests.Leisure;
            _loan.text = a.Debt > 0 ? $"借入残 {LeisureUi.Yen(a.Debt)}　返済日 DAY{a.LoanDue} の朝" : "借入はありません。";
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_t > 0.5f) { _t = 0; Refresh(); }
        }
    }
}
