using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    /// <summary>
    /// 新しいゲームの DAY1 から DAY30 のエンディングまでを、続けて自動でプレイする（リリース前の通し確認）。
    /// 操作役は「手順を守るうまいプレイヤー」: 正解の判断・問い合わせの解決に加え、ログの兆候から隔離・遮断・停止・報告をする。
    /// ツールは買わない。夜をまたぐ状態（記録・所持金・感染・フラグ）はそのまま引き継ぐ。
    /// 例外のログが出たら失敗する。隔離したプロジェクトのコピーで実行する（セーブを消す）。
    /// </summary>
    public class CampaignPlayTests
    {
        GameManager gm;
        ShiftDesktop desktop;
        ShiftSession S => gm.Session;
        string directory;
        readonly StringBuilder report = new StringBuilder();
        static object Call(object o, string name, params object[] args) => o.GetType()
            .GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, args);

        [UnitySetUp] public IEnumerator Setup()
        {
            if (!AuditGuard.Allowed) Assert.Ignore("Requires isolated audit project copy.");
            yield return null;
            gm = GameManager.Instance; Assert.IsNotNull(gm);
            gm.ShowTitle(); yield return null;
            PlayerPrefs.DeleteAll(); CareerRecord.SaveDisabled = false;
            Call(gm, "StartNewGame");
            GameSettings.Current.volume = 0; GameSettings.Current.debugMode = false;
            if (Object.FindAnyObjectByType<AudioListener>() == null) gm.gameObject.AddComponent<AudioListener>();
            directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Campaign"));
            Directory.CreateDirectory(directory);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (gm != null)
            {
                typeof(GameManager).GetField("_completeTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gm, -1f);
                gm.ShowTitle();
            }
            if (AuditGuard.Allowed) PlayerPrefs.DeleteAll();
            yield return null;
        }

        // ---------------- 夜の操作 ----------------
        int _lastLog;
        readonly HashSet<string> _done = new HashSet<string>();

        void ClickRequest(EventInstance e)
        {
            ConnectionMonitorView.Open(desktop, e);
            string prefix = e.Expected == ExpectedAction.Allow ? "接続を許可" : "接続を拒否";
            var w = desktop.Windows.Find(ConnectionMonitorView.Id);
            var b = w.GetComponentsInChildren<RetroButton>().FirstOrDefault(x => x.Label.text.StartsWith(prefix));
            if (b != null && b.Interactable) b.Button.onClick.Invoke();
            else S.Requests.Decide(e, e.Expected == ExpectedAction.Allow ? PlayerAction.Allow : PlayerAction.Block);
        }

        void Fix(Ticket t)
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
                        var browser = t.PC.RunningProcesses.Find(p => p.Name == RemotePC.VirtualPCState.BrowserProcess);
                        if (browser != null) t.PC.KillProcess(browser);
                        break;
                    case "MalwareState":
                        // 手作業の駆除: 感染しているウイルスのプロセスを終了する
                        string proc = VirusCatalog.ProcessOf(t.PC.MalwareState);
                        var p = t.PC.RunningProcesses.Find(x => x.Name == proc);
                        if (p != null) t.PC.KillProcess(p);
                        break;
                    case "Rebooted": case "UnknownToolInstalled": case "LogsCleared": break;
                    default: t.PC.SetValue(c.key, c.value); break;
                }
            }
        }

        string DeviceOf(string ip)
        {
            foreach (var e in S.Employees.All) if (S.Employees.GetIp(e) == ip) return e.deviceId;
            return null;
        }

        static string Range(string ip) { int d = ip.LastIndexOf('.'); return ip.Substring(0, d + 1) + "*"; }

        /// <summary>ログの兆候に手順どおり対応する（ゲームの手がかりだけを使う）</summary>
        void RespondToSigns()
        {
            var logs = S.Logs.Entries;
            var fails = new Dictionary<string, int>();
            var reads = new Dictionary<string, int>();
            for (int i = 0; i < logs.Count; i++)
            {
                var l = logs[i];
                string d = l.Detail ?? "";
                if (l.Result == "FAIL" && !l.Source.StartsWith("10.")) { fails.TryGetValue(l.Source, out int n); fails[l.Source] = n + 1; }
                if (d.StartsWith("FILE READ")) { reads.TryGetValue(l.Source, out int n); reads[l.Source] = n + 1; }
                if (l.Sequence <= _lastLog) continue;
                // 感染の兆候 → 送信元の端末を隔離
                if (d.StartsWith("POST /gate.php") || (d.StartsWith("FILE WRITE") && d.Contains(".briar")) || (l.EventType == "DISPLAY" && l.Result == "SEND"))
                {
                    string dev = DeviceOf(l.Source);
                    if (dev != null && _done.Add("ISO:" + dev)) S.Security.Isolate(dev);
                }
                // 管理サーバーの UPS 設定変更 → そのアカウントを停止
                if (l.EventType == "CONFIG" && d.StartsWith("UPS"))
                {
                    var emp = S.Employees.All.FirstOrDefault(e => S.Employees.GetIp(e) == l.Source);
                    if (emp != null && _done.Add("SUS:" + emp.employeeId)) S.Security.Suspend(emp);
                }
            }
            _lastLog = logs.Count > 0 ? logs[logs.Count - 1].Sequence : _lastLog;
            if (desktop.Unlocked(Features.FirewallBlock))
                foreach (var kv in fails) if (kv.Value >= 5 && _done.Add("BLK:" + Range(kv.Key))) S.Security.Block(Range(kv.Key));
            if (desktop.Unlocked(Features.Report))
                foreach (var kv in reads) if (kv.Value >= 8 && _done.Add("REP:" + kv.Key)) S.Security.Report($"大量の読み込み（{kv.Key}）", true, kv.Key);
            // 自律型の攻撃: 拒否した攻撃の接続元は範囲ごと遮断
            if (desktop.Unlocked(Features.FirewallBlock))
                foreach (var e in S.Requests.Processed)
                    if (e.IsMalicious && e.Zone == SourceZone.External && e.Owner is ThreatScenarioRun run && run.Definition.adaptive && _done.Add("BLK:" + Range(e.SourceIp)))
                        S.Security.Block(Range(e.SourceIp));
            // サーバー: 見慣れないプロセスを止め、書き換えられたら復元
            if (desktop.Unlocked(Features.ServerManagement))
                foreach (var st in S.Servers.Servers)
                {
                    foreach (var p in st.Processes.Where(p => p.Suspicious).ToList()) S.Servers.KillProcess(st, p);
                    if (st.Defaced && !st.Restoring) S.Servers.StartRestore(st);
                }
            // 偽メールは開かずに報告
            if (desktop.Unlocked(Features.Report))
                foreach (var m in S.Mail.Inbox)
                    if (m.IsPhish && !m.Reported) { m.Reported = true; S.Security.Report($"不審なメール「{m.Subject}」", true); }
            // パスワード変更の依頼
            if (S.HasOpenTask(OperatorTask.PasswordChange)) S.MarkPasswordChanged();
        }

        IEnumerator PlayNight()
        {
            desktop = gm.ScreenLayer.GetComponentInChildren<ShiftDesktop>();
            Assert.IsNotNull(desktop, $"DAY{gm.CurrentDay}: デスクトップがない");
            _lastLog = 0; _done.Clear();
            desktop.PendingStart();
            S.Time.DebugTimeScale = 60;
            float deadline = Time.realtimeSinceStartup + 120;
            while (gm.State == GameState.Shift && Time.realtimeSinceStartup < deadline)
            {
                RespondToSigns();
                var e = S.Requests.Pending.FirstOrDefault();
                if (e != null) ClickRequest(e);
                else
                {
                    var t = S.HelpDesk.Tickets.FirstOrDefault(x => x.IsOpen);
                    if (t != null)
                    {
                        if (t.Type == HelpDeskType.Binary) S.HelpDesk.Answer(t, t.CorrectIndex, "campaign");
                        else if (S.HelpDesk.Connect(t) != null) { Fix(t); S.HelpDesk.Report(t, "campaign"); }
                    }
                }
                if (S.OvertimeOffered) S.DeclineOvertime();
                yield return null;
            }
            Assert.AreNotEqual(GameState.Shift, gm.State, $"DAY{gm.CurrentDay}: 時間内に夜が終わらなかった");
        }

        /// <summary>夜の間の画面（ニュース・出勤・ログイン・朝の選択）を進めて、次の業務連絡まで行く</summary>
        IEnumerator ToNextBriefing(int expectDay)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (gm.State == GameState.Briefing && gm.CurrentDay == expectDay) yield break;
                if (gm.State == GameState.ClockIn || gm.State == GameState.Login || gm.State == GameState.DayIntro)
                {
                    // ニュース・タイムカード・ログイン画面は表示できることだけ確かめ、業務連絡へ進む
                    yield return new WaitForSecondsRealtime(0.3f);
                    Call(gm, "ShowBriefing");
                }
                else
                {
                    // 朝の二択などは「続ける」側
                    var b = gm.ScreenLayer.GetComponentsInChildren<RetroButton>().FirstOrDefault(x => x.Interactable && (x.Label.text.Contains("離脱") || x.Label.text.Contains("続ける") || x.Label.text.Contains("断る")));
                    b?.Button.onClick.Invoke();
                }
                yield return null;
            }
            Assert.Fail($"DAY{expectDay} の業務連絡に進めなかった（状態 {gm.State}）");
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator Campaign_Day1To30_ReachesAnEnding()
        {
            report.AppendLine("| DAY | ランク | 点数 | 処理 | 期限切れ | 被害 | 致命的 | 信用 | 所持金 |");
            report.AppendLine("|---:|---|---:|---:|---:|---:|---|---:|---:|");
            gm.StartDay(1); yield return null;
            for (int day = 1; day <= DefaultDataFactory.LastDay; day++)
            {
                Assert.AreEqual(day, gm.CurrentDay);
                yield return PlayNight();
                float deadline = Time.realtimeSinceStartup + 15;
                while (gm.State != GameState.Evaluation && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(GameState.Evaluation, gm.State, $"DAY{day}: 評価に進めなかった");
                var c = CareerRecord.Current;
                var n = c.Night(day);
                report.AppendLine($"| {day} | {n?.rank} | {n?.score} | {S.Requests.Processed.Count} | {S.Evaluation.ExpiredLegit + S.Evaluation.ExpiredBlock} | {S.Damage.Count} | {S.FatalIncident ?? "-"} | {c.trust} | {gm.Wallet.Balance} |");
                File.WriteAllText(Path.Combine(directory, "campaign.md"), "# 30日の通しプレイ\n\n" + report);
                Assert.IsTrue(string.IsNullOrEmpty(S.FatalIncident), $"DAY{day}: 致命的な被害 {S.FatalIncident}");
                var next = gm.ScreenLayer.GetComponentsInChildren<RetroButton>().First(b => b.Label.text.StartsWith("翌日へ"));
                next.Button.onClick.Invoke();
                yield return null;
                if (day == DefaultDataFactory.LastDay) break;
                yield return ToNextBriefing(day + 1);
            }
            yield return new WaitForSecondsRealtime(1);
            // 最後はエンディング（ゲームオーバーではない）
            var texts = string.Join("\n", gm.ScreenLayer.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t => t.text));
            report.AppendLine().AppendLine("最後の画面: " + gm.State).AppendLine().AppendLine("終了時の記録: 信用 " + CareerRecord.Current.trust + " ／ 見たエンディング " + string.Join(",", CareerRecord.Current.endingsSeen));
            File.WriteAllText(Path.Combine(directory, "campaign.md"), "# 30日の通しプレイ\n\n" + report);
            Assert.IsTrue(CareerRecord.Current.endingsSeen.Count > 0, "エンディングに到達");
        }
    }
}
