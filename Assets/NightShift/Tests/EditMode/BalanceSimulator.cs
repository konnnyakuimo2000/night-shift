using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using NightShift.UI.Shift;
using NUnit.Framework;

namespace NightShift.Tests
{
    /// <summary>
    /// ゲームバランスの検証用シミュレーター。腕前の違うプレイヤーに DAY8〜30 を自動で遊ばせ、
    /// 仕事量・期限切れ・被害・ランク・給与・購入・エンディングを集計して Logs/BalanceReport.md に書き出す。
    /// 通常のテストでは実行しない（Category "Balance"）。
    /// </summary>
    public class BalanceSimulator : NightShiftTest
    {
        public class Skill
        {
            public string Name;
            public float RequestSec, RequestAccuracy;      // 接続要求1件の審査にかかる実秒・正答率
            public float BinarySec, BinaryAccuracy;        // 二択の問い合わせ
            public float RemoteSec, RemoteSuccess;         // リモート操作
            public float NoticeMinutes, NoticeChance;      // 攻撃に気付くまでのゲーム内分・気付く確率
            public float ActionSec;                        // 遮断・隔離などの操作にかかる実秒
            public float PhishReport, PhishFall;           // 偽メールを報告する確率・引っかかる確率
            public int TrainingPayPerNight;                // 研修7夜の平均手取り
        }

        public static readonly Skill Expert = new Skill { Name = "上級", RequestSec = 8, RequestAccuracy = 0.97f, BinarySec = 8, BinaryAccuracy = 0.95f, RemoteSec = 35, RemoteSuccess = 0.95f, NoticeMinutes = 5, NoticeChance = 0.9f, ActionSec = 8, PhishReport = 0.9f, PhishFall = 0.02f, TrainingPayPerNight = 4300 };
        public static readonly Skill Average = new Skill { Name = "平均", RequestSec = 14, RequestAccuracy = 0.9f, BinarySec = 14, BinaryAccuracy = 0.85f, RemoteSec = 60, RemoteSuccess = 0.8f, NoticeMinutes = 10, NoticeChance = 0.65f, ActionSec = 14, PhishReport = 0.5f, PhishFall = 0.1f, TrainingPayPerNight = 3800 };
        public static readonly Skill Novice = new Skill { Name = "初心者", RequestSec = 22, RequestAccuracy = 0.8f, BinarySec = 22, BinaryAccuracy = 0.7f, RemoteSec = 90, RemoteSuccess = 0.6f, NoticeMinutes = 20, NoticeChance = 0.35f, ActionSec = 20, PhishReport = 0.2f, PhishFall = 0.25f, TrainingPayPerNight = 3200 };

        public class NightStats
        {
            public int Day, Requests, Tickets, Threats, PeakPending, Expired, MaliciousAllowed, PolicyAllowed, FalsePositive, Damage, AutoAllowed;
            public bool Fatal;
            public string Rank;
            public int Score, Pay;
            public float BusyRatio;
        }

        // ============================================================ 1夜のシミュレーション
        public static NightStats SimulateNight(int day, int seed, Skill k, HashSet<string> tools, CareerRecord career)
        {
            CareerRecord.SetCurrentForTests(career);
            var s = TestData.Session(day, seed);
            var rng = new Random(seed * 7919 + day);
            float spm = s.Day.realSecondsPerGameMinute;
            float M(float realSec) => realSec / spm;     // 実秒 → ゲーム内分
            bool Has(string id) => tools.Contains(id) && tools.Contains(Items.Monitor1);
            if (Has(Items.AutoReview)) foreach (var c in AutoReviewTool.Conditions) if (!career.autoReviewConditions.Contains(c)) career.autoReviewConditions.Add(c);

            s.StartShift();
            float busyUntil = 0, busyTotal = 0;
            var handledRuns = new HashSet<ThreatScenarioRun>();
            var mailsSeen = new HashSet<string>();
            int peak = 0;
            float dt = 0.1f;
            float end = s.Day.endMinute;

            for (float t = 0; t <= end + 12; t += dt)
            {
                s.Time.JumpTo(t);
                if (t <= end) s.Director.Tick(t, dt);
                s.Requests.Tick(t);
                s.HelpDesk.Tick(t, dt);
                s.Servers.Tick(t);
                peak = Math.Max(peak, s.Requests.Pending.Count);

                // 自動審査ツール
                if (Has(Items.AutoReview))
                    foreach (var e in new List<EventInstance>(s.Requests.Pending))
                        if (AutoReviewTool.Eligible(s, e, career.autoReviewConditions)) { e.AutoDecided = true; s.AutoAllowed++; s.Requests.Decide(e, PlayerAction.Allow); }

                if (t < busyUntil) continue;
                float cost = 0;

                // 1) 攻撃への対応（気付いたら）
                foreach (var run in s.Director.Threats.Runs)
                {
                    if (handledRuns.Contains(run)) continue;
                    float notice = k.NoticeMinutes * (Has(Items.LogAnalyzer) || Has(Items.ServerMonitor) ? 0.6f : 1f);
                    if (t < run.StartTime + notice) continue;
                    handledRuns.Add(run);
                    // ファイルサーバー監視のアラートが出る攻撃（身代金ウイルス）は気付きやすい
                    float chance = id(run).StartsWith("T11") ? Math.Max(k.NoticeChance, 0.85f) : k.NoticeChance;
                    if (rng.NextDouble() > chance) continue;
                    cost = M(k.ActionSec) + React(s, run, day);
                    break;
                }

                // 2) 偽メール
                if (cost <= 0)
                    foreach (var m in s.Mail.Inbox)
                    {
                        if (!m.IsPhish || mailsSeen.Contains(m.MailId)) continue;
                        mailsSeen.Add(m.MailId);
                        double r = rng.NextDouble();
                        if (r < k.PhishFall)
                        {
                            if (m.ActionKey == MailManager.ActionPhishForm) PhishingMail.Submitted(s, m);
                            else PhishingMail.OpenAttachment(s, m, Has(Items.Vaccine));
                        }
                        else if (r < k.PhishFall + k.PhishReport && s.Day.IsUnlocked(Features.Report)) { m.Reported = true; s.Security.Report("phish", true); }
                        cost = M(10);
                        break;
                    }

                // 3) 接続要求（セキュリティ警告 → 期限の近い順）
                if (cost <= 0 && s.Requests.Pending.Count > 0)
                {
                    EventInstance pick = null;
                    foreach (var e in s.Requests.Pending)
                        if (pick == null || (e.NotifyAsSecurityAlert && !pick.NotifyAsSecurityAlert) || (e.NotifyAsSecurityAlert == pick.NotifyAsSecurityAlert && e.TimeoutAt < pick.TimeoutAt)) pick = e;
                    bool correct = rng.NextDouble() < k.RequestAccuracy;
                    bool allow = (pick.Expected == ExpectedAction.Allow) == correct;
                    s.Requests.Decide(pick, allow ? PlayerAction.Allow : PlayerAction.Block);
                    cost = M(k.RequestSec * (Has(Items.ProfilePanel) ? 0.65f : 1f));
                }

                // 4) 問い合わせ
                if (cost <= 0)
                    foreach (var tk in s.HelpDesk.Tickets)
                    {
                        if (!tk.IsOpen) continue;
                        if (tk.Type == HelpDeskType.Binary)
                        {
                            bool ok = rng.NextDouble() < k.BinaryAccuracy;
                            s.HelpDesk.Answer(tk, ok ? tk.CorrectIndex : 1 - tk.CorrectIndex, "tester");
                            cost = M(k.BinarySec);
                        }
                        else
                        {
                            s.HelpDesk.Connect(tk);
                            if (rng.NextDouble() < k.RemoteSuccess) ApplyFix(tk);
                            s.HelpDesk.Report(tk, "tester");
                            if (tk.IsOpen) { ApplyFix(tk); s.HelpDesk.Report(tk, "tester"); } // 2回目で直す
                            cost = M(k.RemoteSec * (Has(Items.RemoteKit) ? 0.7f : 1f));
                        }
                        break;
                    }

                if (cost > 0) { busyUntil = t + cost; busyTotal += cost; }
            }
            s.Complete();

            var result = s.Evaluation.Compute();
            var slip = Payroll.Compute(day, result, s.Evaluation, TestData.Db.payroll);
            NightClosing.Close(s, result, slip);
            var ev = s.Evaluation;
            return new NightStats
            {
                Day = day, Requests = s.Requests.Processed.Count, Tickets = s.HelpDesk.Tickets.Count, Threats = s.Director.Threats.Runs.Count,
                PeakPending = peak, Expired = ev.ExpiredLegit + ev.ExpiredBlock, MaliciousAllowed = ev.MaliciousAllow, PolicyAllowed = ev.PolicyViolation,
                FalsePositive = ev.FalsePositive, Damage = s.Damage.Count, Fatal = !string.IsNullOrEmpty(s.FatalIncident),
                Rank = result.Rank, Score = result.Overall, Pay = slip.total, AutoAllowed = s.AutoAllowed, BusyRatio = busyTotal / end
            };
        }

        static string id(ThreatScenarioRun r) => r.Definition.scenarioId;

        /// <summary>攻撃に気付いたときの正しい対応。追加でかかるゲーム内分を返す</summary>
        static float React(ShiftSession s, ThreatScenarioRun run, int day)
        {
            string id = run.Definition.scenarioId;
            if (id.StartsWith("T3") && s.Day.IsUnlocked(Features.FirewallBlock)) s.Security.Block(run.AttackerIp);
            else if ((id.StartsWith("T7") || id.StartsWith("T11") || id.StartsWith("CARRY")) && run.Victim != null && s.Day.IsUnlocked(Features.Isolation)) s.Security.Isolate(run.Victim.deviceId);
            else if (id.StartsWith("T8") && run.Victim != null) s.Security.Report("exfil", true, s.Employees.GetIp(run.Victim));
            else if (id.StartsWith("T9"))
            {
                var st = s.Servers.Find("WEB-SRV01");
                var p = st?.Processes.Find(x => x.Tag == "T9");
                if (p != null) s.Servers.KillProcess(st, p);
            }
            else if (id.StartsWith("T10") && run.Victim != null && s.Day.IsUnlocked(Features.UserManagement)) s.Security.Suspend(run.Victim);
            else if (id.StartsWith("T14")) s.Security.Block(run.AttackerIp);
            return 0;
        }

        /// <summary>リモート操作の正しい対処（解決条件を満たす）</summary>
        static void ApplyFix(Ticket t)
        {
            var pc = t.PC;
            foreach (var c in t.Definition.resolutionConditions)
            {
                switch (c.key)
                {
                    case "InternetConnectivity":
                        if (c.value == "false") pc.SetWiFi(false);
                        else { pc.SetWiFi(true); pc.SetAdapter(true); pc.SetDhcp(true); }
                        break;
                    case "LogsSaved": pc.SaveLogs(); break;
                    case "BackupDone": pc.Backup(); break;
                    case "ReportKind": pc.ReportKind = c.value; break;
                    case "FakeAlertOpen":
                        var b = pc.RunningProcesses.Find(x => x.Name == RemotePC.VirtualPCState.BrowserProcess);
                        if (b != null) pc.KillProcess(b);
                        break;
                    case "Rebooted": case "UnknownToolInstalled": case "LogsCleared": break;
                    default: pc.SetValue(c.key, c.value); break;
                }
            }
        }

        // ============================================================ 30日間（購入しながら）
        public class Campaign
        {
            public Skill Skill;
            public bool BuyTools;
            public List<NightStats> Nights = new List<NightStats>();
            public List<string> Purchases = new List<string>();
            public int Money, Fines;
            public string GameOver;
            public string Ending;
            public int Trust;
        }

        static readonly string[] BuyOrder =
        {
            Items.Monitor1, Items.ProfilePanel, Items.AutoReview, Items.LogAnalyzer, Items.Vaccine, Items.FwConsole,
            Items.Isolator, Items.RemoteKit, Items.ServerMonitor, Items.MailInspector, Items.Correlator, Items.Monitor2
        };

        public static Campaign RunCampaign(Skill k, bool buyTools, int seed)
        {
            var career = new CareerRecord { seed = seed, trust = 70 };
            var c = new Campaign { Skill = k, BuyTools = buyTools };
            int money = k.TrainingPayPerNight * 7;
            var tools = new HashSet<string>();
            for (int day = 8; day <= 30; day++)
            {
                if (buyTools)
                    foreach (var id in BuyOrder)
                    {
                        if (tools.Contains(id)) continue;
                        var it = TestData.Db.FindItem(id);
                        if (it == null || it.unlockDay > day || money < it.price) continue;
                        if (!string.IsNullOrEmpty(it.prerequisite) && !tools.Contains(it.prerequisite)) continue;
                        money -= it.price;
                        tools.Add(id);
                        career.AddItem(id);
                        if (id == Items.Vaccine) career.vaccineDefinitionDay = day;
                        c.Purchases.Add($"DAY{day} {it.displayName}");
                    }
                var n = SimulateNight(day, seed * 100 + day, k, tools, career);
                c.Nights.Add(n);
                money += n.Pay;
                var tmp = TestData.Session(day, 1);
                tmp.FatalIncident = n.Fatal ? "fatal" : null;
                tmp.Evaluation.MaliciousAllow = n.MaliciousAllowed;
                var go = EndingResolver.CheckGameOver(career, tmp);
                if (go != null) { c.GameOver = $"{go.Id}（DAY{day}）"; break; }
            }
            c.Money = money;
            c.Trust = career.trust;
            foreach (var r in career.nights) c.Fines += r.fines;
            if (c.GameOver == null)
            {
                var last = c.Nights[c.Nights.Count - 1];
                var outcome = last.Fatal ? FinalOutcome.Critical : last.Damage > 0 || last.MaliciousAllowed > 0 ? FinalOutcome.PartialDamage : FinalOutcome.Defended;
                var ending = EndingResolver.Resolve(career, outcome);
                c.Ending = $"{ending.Id} {ending.Title}";
            }
            return c;
        }

        // ============================================================ レポート
        [Test, Category("Balance"), Explicit("バランス検証用。-testCategory Balance で実行")]
        public void WriteBalanceReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# NIGHT//SHIFT バランス検証レポート（自動生成）");
            sb.AppendLine();
            var skills = new[] { Expert, Average, Novice };
            const int Seeds = 4;

            // ---- 1夜ごとの仕事量と結果（ツールなし） ----
            foreach (var withTools in new[] { false, true })
            {
                sb.AppendLine(withTools ? "## 1夜の結果（ツールあり: サブモニター・社員情報パネル・自動審査・ログ解析・リモート診断キット）" : "## 1夜の結果（ツールなし）");
                sb.AppendLine();
                sb.AppendLine("| DAY | 腕前 | 要求 | 問合せ | 攻撃 | 最大保留 | 期限切れ | 不正許可 | 違反許可 | 誤拒否 | 被害 | 致命 | 平均点 | ランク | 手取り | 稼働率 |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                var tools = withTools ? new HashSet<string> { Items.Monitor1, Items.ProfilePanel, Items.AutoReview, Items.LogAnalyzer, Items.RemoteKit } : new HashSet<string>();
                foreach (int day in new[] { 8, 11, 14, 17, 20, 23, 26, 28, 30 })
                    foreach (var k in skills)
                    {
                        float req = 0, tk = 0, th = 0, peak = 0, exp = 0, mal = 0, pol = 0, fp = 0, dmg = 0, fatal = 0, score = 0, pay = 0, busy = 0;
                        var ranks = new Dictionary<string, int>();
                        for (int seed = 1; seed <= Seeds; seed++)
                        {
                            var career = new CareerRecord { seed = seed, trust = 70 };
                            var n = SimulateNight(day, seed, k, tools, career);
                            req += n.Requests; tk += n.Tickets; th += n.Threats; peak += n.PeakPending; exp += n.Expired; mal += n.MaliciousAllowed;
                            pol += n.PolicyAllowed; fp += n.FalsePositive; dmg += n.Damage; fatal += n.Fatal ? 1 : 0; score += n.Score; pay += n.Pay; busy += n.BusyRatio;
                            ranks[n.Rank] = ranks.TryGetValue(n.Rank, out int r) ? r + 1 : 1;
                        }
                        float d = Seeds;
                        var rk = new StringBuilder();
                        foreach (var kv in ranks) rk.Append($"{kv.Key}{kv.Value} ");
                        sb.AppendLine($"| {day} | {k.Name} | {req / d:0.0} | {tk / d:0.0} | {th / d:0.0} | {peak / d:0.0} | {exp / d:0.0} | {mal / d:0.0} | {pol / d:0.0} | {fp / d:0.0} | {dmg / d:0.0} | {fatal / d:P0} | {score / d:0} | {rk} | ¥{pay / d:N0} | {busy / d:P0} |");
                    }
                sb.AppendLine();
            }

            // ---- 30日間（購入しながら） ----
            sb.AppendLine("## 30日間の通し（DAY8〜30）");
            sb.AppendLine();
            sb.AppendLine("| 腕前 | ツール購入 | 種 | 結末 | 信用 | 所持金 | 罰金合計 | 平均ランク | 購入（日） |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var k in skills)
                foreach (var buy in new[] { false, true })
                    for (int seed = 1; seed <= 3; seed++)
                    {
                        var c = RunCampaign(k, buy, seed);
                        float avg = 0; foreach (var n in c.Nights) avg += CareerRecord.RankValue(n.Rank);
                        avg /= Math.Max(1, c.Nights.Count);
                        sb.AppendLine($"| {k.Name} | {(buy ? "する" : "しない")} | {seed} | {c.GameOver ?? c.Ending} | {c.Trust} | ¥{c.Money:N0} | ¥{c.Fines:N0} | {avg:0.00} | {string.Join("、", c.Purchases)} |");
                    }

            string dir = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "BalanceReport.md"), sb.ToString(), Encoding.UTF8);
            UnityEngine.Debug.Log("[Balance] " + Path.Combine(dir, "BalanceReport.md"));
        }
    }
}
