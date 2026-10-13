using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using NightShift.UI;
using NightShift.UI.Shift;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightShift.Tests
{
    // Run in an isolated project copy with companyName NightShiftPlayModeAudit.
    // These tests deliberately exercise real save/retry paths. Never use player saves.
    public class GameplayAuditTests
    {
        GameManager gm;
        ShiftDesktop desktop;
        ShiftSession S => gm.Session;
        static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        [UnitySetUp]
        public IEnumerator Setup()
        {
            if (!AuditGuard.Allowed)
                Assert.Ignore("Run in the isolated project copy described in Docs/PLAYMODE_AUDIT.md.");
            yield return null;
            gm = GameManager.Instance;
            Assert.IsNotNull(gm);
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null)
                gm.gameObject.AddComponent<AudioListener>();
            gm.ShowTitle();
            yield return null;
            PlayerPrefs.DeleteAll(); // Only the isolated test application's prefs.
            CareerRecord.SaveDisabled = false;
            Call(gm, "StartNewGame");
            GameSettings.Current.uiScalePercent = 100;
            GameSettings.Current.volume = 0;
            Call(gm, "ApplyUiScale");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (gm != null)
            {
                // Reset the harness between cases even if a completion-timer assertion failed.
                typeof(GameManager).GetField("_completeTimer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gm, -1f);
                gm.ShowTitle(); yield return null;
            }
            if (AuditGuard.Allowed) PlayerPrefs.DeleteAll();
        }

        IEnumerator Day(int day = 20, bool start = false)
        {
            gm.StartDay(day);
            yield return null;
            desktop = gm.ScreenLayer.GetComponentInChildren<ShiftDesktop>();
            Assert.IsNotNull(desktop);
            if (start) { desktop.PendingStart(); yield return null; }
        }

        EventInstance Request(bool hostile = false)
        {
            var def = hostile ? S.Day.violationPool.Find(x => x.eventId == "T1_SPOOFED_SOURCE") : S.Day.routinePool[0];
            Assert.IsNotNull(def);
            EventInstance e = null;
            for (int i = 0; i < 30 && e == null; i++) e = S.Factory.Build(def);
            Assert.IsNotNull(e);
            S.Requests.Submit(e);
            return e;
        }

        RetroButton Button(Transform root, string prefix) => root.GetComponentsInChildren<RetroButton>()
            .First(b => b.Label.text.StartsWith(prefix, StringComparison.Ordinal));

        void Click(Transform root, string prefix)
        {
            var b = Button(root, prefix);
            Assert.IsTrue(b.Button.IsInteractable(), prefix);
            b.Button.onClick.Invoke();
        }

        void OwnTools()
        {
            foreach (var it in gm.Database.storeItems)
                if (!it.consumable && !CareerRecord.Current.Owns(it.itemId)) CareerRecord.Current.AddItem(it.itemId);
            desktop.RebuildMonitors();
        }

        Ticket Ticket()
        {
            var def = S.Day.helpDeskPool.First(x => x.type == HelpDeskType.Binary);
            return S.HelpDesk.Create(def, S.Employees.All.First(x => x.IsActive && x.onShift));
        }

        [UnityTest] public IEnumerator Briefing_ClockDoesNotAdvance()
        {
            yield return Day();
            float before = S.Time.CurrentMinute;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(GameState.Briefing, gm.State);
            Assert.AreEqual(before, S.Time.CurrentMinute);
        }

        [UnityTest] public IEnumerator StartPauseResume_UsesGameManagerUpdate()
        {
            yield return Day(20, true);
            Assert.AreEqual(GameState.Shift, gm.State);
            desktop.OpenPause();
            float before = S.Time.CurrentMinute;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(before, S.Time.CurrentMinute);
            Click(desktop.transform, "業務に戻る");
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.Greater(S.Time.CurrentMinute, before);
        }

        [UnityTest] public IEnumerator Connection_AllowButtonProcessesExactlyOnce()
        {
            yield return Day();
            var e = Request();
            ConnectionMonitorView.Open(desktop, e);
            yield return null;
            var w = desktop.Windows.Find(ConnectionMonitorView.Id);
            Click(w.transform, "接続を許可");
            yield return null;
            Assert.AreEqual(EventState.Allowed, e.State);
            Assert.AreEqual(1, S.Requests.Processed.Count);
            Assert.IsFalse(Button(w.transform, "接続を許可").Interactable);
        }

        [UnityTest] public IEnumerator Connection_BlockAndRepeatedReport()
        {
            yield return Day();
            var e = Request(true);
            ConnectionMonitorView.Open(desktop, e);
            yield return null;
            var w = desktop.Windows.Find(ConnectionMonitorView.Id);
            Click(w.transform, "報告");
            Assert.IsFalse(Button(w.transform, "報告").Interactable);
            // Also probe the backend duplicate guard, bypassing the now-disabled button.
            Button(w.transform, "報告").Button.onClick.Invoke();
            Assert.AreEqual(1, S.CorrectReports);
            Click(w.transform, "接続を拒否");
            Assert.AreEqual(EventState.Blocked, e.State);
            Assert.AreEqual(1, S.Evaluation.CorrectBlock);
        }

        [UnityTest] public IEnumerator AutoReview_FirstLaunchDoesNotProcessPending()
        {
            yield return Day(); OwnTools();
            var e = Request(true);
            desktop.ShowMonitor(1); Tools.Launch(desktop.Stations[0], Items.AutoReview);
            yield return null;
            Assert.AreEqual(EventState.Pending, e.State);
            Assert.IsFalse(CareerRecord.Current.autoReviewEnabled);
        }

        [UnityTest] public IEnumerator AutoReview_ContinuesOnMainMonitor_StopsWhenClosed()
        {
            yield return Day(); OwnTools();
            CareerRecord.Current.autoReviewConditions.AddRange(AutoReviewTool.Conditions);
            CareerRecord.Current.autoReviewEnabled = true;
            desktop.ShowMonitor(1); Tools.Launch(desktop.Stations[0], Items.AutoReview);
            yield return null;
            desktop.ShowMonitor(0);
            var allowed = Request(); var blocked = Request(true);
            Assert.AreEqual(EventState.Allowed, allowed.State);
            Assert.AreEqual(EventState.Pending, blocked.State);
            desktop.Stations[0].Windows.Find("tool:" + Items.AutoReview).Close();
            yield return null;
            Assert.AreEqual(EventState.Pending, Request().State);
        }

        [UnityTest] public IEnumerator AllTools_OpenAndCloseWithoutExceptions()
        {
            yield return Day(30); OwnTools(); desktop.ShowMonitor(1);
            foreach (var id in new[] { Items.ProfilePanel, Items.AutoReview, Items.LogAnalyzer, Items.FwConsole,
                Items.MailInspector, Items.RemoteKit, Items.Isolator, Items.ServerMonitor, Items.Correlator, Items.Vaccine, Items.SocAi })
            {
                Tools.Launch(desktop.Stations[0], id); yield return null;
                if (ToolApps.IsRetired(id))
                {
                    // 販売終了した商品は、統合先の業務画面が開く（アプリはない）
                    Assert.IsNull(desktop.Stations[0].Windows.Find("tool:" + id), id);
                    continue;
                }
                var app = ToolApps.ForItem(id);
                var w = desktop.Stations[0].Windows.Find("tool:" + app.Id);
                Assert.IsNotNull(w, id); w.Close(); yield return null;
            }
        }

        [UnityTest] public IEnumerator Window_ReopenDoesNotDuplicate_AndRestoresMinimized()
        {
            yield return Day(); ConnectionMonitorView.Open(desktop, null);
            yield return null;
            var w = desktop.Windows.Find(ConnectionMonitorView.Id); int n = desktop.Windows.Windows.Count;
            w.Minimize(); ConnectionMonitorView.Open(desktop, null); yield return null;
            Assert.AreEqual(n, desktop.Windows.Windows.Count); Assert.IsFalse(w.IsMinimized);
            Assert.AreSame(w, desktop.Windows.Active);
        }

        [UnityTest] public IEnumerator Grace_WaitsForHelpDesk_ThenCompletes()
        {
            yield return Day(20, true); var ticket = Ticket();
            S.EndShift(); S.Tick(0.1f);
            Assert.AreEqual(ShiftPhase.Grace, S.Phase);
            S.HelpDesk.Answer(ticket, ticket.CorrectIndex, "tester"); S.Tick(0.1f);
            Assert.AreEqual(ShiftPhase.Complete, S.Phase);
        }

        [UnityTest] public IEnumerator Grace_TimeoutRecordsUnresolved()
        {
            yield return Day(20, true); Ticket(); S.EndShift(); S.Tick(S.Day.graceRealSeconds + 1);
            Assert.AreEqual(ShiftPhase.Complete, S.Phase); Assert.AreEqual(1, S.Evaluation.Unresolved);
        }

        [UnityTest] public IEnumerator Overtime_ConsumesOneCoffee_AndPreservesRequests()
        {
            yield return Day(20, true); CareerRecord.Current.AddItem(Items.Coffee, 2);
            var e = Request(); S.EndShift(); Assert.IsTrue(S.OvertimeOffered);
            S.StartOvertime(); S.StartOvertime();
            Assert.AreEqual(1, CareerRecord.Current.ItemCount(Items.Coffee));
            S.Tick(1); Assert.AreEqual(EventState.Pending, e.State);
            S.Requests.Decide(e, PlayerAction.Allow); S.Tick(0.1f);
            Assert.AreEqual(ShiftPhase.Complete, S.Phase);
        }

        [UnityTest] public IEnumerator Restart_RestoresWalletInventoryAndSession()
        {
            yield return Day(); var old = S; int balance = gm.Wallet.Balance;
            CareerRecord.Current.AddItem(Items.Coffee); gm.Wallet.DebugAddMoney(1000); Request();
            gm.RestartDay(); yield return null;
            Assert.AreNotSame(old, S); Assert.AreEqual(0, S.Requests.Pending.Count);
            Assert.AreEqual(0, CareerRecord.Current.ItemCount(Items.Coffee)); Assert.AreEqual(balance, gm.Wallet.Balance);
            Assert.AreEqual(GameState.Briefing, gm.State);
        }

        [UnityTest] public IEnumerator NewGame_DoesNotResumeOldHighestDay()
        {
            PlayerPrefs.SetInt("NightShift.ReachedDay", 20);
            Call(gm, "StartNewGame"); yield return null;
            Assert.LessOrEqual(gm.ResumeDay, 1, "New game must not offer an old campaign's DAY20.");
        }

        [UnityTest] public IEnumerator Continue_RestoresCheckpointInsteadOfMidnightMutations()
        {
            yield return Day(20);
            int balance = gm.Wallet.Balance;
            CareerRecord.Current.trust = 1; CareerRecord.Current.AddItem(Items.Coffee);
            gm.Wallet.DebugAddMoney(1234);
            Call(gm, "ContinueGame"); yield return null;
            Assert.AreEqual(20, gm.CurrentDay); Assert.AreEqual(70, CareerRecord.Current.trust);
            Assert.AreEqual(0, CareerRecord.Current.ItemCount(Items.Coffee));
            Assert.AreEqual(balance, gm.Wallet.Balance);
        }

        [UnityTest] public IEnumerator WeekRetry_ChangesResumeDayAndRestoresCareer()
        {
            yield return Day(15);
            CareerRecord.Current.trust = 12; yield return Day(17);
            Call(gm, "RetryWeek", 15); yield return null;
            Assert.AreEqual(15, gm.CurrentDay); Assert.AreEqual(15, gm.ResumeDay);
            Assert.AreEqual(70, CareerRecord.Current.trust);
        }

        [UnityTest] public IEnumerator FatalIncident_EvaluationToGameOverToRetry()
        {
            yield return Day(20, true); S.FatalIncident = "audit fatal incident"; S.Complete();
            float until = Time.realtimeSinceStartup + 8;
            while (gm.State != GameState.Evaluation && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(GameState.Evaluation, gm.State);
            foreach (var p in gm.ScreenLayer.GetComponentsInChildren<NightShift.UI.Screens.TimelinePlayer>()) p.Skip();
            yield return null; Click(gm.ScreenLayer, "翌日へ"); yield return null;
            Assert.IsNotNull(gm.ScreenLayer.Find("GameOver"));
            Click(gm.ScreenLayer, "その夜からやり直す"); yield return null;
            Assert.AreEqual(GameState.Briefing, gm.State); Assert.IsNull(S.FatalIncident);
            Assert.AreEqual(20, gm.CurrentDay);
        }

        [UnityTest] public IEnumerator Complete_EvaluationAndNextDay()
        {
            yield return Day(8, true); S.Complete();
            float until = Time.realtimeSinceStartup + 8;
            while (gm.State != GameState.Evaluation && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(GameState.Evaluation, gm.State);
            foreach (var p in gm.ScreenLayer.GetComponentsInChildren<NightShift.UI.Screens.TimelinePlayer>()) p.Skip();
            yield return null;
            Assert.IsNotNull(gm.Wallet.Find(8)); Assert.AreEqual(9, gm.ResumeDay);
            Click(gm.ScreenLayer, "翌日へ"); yield return null;
            Assert.AreEqual(9, gm.CurrentDay);
        }

        [UnityTest] public IEnumerator Complete_RestartBeforeEvaluationDoesNotCrash()
        {
            yield return Day(8, true); S.Complete(); gm.RestartDay();
            yield return new WaitForSecondsRealtime(4);
            Assert.AreEqual(GameState.Briefing, gm.State, "Old completion timer must not evaluate the restarted shift.");
            Assert.IsNull(gm.Wallet.Find(8));
        }

        [UnityTest] public IEnumerator AllThirtyDays_BriefingScreensBuild()
        {
            for (int day = 1; day <= 30; day++)
            {
                yield return Day(day);
                Assert.AreEqual(day, S.Day.dayNumber);
                Assert.IsNotNull(desktop.Windows.Find(MailboxView.Id));
                gm.ShowTitle(); yield return null;
            }
        }

        IEnumerator RunNight(int day)
        {
            yield return Day(day, true);
            S.Time.DebugTimeScale = 120; // Lifecycle stress only, not a human-balance measurement.
            float until = Time.realtimeSinceStartup + 35;
            while (gm.State == GameState.Shift && Time.realtimeSinceStartup < until)
            {
                foreach (var e in S.Requests.Pending.ToArray())
                    S.Requests.Decide(e, e.Expected == ExpectedAction.Allow ? PlayerAction.Allow : PlayerAction.Block);
                foreach (var t in S.HelpDesk.Tickets.Where(t => t.IsOpen && t.Type == HelpDeskType.Binary).ToArray())
                    S.HelpDesk.Answer(t, t.CorrectIndex, "audit");
                yield return null;
            }
            Assert.AreEqual(ShiftPhase.Complete, S.Phase);
            Assert.Greater(S.Requests.Processed.Count, 0);
            while (gm.State != GameState.Evaluation && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(GameState.Evaluation, gm.State);
            Assert.IsNotNull(gm.Wallet.Find(day));
            Assert.IsNotNull(CareerRecord.Current.Night(day));
        }

        [UnityTest, Timeout(60000)] public IEnumerator Day08_FullNightThroughRealUpdate() { yield return RunNight(8); }
        [UnityTest, Timeout(60000)] public IEnumerator Day20_FullNightThroughRealUpdate() { yield return RunNight(20); }
        [UnityTest, Timeout(60000)] public IEnumerator Day30_FullNightThroughRealUpdate() { yield return RunNight(30); }

        [UnityTest] public IEnumerator AutoReview_EnableButtonProcessesOnlyMatchingRequests()
        {
            yield return Day(); OwnTools();
            CareerRecord.Current.autoReviewConditions.AddRange(AutoReviewTool.Conditions);
            var routine = Request(); var spoof = Request(true);
            desktop.ShowMonitor(1); Tools.Launch(desktop.Stations[0], Items.AutoReview);
            yield return null;
            var tool = desktop.Stations[0].Windows.Find("tool:" + Items.AutoReview);
            var button = tool.GetComponentsInChildren<FlatButton>().First(b => b.Label.text == "自動許可を始める");
            Assert.IsTrue(button.Interactable); button.OnClick(); yield return null;
            Assert.AreEqual(EventState.Allowed, routine.State); Assert.AreEqual(EventState.Pending, spoof.State);
        }

        [UnityTest] public IEnumerator Connection_150PercentButtonsFitCanvas()
        {
            yield return Day();
            var rt = new RenderTexture(1280, 800, 24);
            var cameraObject = new GameObject("AuditCamera");
            var cam = cameraObject.AddComponent<Camera>(); cam.targetTexture = rt;
            var originalMode = gm.Canvas.renderMode;
            try
            {
                gm.Canvas.renderMode = RenderMode.ScreenSpaceCamera; gm.Canvas.worldCamera = cam; gm.Canvas.planeDistance = 10;
                GameSettings.Current.uiScalePercent = 150; Call(gm, "ApplyUiScale");
                yield return null; Canvas.ForceUpdateCanvases();
                ConnectionMonitorView.Open(desktop, Request()); yield return null; Canvas.ForceUpdateCanvases();
                var button = Button(desktop.Windows.Find(ConnectionMonitorView.Id).transform, "接続を許可");
                var corners = new Vector3[4]; button.Rect.GetWorldCorners(corners);
                cam.Render();
                var old = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply(); RenderTexture.active = old;
                string dir = System.IO.Path.Combine(Application.dataPath, "..", "Logs");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "AuditConnection150.png"), tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
                foreach (var p in corners)
                {
                    var v = cam.WorldToViewportPoint(p);
                    Assert.That(v.x, Is.InRange(0f, 1f), "Allow button horizontal bounds");
                    Assert.That(v.y, Is.InRange(0f, 1f), "Allow button vertical bounds");
                }
            }
            finally
            {
                gm.Canvas.renderMode = originalMode; gm.Canvas.worldCamera = null;
                cam.targetTexture = null; UnityEngine.Object.Destroy(cameraObject); rt.Release(); UnityEngine.Object.Destroy(rt);
                GameSettings.Current.uiScalePercent = 100; Call(gm, "ApplyUiScale");
            }
        }
    }
}
