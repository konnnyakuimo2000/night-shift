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
                float x = 0;
                foreach (int v in new[] { 1000, 5000, 10000 })
                {
                    int amount = v;
                    ToolStyle.Button(box, "入金 " + Yen(v), x, 48, 100, 24, () => _status(_d.S.Quests.Leisure.Deposit(amount)), accent, false);
                    x += 104;
                }
                ToolStyle.Button(box, "全額出金", x, 48, 90, 24, () => _status(_d.S.Quests.Leisure.Withdraw(_d.S.Quests.Leisure.Free)), accent, false);
                ToolStyle.Button(box, "借入・返済…", x + 94, 48, 100, 24, () => LeisureAccountView.Open(_d), accent, false);
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

    // ============================================================ NIGHT CLUB
    public class NightClubTool : ToolWindow
    {
        LeisureUi.AccountPanel _account;
        readonly List<(int amount, FlatButton b)> _stakes = new List<(int, FlatButton)>();
        int _stake = 500;
        Text _info, _result, _hist, _stats, _caps;
        Image _wait;
        FlatButton _red, _black, _capBtn, _stopBtn;
        static readonly int[] CapSteps = { 0, 3000, 5000, 10000, 20000 };

        protected override float RefreshInterval => 0.25f;
        LeisureAccount A => S.Quests.Leisure;

        protected override void Build(RectTransform p)
        {
            var accent = LeisureUi.Club;
            float y = ToolStyle.Header(p, "NIGHT CLUB", "会員制の赤／黒。当たる確率は 赤 48%・黒 48%・ハウス 4%（どちらでもない）。", "globe", accent);
            var acc = ToolStyle.Box(p, 0, 1, y, 90, "遊興口座（給料の財布とは別）");
            _account = new LeisureUi.AccountPanel(acc, D, accent, s => W.SetStatus(0, s));
            y += 98;

            var bet = ToolStyle.Box(p, 0, 1, y, 158, "賭ける（1回 ゲーム内2分・受付後の取消不可・一夜6回まで）");
            float x = 0;
            foreach (int v in new[] { 500, 1000, 3000, 5000, LeisureAccount.VipStake })
            {
                int amount = v;
                var b = ToolStyle.Button(bet, LeisureUi.Yen(v), x, 0, 84, 26, () => { _stake = amount; Refresh(); }, accent, false);
                _stakes.Add((v, b));
                x += 88;
            }
            _info = ToolStyle.Text(bet, "", 0, 32, 520, 20, 12, ToolStyle.Ink, TextAnchor.MiddleLeft);
            _red = ToolStyle.Button(bet, "赤 に賭ける", 0, 56, 160, 40, () => Place(0), new Color(0.65f, 0.05f, 0.05f));
            _black = ToolStyle.Button(bet, "黒 に賭ける", 168, 56, 160, 40, () => Place(1), new Color(0.1f, 0.1f, 0.12f));
            _wait = ToolStyle.Progress(bet, 340, 62, 200, 26, accent);
            _result = ToolStyle.Text(bet, "", 0, 102, 540, 22, 13, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            y += 166;

            var set = ToolStyle.Box(p, 0, 1, y, 64, "自分で決める上限（アプリ内で設定・追加料金なし）");
            _capBtn = ToolStyle.Button(set, "", 0, 0, 200, 24, () => { NightCap = Next(CapSteps, A.NightCap); }, accent, false);
            _stopBtn = ToolStyle.Button(set, "", 206, 0, 220, 24, () => { StopLoss = Next(CapSteps, A.StopLoss); }, accent, false);
            _caps = ToolStyle.Text(set, "", 0, 28, 540, 18, 11, ToolStyle.Muted, TextAnchor.MiddleLeft);
            y += 72;

            var stats = ToolStyle.Box(p, 0, 1, y, 40);
            _stats = ToolStyle.Text(stats, "", 0, 0, 540, 20, 12, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            y += 48;
            var hist = ToolStyle.Box(p, 0, 1, y, 0, "履歴（過去の結果は、次の結果を予測しません）");
            _hist = LeisureUi.History(hist);
        }

        int NightCap { set => A.NightCap = value; }
        int StopLoss { set => A.StopLoss = value; }
        static int Next(int[] steps, int cur) { int i = Array.IndexOf(steps, cur); return steps[(i + 1) % steps.Length]; }

        void Place(int color)
        {
            string msg = A.PlaceBet(color, _stake);
            W.SetStatus(0, msg);
            Refresh();
        }

        protected override void Refresh()
        {
            if (_info == null || S?.Quests == null) return;
            _account.Refresh();
            foreach (var (amount, b) in _stakes)
            {
                b.gameObject.SetActive(amount != LeisureAccount.VipStake || A.Vip);
                b.SetLatched(amount == _stake);
            }
            string blocked = A.BetBlocked(_stake);
            _info.text = $"賭け金 {LeisureUi.Yen(_stake)}　当たれば +{LeisureUi.Yen(_stake)}　外れ・ハウスなら −{LeisureUi.Yen(_stake)}（最大損失）";
            _red.Interactable = _black.Interactable = blocked == null;
            var pb = A.PendingBet;
            if (pb != null)
            {
                float k = 1f - Mathf.Clamp01((pb.ResolveAt - S.Time.CurrentMinute) / LeisureAccount.BetMinutes);
                _wait.fillAmount = k;
                _result.text = $"結果待ち: {LeisureAccount.ColorLabel(pb.Color)} に {LeisureUi.Yen(pb.Amount)}（{TimeManager.Format(pb.ResolveAt)} に確定）";
            }
            else
            {
                _wait.fillAmount = 0;
                _result.text = blocked != null && A.PlaysTonight > 0 ? blocked : (blocked ?? "賭け金と色を選んでください。");
            }
            _capBtn.SetText(A.NightCap > 0 ? $"一夜の支出上限: {LeisureUi.Yen(A.NightCap)}" : "一夜の支出上限: なし");
            _stopBtn.SetText(A.StopLoss > 0 ? $"この損失で新規停止: {LeisureUi.Yen(A.StopLoss)}" : "損失での新規停止: なし");
            _caps.text = $"今夜: {A.PlaysTonight}/{LeisureAccount.NightlyLimit} 回　賭けた額 {LeisureUi.Yen(A.StakedTonight)}　払戻し {LeisureUi.Yen(A.PaidTonight)}";
            _stats.text = $"ギャンブル純利益（ツール代込み）: {LeisureUi.Signed(A.GambleNet)}　　賭けた夜: {A.GambleNights} 夜{(A.Vip ? "　　VIP席" : "")}";
            _hist.text = LeisureUi.HistoryText(A);
        }
    }

    // ============================================================ MIDNIGHT FX
    public class MidnightFxTool : ToolWindow
    {
        LeisureUi.AccountPanel _account;
        readonly List<Image> _bars = new List<Image>();
        readonly List<(int v, FlatButton b)> _margins = new List<(int, FlatButton)>(), _levs = new List<(int, FlatButton)>();
        int _margin = 500, _lev = 1;
        Text _price, _releases, _order, _pos, _stats, _hist;
        FlatButton _buy, _sell, _close;
        InputField _tp, _sl, _alert;
        const int Bars = 72;

        protected override float RefreshInterval => 0.3f;
        LeisureAccount A => S.Quests.Leisure;

        protected override void Build(RectTransform p)
        {
            var accent = LeisureUi.Fx;
            float y = ToolStyle.Header(p, "MIDNIGHT FX", "架空通貨ペア NEX/CRD。値動きの方向は予測しません。損失は証拠金まで。終業時に自動で決済されます。", "network", accent);
            var acc = ToolStyle.Box(p, 0, 1, y, 90, "遊興口座（給料の財布とは別）");
            _account = new LeisureUi.AccountPanel(acc, D, accent, s => W.SetStatus(0, s));
            y += 98;

            var chart = ToolStyle.Box(p, 0, 1, y, 150, "NEX/CRD");
            var area = UIFactory.Image(chart, RetroTheme.Field, new Color(0.05f, 0.08f, 0.07f), "Chart");
            area.rectTransform.Stretch(0, 0, 170, 0);
            for (int i = 0; i < Bars; i++)
            {
                var b = UIFactory.Solid(area.transform, new Color(0.3f, 0.95f, 0.55f), "Bar");
                var rt = b.rectTransform;
                rt.anchorMin = new Vector2((float)i / Bars, 0); rt.anchorMax = new Vector2((i + 0.8f) / Bars, 0.5f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                b.raycastTarget = false;
                _bars.Add(b);
            }
            _price = ToolStyle.Text(chart, "", 0, 0, 160, 34, 22, accent, TextAnchor.MiddleRight, true, true);
            _price.rectTransform.AnchorTopRight(0, 0, 160, 34);
            _releases = ToolStyle.Text(chart, "", 0, 0, 160, 100, 11, ToolStyle.Muted, TextAnchor.UpperRight);
            _releases.rectTransform.AnchorTopRight(0, 38, 160, 100);
            y += 158;

            var order = ToolStyle.Box(p, 0, 1, y, 128, "注文（同時に1つまで・一夜6回まで）");
            float x = 0;
            foreach (int v in LeisureAccount.Stakes)
            {
                int amount = v;
                var b = ToolStyle.Button(order, LeisureUi.Yen(v), x, 0, 78, 24, () => { _margin = amount; Refresh(); }, accent, false);
                _margins.Add((v, b)); x += 82;
            }
            x += 10;
            foreach (int v in LeisureAccount.Leverages)
            {
                int lev = v;
                var b = ToolStyle.Button(order, $"×{v}", x, 0, 46, 24, () => { _lev = lev; Refresh(); }, accent, false);
                _levs.Add((v, b)); x += 50;
            }
            _order = ToolStyle.Text(order, "", 0, 28, 640, 18, 11, ToolStyle.Ink, TextAnchor.MiddleLeft);
            ToolStyle.Text(order, "利益確定", 0, 52, 60, 22, 11, ToolStyle.Muted, TextAnchor.MiddleLeft);
            _tp = ToolStyle.Input(order, 60, 52, 80, 22);
            ToolStyle.Text(order, "損失確定", 150, 52, 60, 22, 11, ToolStyle.Muted, TextAnchor.MiddleLeft);
            _sl = ToolStyle.Input(order, 210, 52, 80, 22);
            ToolStyle.Text(order, "到達通知", 300, 52, 60, 22, 11, ToolStyle.Muted, TextAnchor.MiddleLeft);
            _alert = ToolStyle.Input(order, 360, 52, 80, 22);
            ToolStyle.Button(order, "通知を設定", 446, 52, 90, 22, SetAlert, accent, false);
            _buy = ToolStyle.Button(order, "買い", 0, 80, 150, 30, () => Place(1), new Color(0.05f, 0.4f, 0.15f));
            _sell = ToolStyle.Button(order, "売り", 156, 80, 150, 30, () => Place(-1), new Color(0.55f, 0.08f, 0.08f));
            y += 136;

            var pos = ToolStyle.Box(p, 0, 1, y, 56, "建玉");
            _pos = ToolStyle.Text(pos, "", 0, 0, 500, 36, 12, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            _close = ToolStyle.Button(pos, "決済する", 0, 0, 110, 30, () => { W.SetStatus(0, A.CloseNow()); Refresh(); }, accent);
            _close.Rect.AnchorTopRight(0, 2, 110, 30);
            y += 64;

            var stats = ToolStyle.Box(p, 0, 1, y, 40);
            _stats = ToolStyle.Text(stats, "", 0, 0, 640, 20, 12, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            y += 48;
            _hist = LeisureUi.History(ToolStyle.Box(p, 0, 1, y, 0, "履歴"));
        }

        static decimal ParsePrice(InputField f) => f != null && decimal.TryParse(f.text, out var v) && v > 0 ? v : 0;

        void SetAlert()
        {
            A.Alert = ParsePrice(_alert);
            W.SetStatus(0, A.Alert > 0 ? $"価格が {A.Alert:0.000} に到達したら通知します。" : "到達通知を解除しました。");
        }

        void Place(int side)
        {
            W.SetStatus(0, A.PlaceOrder(side, _margin, _lev, ParsePrice(_tp), ParsePrice(_sl)));
            Refresh();
        }

        protected override void Refresh()
        {
            if (_price == null || S?.Quests == null) return;
            _account.Refresh();
            foreach (var (v, b) in _margins) b.SetLatched(v == _margin);
            foreach (var (v, b) in _levs) b.SetLatched(v == _lev);
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
            var rel = new System.Text.StringBuilder(A.SpikeNight ? "<color=#C03020><b>急変夜</b></color>\n" : "");
            rel.Append("架空指標の発表:\n");
            foreach (var t in A.Releases) rel.Append(TimeManager.Format(t)).Append('\n');
            _releases.text = rel.ToString();
            decimal fee = LeisureAccount.Fee(_margin, _lev);
            _order.text = $"証拠金 {LeisureUi.Yen(_margin)} ×{_lev} = 想定元本 {LeisureUi.Yen(_margin * _lev)}　手数料 {LeisureUi.Yen((int)fee)}（往復・新規時）　{A.OrdersTonight}/{LeisureAccount.NightlyLimit} 回";
            string blocked = A.OrderBlocked(_margin, _lev);
            _buy.Interactable = _sell.Interactable = blocked == null;
            var o = A.Open;
            _close.Interactable = o != null;
            _pos.text = o == null ? (blocked ?? "建玉はありません。")
                : $"{(o.Side > 0 ? "買い" : "売り")} {LeisureUi.Yen(o.Margin)} ×{o.Leverage} @ {o.Entry:0.000}　含み損益 {LeisureUi.Signed((long)Math.Round(A.UnrealizedNow))}" +
                  $"\n<size=11>{(o.TakeProfit > 0 ? $"利益確定 {o.TakeProfit:0.000}　" : "")}{(o.StopLoss > 0 ? $"損失確定 {o.StopLoss:0.000}　" : "")}残りの証拠金が20%を切ると強制決済</size>";
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
