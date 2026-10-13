using NightShift.Data;
using NightShift.Events;
using NightShift.RemotePC;
using NightShift.Systems;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>防御・リモート対応・復旧の整合性（TOOL_REWORK_CLAUDE.md §9）</summary>
    public class DefenseConsistencyTests : NightShiftTest
    {
        static EventInstance InternalRequest(ShiftSession s, EmployeeData emp, string ip = null)
        {
            NetworkNodeDefinition file = null;
            foreach (var n in s.Network.Nodes) if (n.nodeId == "FILE-SRV01") file = n;
            return new EventInstance
            {
                Employee = emp, DisplayUser = emp.fullName, DisplayEmployeeId = emp.employeeId, Device = emp.deviceId,
                SourceIp = ip ?? s.Employees.GetIp(emp), Zone = SourceZone.Internal, Destination = file, AccessType = "共有ファイル（閲覧）"
            };
        }

        /// <summary>自動審査・SOC-AI のように、登録の通知を受けたらすぐ許可する購読者</summary>
        static void AutoAllowOnCreated(ShiftSession s) =>
            s.Requests.Created += e => { if (e.State == EventState.Pending) { e.AutoDecided = true; s.Requests.Decide(e, PlayerAction.Allow); } };

        // 31
        [Test]
        public void ForcedDeny_WinsOverAutoAllow_ForBlockedIsolatedAndSuspended()
        {
            var s = TestData.Session(19);
            AutoAllowOnCreated(s);
            var a = s.Employees.All[0];
            var b = s.Employees.All[1];
            var c = s.Employees.All[2];
            s.Security.Block(s.Employees.GetIp(a));
            s.Security.Isolate(b.deviceId);
            s.Security.Suspend(c);
            int correct = s.Evaluation.SecurityActionsCorrect, wrong = s.Evaluation.SecurityActionsWrong;
            foreach (var emp in new[] { a, b, c })
            {
                var e = InternalRequest(s, emp);
                s.Requests.Submit(e);
                Assert.AreEqual(EventState.Blocked, e.State, emp.employeeId);
                Assert.IsTrue(e.AutoDecided);
                Assert.AreEqual(1, CountProcessed(s, e), "処理済みに一度だけ入る");
            }
            Assert.AreEqual(0, s.Requests.Pending.Count);
            Assert.AreEqual(correct, s.Evaluation.SecurityActionsCorrect, "対応の評価は増えない");
            Assert.AreEqual(wrong, s.Evaluation.SecurityActionsWrong);
        }

        static int CountProcessed(ShiftSession s, EventInstance e)
        {
            int n = 0;
            foreach (var x in s.Requests.Processed) if (ReferenceEquals(x, e)) n++;
            return n;
        }

        // 32
        [Test]
        public void Isolation_DeniesPendingFromThatDeviceOnly_AndLaterAllowCannotOverturn()
        {
            var s = TestData.Session(16);
            var a = s.Employees.All[0];
            var b = s.Employees.All[1];
            var ea = InternalRequest(s, a);
            var eb = InternalRequest(s, b);
            s.Requests.Submit(ea);
            s.Requests.Submit(eb);
            s.Security.Isolate(a.deviceId);
            Assert.AreEqual(EventState.Blocked, ea.State);
            Assert.IsTrue(ea.AutoDecided, "自動拒否は手動の操作として数えない");
            Assert.AreEqual(EventState.Pending, eb.State, "別の端末の要求は残る");
            s.Requests.Decide(ea, PlayerAction.Allow);
            Assert.AreEqual(EventState.Blocked, ea.State, "古い許可操作で覆らない");

            // 隔離後に届いた要求を（手動で）許可しようとしても拒否になる
            var late = InternalRequest(s, a);
            s.Requests.Submit(late);
            Assert.AreEqual(EventState.Blocked, late.State);
        }

        // 33
        [Test]
        public void IsolatingInfectedPc_CompletesTheIsolationTicket()
        {
            var s = TestData.Session(16);
            var emp = s.Employees.All[0];
            var t = s.HelpDesk.Create(TestData.FindHelpDesk("R3_ISOLATE"), emp);
            Assert.IsTrue(t.PC.InternetConnectivity);
            s.Security.Isolate(emp.deviceId);
            Assert.IsFalse(t.PC.InternetConnectivity, "隔離した端末は通信できない");
            Assert.IsTrue(s.HelpDesk.Report(t, "tester"), "PC側のWi-Fiを触らなくても、隔離で完了");
        }

        [Test]
        public void IsolationTicket_StillFailsIfRebooted()
        {
            var s = TestData.Session(16);
            var emp = s.Employees.All[0];
            var t = s.HelpDesk.Create(TestData.FindHelpDesk("R3_ISOLATE"), emp);
            t.PC.Reboot();
            s.Security.Isolate(emp.deviceId);
            Assert.IsFalse(s.HelpDesk.Report(t, "tester"), "再起動したら痕跡が消えているので失敗");
        }

        // 34
        [Test]
        public void IsolationDoesNotSolve_ConnectivityTicket_AndLaterPcStartsIsolated()
        {
            var s = TestData.Session(16);
            var emp = s.Employees.All[0];
            var t = s.HelpDesk.Create(TestData.FindHelpDesk("R6_DHCP"), emp);
            s.Security.Isolate(emp.deviceId);
            t.PC.SetDhcp(true);
            Assert.IsFalse(s.HelpDesk.Report(t, "tester"), "通信を戻す依頼は、隔離しただけでは成功しない");
            var later = s.HelpDesk.Create(TestData.FindHelpDesk("R5_PATCHES"), emp);
            Assert.IsTrue(later.PC.Quarantined);
            Assert.IsFalse(later.PC.InternetConnectivity, "隔離後に作った同じ端末のPCも通信できない");
        }

        // 35
        [Test]
        public void NameResolution_IsSharedAndDistinguishesDnsFromNoNetwork()
        {
            var s = TestData.Session(13);
            var emp = s.Employees.All[0];
            var pc = s.HelpDesk.Create(TestData.FindHelpDesk("R7_DNS"), emp).PC;
            Assert.IsFalse(pc.DnsAuto);
            Assert.AreEqual(TerminalCommands.Resolution.NameNotFound, TerminalCommands.Resolve(pc, "intranet", out _));
            Assert.AreEqual(TerminalCommands.Resolution.Ok, TerminalCommands.Resolve(pc, TerminalCommands.IntranetIp, out var ip));
            Assert.AreEqual(TerminalCommands.IntranetIp, ip);
            pc.SetDnsAuto(true);
            Assert.AreEqual(TerminalCommands.Resolution.Ok, TerminalCommands.Resolve(pc, "intranet", out ip));
            Assert.AreEqual(TerminalCommands.IntranetIp, ip);
            pc.SetWiFi(false);
            Assert.AreEqual(TerminalCommands.Resolution.NoNetwork, TerminalCommands.Resolve(pc, "intranet", out _));
            Assert.AreEqual(TerminalCommands.Resolution.NoNetwork, TerminalCommands.Resolve(pc, TerminalCommands.IntranetIp, out _));
        }

        // 36
        [Test]
        public void GarbledLogs_AreUnreadableEverywhere_UntilRestored()
        {
            var s = TestData.Session(17);
            s.StartShift();
            s.Logs.Add(1, "198.51.100.9", "WEB-SRV01", "-", "HTTP", "OK", "GET /index.html 200");
            var log = s.Logs.Entries[s.Logs.Entries.Count - 1];
            s.Servers.SetDefaced("WEB-SRV01", true);
            Assert.AreNotEqual(log.Detail, UI.Shift.LogViewerView.VisibleDetail(s, log));
            Assert.AreEqual("GET /index.html 200", log.Detail, "元のログは壊さない");
            var st = s.Servers.Find("WEB-SRV01");
            s.Servers.StartRestore(st);
            s.Servers.Tick(s.Time.CurrentMinute + ServerOps.RestoreMinutes + 1);
            Assert.AreEqual(log.Detail, UI.Shift.LogViewerView.VisibleDetail(s, log));
        }

        // 37
        [Test]
        public void Restore_ReturnsKilledLegitProcesses_WithoutDuplicates()
        {
            var s = TestData.Session(17);
            var st = s.Servers.Find("WEB-SRV01");
            var httpd = st.Processes.Find(p => p.Name == "httpd.exe");
            s.Servers.Implant("WEB-SRV01", "x.exe", "T");
            s.Servers.KillProcess(st, httpd);
            Assert.IsFalse(st.AllRunning);
            s.Servers.StartRestore(st);
            s.Servers.Tick(s.Time.CurrentMinute + ServerOps.RestoreMinutes + 1);
            Assert.IsTrue(st.AllRunning);
            Assert.AreEqual(1, st.Processes.FindAll(p => p.Name == "httpd.exe").Count);
            Assert.AreEqual(0, st.Killed.Count);
            Assert.IsFalse(st.Processes.Exists(p => p.Suspicious), "不審なプロセスは残らない");
            s.Servers.RestartService(st, st.Services[0]);
            Assert.AreEqual(1, st.Processes.FindAll(p => p.Name == "httpd.exe").Count, "再起動でも二重にならない");
        }
    }
}
