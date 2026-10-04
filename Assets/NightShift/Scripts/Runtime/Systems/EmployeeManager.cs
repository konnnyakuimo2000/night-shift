using System.Collections.Generic;
using System.Linq;
using NightShift.Data;

namespace NightShift.Systems
{
    public class EmployeeManager
    {
        readonly List<EmployeeData> _employees;
        readonly HashSet<EmployeeData> _usedToday = new HashSet<EmployeeData>();
        readonly System.Random _rng;

        public IReadOnlyList<EmployeeData> All => _employees;

        public EmployeeManager(IEnumerable<EmployeeData> employees, System.Random rng)
        {
            _employees = employees.Where(e => e != null).ToList();
            _rng = rng;
        }

        public EmployeeData FindById(string id) => _employees.FirstOrDefault(e => e.employeeId == id);

        public EmployeeData FindByDevice(string device) => _employees.FirstOrDefault(e => e.OwnsDevice(device));

        public bool IsRegisteredDevice(string device) => FindByDevice(device) != null;

        public IEnumerable<string> Departments => _employees.Select(e => e.department).Distinct();

        public string GetIp(EmployeeData e)
        {
            if (!string.IsNullOrEmpty(e.deviceIp)) return e.deviceIp;
            int idx = _employees.IndexOf(e);
            return "10.10.9." + (20 + idx);
        }

        public bool Matches(EmployeeData e, EmployeeCondition c)
        {
            if (c == null) return true;
            if (c.requireOnShift && !e.onShift) return false;
            if (c.requireOffShift && e.onShift) return false;
            if (c.employment == EmploymentFilter.ActiveOnly && !e.IsActive) return false;
            if (c.employment == EmploymentFilter.NotActive && e.IsActive) return false;
            if (c.departments.Count > 0 && !c.departments.Contains(e.department)) return false;
            if (c.excludeDepartments.Contains(e.department)) return false;
            foreach (var p in c.requiredPermissions) if (p != null && !e.HasPermission(p.permissionId)) return false;
            foreach (var p in c.excludedPermissions) if (p != null && e.HasPermission(p.permissionId)) return false;
            foreach (var t in c.requiredNormalTargets) if (!e.UsesNormally(t)) return false;
            if (c.requireVpnEnabled && !e.vpnEnabled) return false;
            if (c.requireVpnDisabled && e.vpnEnabled) return false;
            if (c.requireLaptop && string.IsNullOrEmpty(e.laptopDeviceId)) return false;
            return true;
        }

        /// <summary>条件に合う社員をランダムに選ぶ。preferUnused の場合は当日未登場の社員を優先。</summary>
        public EmployeeData Select(EmployeeCondition c, ICollection<EmployeeData> avoid = null)
        {
            var candidates = _employees.Where(e => Matches(e, c)).ToList();
            if (candidates.Count == 0) return null;
            var filtered = candidates;
            if (avoid != null)
            {
                var f = filtered.Where(e => !avoid.Contains(e)).ToList();
                if (f.Count > 0) filtered = f;
            }
            if (c != null && c.preferUnused)
            {
                var f = filtered.Where(e => !_usedToday.Contains(e)).ToList();
                if (f.Count > 0) filtered = f;
            }
            var pick = filtered[_rng.Next(filtered.Count)];
            return pick;
        }

        public void MarkUsed(EmployeeData e)
        {
            if (e != null) _usedToday.Add(e);
        }

        public EmployeeData RandomOnShift()
        {
            var list = _employees.Where(e => e.onShift && e.IsActive).ToList();
            return list.Count == 0 ? null : list[_rng.Next(list.Count)];
        }
    }
}
