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
                if (e.onShift && e.IsActive) rate += e.normalBehavior.activityPerHour / 60f;
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
            foreach (var e in _s.Employees.All) if (e.onShift && e.IsActive) total += e.normalBehavior.activityPerHour;
            if (total <= 0) return null;
            double r = _s.Rng.NextDouble() * total;
            foreach (var e in _s.Employees.All)
            {
                if (!e.onShift || !e.IsActive) continue;
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
            if (day.randomEvents) PlanRandom(scheduler, day);
            foreach (var def in day.helpDesk)
            {
                if (def == null || day.dayNumber < def.minimumDay || day.dayNumber > def.maximumDay) continue;
                float t = Lerp(def.triggerWindow.x, def.triggerWindow.y, (float)_s.Rng.NextDouble());
                var d = def;
                if (t < day.tutorialGateUntilMinute)
                    scheduler.AddGated(t, day.tutorialGateMaxDelayMinutes, "HELPDESK " + def.definitionId, () => Spawn(d));
                else
                    scheduler.Add(t, "HELPDESK " + def.definitionId, () => Spawn(d));
            }
        }

        /// <summary>このプレイで、その日に出題できるか（解禁日・回数）</summary>
        public static bool IsAvailable(HelpDeskDefinition def, int day)
        {
            var career = Core.CareerRecord.Current;
            int min = def.randomUnlock ? career.RandomUnlockDay(def.definitionId, def.randomUnlockMin, def.randomUnlockMax) : def.minimumDay;
            if (day < min || day > def.maximumDay) return false;
            return career.HelpDeskCount(def.definitionId) < System.Math.Max(1, def.maxUses);
        }

        /// <summary>本番業務: プールから問い合わせを選ぶ（二択はおよそ4割が「はい」）</summary>
        void PlanRandom(TimelineScheduler scheduler, DayDefinition day)
        {
            var picked = new List<HelpDeskDefinition>();
            int count = _s.SeniorHelp ? System.Math.Max(1, day.helpDeskCount - ShiftSession.SeniorHelpTickets) : day.helpDeskCount;
            for (int i = 0; i < count; i++)
            {
                bool wantYes = _s.Rng.NextDouble() < 0.4;
                var def = Pick(day, picked, d => d.type != HelpDeskType.Binary || d.correctIsYes == wantYes)
                          ?? Pick(day, picked, d => true);
                if (def == null) break;
                picked.Add(def);
                float t = Lerp(day.startMinute + 10f, day.endMinute - 45f, (float)_s.Rng.NextDouble());
                var d2 = def;
                scheduler.Add(t, "HELPDESK " + def.definitionId, () => Spawn(d2));
            }
        }

        HelpDeskDefinition Pick(DayDefinition day, List<HelpDeskDefinition> exclude, System.Predicate<HelpDeskDefinition> filter)
        {
            float total = 0;
            var cands = new List<HelpDeskDefinition>();
            foreach (var d in day.helpDeskPool)
            {
                if (d == null || exclude.Contains(d) || !filter(d) || !IsAvailable(d, day.dayNumber)) continue;
                cands.Add(d);
                total += d.weight;
            }
            if (cands.Count == 0 || total <= 0) return null;
            double r = _s.Rng.NextDouble() * total;
            foreach (var d in cands)
            {
                r -= d.weight;
                if (r <= 0) return d;
            }
            return cands[cands.Count - 1];
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
        public EmployeeData Victim;     // 被害を受ける社員（感染PC・乗っ取り・持ち出しなど）
        public int Attempts;            // 自律型: 経路を変えた回数
        public string C2Ip;             // 感染PCの通信先
        public bool RequestAllowed;     // シナリオの悪意ある要求を、プレイヤーが許可した
    }

    /// <summary>
    /// 小規模な攻撃チェーン。DAY1の攻撃者は人間または一般的な自動スクリプトで、プレイヤー行動への適応はしない。
    /// </summary>
}
