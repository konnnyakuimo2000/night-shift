using System.Linq;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using NightShift.Systems.SideQuests;
using NightShift.UI.Shift;
using NUnit.Framework;
using UnityEngine;

namespace NightShift.Tests
{
    /// <summary>VACCINE 99・SOC-AI・購入制限・感染の経路（TOOL_REWORK_CLAUDE.md §2〜4・§11）</summary>
    public class ToolReworkTests : NightShiftTest
    {
        static CareerRecord Career => CareerRecord.Current;

        static void Own(params string[] ids) { foreach (var id in ids) Career.AddItem(id); }

        static Ticket RemoteTicket(ShiftSession s, EmployeeData emp, string def = "R5_PATCHES") => s.HelpDesk.Create(TestData.FindHelpDesk(def), emp);

        static void Advance(ShiftSession s, float minutes)
        {
            float to = s.Time.CurrentMinute + minutes;
            s.Time.JumpTo(to);
            s.Vaccine.Tick(to);
        }

        // ---------------- §2 VACCINE 99 ----------------

        // 5
        [Test]
        public void RemoteScan_IsFixedToTarget_AndAbortsOnSwitchOrDisconnect()
        {
            var s = TestData.Session(17);
            Own(Items.Monitor1, Items.Vaccine);
            Career.vaccineDefinitionDay = 17;
            var a = s.Employees.All[0];
            var b = s.Employees.All[1];
            s.Security.MarkInfected(b.deviceId, "V3");
            var ta = RemoteTicket(s, a);
            var tb = RemoteTicket(s, b);
            s.HelpDesk.Connect(ta);
            StringAssert.Contains(a.deviceId, s.Vaccine.Begin(ScanTarget.Remote));
            s.HelpDesk.Connect(tb); // 検査中に別のPCへ
            Advance(s, 3);
            Assert.IsFalse(s.Vaccine.Running);
            Assert.IsFalse(s.Vaccine.Last.Completed, "中断は未完了として残る");
            Assert.AreEqual(0, s.Vaccine.Last.Found.Count);
            Assert.IsTrue(s.Security.IsInfected(b.deviceId), "接続先のBを駆除しない");

            s.Vaccine.Begin(ScanTarget.Remote); // B を検査中に切断
            s.HelpDesk.Disconnect();
            Advance(s, 3);
            Assert.IsFalse(s.Vaccine.Last.Completed);
            Assert.IsTrue(s.Security.IsInfected(b.deviceId));
        }

        // 6
        [Test]
        public void Scan_DetectsOnlyKnown_RemovesOnce_AndWorkstationScanWorks()
        {
            var s = TestData.Session(17);
            Own(Items.Monitor1, Items.Vaccine);
            Career.vaccineDefinitionDay = 13; // V4 は載っている・V3（DAY16）は載っていない
            var emp = s.Employees.All[0];
            s.Security.MarkInfected(emp.deviceId, "V3");
            var t = RemoteTicket(s, emp);
            s.HelpDesk.Connect(t);
            s.Vaccine.Begin(ScanTarget.Remote);
            Advance(s, 3);
            Assert.IsTrue(s.Vaccine.Last.Completed);
            Assert.AreEqual(0, s.Vaccine.Last.Found.Count);
            Assert.IsTrue(s.Vaccine.Last.MissedUnknown);
            Assert.IsTrue(s.Security.IsInfected(emp.deviceId), "定義にないウイルスは駆除できない");

            // ワークステーション: 添付で感染した BLUESTONE は検査で駆除できる
            s.Security.MarkInfected(VaccineScanner.WorkstationHost, "V4");
            int before = s.Evaluation.SecurityActionsCorrect;
            s.Vaccine.Begin(ScanTarget.Workstation);
            Advance(s, 6);
            Assert.AreEqual(1, s.Vaccine.Last.Found.Count);
            Assert.IsFalse(s.Security.IsInfected(VaccineScanner.WorkstationHost));
            Assert.AreEqual(before + 1, s.Evaluation.SecurityActionsCorrect);
            s.Vaccine.Begin(ScanTarget.Workstation);
            Advance(s, 6);
            Assert.AreEqual(0, s.Vaccine.Last.Found.Count);
            Assert.AreEqual(before + 1, s.Evaluation.SecurityActionsCorrect, "駆除の加点は一度だけ");
        }

        [Test]
        public void ServerScan_NeedsTheExtraLicense()
        {
            var s = TestData.Session(17);
            Own(Items.Monitor1, Items.Vaccine);
            Career.vaccineDefinitionDay = 17;
            s.Vaccine.Begin(ScanTarget.Server);
            Assert.IsFalse(s.Vaccine.Running);
            Own(Items.VaccineServer);
            s.Servers.Implant("WEB-SRV01", "httpd_cache.exe", "T9");
            s.Vaccine.Begin(ScanTarget.Server);
            Advance(s, 6);
            Assert.AreEqual(1, s.Vaccine.Last.Found.Count);
            Assert.IsFalse(s.Servers.HasTag("WEB-SRV01", "T9"));
        }

        // ---------------- §3 SOC-AI ----------------
        static EventInstance PendingRequest(ShiftSession s)
        {
            s.StartShift();
            EventInstance e = null;
            for (int i = 0; i < s.Day.routinePool.Count && e == null; i++) e = TestData.Build(s, s.Day.routinePool[i]);
            s.Requests.Submit(e);
            return e;
        }

        // 7
        [Test]
        public void Ai_DoesNotSpend_OnInvalidRequests()
        {
            var s = TestData.Session(25);
            Own(Items.Monitor1, Items.SocAi);
            var e = PendingRequest(s);
            Assert.IsNull(s.SocAi.Ask(AiTask.Review, e, out var err), "自動審査ツールがないと審査できない");
            StringAssert.Contains("プラグイン", err);
            Own(Items.AutoReview);
            Assert.IsNull(s.SocAi.Ask(AiTask.Review, null, out _), "未選択");
            s.Requests.Decide(e, PlayerAction.Block);
            Assert.IsNull(s.SocAi.Ask(AiTask.Review, e, out _), "処理済み");
            Assert.IsNull(s.SocAi.Ask(AiTask.Logs, null, out _), "ログ解析がない");
            Assert.AreEqual(0, s.AiTokensUsed);
        }

        // 8
        [Test]
        public void Ai_TargetIsFixed_AndRefundHappensOnce()
        {
            var s = TestData.Session(25);
            Own(Items.Monitor1, Items.SocAi, Items.AutoReview);
            var a = PendingRequest(s);
            var b = TestData.Build(s, s.Day.routinePool[0]);
            s.Requests.Submit(b);
            var job = s.SocAi.Ask(AiTask.Review, a, out _);
            Assert.IsNotNull(job);
            Assert.AreEqual(SocAiService.ReviewCost, s.AiTokensUsed);
            var ans = s.SocAi.Complete(job);
            Assert.AreSame(a, ans.Proposal, "考えている間に選択を変えても、対象は受付時の要求");

            var job2 = s.SocAi.Ask(AiTask.Review, b, out _);
            s.Requests.Decide(b, PlayerAction.Block); // 考えている間に処理済み
            var ans2 = s.SocAi.Complete(job2);
            Assert.IsNull(ans2.Proposal);
            Assert.AreEqual(SocAiService.ReviewCost, s.AiTokensUsed, "処理済みになった依頼は返却");
            Assert.IsNull(s.SocAi.Complete(job2), "二重に実行しない");
            s.SocAi.Refund(job2);
            s.SocAi.Cancel();
            Assert.AreEqual(SocAiService.ReviewCost, s.AiTokensUsed, "二重に返却しない");
        }

        // 9
        [Test]
        public void Ai_AutoNeedsAutoReview_AndDoesNotNameUnknownInfections()
        {
            var s = TestData.Session(25);
            Own(Items.Monitor1, Items.SocAi);
            var e = PendingRequest(s);
            Assert.IsNull(s.SocAi.AutoDecide(e, out _));
            Assert.AreEqual(EventState.Pending, e.State);
            Assert.AreEqual(0, s.AiTokensUsed);
            Career.disabledTools.Add(Items.AutoReview);
            Own(Items.AutoReview);
            Assert.IsFalse(SocAiService.CanAuto, "無効にしたツールは使わない");

            Own(Items.Vaccine);
            Career.vaccineDefinitionDay = 10; // NIGHTCRY（DAY16）は載っていない
            var emp = s.Employees.All[0];
            s.Security.MarkInfected(emp.deviceId, "V3");
            s.Logs.Add(s.Time.CurrentMinute, s.Employees.GetIp(emp), "203.0.113.200", emp.fullName, "HTTPS", "OK", "POST /gate.php (" + emp.deviceId + ")");
            var job = s.SocAi.Ask(AiTask.Infections, null, out _);
            var ans = s.SocAi.Complete(job);
            StringAssert.DoesNotContain("NIGHTCRY", ans.Message, "定義にないウイルスを名指ししない");
        }

        // ---------------- §4 購入制限 ----------------

        // 10
        [Test]
        public void TokenPacks_ThreePerNight_DefinitionUpdate_OncePerDay_SurvivesReload()
        {
            var pack = TestData.Db.FindItem(Items.TokenPack);
            var upd = TestData.Db.FindItem(Items.VaccineUpdate);
            for (int i = 0; i < 3; i++)
            {
                Assert.IsNull(StoreView.StockBlockedReason(pack, 25));
                StoreView.ApplyPurchase(Career, pack, 25);
            }
            Assert.IsNotNull(StoreView.StockBlockedReason(pack, 25), "1夜に3回まで");
            Assert.AreEqual(60000, Career.extraTokensTonight, "量と回数は別に数える");
            Career.extraTokensTonight = 0; // 使い切っても回数は戻らない
            Assert.IsNotNull(StoreView.StockBlockedReason(pack, 25));
            Assert.IsNull(StoreView.StockBlockedReason(pack, 26), "次の夜はまた買える");

            Assert.IsNull(StoreView.StockBlockedReason(upd, 12));
            StoreView.ApplyPurchase(Career, upd, 12);
            Assert.IsNotNull(StoreView.StockBlockedReason(upd, 12), "当日版なら買えない");
            var reloaded = JsonUtility.FromJson<CareerRecord>(Career.Snapshot());
            CareerRecord.SetCurrentForTests(reloaded);
            Assert.IsNotNull(StoreView.StockBlockedReason(upd, 12), "再読込しても同じ");
            Assert.IsNotNull(StoreView.StockBlockedReason(pack, 25));
            Assert.IsNull(StoreView.StockBlockedReason(upd, 13));
        }

        [Test]
        public void OtherConsumableLimits_AreUnchanged()
        {
            var coffee = TestData.Db.FindItem(Items.Coffee);
            Career.AddItem(Items.Coffee, coffee.maxStock);
            Assert.IsNotNull(StoreView.StockBlockedReason(coffee, 25));
            var charm = TestData.Db.FindItem(Items.Charm);
            Career.charm.boughtToday = 1;
            Assert.IsNotNull(StoreView.StockBlockedReason(charm, 25));
        }

        // 14
        [Test]
        public void QuestTargets_AreNeverCountedAsManual_WhenAutoProcessed()
        {
            var s = TestData.Session(20);
            var emp = s.Employees.All[0];
            s.Security.Isolate(emp.deviceId);
            var q = new EventInstance { QuestTag = "SQ01-TEST", Employee = emp, DisplayUser = emp.fullName, Device = emp.deviceId, SourceIp = s.Employees.GetIp(emp) };
            s.Requests.Submit(q);
            Assert.AreEqual(EventState.Blocked, q.State);
            Assert.IsFalse(SideQuestManager.ManualBlock(q), "自動拒否は手動の加担・報酬として数えない");
            Assert.IsFalse(AutoReviewTool.Eligible(s, q, TestData.AllConditions()));
            Own(Items.Monitor1, Items.SocAi, Items.AutoReview);
            var q2 = new EventInstance { QuestTag = "SQ02-TEST", Employee = s.Employees.All[1], Device = s.Employees.All[1].deviceId, SourceIp = s.Employees.GetIp(s.Employees.All[1]) };
            s.Requests.Submit(q2);
            Assert.IsNull(s.SocAi.AutoDecide(q2, out _));
            Assert.AreEqual(EventState.Pending, q2.State);
        }

        // ---------------- §11 感染の経路 ----------------
        static ThreatScenarioRun Run(ShiftSession s, EmployeeData victim)
        {
            var def = ScriptableObject.CreateInstance<ThreatScenarioDefinition>();
            def.scenarioId = "TEST";
            return new ThreatScenarioRun { Definition = def, Victim = victim, AttackerIp = "192.0.2.50", C2Ip = "203.0.113.200" };
        }

        static void Step(ShiftSession s, ThreatScenarioRun run, string script, int index = 0) =>
            ThreatScripts.Run(s.Director.Threats, run, new ThreatStep { kind = ThreatStepKind.Script, script = script }, index);

        static int LogsWith(ShiftSession s, string text) => s.Logs.Entries.Count(l => (l.Detail ?? "").Contains(text));

        // 43
        [Test]
        public void Removing_StopsBeaconSpreadAndEncryption_ButKeepsEncryptedFiles()
        {
            var s = TestData.Session(20);
            var emp = s.Employees.All.First(e => e.onShift && e.IsActive);
            var run = Run(s, emp);
            Step(s, run, "INFECT:V3");
            Step(s, run, "BEACON");
            Assert.AreEqual(1, LogsWith(s, "POST /gate.php"));
            s.Security.ClearInfection(emp.deviceId);
            Step(s, run, "BEACON");
            Step(s, run, "SPREAD_CHECK");
            Assert.AreEqual(1, LogsWith(s, "POST /gate.php"), "駆除後は定期通信しない");
            Assert.AreEqual(0, s.Damage.Count, "駆除後は広がらない");

            var other = s.Employees.All.First(e => e != emp && e.IsActive);
            var r2 = Run(s, other);
            Step(s, r2, "INFECT:V6");
            Step(s, r2, "RANSOM_WRITE");
            Assert.AreEqual(1, s.RansomEncryptedFiles);
            s.Security.ClearInfection(other.deviceId);
            Step(s, r2, "RANSOM_WRITE", 1);
            Step(s, r2, "RANSOM_CHECK");
            Assert.AreEqual(1, s.RansomEncryptedFiles, "暗号化済みは残る・以後は増えない");
            Assert.IsTrue(string.IsNullOrEmpty(s.FatalIncident));
        }

        // 44
        [Test]
        public void Spread_SkipsIsolatedTargets_AndRecordsOnlyRealInfections()
        {
            var s = TestData.Session(20);
            var emp = s.Employees.All.First(e => e.onShift && e.IsActive);
            foreach (var e in s.Employees.All) if (e != emp) s.Security.Isolate(e.deviceId);
            var run = Run(s, emp);
            Step(s, run, "INFECT:V3");
            Step(s, run, "SPREAD_CHECK");
            Assert.AreEqual(1, s.Security.Infections.Count());
            Assert.AreEqual(0, s.Damage.Count, "広がらなければ被害にしない");

            var s2 = TestData.Session(20);
            var v = s2.Employees.All.First(e => e.onShift && e.IsActive);
            var r2 = Run(s2, v);
            Step(s2, r2, "INFECT:V3");
            Step(s2, r2, "SPREAD_CHECK");
            int spread = s2.Security.Infections.Count() - 1;
            Assert.AreEqual(spread > 0 ? 1 : 0, s2.Damage.Count);
            if (spread > 0) StringAssert.Contains($"{spread}台", s2.Damage[0]);
        }

        [Test]
        public void Infection_DoesNotOverwriteAnotherVirus()
        {
            var s = TestData.Session(20);
            var emp = s.Employees.All[0];
            s.Security.MarkInfected(emp.deviceId, "V1");
            var run = Run(s, emp);
            Step(s, run, "INFECT:V6");
            Assert.AreEqual("V1", s.Security.InfectionOf(emp.deviceId));
            Assert.IsNull(run.Infection);
            Step(s, run, "RANSOM_WRITE");
            Assert.AreEqual(0, s.RansomEncryptedFiles);
        }

        // 45・46
        [Test]
        public void CarriedInfections_KeepTypeAndIsolation_AndAreNotDuplicated()
        {
            var probe = TestData.Session(21);
            var devs = probe.Employees.All.Where(e => e.IsActive).Take(3).Select(e => e.deviceId).ToArray();
            Career.infections.AddRange(new[] { $"V6:{devs[0]}", $"V3:{devs[1]}:ISO", $"V4:{VaccineScanner.WorkstationHost}", $"V1:{devs[2]}" });
            var s = TestData.Session(21);
            s.StartShift();
            Assert.AreEqual("V6", s.Security.InfectionOf(devs[0]));
            Assert.AreEqual("V3", s.Security.InfectionOf(devs[1]));
            Assert.IsTrue(s.Security.IsIsolated(devs[1]), "隔離は翌夜も続く（感染は消えない）");
            Assert.AreEqual("V4", s.Security.InfectionOf(VaccineScanner.WorkstationHost), "ワークステーションも持ち越す");
            Assert.AreEqual("V1", s.Security.InfectionOf(devs[2]));
            var carry = s.Director.Threats.Runs.Where(r => r.Definition.scenarioId.StartsWith("CARRY_")).ToList();
            Assert.AreEqual(3, carry.Count, "隔離中の感染は活動しない");
            var v6 = carry.First(r => r.Definition.scenarioId.Contains("V6"));
            Assert.IsTrue(v6.Definition.steps.Any(x => x.script == "RANSOM_WRITE"));
            Assert.IsFalse(v6.Definition.steps.Any(x => x.script == "SPREAD_CHECK"), "BRIAR ROSE を NIGHTCRY の拡大に変えない");
            var v1 = carry.First(r => r.Definition.scenarioId.Contains("V1"));
            Assert.IsFalse(v1.Definition.steps.Any(x => x.script == "SPREAD_CHECK"));

            // 夜の開始を繰り返しても重複しない
            s.Director.ScheduleDay(s.Day);
            Assert.AreEqual(3, s.Director.Threats.Runs.Count(r => r.Definition.scenarioId.StartsWith("CARRY_")));

            // 駆除後の再感染に、古い予約処理は作用しない
            s.Security.ClearInfection(devs[0]);
            s.Security.MarkInfected(devs[0], "V6");
            Step(s, v6, "RANSOM_WRITE");
            Assert.AreEqual(0, s.RansomEncryptedFiles);
        }

        [Test]
        public void CarriedInfection_FormatIsBackwardCompatible()
        {
            Assert.IsTrue(Events.EventDirector.CarriedInfection.TryParse("V3:SALES-PC05", out var a));
            Assert.AreEqual("V3", a.VirusId); Assert.AreEqual("SALES-PC05", a.Device); Assert.IsFalse(a.Isolated);
            Assert.IsTrue(Events.EventDirector.CarriedInfection.TryParse("V6:DEV-PC03:ISO", out var b));
            Assert.IsTrue(b.Isolated);
            Assert.AreEqual("V6:DEV-PC03:ISO", b.ToString());
            Assert.IsFalse(Events.EventDirector.CarriedInfection.TryParse("broken", out _));
        }

        // 47
        [Test]
        public void Garble_BlockedBeforeImplant_IsStopped_ButBlockingLaterDoesNotRemoveIt()
        {
            var s = TestData.Session(17);
            var run = Run(s, null);
            s.Security.Block(run.AttackerIp);
            Step(s, run, "WEB_IMPLANT");
            Assert.IsFalse(s.Servers.HasTag("WEB-SRV01", "T9"), "侵入前に遮断すれば配置されない");

            var s2 = TestData.Session(17);
            var r2 = Run(s2, null);
            Step(s2, r2, "WEB_IMPLANT");
            s2.Security.Block(r2.AttackerIp);
            Assert.IsTrue(s2.Servers.HasTag("WEB-SRV01", "T9"), "配置後の遮断は駆除ではない");
            var st = s2.Servers.Find("WEB-SRV01");
            s2.Servers.KillProcess(st, st.Processes.First(p => p.Tag == "T9"));
            Assert.IsFalse(s2.Servers.HasTag("WEB-SRV01", "T9"));
        }

        // 48
        [Test]
        public void Attachment_OpenedThenReported_EachOnce_AndVaccineBlocks()
        {
            var s = TestData.Session(13);
            s.StartShift();
            var m = PhishingMail.SendAttachment(s);
            PhishingMail.OpenAttachment(s, m, false);
            PhishingMail.OpenAttachment(s, m, false);
            Assert.AreEqual(1, s.Damage.Count, "連打で感染・被害が重ならない");
            Assert.IsFalse(m.Reported, "開いた後でも報告できる");
            Assert.IsTrue(s.Security.IsInfected(VaccineScanner.WorkstationHost));

            var s2 = TestData.Session(13);
            s2.StartShift();
            Own(Items.Monitor1, Items.Vaccine);
            Career.vaccineDefinitionDay = 13;
            var m2 = PhishingMail.SendAttachment(s2);
            PhishingMail.OpenAttachment(s2, m2, true);
            Assert.IsTrue(m2.AttachmentBlocked);
            Assert.IsFalse(s2.Security.IsInfected(VaccineScanner.WorkstationHost));
            Career.vaccineDefinitionDay = 12;
            var m3 = PhishingMail.SendAttachment(s2);
            PhishingMail.OpenAttachment(s2, m3, true);
            Assert.IsTrue(s2.Security.IsInfected(VaccineScanner.WorkstationHost), "定義が対応していなければ感染する");
        }

        // 49（経路 D）
        [Test]
        public void RemoteBaitFile_InfectsOnlyThatPc_OnceAndVaccineBlocks()
        {
            var s = TestData.Session(16);
            var emp = s.Employees.All[0];
            var t = RemoteTicket(s, emp);
            t.PC.BaitFile = "写真.jpg.exe";
            Assert.IsTrue(PhishingMail.RunRemoteFile(s, t.PC, t.PC.BaitFile));
            Assert.IsFalse(PhishingMail.RunRemoteFile(s, t.PC, t.PC.BaitFile));
            Assert.AreEqual("V4", s.Security.InfectionOf(emp.deviceId));
            Assert.IsFalse(s.Security.IsInfected(VaccineScanner.WorkstationHost), "担当者の端末は感染しない");
            Assert.AreEqual(1, s.Damage.Count);
        }

        [Test]
        public void VirusDex_RecordsStagesOnce_AndKeepsThemOnReinfection()
        {
            var s = TestData.Session(16);
            s.Discover("V3", DiscoveryStage.Observed);
            s.Discover("V3", DiscoveryStage.Removed);
            s.Discover("V3", DiscoveryStage.Removed);
            s.Discover("V3", DiscoveryStage.Observed); // 再感染
            var e = VirusDex.Find(Career, "V3");
            Assert.AreEqual((int)DiscoveryStage.Recovered, e.stage, "残る被害のない種類は駆除で対応済み・再感染でも下がらない");
            Assert.AreEqual(1, e.history.Count, "同じ夜の同じ対応は1回だけ");
            s.Discover("V6", DiscoveryStage.Removed);
            Assert.AreEqual((int)DiscoveryStage.Removed, VirusDex.Find(Career, "V6").stage, "暗号化が残る種類は、駆除だけでは対応済みにならない");
        }
    }
}
