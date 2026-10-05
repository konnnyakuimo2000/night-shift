using System;
using System.Collections.Generic;
using NightShift.Data;
using NightShift.Events;

namespace NightShift.Systems
{
    /// <summary>
    /// 本番業務の「対応手段」: 接続元の遮断（ファイアウォール）・端末の隔離・アカウントの一時停止・報告。
    /// どれも自動では判断しない。正しかったかは夜の評価で数える。
    /// </summary>
    public class SecurityOps
    {
        readonly ShiftSession _s;
        readonly List<string> _blocked = new List<string>();          // "198.51.100.23" または範囲 "198.51.100.*"
        readonly HashSet<string> _isolated = new HashSet<string>();     // 端末名
        readonly HashSet<string> _suspended = new HashSet<string>();    // 社員番号
        readonly Dictionary<string, string> _infected = new Dictionary<string, string>(); // 端末名 → ウイルスID
        readonly HashSet<string> _compromisedAccounts = new HashSet<string>(); // 乗っ取られている社員番号
        readonly HashSet<string> _hostileIps = new HashSet<string>();   // 攻撃に使われた接続元（評価用）

        public IReadOnlyList<string> Blocked => _blocked;
        public event Action Changed;

        public SecurityOps(ShiftSession s) { _s = s; }

        // ---------------- 攻撃の記録（評価・判定用。プレイヤーには見せない） ----------------
        public void MarkHostile(string ip) { if (!string.IsNullOrEmpty(ip)) _hostileIps.Add(ip); }
        public bool IsHostile(string ip) => !string.IsNullOrEmpty(ip) && _hostileIps.Contains(ip);
        public void MarkInfected(string device, string virusId) { if (!string.IsNullOrEmpty(device)) _infected[device] = virusId; Changed?.Invoke(); }
        public void ClearInfection(string device) { _infected.Remove(device); Changed?.Invoke(); }
        public bool IsInfected(string device) => !string.IsNullOrEmpty(device) && _infected.ContainsKey(device);
        public string InfectionOf(string device) => device != null && _infected.TryGetValue(device, out var v) ? v : null;
        public IEnumerable<KeyValuePair<string, string>> Infections => _infected;
        public void MarkCompromised(string employeeId) { if (!string.IsNullOrEmpty(employeeId)) _compromisedAccounts.Add(employeeId); }
        public bool IsCompromised(string employeeId) => _compromisedAccounts.Contains(employeeId);

        // ---------------- 接続元の遮断 ----------------
        public bool IsBlocked(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;
            foreach (var b in _blocked)
            {
                if (b == ip) return true;
                if (b.EndsWith("*") && ip.StartsWith(b.Substring(0, b.Length - 1))) return true;
            }
            return false;
        }

        static bool IsInternal(string ip) => ip != null && (ip.StartsWith("10.") || ip.StartsWith("172.16."));

        /// <summary>接続元（IP または「198.51.100.*」の範囲）を遮断する。戻り値はメッセージ</summary>
        public string Block(string target)
        {
            if (string.IsNullOrEmpty(target) || target == "-") return "遮断する接続元がありません。";
            if (IsBlocked(target)) return $"{target} はすでに遮断しています。";
            _blocked.Add(target);
            float now = _s.Time.CurrentMinute;
            _s.Logs.Add(now, "FIREWALL", target, _s.OperatorName, "FW", "BLOCK", "接続元を遮断リストに登録");

            // 評価: 攻撃に使われた接続元なら正しい対応。社内の端末・正規の接続元を止めたら業務妨害
            bool hostile = false;
            foreach (var h in _hostileIps) if (h == target || (target.EndsWith("*") && h.StartsWith(target.Substring(0, target.Length - 1)))) hostile = true;
            if (hostile) _s.Evaluation.SecurityActionsCorrect++;
            else
            {
                _s.Evaluation.SecurityActionsWrong++;
                if (IsInternal(target)) _s.Logs.Add(now, "FIREWALL", target, "-", "FW", "WARN", "社内の接続元を遮断（業務に影響）");
            }

            // 遮断した接続元からの保留中の要求は、自動的に拒否
            var pending = new List<EventInstance>(_s.Requests.Pending);
            foreach (var e in pending)
                if (IsBlocked(e.SourceIp)) { e.AutoDecided = true; _s.Requests.Decide(e, PlayerAction.Block); }
            Changed?.Invoke();
            return $"{target} を遮断しました。";
        }

        // ---------------- 端末の隔離 ----------------
        public bool IsIsolated(string device) => !string.IsNullOrEmpty(device) && _isolated.Contains(device);

        public string Isolate(string device)
        {
            if (string.IsNullOrEmpty(device)) return "隔離する端末がありません。";
            if (_isolated.Contains(device)) return $"{device} はすでに隔離しています。";
            _isolated.Add(device);
            _s.Logs.Add(_s.Time.CurrentMinute, device, "-", _s.OperatorName, "ISOLATE", "OK", "端末をネットワークから隔離");
            if (IsInfected(device)) _s.Evaluation.SecurityActionsCorrect++;
            else _s.Evaluation.SecurityActionsWrong++;
            Changed?.Invoke();
            return $"{device} をネットワークから隔離しました。";
        }

        // ---------------- アカウントの一時停止 ----------------
        public bool IsSuspended(string employeeId) => !string.IsNullOrEmpty(employeeId) && _suspended.Contains(employeeId);

        public string Suspend(EmployeeData e)
        {
            if (e == null) return "";
            if (_suspended.Contains(e.employeeId)) return $"{e.employeeId} はすでに停止しています。";
            _suspended.Add(e.employeeId);
            _s.Logs.Add(_s.Time.CurrentMinute, "AUTH-SRV01", "-", _s.OperatorName, "ACCOUNT", "SUSPEND", $"{e.employeeId} のアカウントを一時停止");
            if (IsCompromised(e.employeeId) || !e.IsActive) _s.Evaluation.SecurityActionsCorrect++;
            else _s.Evaluation.SecurityActionsWrong++;
            var pending = new List<EventInstance>(_s.Requests.Pending);
            foreach (var r in pending)
                if (r.Employee == e) { r.AutoDecided = true; _s.Requests.Decide(r, PlayerAction.Block); }
            Changed?.Invoke();
            return $"{e.employeeId} のアカウントを一時停止しました。";
        }

        // ---------------- 報告 ----------------
        readonly HashSet<string> _reported = new HashSet<string>();
        public bool WasReported(string ip) => !string.IsNullOrEmpty(ip) && _reported.Contains(ip);

        /// <summary>報告。correct = 報告すべきもの（不正・不審）だったか。ip = 報告した接続元</summary>
        public string Report(string what, bool correct, string ip = null)
        {
            // 同じ接続元を何度報告しても、評価は最初の1回だけ（報告の連打で点を稼げないように）
            if (!string.IsNullOrEmpty(ip) && !_reported.Add(ip)) return $"{ip} はすでに報告しています。";
            _s.Logs.Add(_s.Time.CurrentMinute, "NIGHT-OPS", "情報システム部", _s.OperatorName, "REPORT", "SENT", what);
            if (correct) { _s.CorrectReports++; _s.Evaluation.SecurityActionsCorrect++; }
            else { _s.WrongReports++; _s.Evaluation.SecurityActionsWrong++; }
            Changed?.Invoke();
            return "情報システム部に報告しました。";
        }
    }
}
