using System.Collections.Generic;
using NightShift.Core;
using NightShift.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// 社内購買（DAY8〜）。業務に使う機材・ツールを自腹で買う（費用は給与から精算）。
    /// 業務時間中は注文できない（業務開始前に買う）。見た目は通販カタログ風（ToolStyle）。
    /// </summary>
    public class StoreView : MonoBehaviour
    {
        public const string Id = "store";
        static readonly Color Accent = ToolStyle.Navy;
        ShiftDesktop _d;
        RetroWindow _w;
        ToolList _list;
        Text _balance, _name, _price, _desc, _ad, _owned;
        Image _icon;
        RectTransform _catBadge;
        GameObject _detail, _reasonHost;
        Text _reason;
        FlatButton _buy;
        readonly List<FlatButton> _filters = new List<FlatButton>();
        StoreCategory? _filter;

        public static void Open(ShiftDesktop d)
        {
            if (!d.Require(Features.Store)) return;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "社内購買", Icon = "folder", Width = 860, Height = 600, StatusCells = 1
            }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<StoreView>();
            v._d = d; v._w = w;
            v.Build();
        }

        void Build()
        {
            var p = ToolStyle.Page(_w);
            float y = ToolStyle.Header(p, "社内購買", "仕事に使う機材・ツールを買えます。代金は給与から精算されます（会社は1円も出しません）。", "nexus", Accent, false);
            _balance = ToolStyle.Text(p, "", 0, 0, 260, 28, 18, Color.white, TextAnchor.MiddleRight, true);
            _balance.rectTransform.AnchorTopRight(18, 8, 300, 26);
            var balLabel = ToolStyle.Text(p, "所持金", 0, 0, 100, 16, 11, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleRight);
            balLabel.rectTransform.AnchorTopRight(18, 34, 200, 16);

            if (_d.ShiftStarted)
            {
                var host = UIFactory.Rect("Notice", p);
                host.StretchH(y, 34, ToolStyle.Pad, ToolStyle.Pad);
                var t = ToolStyle.Banner(host, "業務時間中は注文できません。次の夜、業務を始める前（業務連絡を読んでいる間）に注文してください。", Tone.Warn, 0, 0, 10, 34);
                ((RectTransform)t.transform.parent).Stretch();
                y += 44;
            }

            // 種類で絞り込む
            var chips = UIFactory.Rect("Filters", p);
            chips.StretchH(y, 30, ToolStyle.Pad, ToolStyle.Pad);
            float x = 0;
            void Chip(string label, StoreCategory? cat)
            {
                var b = ToolStyle.Button(chips, label, x, 0, 96, 28, () => { _filter = cat; Refresh(); }, Accent, false);
                b.name = cat.HasValue ? cat.Value.ToString() : "All";
                _filters.Add(b);
                x += 102;
            }
            Chip("すべて", null);
            Chip("機材", StoreCategory.Hardware);
            Chip("ツール", StoreCategory.Tool);
            Chip("対策ソフト", StoreCategory.Security);
            Chip("AI", StoreCategory.AI);
            Chip("消耗品", StoreCategory.Consumable);
            y += 40;

            var lg = ToolStyle.Box(p, 0, 0.52f, y, 0, "商品");
            _list = ToolList.Create(lg, Accent, "この種類の商品はありません。");
            _list.SelectionChanged += _ => RefreshDetail();

            var dg = ToolStyle.Box(p, 0.52f, 1, y, 0);
            _detail = UIFactory.Rect("Detail", dg).Stretch().gameObject;
            var d = _detail.transform;
            _icon = UIFactory.Image(d, null, Color.white, "Icon");
            _icon.rectTransform.Place(0, 2, 32, 32);
            _name = ToolStyle.Text(d, "", 44, 0, 330, 22, 16, ToolStyle.Ink, TextAnchor.MiddleLeft, true);
            _price = ToolStyle.Text(d, "", 44, 22, 200, 26, 20, Accent, TextAnchor.MiddleLeft, true);
            _catBadge = null;
            _owned = ToolStyle.Text(d, "", 0, 56, 360, 18, 12, ToolStyle.Muted, TextAnchor.MiddleLeft, true);
            _desc = ToolStyle.Text(d, "", 13, ToolStyle.Ink);
            _desc.rectTransform.StretchH(82, 150, 0, 0);
            _ad = ToolStyle.Text(d, "", 12, new Color(0.55f, 0.35f, 0.1f));
            _ad.fontStyle = FontStyle.Italic;
            _ad.rectTransform.StretchH(236, 40, 0, 0);
            var bottom = UIFactory.Rect("Bottom", d);
            bottom.anchorMin = new Vector2(0, 0); bottom.anchorMax = new Vector2(1, 0); bottom.pivot = new Vector2(0, 0);
            bottom.offsetMin = new Vector2(0, 0); bottom.offsetMax = new Vector2(0, 96);
            _reasonHost = UIFactory.Rect("Reason", bottom).gameObject;
            ((RectTransform)_reasonHost.transform).StretchH(0, 40);
            _reason = ToolStyle.Banner(_reasonHost.transform, "", Tone.Warn, 0, 0, 10, 40);
            ((RectTransform)_reason.transform.parent).Stretch();
            _buy = ToolStyle.Button(bottom, "購入する(B)", 0, 0, 10, 44, Buy, Accent);
            _buy.Rect.StretchH(50, 44);

            _w.Accelerators[KeyCode.B] = Buy;
            _w.Accelerators[KeyCode.C] = _w.RequestClose;
            _w.SetStatus(0, "商品を選ぶと、右に説明と購入ボタンが出ます。");
            Refresh();
            var items = Items();
            if (items.Count > 0) _list.Select(items[0]);
        }

        List<StoreItemDefinition> Items()
        {
            var list = new List<StoreItemDefinition>();
            foreach (var it in _d.Game.Database.storeItems)
                if (it != null && it.unlockDay <= _d.S.Day.dayNumber && OnSale(it) && (!_filter.HasValue || it.category == _filter.Value)) list.Add(it);
            list.Sort((a, b) => a.unlockDay != b.unlockDay ? a.unlockDay.CompareTo(b.unlockDay) : a.price.CompareTo(b.price));
            return list;
        }

        public const string CharmFlag = "SQ03_CHARM";

        /// <summary>サブクエストで解禁される商品は、招待・案内のあとにだけ並ぶ（新商品の通知は出さない）</summary>
        public static bool OnSale(StoreItemDefinition it, int day)
        {
            if (string.IsNullOrEmpty(it.questFlag)) return true;
            var c = CareerRecord.Current;
            if (it.questFlag == CharmFlag) return Systems.SideQuests.Sq03Route.CharmOnSale(c, day);
            return c.HasFlag(it.questFlag);
        }

        bool OnSale(StoreItemDefinition it) => OnSale(it, _d.S.Day.dayNumber);

        public static string CategoryLabel(StoreCategory c)
        {
            switch (c)
            {
                case StoreCategory.Hardware: return "機材";
                case StoreCategory.Tool: return "ツール";
                case StoreCategory.Security: return "対策ソフト";
                case StoreCategory.AI: return "AI";
                default: return "消耗品";
            }
        }

        void Refresh()
        {
            var career = CareerRecord.Current;
            int day = _d.S.Day.dayNumber;
            int balance = _d.Game.Wallet.Balance;
            _list.SetItems(Items(), it =>
            {
                string badge = null; Tone tone = Tone.Neutral;
                if (!it.consumable && career.Owns(it.itemId)) { badge = "購入済み"; tone = Tone.Ok; }
                else if (it.consumable && career.ItemCount(it.itemId) > 0) { badge = $"所持 {career.ItemCount(it.itemId)} 個"; tone = Tone.Ok; }
                else if (it.unlockDay == day) { badge = "NEW"; tone = Tone.Info; }
                else if (balance < it.price) { badge = "所持金が足りない"; tone = Tone.Warn; }
                var row = new ToolList.Row(it.displayName, $"{CategoryLabel(it.category)}　・　{OperatorWallet.Yen(it.price)}", badge, tone, it.icon);
                row.Blink = badge == "NEW";
                return row;
            });
            _balance.text = OperatorWallet.Yen(balance);
            foreach (var b in _filters)
            {
                bool on = (_filter.HasValue ? _filter.Value.ToString() : "All") == b.name;
                b.SetLatched(on);
            }
            RefreshDetail();
        }

        /// <summary>
        /// 所持数・回数による購入制限。確認画面を出した後にも照合する。
        /// 定義ファイル更新と追加トークンは所持品にならないので、それぞれの記録で判定する（today = 今の夜）
        /// </summary>
        public static string StockBlockedReason(StoreItemDefinition it, int today = 0)
        {
            var career = CareerRecord.Current;
            if (it.itemId == Data.Items.VaccineUpdate)
                return today > 0 && career.vaccineDefinitionDay >= today ? $"定義ファイルは最新です（DAY{career.vaccineDefinitionDay} 版）。" : null;
            if (it.itemId == Data.Items.TokenPack)
                return today > 0 && career.tokenPacksDay == today && career.tokenPacksBought >= it.maxStock ? $"1夜に{it.maxStock}回までです。" : null;
            if (!it.consumable && career.Owns(it.itemId)) return "購入済みです。";
            if (it.consumable && career.ItemCount(it.itemId) >= it.maxStock) return "これ以上は持てません。";
            if (it.itemId == Data.Items.Charm && career.charm.boughtToday >= 1) return "1夜に1枚までです。";
            return null;
        }

        /// <summary>代金を払った後に、品物を記録へ反映する</summary>
        public static void ApplyPurchase(CareerRecord career, StoreItemDefinition it, int today)
        {
            if (it.itemId == Data.Items.VaccineUpdate) career.vaccineDefinitionDay = today;
            else if (it.itemId == Data.Items.TokenPack)
            {
                if (career.tokenPacksDay != today) { career.tokenPacksDay = today; career.tokenPacksBought = 0; }
                career.tokenPacksBought++;
                career.extraTokensTonight += 20000;
            }
            else career.AddItem(it.itemId);
            if (it.itemId == Data.Items.Charm) career.charm.boughtToday++;
            // 買った時点の定義ファイルが付いている
            if (it.itemId == Data.Items.Vaccine) career.vaccineDefinitionDay = Mathf.Max(career.vaccineDefinitionDay, today);
        }

        /// <summary>買えない理由（null = 買える）</summary>
        string Reason(StoreItemDefinition it)
        {
            var career = CareerRecord.Current;
            if (_d.ShiftStarted) return "業務時間中は注文できません。";
            string stock = StockBlockedReason(it, _d.S.Day.dayNumber);
            if (stock != null) return stock;
            if (!string.IsNullOrEmpty(it.prerequisite) && !career.Owns(it.prerequisite))
            {
                var pre = _d.Game.Database.FindItem(it.prerequisite);
                return $"先に「{(pre != null ? pre.displayName : it.prerequisite)}」が必要です。";
            }
            if (_d.Game.Wallet.Balance < it.price) return $"所持金が {OperatorWallet.Yen(it.price - _d.Game.Wallet.Balance)} 足りません。";
            return null;
        }

        void RefreshDetail()
        {
            var it = _list.Selected as StoreItemDefinition;
            _detail.SetActive(it != null);
            if (it == null) return;
            var career = CareerRecord.Current;
            _icon.sprite = RetroTheme.BigIcon(it.icon);
            _name.text = it.displayName;
            _price.text = OperatorWallet.Yen(it.price);
            if (_catBadge) Destroy(_catBadge.gameObject);
            _catBadge = ToolStyle.BadgeRight(_detail.transform, CategoryLabel(it.category), Tone.Info, 0, 2);
            _owned.text = it.consumable ? $"所持 {career.ItemCount(it.itemId)} 個（最大 {it.maxStock} 個）" : career.Owns(it.itemId) ? "購入済み" : "未購入";
            _desc.text = Description(it).Replace(' ', '\u00A0'); // 日本語の中の半角スペースで不自然に改行しないように
            _ad.text = string.IsNullOrEmpty(it.adCopy) ? "" : $"「{it.adCopy}」";
            string reason = Reason(it);
            _reasonHost.SetActive(reason != null);
            _reason.text = reason ?? "";
            _buy.Interactable = reason == null;
            _buy.SetText(reason == null ? $"購入する（{OperatorWallet.Yen(it.price)}）(B)" : "購入できません");
        }

        /// <summary>機器保護札の説明は、発動を経験したあと・終盤で変わる（価格・残数・効果の表示は変えない）</summary>
        string Description(StoreItemDefinition it)
        {
            string d = it.description ?? "";
            if (it.itemId != Data.Items.Charm) return d;
            var c = CareerRecord.Current.charm;
            var st = CareerRecord.Current.FindQuest("SQ03");
            if (st != null && st.stage == "H3") d += "\n貼る場所が残っていることをご確認ください。";
            else if (c.everTriggered) d += "\n剥がしたものを再使用しないでください。";
            return d;
        }

        void Buy()
        {
            if (!(_list.Selected is StoreItemDefinition it)) { AudioFx.Error(); return; }
            string reason = Reason(it);
            if (reason != null) { AudioFx.Error(); _w.SetStatus(0, reason); return; }
            MessageBox.Confirm(_d, "ご注文の確認", $"「{it.displayName}」を {OperatorWallet.Yen(it.price)} で注文します。\n代金は給与から精算されます。よろしいですか？", () =>
            {
                // 確認画面の間に状況が変わっていないか、上限・残高・前提をもう一度確かめる
                string again = Reason(it);
                if (again != null) { AudioFx.Error(); _w.SetStatus(0, again); Refresh(); return; }
                if (!_d.Game.Wallet.TrySpend(it.price)) { AudioFx.Error(); return; }
                var career = CareerRecord.Current;
                ApplyPurchase(career, it, _d.S.Day.dayNumber);
                career.Save();
                AudioFx.Chime();
                _w.SetStatus(0, $"「{it.displayName}」を注文しました。");
                _d.OnInventoryChanged();
                Refresh();
                ToolFx.Flash(_balance);
                ToolFx.Flash(_icon);
            }, "folder");
        }
    }
}
