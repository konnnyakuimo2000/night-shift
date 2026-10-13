using System.Collections.Generic;
using NightShift.Data;
using NightShift.Systems;

namespace NightShift.Events
{
    /// <summary>
    /// 攻撃チェーン。研修では人間・自動スクリプトの単純な攻撃、本番では感染・改ざん・乗っ取り・
    /// 自律型攻撃AI（拒否されると経路を変える）まで扱う。
    /// </summary>
    public class ThreatScenarioManager
    {
        readonly ShiftSession _s;
        readonly List<ThreatScenarioRun> _runs = new List<ThreatScenarioRun>();
        TimelineScheduler _scheduler;
        public IReadOnlyList<ThreatScenarioRun> Runs => _runs;

        public TimelineScheduler Scheduler => _scheduler;
        public ShiftSession Session => _s;

        /// <summary>自律型攻撃AIが経路を変えるときに使う接続元の範囲</summary>
        public static readonly string[] AdaptivePrefixes = { "198.51.100.", "203.0.113.", "192.0.2.", "100.70.12.", "100.88.3." };

        public ThreatScenarioManager(ShiftSession s)
        {
            _s = s;
            _s.Requests.Resolved += OnRequestResolved;
            _s.Requests.Resolved += RouteA;
        }

        readonly HashSet<string> _routeAPlanted = new HashSet<string>();

        /// <summary>
        /// 経路 A（VIRUS_CATALOG.md §2）: 悪意ある接続要求を許可すると、接続先のサーバーに GARBLE が置かれ、約25分でページ・ログが書き換わる。
        /// 対象はファイルサーバー・公開Web だけ（管理サーバーは T10 の流れ、メールサーバー → TINNITUS は感染させる端末がないので未接続）。
        /// 拒否・期限切れは感染にしない。1夜に1台につき1回
        /// </summary>
        void RouteA(EventInstance e)
        {
            if (e.State != EventState.Allowed || !e.IsMalicious || e.Destination == null) return;
            string node = e.Destination.nodeId;
            if ((node != "FILE-SRV01" && node != "WEB-SRV01") || !_routeAPlanted.Add(node)) return;
            if (_s.Day.dayNumber < VirusCatalog.Get("V2").DefinitionDay) return; // GARBLE は DAY17 から
            _s.Servers.Implant(node, "svcupd.exe", "GARBLE");
            _s.Logs.Add(_s.Time.CurrentMinute + 0.3f, node, "-", "SYSTEM", "PROCESS", "START", "svcupd.exe");
            _scheduler?.Add(_s.Time.CurrentMinute + 25f, "ROUTE-A GARBLE " + node, () =>
            {
                if (!_s.Servers.HasTag(node, "GARBLE")) return; // 止めていれば何も起きない
                _s.Servers.SetDefaced(node, true);
                _s.Discover("V2", DiscoveryStage.Observed);
                _s.AddDamage($"{node} のファイルとログが書き換えられた（GARBLE）");
            });
        }

        public void ScheduleDay(TimelineScheduler scheduler, DayDefinition day)
        {
            _scheduler = scheduler;
            foreach (var def in day.threats)
            {
                if (def == null || day.dayNumber < def.minimumDay || day.dayNumber > def.maximumDay) continue;
                // 初めて出る日は必ず起きる
                if (def.chance < 1f && def.minimumDay != day.dayNumber && _s.Rng.NextDouble() > def.chance) continue;
                float start = def.startWindow.x + (def.startWindow.y - def.startWindow.x) * (float)_s.Rng.NextDouble();
                Schedule(scheduler, def, start);
            }
        }

        public ThreatScenarioRun Schedule(TimelineScheduler scheduler, ThreatScenarioDefinition def, float start)
        {
            _scheduler = scheduler;
            int last = _s.Rng.Next(20, 230);
            var run = new ThreatScenarioRun
            {
                Definition = def,
                StartTime = start,
                AttackerIp = def.sourceIpPrefix + last,
                RelatedIp = def.sourceIpPrefix + (last + _s.Rng.Next(1, 4)),
                C2Ip = "203.0.113." + _s.Rng.Next(150, 250)
            };
            _runs.Add(run);
            _s.Security.MarkHostile(run.AttackerIp);
            _s.Security.MarkHostile(run.RelatedIp);
            foreach (var step in def.steps)
            {
                for (int i = 0; i < System.Math.Max(1, step.repeat); i++)
                {
                    var st = step;
                    int index = i;
                    float when = start + step.offsetMinutes + i * step.repeatIntervalMinutes;
                    string label = $"THREAT {def.scenarioId}: {step.label}";
                    // チュートリアル中はプレイヤーが判断する要求だけ「1件ずつ」の対象にする（ログは流れる）
                    if (step.kind == ThreatStepKind.ConnectionRequest && when < _s.Day.tutorialGateUntilMinute)
                        scheduler.AddGated(when, _s.Day.tutorialGateMaxDelayMinutes, label, () => ExecuteStep(run, st, index));
                    else
                        scheduler.Add(when, label, () => ExecuteStep(run, st, index));
                }
            }
            return run;
        }

        void ExecuteStep(ThreatScenarioRun run, ThreatStep step, int index)
        {
            if (run.Stopped) return;
            string src = step.useRelatedSource ? run.RelatedIp : run.AttackerIp;
            float now = _s.Time.CurrentMinute;
            bool fw = _s.Security.IsBlocked(src);
            if (step.compromiseUnlessBlocked && index == 0 && !fw && !run.Compromised)
            {
                run.Compromised = true;
                _s.AddDamage(run.Definition.compromiseLog);
            }
            if (fw && (step.kind == ThreatStepKind.Traffic || step.kind == ThreatStepKind.LogEntry))
            {
                _s.Network.Emit(true, step.destination, TrafficResult.Denied);
                _s.Logs.Add(now, src, step.destination != null ? step.destination.nodeId : "-", "-", "FW", "DENY", "遮断中の接続元");
                return;
            }
            switch (step.kind)
            {
                case ThreatStepKind.Traffic:
                    _s.Network.Emit(true, step.destination, step.result == "DENY" ? TrafficResult.Denied : TrafficResult.Delivered);
                    break;
                case ThreatStepKind.LogEntry:
                    _s.Network.Emit(true, step.destination, step.result == "DENY" ? TrafficResult.Denied : TrafficResult.Delivered);
                    string detail = step.detail ?? "";
                    var parts = detail.Split('|');
                    detail = parts[index % parts.Length].Replace("{src}", src);
                    _s.Logs.Add(now, src, step.destination != null ? step.destination.nodeId : "-", "-", step.eventType, step.result, detail);
                    break;
                case ThreatStepKind.ConnectionRequest:
                    SubmitRequest(run, step.request, step.useRelatedSource ? run.RelatedIp : null);
                    break;
                case ThreatStepKind.Script:
                    ThreatScripts.Run(this, run, step, index);
                    break;
            }
        }

        /// <summary>シナリオの接続要求を出す。最初に登場した社員を「被害者」として覚える</summary>
        public EventInstance SubmitRequest(ThreatScenarioRun run, EventDefinition def, string overrideIp = null)
        {
            if (def == null) return null;
            var e = _s.Factory.Build(def, run);
            if (e == null) return null;
            if (!string.IsNullOrEmpty(overrideIp)) e.SourceIp = overrideIp;
            if (run.Victim == null && e.Employee != null) run.Victim = e.Employee;
            if (e.Employee != null && e.IsMalicious) _s.Security.MarkCompromised(e.Employee.employeeId);
            _s.Requests.Submit(e);
            return e;
        }

        void OnRequestResolved(EventInstance e)
        {
            if (!(e.Owner is ThreatScenarioRun run)) return;
            var def = run.Definition;
            if (e.State == EventState.Blocked || e.State == EventState.Expired)
            {
                if (def.adaptive) Adapt(run, e);
                else if (def.stopOnBlock) run.Stopped = true;
            }
            else if (e.State == EventState.Allowed && e.IsMalicious)
            {
                run.Compromised = true;
                run.RequestAllowed = true;
                _s.Logs.Add(_s.Time.CurrentMinute + 0.2f, e.SourceIp, e.DestinationLabel, e.DisplayUser, "AUTH", "SUCCESS", def.compromiseLog);
                if (def.fatalIfAllowed) _s.AddDamage(def.compromiseLog, true);
                else if (def.adaptive) _s.AddDamage(def.compromiseLog);
            }
        }

        /// <summary>自律型攻撃AI: 拒否されると、遮断されていない別の範囲から、少し時間をおいて再挑戦する</summary>
        void Adapt(ThreatScenarioRun run, EventInstance blocked)
        {
            if (_scheduler == null || run.Stopped) return;
            run.Attempts++;
            if (run.Attempts >= run.Definition.maxAttempts)
            {
                run.Stopped = true;
                _s.Logs.Add(_s.Time.CurrentMinute, run.AttackerIp, "-", "-", "IDS", "INFO", "同じ攻撃者と見られる通信が途絶えた");
                return;
            }
            var free = new List<string>();
            foreach (var p in AdaptivePrefixes) if (!_s.Security.IsBlocked(p + "1")) free.Add(p);
            if (free.Count == 0)
            {
                run.Stopped = true;
                _s.Logs.Add(_s.Time.CurrentMinute, "FIREWALL", "-", "-", "FW", "INFO", "攻撃者の経路がすべて遮断された");
                _s.Discover("V8", DiscoveryStage.Removed); // DAYBREAK はウイルスではないので「封じ込め」
                return;
            }
            _s.Discover("V8", DiscoveryStage.Observed);
            string prefix = free[_s.Rng.Next(free.Count)];
            run.AttackerIp = prefix + _s.Rng.Next(20, 240);
            run.RelatedIp = run.AttackerIp;
            _s.Security.MarkHostile(run.AttackerIp);
            string ip = run.AttackerIp;
            float when = _s.Time.CurrentMinute + 2f + (float)_s.Rng.NextDouble() * 3f;
            string dest = blocked.DestinationLabel;
            // 経路を変える前に、新しい接続元からの探索がログに残る（同じ攻撃者だと気付く手がかり）
            _scheduler.Add(when - 1f, "ADAPT-PROBE", () =>
                _s.Logs.Add(_s.Time.CurrentMinute, ip, dest, "-", "FW", "DENY", "TCP 3389 (経路を変えた探索)"));
            var def = blocked.Definition;
            _scheduler.Add(when, "ADAPT " + run.Definition.scenarioId, () => { if (!run.Stopped) SubmitRequest(run, def); });
        }
    }
}
