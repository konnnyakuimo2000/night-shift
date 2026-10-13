using System.Collections.Generic;
using System.Linq;
using NightShift.Core;
using NightShift.Data;
using NightShift.Systems;
using NightShift.UI.Shift;
using NUnit.Framework;
using UnityEngine;

namespace NightShift.Tests
{
    /// <summary>ツールの統合（§5）: 相関・アプリと権利・販売終了の商品</summary>
    public class ToolIntegrationTests : NightShiftTest
    {
        static LogEntry L(float t, string src, string user) => new LogEntry { Time = t, Source = src, Destination = "AUTH-SRV01", User = user, EventType = "AUTH", Result = "OK", Detail = "" };

        // 11
        [Test]
        public void Correlation_FollowsSameUserAcrossRanges_ButNotUnknownUsers()
        {
            var logs = new List<LogEntry>
            {
                L(1, "198.51.100.7", "佐藤 健 (E-0102)"),
                L(2, "203.0.113.40", "佐藤 健 (E-0102)"),
                L(3, "198.51.100.9", "-"),
                L(4, "192.0.2.15", "-"),
                L(5, "10.10.2.12", "鈴木 花 (E-0201)"),
                L(6, "10.10.2.12", "鈴木 花 (E-0201)"),
            };
            var users = LogCorrelation.ByUserAcrossRanges(logs);
            Assert.AreEqual(1, users.Count);
            Assert.AreEqual("佐藤 健 (E-0102)", users[0].Key);
            Assert.AreEqual(2, users[0].Value.Count);
            Assert.IsFalse(users.Any(u => u.Key == "-"), "利用者「-」を一人として結び付けない");
            var ranges = LogCorrelation.ByRange(logs);
            Assert.IsTrue(ranges.Any(r => r.Key == "198.51.100.*" && r.Value.Count == 2));
            Assert.IsFalse(ranges.Any(r => r.Key.StartsWith("10.")), "社内は範囲の一覧に出さない");
        }

        // 12
        [Test]
        public void Apps_MapOldRights_AndDoNotUnlockUnownedTabs()
        {
            var log = ToolApps.ForItem(Items.Correlator);
            Assert.AreSame(ToolApps.ForItem(Items.LogAnalyzer), log);
            Assert.AreSame(ToolApps.ForItem(Items.RemoteKit), ToolApps.ForItem(Items.Isolator));
            CareerRecord.Current.AddItem(Items.Isolator); // 隔離だけを持つ古いセーブ
            var dev = ToolApps.ForItem(Items.Isolator);
            Assert.IsTrue(ToolApps.Owns(dev));
            Assert.IsFalse(CareerRecord.Current.Owns(Items.RemoteKit), "診断の権利は勝手に増えない");
            Assert.IsFalse(ToolApps.Owns(log));
            Assert.IsNull(ToolApps.ForItem(Items.FwConsole), "販売終了の商品にアプリはない");
        }

        // 13
        [Test]
        public void RetiredItems_AreHidden_ButOwnershipAndBalanceSurviveReload()
        {
            var fw = TestData.Db.FindItem(Items.FwConsole);
            var mail = TestData.Db.FindItem(Items.MailInspector);
            Assert.IsTrue(fw.retired && mail.retired);
            Assert.IsFalse(StoreView.OnSale(fw, 25));
            Assert.IsFalse(StoreView.OnSale(mail, 25));
            Assert.IsTrue(StoreView.OnSale(TestData.Db.FindItem(Items.Correlator), 25));
            CareerRecord.Current.AddItem(Items.FwConsole);
            var wallet = new OperatorWallet();
            int balance = wallet.Balance;
            var reloaded = JsonUtility.FromJson<CareerRecord>(CareerRecord.Current.Snapshot());
            Assert.IsTrue(reloaded.Owns(Items.FwConsole), "所有の記録は残す");
            Assert.AreEqual(balance, wallet.Balance, "自動返金はしない");
            Assert.AreEqual(21, TestData.Db.storeItems.Count, "ID は消さない");
        }

        [Test]
        public void ProductNames_AreConsistentWithApps()
        {
            Assert.AreEqual("VACCINE 99 サーバー検査ライセンス", TestData.Db.FindItem(Items.VaccineServer).displayName);
            StringAssert.StartsWith("端末管理", TestData.Db.FindItem(Items.Isolator).displayName);
            StringAssert.StartsWith("端末管理", TestData.Db.FindItem(Items.RemoteKit).displayName);
            StringAssert.StartsWith("ログ解析", TestData.Db.FindItem(Items.Correlator).displayName);
            // 価格・解禁日は変えない
            Assert.AreEqual(19800, TestData.Db.FindItem(Items.Correlator).price);
            Assert.AreEqual(22, TestData.Db.FindItem(Items.Correlator).unlockDay);
            Assert.AreEqual(16800, TestData.Db.FindItem(Items.Isolator).price);
        }

        [Test]
        public void SendingServer_ComesFromMailData_NotFromThePhishFlag()
        {
            var inside = new MailMessage { FromAddress = "morita@" + MailManager.CompanyDomain, IsPhish = true };
            Assert.AreEqual("MAIL-SRV01 (10.10.0.20)", MailManager.SendingServer(inside), "正解の印ではなくアドレスから決める");
            var outside = new MailMessage { FromAddress = "it@nexus-c0rp.example", IsPhish = false };
            Assert.AreEqual("mail.nexus-c0rp.example", MailManager.SendingServer(outside));
            var given = new MailMessage { FromAddress = "a@b.example", FromServer = "relay.example" };
            Assert.AreEqual("relay.example", MailManager.SendingServer(given));
        }
    }
}
