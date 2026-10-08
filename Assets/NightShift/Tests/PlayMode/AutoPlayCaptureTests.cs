using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using NightShift.UI;
using NightShift.UI.Shift;
using NightShift.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NightShift.Tests
{
    /// <summary>Independent DAY8/20/30 automated runs with real Update and rendered evidence.
    /// Uses answer data for actions; not a human skill/balance simulation. Requires isolated prefs.</summary>
    public class AutoPlayCaptureTests
    {
        GameManager gm;
        ShiftDesktop desktop;
        Camera cam;
        RenderTexture target;
        string directory;
        int sequence;
        ShiftSession S => gm.Session;
        readonly List<string> rows = new List<string>();
        static object Call(object o, string name, params object[] args) => o.GetType()
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, args);

        [UnitySetUp] public IEnumerator Setup()
        {
            if (!AuditGuard.Allowed) Assert.Ignore("Requires isolated audit project copy.");
            yield return null;
            gm = GameManager.Instance; Assert.IsNotNull(gm);
            gm.ShowTitle(); yield return null;
            PlayerPrefs.DeleteAll(); CareerRecord.SaveDisabled = false;
            Call(gm, "StartNewGame");
            GameSettings.Current.uiScalePercent = 100;
            GameSettings.Current.volume = 0; GameSettings.Current.debugMode = false;
            Call(gm, "ApplyUiScale");
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null) gm.gameObject.AddComponent<AudioListener>();
            target = new RenderTexture(1280, 800, 24);
            cam = new GameObject("AutoPlayCaptureCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.targetTexture = target;
            gm.Canvas.renderMode = RenderMode.ScreenSpaceCamera; gm.Canvas.worldCamera = cam; gm.Canvas.planeDistance = 10;
            sequence = 0; rows.Clear();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (gm != null)
            {
                typeof(GameManager).GetField("_completeTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gm, -1f);
                gm.ShowTitle(); gm.Canvas.renderMode = RenderMode.ScreenSpaceOverlay; gm.Canvas.worldCamera = null;
            }
            if (cam != null) { cam.targetTexture = null; UnityEngine.Object.Destroy(cam.gameObject); }
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (AuditGuard.Allowed) PlayerPrefs.DeleteAll();
            yield return null;
        }

        IEnumerator Shot(string label)
        {
            bool wasPaused = S != null && S.Time.Paused;
            if (S != null) S.Time.Paused = true;
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); cam.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply(); RenderTexture.active = previous;
            string file = (++sequence).ToString("00") + "_" + label + ".png";
            File.WriteAllBytes(Path.Combine(directory, file), image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            rows.Add($"| {file} | {S?.Time.Now ?? "-"} | {gm.State} | {S?.Requests.Pending.Count ?? 0} | {S?.HelpDesk.OpenCount ?? 0} |");
            if (S != null) S.Time.Paused = wasPaused;
            File.WriteAllText(Path.Combine(directory, "frames.md"), "| Image | Game time | Screen | Pending requests | Open tickets |\n|---|---|---|---:|---:|\n" + string.Join("\n", rows));
        }

        void ClickRequest(EventInstance e)
        {
            string prefix = e.Expected == ExpectedAction.Allow ? "接続を許可" : "接続を拒否";
            var w = desktop.Windows.Find(ConnectionMonitorView.Id);
            var b = w.GetComponentsInChildren<RetroButton>().First(x => x.Label.text.StartsWith(prefix));
            Assert.IsTrue(b.Interactable); b.Button.onClick.Invoke();
        }

        static void Fix(Ticket t)
        {
            foreach (var c in t.Definition.resolutionConditions)
            {
                switch (c.key)
                {
                    case "InternetConnectivity":
                        if (c.value == "false") t.PC.SetWiFi(false);
                        else { t.PC.SetWiFi(true); t.PC.SetAdapter(true); t.PC.SetDhcp(true); }
                        break;
                    case "LogsSaved": t.PC.SaveLogs(); break;
                    case "BackupDone": t.PC.Backup(); break;
                    case "ReportKind": t.PC.ReportKind = c.value; break;
                    case "FakeAlertOpen":
                        var browser = t.PC.RunningProcesses.Find(p => p.Name == NightShift.RemotePC.VirtualPCState.BrowserProcess);
                        if (browser != null) t.PC.KillProcess(browser);
                        break;
                    case "Rebooted": case "UnknownToolInstalled": case "LogsCleared": break;
                    default: t.PC.SetValue(c.key, c.value); break;
                }
            }
        }

        IEnumerator Play(int day)
        {
            directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AutoPlay", "DAY" + day.ToString("00")));
            Directory.CreateDirectory(directory);
            gm.StartDay(day); yield return null;
            desktop = gm.ScreenLayer.GetComponentInChildren<ShiftDesktop>();
            int seed = S.Seed;
            yield return Shot("briefing");
            desktop.PendingStart(); S.Time.DebugTimeScale = 60;
            ConnectionMonitorView.Open(desktop, null);
            yield return Shot("shift_start");
            int reviewed = 0, tickets = 0, nextHour = 60;
            float deadline = Time.realtimeSinceStartup + 100;
            while (gm.State == GameState.Shift && Time.realtimeSinceStartup < deadline)
            {
                if (S.Time.CurrentMinute >= nextHour && S.Phase == ShiftPhase.Running)
                {
                    LogViewerView.Open(desktop, null); yield return Shot("hour_" + (nextHour / 60)); nextHour += 60;
                }
                var e = S.Requests.Pending.FirstOrDefault();
                if (e != null)
                {
                    ConnectionMonitorView.Open(desktop, e);
                    if (reviewed < 3) yield return Shot("request_" + e.RequestId + "_before");
                    // A request could expire on the frame in which its screen was opened.
                    if (e.State == EventState.Pending) ClickRequest(e);
                    if (reviewed < 3) yield return Shot("request_" + e.RequestId + "_after");
                    reviewed++;
                }
                else
                {
                    var t = S.HelpDesk.Tickets.FirstOrDefault(x => x.IsOpen);
                    if (t != null)
                    {
                        HelpDeskView.Open(desktop, t);
                        if (tickets < 2) yield return Shot("ticket_" + t.TicketId + "_before");
                        if (t.Type == HelpDeskType.Binary) S.HelpDesk.Answer(t, t.CorrectIndex, "autoplay");
                        else
                        {
                            var remote = S.HelpDesk.Connect(t);
                            if (remote != null)
                            {
                                RemoteSupportView.Open(desktop, remote);
                                if (tickets < 2) yield return Shot("ticket_" + t.TicketId + "_remote");
                                Fix(t); S.HelpDesk.Report(t, "autoplay");
                            }
                        }
                        if (tickets < 2) yield return Shot("ticket_" + t.TicketId + "_after");
                        tickets++;
                    }
                }
                yield return null;
            }
            Assert.AreEqual(ShiftPhase.Complete, S.Phase, "Run must finish within bounded real time.");
            yield return Shot("shift_complete");
            while (gm.State != GameState.Evaluation && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(GameState.Evaluation, gm.State);
            foreach (var player in gm.ScreenLayer.GetComponentsInChildren<TimelinePlayer>()) player.Skip();
            yield return Shot("evaluation");
            var result = S.Evaluation.Compute();
            string summary = $"# DAY{day} automatic play\n\nSeed: {seed}\n\nRank: {result.Rank}; score: {result.Overall}; processed: {S.Requests.Processed.Count}; expired: {S.Evaluation.ExpiredLegit + S.Evaluation.ExpiredBlock}; damage: {S.Damage.Count}; fatal: {S.FatalIncident ?? "none"}; pay: {gm.Wallet.Find(day)?.total}\n\n"
                + "Independent fresh start, 60x clock, answer-aware request/button and ticket/system actions. No tools purchased and no proactive threat response. This is execution evidence, not human balance or a continuous campaign. Frames pause only the game clock briefly for layout/rendering.\n\n"
                + string.Join("\n", S.Damage.Select(x => "- " + x));
            File.WriteAllText(Path.Combine(directory, "result.md"), summary);
            var next = gm.ScreenLayer.GetComponentsInChildren<RetroButton>().First(b => b.Label.text.StartsWith("翌日へ"));
            Assert.IsTrue(next.Interactable); next.Button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1);
            yield return Shot(day == 30 ? "ending" : "next_screen");
            Assert.Greater(sequence, 8);
        }

        [UnityTest, Timeout(180000)] public IEnumerator CaptureDay08() { yield return Play(8); }
        [UnityTest, Timeout(180000)] public IEnumerator CaptureDay20() { yield return Play(20); }
        [UnityTest, Timeout(180000)] public IEnumerator CaptureDay30() { yield return Play(30); }
    }
}
