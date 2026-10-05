using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>対応手段（遮断・隔離・停止・報告）とサーバー運用管理</summary>
    public class SecurityAndServerTests : NightShiftTest
    {
        static EventInstance Request(ShiftSession s, string ip, bool malicious)
        {
            NetworkNodeDefinition auth = null;
            foreach (var n in s.Network.Nodes) if (n.nodeId == "AUTH-SRV01") auth = n;
            return new EventInstance
            {
                DisplayUser = "UNKNOWN", DisplayEmployeeId = "-", Device = "NB-TEST", SourceIp = ip,
                Zone = SourceZone.External, Destination = auth, AccessType = "VPN接続", IsMalicious = malicious
            };
        }

        [Test]
        public void Block_RangeMatchesPrefix()
        {
            var s = TestData.Session(11);
            s.Security.Block("198.51.100.*");
            Assert.IsTrue(s.Security.IsBlocked("198.51.100.7"));
            Assert.IsTrue(s.Security.IsBlocked("198.51.100.250"));
            Assert.IsFalse(s.Security.IsBlocked("203.0.113.7"));
        }

        [Test]
        public void Block_HostileSourceCountsAsCorrect_InternalAsWrong()
        {
            var s = TestData.Session(11);
            s.Security.MarkHostile("198.51.100.20");
            s.Security.Block("198.51.100.20");
            Assert.AreEqual(1, s.Evaluation.SecurityActionsCorrect);
            s.Security.Block("10.10.2.12");
            Assert.AreEqual(1, s.Evaluation.SecurityActionsWrong);
        }

        [Test]
        public void RequestsFromBlockedSource_AreDeniedAutomatically()
        {
            var s = TestData.Session(11);
            s.Security.Block("203.0.113.*");
            var e = Request(s, "203.0.113.44", true);
            s.Requests.Submit(e);
            Assert.AreEqual(EventState.Blocked, e.State);
            Assert.IsTrue(e.AutoDecided);
            Assert.AreEqual(0, s.Requests.Pending.Count);
        }

        [Test]
        public void Blocking_DeniesAlreadyPendingRequests()
        {
            var s = TestData.Session(11);
            var e = Request(s, "192.0.2.80", true);
            s.Requests.Submit(e);
            Assert.AreEqual(EventState.Pending, e.State);
            s.Security.Block("192.0.2.80");
            Assert.AreEqual(EventState.Blocked, e.State);
        }

        [Test]
        public void Isolation_OfInfectedDeviceIsCorrect()
        {
            var s = TestData.Session(16);
            s.Security.MarkInfected("SALES-PC05", "V3");
            s.Security.Isolate("SALES-PC05");
            Assert.IsTrue(s.Security.IsIsolated("SALES-PC05"));
            Assert.AreEqual(1, s.Evaluation.SecurityActionsCorrect);
            s.Security.Isolate("DEV-PC03");
            Assert.AreEqual(1, s.Evaluation.SecurityActionsWrong, "健康なPCの隔離は業務妨害");
        }

        [Test]
        public void SuspendedAccount_RequestsAreDenied()
        {
            var s = TestData.Session(19);
            var emp = s.Employees.FindById("E-0078");
            s.Security.MarkCompromised(emp.employeeId);
            s.Security.Suspend(emp);
            Assert.AreEqual(1, s.Evaluation.SecurityActionsCorrect);
            var e = Request(s, "10.10.8.13", true);
            e.Employee = emp;
            s.Requests.Submit(e);
            Assert.AreEqual(EventState.Blocked, e.State);
        }

        [Test]
        public void Report_TracksSourceAndCorrectness()
        {
            var s = TestData.Session(13);
            s.Security.Report("test", true, "10.10.2.12");
            s.Security.Report("test", false);
            Assert.IsTrue(s.Security.WasReported("10.10.2.12"));
            Assert.AreEqual(1, s.CorrectReports);
            Assert.AreEqual(1, s.WrongReports);
        }

        [Test]
        public void Report_SameSourceTwice_ScoresOnce()
        {
            var s = TestData.Session(13);
            s.Security.Report("test", true, "198.51.100.7");
            int security = s.Evaluation.SecurityActionsCorrect;
            s.Security.Report("test again", true, "198.51.100.7");
            Assert.AreEqual(1, s.CorrectReports);
            Assert.AreEqual(security, s.Evaluation.SecurityActionsCorrect);
        }

        [Test]
        public void Server_KillingSuspiciousProcessIsCorrect()
        {
            var s = TestData.Session(17);
            var p = s.Servers.Implant("WEB-SRV01", "httpd_cache.exe", "T9");
            Assert.IsTrue(s.Servers.HasTag("WEB-SRV01", "T9"));
            var st = s.Servers.Find("WEB-SRV01");
            s.Servers.KillProcess(st, p);
            Assert.IsFalse(s.Servers.HasTag("WEB-SRV01", "T9"));
            Assert.AreEqual(1, s.Evaluation.SecurityActionsCorrect);
            Assert.IsTrue(st.AllRunning);
        }

        [Test]
        public void Server_KillingCriticalProcessStopsServices_RestartBringsThemBack()
        {
            var s = TestData.Session(17);
            var st = s.Servers.Find("MAIL-SRV01");
            var smtp = st.Processes.Find(x => x.Name == "smtpd.exe");
            s.Servers.KillProcess(st, smtp);
            Assert.IsFalse(st.AllRunning);
            Assert.AreEqual(1, s.Evaluation.FalsePositive, "正常業務を止めた");
            s.Servers.RestartService(st, st.Services[0]);
            Assert.IsTrue(st.AllRunning);
            Assert.IsNotNull(st.Processes.Find(x => x.Name == "smtpd.exe"), "終了させた正規のプロセスが戻る");
        }

        [Test]
        public void Server_RestoreTakesTimeAndClearsDefacement()
        {
            var s = TestData.Session(17);
            s.Servers.Implant("WEB-SRV01", "httpd_cache.exe", "T9");
            s.Servers.SetDefaced("WEB-SRV01", true);
            var st = s.Servers.Find("WEB-SRV01");
            s.Servers.StartRestore(st);
            Assert.IsTrue(st.Restoring);
            Assert.IsFalse(st.AllRunning, "復元中はサービスが止まる");
            s.Servers.Tick(ServerOps.RestoreMinutes - 1);
            Assert.IsTrue(st.Restoring);
            s.Servers.Tick(ServerOps.RestoreMinutes + 1);
            Assert.IsFalse(st.Restoring);
            Assert.IsFalse(st.Defaced);
            Assert.IsFalse(st.Garbled);
            Assert.IsFalse(s.Servers.HasTag("WEB-SRV01", "T9"));
        }
    }
}
