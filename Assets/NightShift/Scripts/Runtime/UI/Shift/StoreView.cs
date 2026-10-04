using System.Collections.Generic;
using NightShift.Core;
using NightShift.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// 社内購買システム（DAY8〜）。業務に使う機材・ツールを自腹で買う（費用は給与から精算）。
    /// 業務時間中は利用できない（業務開始前に買う）。
    /// </summary>
    public class StoreView : MonoBehaviour
    {
        public const string Id = "store";
        ShiftDesktop _d;
        RetroWindow _w;
        TableView _table;
        Text _desc, _balance;
        RetroButton _buy;

        public static void Open(ShiftDesktop d)
        {
            if (!d.Require(Features.Store)) return;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "社内購買システム", Icon = "folder", Width = 760, Height = 520,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 1
            }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<StoreView>();
            v._d = d; v._w = w;
            v.Build();
        }

        void Build()
        {
            var c = _w.Content;
            var head = UIFactory.Solid(c, RetroTheme.Navy, "Head");
            head.rectTransform.StretchH(0, 30);
            UIFactory.Label(head.transform, "  NEXUS 社内購買システム　― 業務用品のご注文（費用は給与から精算されます）", 13, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();

            var lg = UIFactory.Group(c, "商品一覧", 0, 36, 740, 260);
            _table = TableView.CreateStretch(lg, 0, 0, 0, 0,
                new TableColumn("区分", 70), new TableColumn("品名", 260), new TableColumn("価格", 90), new TableColumn("所持", 70), new TableColumn("入荷", 70));
            _table.SelectionChanged += _ => RefreshDetail();

            var dg = UIFactory.Group(c, "商品の説明", 0, 302, 540, 130);
            _desc = UIFactory.Label(dg, "", 12, TextAnchor.UpperLeft);
            _desc.rectTransform.Stretch();

            var bg = UIFactory.Group(c, "ご注文", 546, 302, 194, 130);
            _balance = UIFactory.Label(bg, "", 0, 0, 180, 40, 12, TextAnchor.UpperLeft);
            _buy = UIFactory.Button(bg, "注文する(B)", 0, 46, 180, 30, Buy, "check");
            UIFactory.Button(bg, "閉じる(C)", 0, 82, 180, 24, _w.RequestClose, "exit");
            _w.Accelerators[KeyCode.B] = Buy;
            _w.Accelerators[KeyCode.C] = _w.RequestClose;
            Refresh();
            if (_table.Count > 0) _table.Select(0);
        }

        List<StoreItemDefinition> Items()
        {
            var list = new List<StoreItemDefinition>();
            foreach (var it in _d.Game.Database.storeItems)
                if (it != null && it.unlockDay <= _d.S.Day.dayNumber) list.Add(it);
            list.Sort((a, b) => a.unlockDay != b.unlockDay ? a.unlockDay.CompareTo(b.unlockDay) : a.price.CompareTo(b.price));
            return list;
        }

        static string CategoryLabel(StoreCategory c)
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
            var sel = _table.Selected;
            var career = CareerRecord.Current;
            int day = _d.S.Day.dayNumber;
            _table.SetRows(Items(), it => new[]
            {
                CategoryLabel(it.category), it.displayName, OperatorWallet.Yen(it.price),
                it.consumable ? $"{career.ItemCount(it.itemId)} 個" : career.Owns(it.itemId) ? "購入済み" : "-",
                it.unlockDay == day ? "NEW" : ""
            });
            if (sel != null) _table.SelectData(sel);
            _balance.text = $"所持金: <b>{OperatorWallet.Yen(_d.Game.Wallet.Balance)}</b>";
            RefreshDetail();
        }

        /// <summary>買えない理由（null = 買える）</summary>
        string Reason(StoreItemDefinition it)
        {
            var career = CareerRecord.Current;
            if (_d.ShiftStarted) return "業務時間中は注文できません。業務開始前に注文してください。";
            if (!it.consumable && career.Owns(it.itemId)) return "購入済みです。";
            if (it.consumable && career.ItemCount(it.itemId) >= it.maxStock) return "これ以上は持てません。";
            if (!string.IsNullOrEmpty(it.prerequisite) && !career.Owns(it.prerequisite))
            {
                var pre = _d.Game.Database.FindItem(it.prerequisite);
                return $"先に「{(pre != null ? pre.displayName : it.prerequisite)}」が必要です。";
            }
            if (_d.Game.Wallet.Balance < it.price) return "所持金が足りません。";
            return null;
        }

        void RefreshDetail()
        {
            if (!(_table.Selected is StoreItemDefinition it)) { _desc.text = ""; _buy.Interactable = false; return; }
            string reason = Reason(it);
            _desc.text = $"<b>{it.displayName}</b>　{OperatorWallet.Yen(it.price)}\n\n{it.description}" + (reason != null ? $"\n\n<color=#B00000>{reason}</color>" : "");
            _buy.Interactable = reason == null;
        }

        void Buy()
        {
            if (!(_table.Selected is StoreItemDefinition it)) { AudioFx.Error(); return; }
            string reason = Reason(it);
            if (reason != null) { AudioFx.Error(); _w.SetStatus(0, reason); return; }
            MessageBox.Confirm(_d, "ご注文の確認", $"「{it.displayName}」を {OperatorWallet.Yen(it.price)} で注文します。\n代金は給与から精算されます。よろしいですか？", () =>
            {
                if (!_d.Game.Wallet.TrySpend(it.price)) { AudioFx.Error(); return; }
                var career = CareerRecord.Current;
                if (it.itemId == Data.Items.VaccineUpdate) career.vaccineDefinitionDay = _d.S.Day.dayNumber;
                else if (it.itemId == Data.Items.TokenPack) career.extraTokensTonight += 20000;
                else career.AddItem(it.itemId);
                career.Save();
                AudioFx.Chime();
                _w.SetStatus(0, $"「{it.displayName}」を注文しました。");
                _d.OnInventoryChanged();
                Refresh();
            }, "folder");
        }
    }
}
