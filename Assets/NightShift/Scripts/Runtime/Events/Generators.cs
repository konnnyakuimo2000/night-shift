using System.Collections.Generic;
using NightShift.Data;
using NightShift.Systems;

namespace NightShift.Events
{
    /// <summary>
    /// 会社が生きているように見せる背景通信。社員の NormalBehaviorProfile から生成。
    /// 大部分は Network Monitor 上の通信のみ、一部だけログに残る。プレイヤーの判断対象にはしない。
    /// </summary>
    public class NormalEventGenerator
    {
        readonly ShiftSession _s;
        float _accumulator;
        float _visitorAccumulator;
        public bool Enabled = true;

        public NormalEventGenerator(ShiftSession s) { _s = s; }

        public static string ProtocolFor(string service)
        {
            switch (service)
            {
                case "WEB": return "HTTP";
                case "CORP-WEB": return "HTTP";
                case "MAIL": return "SMTP";
                case "FILE": return "SMB";
                case "DEV": return "GIT";
                case "VPN": return "VPN";
                case "ADMIN": return "RDP";
                default: return "APP";
            }
        }

        public void Tick(float deltaMinutes)
        {
            if (!Enabled || deltaMinutes <= 0) return;
            float rate = 0f; // events per minute
            foreach (var e in _s.Employees.All)
                if (e.onShift) rate += e.normalBehavior.activityPerHour / 60f;
            rate *= _s.Day.backgroundActivityScale;
            _accumulator += rate * deltaMinutes;
            int guard = 0;
            while (_accumulator >= 1f && guard++ < 10)
            {
                _accumulator -= 1f + (float)_s.Rng.NextDouble() * 0.5f - 0.25f;
                SpawnEmployeeTraffic();
            }

            // 公開Webへの一般訪問者（攻撃シナリオの通信が自然に紛れる）
            _visitorAccumulator += 0.35f * deltaMinutes;
            if (_visitorAccumulator >= 1f)
            {
                _visitorAccumulator -= 1f;
                SpawnVisitor();
            }
        }

        public void SpawnEmployeeTraffic()
        {
            var emp = PickWeightedEmployee();
            if (emp == null) return;
            var svc = PickService(emp);
            if (svc == null) return;
            var node = _s.Network.NodeForService(svc);
            if (node == null) return;
            bool external = false;
            string ip = _s.Employees.GetIp(emp);
            string device = emp.deviceId;
            if (svc == "VPN") return;
            _s.Network.Emit(external, node, TrafficResult.Delivered);
            if (_s.Rng.NextDouble() < _s.Day.backgroundLogChance)
            {
                var acc = node.FindByService(svc);
                _s.Logs.Add(_s.Time.CurrentMinute, ip, node.nodeId, $"{emp.fullName} ({emp.employeeId})", ProtocolFor(svc), "OK", $"{acc?.accessType} / {device}");
            }
        }

        void SpawnVisitor()
        {
            var web = _s.Network.NodeForService("CORP-WEB");
            if (web == null) return;
            _s.Network.Emit(true, web, TrafficResult.Delivered);
            if (_s.Rng.NextDouble() < 0.5)
            {
                string[] paths = { "GET /", "GET /products/", "GET /recruit/", "GET /news/0518.html", "GET /company/access.html" };
                _s.Logs.Add(_s.Time.CurrentMinute, _s.Factory.ExternalIp(), web.nodeId, "-", "HTTP", "200", paths[_s.Rng.Next(paths.Length)]);
            }
        }

        EmployeeData PickWeightedEmployee()
        {
            float total = 0;
            foreach (var e in _s.Employees.All) if (e.onShift) total += e.normalBehavior.activityPerHour;
            if (total <= 0) return null;
            double r = _s.Rng.NextDouble() * total;
            foreach (var e in _s.Employees.All)
            {
                if (!e.onShift) continue;
                r -= e.normalBehavior.activityPerHour;
                if (r <= 0) return e;
            }
            return null;
        }

        string PickService(EmployeeData e)
        {
            var list = e.normalBehavior.serviceWeights;
            if (list.Count == 0)
                return e.normalAccessTargets.Count > 0 ? e.normalAccessTargets[_s.Rng.Next(e.normalAccessTargets.Count)] : null;
            float total = 0;
            foreach (var w in list) total += w.weight;
            double r = _s.Rng.NextDouble() * total;
            foreach (var w in list)
            {
                r -= w.weight;
                if (r <= 0) return w.serviceId;
            }
            return list[0].serviceId;
        }
    }

    /// <summary>Help Desk 問い合わせの発生。</summary>
    public class HelpDeskGenerator
    {
        readonly ShiftSession _s;
        public HelpDeskGenerator(ShiftSession s) { _s = s; }

        public void ScheduleDay(TimelineScheduler scheduler, DayDefinition day)
        {
            foreach (var def in day.helpDesk)
            {
                if (def == null || day.dayNumber < def.minimumDay || day.dayNumber > def.maximumDay) continue;
                float t = Lerp(def.triggerWindow.x, def.triggerWindow.y, (float)_s.Rng.NextDouble());
                var d = def;
                scheduler.Add(t, "HELPDESK " + def.definitionId, () => Spawn(d));
            }
        }

        public Ticket Spawn(HelpDeskDefinition def)
        {
            var avoid = new List<EmployeeData>();
            foreach (var t in _s.HelpDesk.Tickets) avoid.Add(t.Employee);
            var emp = _s.Employees.Select(def.requester, avoid);
            if (emp == null)
            {
                UnityEngine.Debug.LogWarning($"[NightShift] {def.definitionId}: 条件に合う社員がいません");
                return null;
            }
            return _s.HelpDesk.Create(def, emp);
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    public class ThreatScenarioRun
    {
        public ThreatScenarioDefinition Definition;
        public string AttackerIp;
        public string RelatedIp;
        public bool Stopped;
        public bool Compromised;
        public float StartTime;
    }

    /// <summary>
    /// 小規模な攻撃チェーン。DAY1の攻撃者は人間または一般的な自動スクリプトで、プレイヤー行動への適応はしない。
    /// </summary>
    public class ThreatScenarioManager
    {
        readonly ShiftSession _s;
        readonly List<ThreatScenarioRun> _runs = new List<ThreatScenarioRun>();
        public IReadOnlyList<ThreatScenarioRun> Runs => _runs;

        public ThreatScenarioManager(ShiftSession s)
        {
            _s = s;
            _s.Requests.Resolved += OnRequestResolved;
        }

        public void ScheduleDay(TimelineScheduler scheduler, DayDefinition day)
        {
            foreach (var def in day.threats)
            {
                if (def == null || day.dayNumber < def.minimumDay || day.dayNumber > def.maximumDay) continue;
                float start = def.startWindow.x + (def.startWindow.y - def.startWindow.x) * (float)_s.Rng.NextDouble();
                Schedule(scheduler, def, start);
            }
        }

        public ThreatScenarioRun Schedule(TimelineScheduler scheduler, ThreatScenarioDefinition def, float start)
        {
            int last = _s.Rng.Next(20, 230);
            var run = new ThreatScenarioRun
            {
                Definition = def,
                StartTime = start,
                AttackerIp = def.sourceIpPrefix + last,
                RelatedIp = def.sourceIpPrefix + (last + _s.Rng.Next(1, 4))
            };
            _runs.Add(run);
            foreach (var step in def.steps)
            {
                for (int i = 0; i < System.Math.Max(1, step.repeat); i++)
                {
                    var st = step;
                    int index = i;
                    scheduler.Add(start + step.offsetMinutes + i * step.repeatIntervalMinutes, $"THREAT {def.scenarioId}: {step.label}",
                        () => ExecuteStep(run, st, index));
                }
            }
            return run;
        }

        void ExecuteStep(ThreatScenarioRun run, ThreatStep step, int index)
        {
            if (run.Stopped) return;
            string src = step.useRelatedSource ? run.RelatedIp : run.AttackerIp;
            float now = _s.Time.CurrentMinute;
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
                    if (step.request == null) return;
                    var e = _s.Factory.Build(step.request, run);
                    if (e == null) return;
                    if (step.useRelatedSource) e.SourceIp = run.RelatedIp;
                    _s.Requests.Submit(e);
                    break;
            }
        }

        void OnRequestResolved(EventInstance e)
        {
            if (!(e.Owner is ThreatScenarioRun run)) return;
            if (e.State == EventState.Blocked || e.State == EventState.Expired)
            {
                if (run.Definition.stopOnBlock) run.Stopped = true;
            }
            else if (e.State == EventState.Allowed && e.IsMalicious)
            {
                run.Compromised = true;
                _s.Logs.Add(_s.Time.CurrentMinute + 0.2f, e.SourceIp, e.DestinationLabel, e.DisplayUser, "AUTH", "SUCCESS", run.Definition.compromiseLog);
            }
        }
    }
}
