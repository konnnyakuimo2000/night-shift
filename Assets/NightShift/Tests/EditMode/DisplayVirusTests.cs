using System.Linq;
using NightShift.Core;
using NightShift.Data;
using NightShift.Systems;
using NightShift.UI;
using NUnit.Framework;
using UnityEngine;

namespace NightShift.Tests
{
    /// <summary>画面に作用するウイルスと感染記録（TOOL_REWORK_CLAUDE.md §7）</summary>
    public class DisplayVirusTests : NightShiftTest
    {
        static ThreatScenarioRunHelper Infect(ShiftSession s, string virus)
        {
            var emp = s.Employees.All.First(e => e.IsActive && e.onShift);
            var def = ScriptableObject.CreateInstance<ThreatScenarioDefinition>();
            def.scenarioId = "TEST_" + virus;
            var run = new Events.ThreatScenarioRun { Definition = def, Victim = emp };
            Events.ThreatScripts.Run(s.Director.Threats, run, new ThreatStep { kind = ThreatStepKind.Script, script = "INFECT:" + virus }, 0);
            return new ThreatScenarioRunHelper { Run = run, Victim = emp };
        }

        class ThreatScenarioRunHelper { public Events.ThreatScenarioRun Run; public EmployeeData Victim; }

        // 16
        [TestCase("V9")]
        [TestCase("V10")]
        [TestCase("V11")]
        [TestCase("V12")]
        [TestCase("V13")]
        [TestCase("V14")]
        public void Progress_Isolation_Removal_Recovery(string virus)
        {
            var s = TestData.Session(26);
            var h = Infect(s, virus);
            var d = s.Display.Active.Single();
            s.Display.Tick(10);
            float p1 = d.Progress;
            Assert.Greater(p1, 0f, "症状が進む");
            s.Security.Isolate(h.Victim.deviceId);
            s.Display.Tick(10);
            Assert.AreEqual(p1, d.Progress, 1e-4, "隔離で広がりが止まる");
            Assert.IsTrue(d.Paused);
            Assert.IsTrue(s.Security.IsInfected(h.Victim.deviceId), "隔離は駆除ではない");

            // 手作業の駆除: そのPCでプロセスを終了
            var t = s.HelpDesk.Create(TestData.FindHelpDesk("R5_PATCHES"), h.Victim);
            int before = s.Evaluation.SecurityActionsCorrect;
            var proc = t.PC.RunningProcesses.First(p => p.Name == VirusCatalog.ProcessOf(virus));
            t.PC.KillProcess(proc);
            Assert.IsFalse(s.Security.IsInfected(h.Victim.deviceId));
            Assert.AreEqual(before + 1, s.Evaluation.SecurityActionsCorrect, "加点は一度だけ");
            s.Display.Tick(1);
            if (VirusDex.HasResidualDamage(virus))
            {
                // GLASS: 駆除しても亀裂は残り、画面の再起動で消える
                s.Display.Tick(10);
                Assert.IsTrue(d.Residual);
                Assert.AreEqual(1, s.Display.Active.Count, "再起動までは残る");
                Assert.IsTrue(s.Display.NeedsScreenRestart);
                Assert.IsTrue(s.Display.RestartScreens());
                s.Display.Tick(1);
            }
            Assert.IsTrue(d.Receding);
            s.Display.Tick(10);
            Assert.AreEqual(0, s.Display.Active.Count, "症状が引いて消える");
            Assert.AreEqual((int)DiscoveryStage.Recovered, VirusDex.Find(CareerRecord.Current, virus).stage);
        }

        [Test]
        public void MaxDamage_IsRecordedOnce()
        {
            var s = TestData.Session(26);
            Infect(s, "V11");
            for (int i = 0; i < 10; i++) s.Display.Tick(10);
            Assert.AreEqual(1, s.Damage.Count(x => x.Contains("BLACK INK")));
        }

        // 17
        [Test]
        public void Detection_DependsOnDefinitionDay_AndCandidatesAreNeverDetected()
        {
            Assert.IsFalse(VirusCatalog.Detectable("V11", 22));
            Assert.IsTrue(VirusCatalog.Detectable("V11", 23));
            Assert.IsFalse(VirusCatalog.Detectable("V14", 25));
            Assert.IsTrue(VirusCatalog.Detectable("V14", 26));
            Assert.IsFalse(VirusCatalog.Detectable("V9", 21)); Assert.IsTrue(VirusCatalog.Detectable("V9", 22));
            Assert.IsFalse(VirusCatalog.Detectable("V10", 24)); Assert.IsTrue(VirusCatalog.Detectable("V10", 25));
            Assert.IsFalse(VirusCatalog.Detectable("V13", 26)); Assert.IsTrue(VirusCatalog.Detectable("V13", 27));
            Assert.IsTrue(VirusCatalog.All.All(v => v.Implemented), "全種類を実装した");
            foreach (var id in new[] { "V5", "V7", "V8" }) Assert.IsFalse(VirusCatalog.Detectable(id, 99), id + " はウイルスではない");
            Assert.IsTrue(VirusCatalog.All.Where(v => v.Implemented).All(v => System.Text.RegularExpressions.Regex.IsMatch(v.Name, "^[A-Z ]+$")), "名前は英語だけ");
        }

        [Test]
        public void VaccineRemovesDisplayVirus_OnlyWithDefinitions()
        {
            var s = TestData.Session(26);
            CareerRecord.Current.AddItem(Items.Monitor1);
            CareerRecord.Current.AddItem(Items.Vaccine);
            CareerRecord.Current.vaccineDefinitionDay = 22;
            var h = Infect(s, "V11");
            var t = s.HelpDesk.Create(TestData.FindHelpDesk("R5_PATCHES"), h.Victim);
            s.HelpDesk.Connect(t);
            s.Vaccine.Begin(ScanTarget.Remote);
            s.Time.JumpTo(s.Time.CurrentMinute + 3); s.Vaccine.Tick(s.Time.CurrentMinute);
            Assert.IsTrue(s.Security.IsInfected(h.Victim.deviceId), "定義の前は駆除できない");
            CareerRecord.Current.vaccineDefinitionDay = 23;
            s.Vaccine.Begin(ScanTarget.Remote);
            s.Time.JumpTo(s.Time.CurrentMinute + 3); s.Vaccine.Tick(s.Time.CurrentMinute);
            Assert.IsFalse(s.Security.IsInfected(h.Victim.deviceId));
        }

        // 20（状態側）: 夜の終わりで演出の状態を残さない
        [Test]
        public void NightEnd_ClearsDisplayState()
        {
            var s = TestData.Session(26);
            Infect(s, "V12");
            s.Display.Tick(5);
            s.Complete();
            Assert.AreEqual(0, s.Display.Active.Count);
        }

        // 21・22・23・24
        [Test]
        public void Dex_RevealsByStage_AndViewingChangesNothing()
        {
            var s = TestData.Session(26);
            CareerRecord.Current.Quest("SQ03");
            string before = CareerRecord.Current.Snapshot();
            var go = new GameObject("DexHost", typeof(RectTransform));
            try
            {
                float h = VirusDexView.Build((RectTransform)go.transform, 600, s);
                Assert.Greater(h, 0);
                string all = string.Join("\n", go.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t => t.text));
                StringAssert.DoesNotContain("BLACK INK", all, "未遭遇の名前を出さない");
                StringAssert.DoesNotContain("黒いインク", all, "未遭遇の症状も出さない");
                StringAssert.DoesNotContain("GLASS", all, "未遭遇の名前は出さない");
                StringAssert.Contains("分類不能の観測記録", all);
                StringAssert.DoesNotContain("駆除で解決", all);
                Assert.AreEqual(before, CareerRecord.Current.Snapshot(), "閲覧で記録・クエストが変わらない");
                Assert.AreEqual(0, s.Damage.Count);
                Assert.AreEqual(0, s.Security.Infections.Count());

                s.Discover("V11", DiscoveryStage.Observed);
                foreach (Transform t in go.transform) Object.DestroyImmediate(t.gameObject);
                VirusDexView.Build((RectTransform)go.transform, 600, s);
                all = string.Join("\n", go.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t => t.text));
                StringAssert.Contains("黒いインク", all, "観測で症状を公開");
                StringAssert.DoesNotContain("BLACK INK", all, "名前は検出してから");
                StringAssert.DoesNotContain(VirusCatalog.Get("V11").Fix, all, "対処は駆除してから");

                // 再感染しても段階は下がらない・旧セーブは空から
                s.Discover("V11", DiscoveryStage.Removed);
                s.Discover("V11", DiscoveryStage.Observed);
                Assert.AreEqual(DiscoveryStage.Recovered, VirusDex.StageOf(CareerRecord.Current, "V11"));
                var old = JsonUtility.FromJson<CareerRecord>("{\"version\":1,\"seed\":3}");
                Assert.IsNotNull(old.virusDex);
                Assert.AreEqual(0, old.virusDex.Count);
            }
            finally { Object.DestroyImmediate(go); }
        }
    
        [Test]
        public void Echo_NeverAddsRequestsToTheQueue()
        {
            var s = TestData.Session(26);
            s.StartShift();
            int before = s.Requests.Pending.Count + s.Requests.Processed.Count;
            Infect(s, "V10");
            for (int i = 0; i < 10; i++) s.Display.Tick(10);
            Assert.AreEqual(before, s.Requests.Pending.Count + s.Requests.Processed.Count, "残像は審査の待ち行列に入らない");
        }

        [Test]
        public void ScreenRestart_DoesNotClearActiveInfections()
        {
            var s = TestData.Session(26);
            var h = Infect(s, "V9");
            s.Display.Tick(10);
            Assert.IsFalse(s.Display.RestartScreens(), "感染が続いている間は、再起動しても消えない");
            Assert.AreEqual(1, s.Display.Active.Count);
            Assert.IsTrue(s.Security.IsInfected(h.Victim.deviceId));
        }
}
}
