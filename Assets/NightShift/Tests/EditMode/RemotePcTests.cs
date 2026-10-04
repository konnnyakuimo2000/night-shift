using NightShift.Data;
using NightShift.RemotePC;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>リモート操作の問い合わせ（R3〜R11）で使うPCの状態</summary>
    public class RemotePcTests : NightShiftTest
    {
        static VirtualPCState Pc()
        {
            var def = TestData.FindHelpDesk("R6_DHCP");
            var emp = TestData.Db.employees.Find(e => e.employeeId == "E-0012");
            return VirtualPCState.Create(def.pcProfile, emp, "10.10.2.12", new System.Random(1));
        }

        static string Run(VirtualPCState pc, string cmd) => TerminalCommands.Execute(cmd, pc, null, null, new System.Random(1));

        [Test]
        public void Dhcp_Off_MeansNoConnectivity()
        {
            var pc = Pc();
            Assert.IsTrue(pc.InternetConnectivity);
            pc.SetValue("DHCPEnabled", "false");
            Assert.IsFalse(pc.InternetConnectivity);
            Assert.AreEqual(pc.StaticIp, pc.IPAddress);
            pc.SetDhcp(true);
            Assert.IsTrue(pc.InternetConnectivity);
        }

        [Test]
        public void WrongDns_BreaksNameLookupOnly()
        {
            var pc = Pc();
            pc.SetValue("DnsAuto", "false");
            Assert.IsTrue(pc.InternetConnectivity);
            StringAssert.Contains("見つかりませんでした", Run(pc, "ping intranet"));
            StringAssert.Contains("応答", Run(pc, "ping 10.10.0.15"), "IP アドレスなら届く");
            pc.SetDnsAuto(true);
            StringAssert.Contains("応答", Run(pc, "ping intranet"));
        }

        [Test]
        public void FileServerDown_PingTimesOut()
        {
            var pc = Pc();
            pc.SetValue("FileServerReachable", "false");
            StringAssert.Contains("タイムアウト", Run(pc, "ping file-srv01"));
            StringAssert.Contains("応答", Run(pc, "ping intranet"), "他のサーバーには届く");
        }

        [Test]
        public void FakeAlert_ClosesWhenBrowserIsKilled()
        {
            var pc = Pc();
            pc.SetValue("FakeAlertOpen", "true");
            var browser = pc.RunningProcesses.Find(p => p.Name == VirtualPCState.BrowserProcess);
            Assert.IsNotNull(browser, "警告を出しているブラウザーが動いている");
            Assert.IsTrue(pc.KillProcess(browser));
            Assert.IsFalse(pc.FakeAlertOpen);
        }

        [Test]
        public void Reboot_IsRecorded()
        {
            var pc = Pc();
            pc.Reboot();
            Assert.AreEqual("true", pc.GetValue("Rebooted"));
        }

        [TestCase("R3_ISOLATE")]
        [TestCase("R4_FAKE_ALERT")]
        [TestCase("R5_PATCHES")]
        [TestCase("R6_DHCP")]
        [TestCase("R7_DNS")]
        [TestCase("R8_SCREEN_LOCK")]
        [TestCase("R9_SERVER_DOWN")]
        [TestCase("R10_SAVE_LOGS")]
        [TestCase("R11_BACKUP")]
        public void RemoteQuestion_StartsUnresolved_AndOneFixResolvesIt(string id)
        {
            var def = TestData.FindHelpDesk(id);
            Assert.IsNotNull(def, id);
            var pc = Pc();
            foreach (var v in def.faultVariants) foreach (var f in v.faults) pc.SetValue(f.key, f.value);
            Assert.IsFalse(pc.Satisfies(def.resolutionConditions), id + " は最初から解決していてはいけない");

            // 想定している1〜2回の操作
            switch (id)
            {
                case "R3_ISOLATE": pc.SetWiFi(false); break;
                case "R4_FAKE_ALERT": pc.KillProcess(pc.RunningProcesses.Find(p => p.Name == VirtualPCState.BrowserProcess)); break;
                case "R5_PATCHES": pc.ApplyPatches(); break;
                case "R6_DHCP": pc.SetDhcp(true); break;
                case "R7_DNS": pc.SetDnsAuto(true); break;
                case "R8_SCREEN_LOCK": pc.SetScreenLock(true); break;
                case "R9_SERVER_DOWN": pc.ReportKind = "SERVER"; break;
                case "R10_SAVE_LOGS": pc.SaveLogs(); break;
                case "R11_BACKUP": pc.Backup(); break;
            }
            Assert.IsTrue(pc.Satisfies(def.resolutionConditions), id + " が解決しない");
        }

        [Test]
        public void R3_RebootFails()
        {
            var def = TestData.FindHelpDesk("R3_ISOLATE");
            var pc = Pc();
            pc.Reboot();
            pc.SetWiFi(false);
            Assert.IsFalse(pc.Satisfies(def.resolutionConditions), "再起動すると痕跡が消えるので失敗");
        }

        [Test]
        public void R10_ClearingLogsFails()
        {
            var def = TestData.FindHelpDesk("R10_SAVE_LOGS");
            var pc = Pc();
            pc.ClearLogs();
            pc.SaveLogs();
            Assert.IsFalse(pc.Satisfies(def.resolutionConditions));
        }
    }
}
