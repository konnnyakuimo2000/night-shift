using System;
using NightShift.Data;
using NightShift.Systems;

namespace NightShift.Events
{
    /// <summary>EventDefinition から EventInstance を組み立てる（社員選択・端末・IPのランダム化）。</summary>
    public class EventFactory
    {
        readonly ShiftSession _s;
        int _instanceCounter;

        public EventFactory(ShiftSession s) { _s = s; }

        Random Rng => _s.Rng;

        public string ExternalIp(string prefix = null)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                string[] pool = { "192.0.2.", "203.0.113." };
                prefix = pool[Rng.Next(pool.Length)];
            }
            return prefix + Rng.Next(11, 250);
        }

        public string HomeIp() => $"100.{Rng.Next(64, 127)}.{Rng.Next(1, 254)}.{Rng.Next(2, 254)}";

        public string ExpandPattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return pattern;
            return pattern.Replace("{n}", Rng.Next(10, 99).ToString())
                          .Replace("{hex}", Rng.Next(0x1000, 0xFFFF).ToString("X4"));
        }

        /// <summary>null = 条件に合う社員がいない等で生成できない</summary>
        public EventInstance Build(EventDefinition def, ThreatScenarioRun run = null)
        {
            var e = new EventInstance
            {
                InstanceId = $"EV-{++_instanceCounter:00000}",
                Definition = def,
                Category = def.category,
                Kind = def.kind,
                IsMalicious = def.isMalicious,
                NotifyAsSecurityAlert = def.notifyAsSecurityAlert,
                Owner = run,
                GeneratedTime = _s.Time.CurrentMinute
            };

            // ---- user ----
            switch (def.userType)
            {
                case UserType.Employee:
                    var emp = _s.Employees.Select(def.employeeConditions);
                    if (emp == null)
                    {
                        UnityEngine.Debug.LogWarning($"[NightShift] {def.eventId}: 条件に合う社員がいないためスキップしました");
                        return null;
                    }
                    e.Employee = emp;
                    e.DisplayUser = emp.fullName;
                    e.DisplayEmployeeId = emp.employeeId;
                    if (def.employeeConditions.preferUnused) _s.Employees.MarkUsed(emp);
                    break;
                case UserType.Unknown:
                    e.DisplayUser = "UNKNOWN";
                    e.DisplayEmployeeId = "-";
                    break;
                case UserType.NamedAccount:
                    e.DisplayUser = string.IsNullOrEmpty(def.accountName) ? "admin" : def.accountName;
                    e.DisplayEmployeeId = "-";
                    // 社員と同名アカウントでも Employee は紐付けない（社員として認証されていない）
                    break;
            }

            // ---- source / device ----
            switch (def.sourceType)
            {
                case SourceType.RegisteredDevice:
                    if (e.Employee == null) return null;
                    e.Device = e.Employee.deviceId;
                    e.SourceIp = _s.Employees.GetIp(e.Employee);
                    e.Zone = SourceZone.Internal;
                    break;
                case SourceType.RegisteredLaptopExternal:
                    if (e.Employee == null || string.IsNullOrEmpty(e.Employee.laptopDeviceId)) return null;
                    e.Device = e.Employee.laptopDeviceId;
                    e.SourceIp = HomeIp();
                    e.Zone = SourceZone.External;
                    break;
                case SourceType.UnregisteredDevice:
                    string dev;
                    int guard = 0;
                    do { dev = ExpandPattern(string.IsNullOrEmpty(def.deviceNamePattern) ? "PC-{hex}" : def.deviceNamePattern); }
                    while (_s.Employees.IsRegisteredDevice(dev) && ++guard < 20);
                    e.Device = dev;
                    e.SourceIp = "10.10.99." + Rng.Next(20, 200);
                    e.Zone = SourceZone.Internal;
                    break;
                case SourceType.ExternalUnknown:
                    e.Device = ExpandPattern(string.IsNullOrEmpty(def.deviceNamePattern) ? "UNKNOWN" : def.deviceNamePattern);
                    e.SourceIp = ExternalIp();
                    e.Zone = SourceZone.External;
                    break;
                case SourceType.OtherEmployeeDevice:
                {
                    if (e.Employee == null) return null;
                    EmployeeData other = null;
                    var all = _s.Employees.All;
                    for (int tries = 0; tries < 20 && other == null; tries++)
                    {
                        var cand = all[Rng.Next(all.Count)];
                        if (cand != e.Employee && !string.IsNullOrEmpty(cand.deviceId)) other = cand;
                    }
                    if (other == null) return null;
                    e.Device = other.deviceId;
                    e.SourceIp = _s.Employees.GetIp(other);
                    e.Zone = SourceZone.Internal;
                    break;
                }
                case SourceType.RegisteredDeviceExternal:
                    if (e.Employee == null) return null;
                    e.Device = e.Employee.deviceId;
                    e.SourceIp = HomeIp();
                    e.Zone = SourceZone.External;
                    break;
                case SourceType.AttackerSource:
                case SourceType.AttackerRelatedSource:
                    e.Device = ExpandPattern(string.IsNullOrEmpty(def.deviceNamePattern) ? "UNKNOWN" : def.deviceNamePattern);
                    e.SourceIp = run != null
                        ? (def.sourceType == SourceType.AttackerRelatedSource ? run.RelatedIp : run.AttackerIp)
                        : ExternalIp("198.51.100.");
                    e.Zone = SourceZone.External;
                    break;
            }

            // ---- destination ----
            if (def.destinationMode == DestinationMode.FromEmployeeNormalTargets && e.Employee != null)
            {
                var targets = e.Employee.normalAccessTargets;
                if (targets.Count == 0) return null;
                for (int tries = 0; tries < 8; tries++)
                {
                    var svc = targets[Rng.Next(targets.Count)];
                    var node = _s.Network.NodeForService(svc);
                    if (node == null) continue;
                    var acc = node.FindByService(svc);
                    e.Destination = node;
                    e.AccessType = acc.accessType;
                    e.ServiceId = svc;
                    break;
                }
                if (e.Destination == null) return null;
            }
            else
            {
                e.Destination = def.destination;
                e.AccessType = def.accessType;
                var acc = def.destination != null ? def.destination.FindAccess(def.accessType) : null;
                e.ServiceId = acc?.serviceId;
            }
            return e;
        }
    }
}
