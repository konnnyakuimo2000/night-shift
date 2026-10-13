using System;
using System.Collections.Generic;
using NightShift.Data;
using NightShift.Events;

namespace NightShift.Systems
{
    /// <summary>端末1台の感染（発生ごとに番号が変わる。駆除後の再感染に古い予約処理が作用しないように）</summary>
    public class Infection
    {
        public string Device;
        public string VirusId;
        public int Serial;
        public float Since;
    }

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
        readonly Dictionary<string, Infection> _infected = new Dictionary<string, Infection>(); // 端末名 → 感染
        int _infectionSerial;
        readonly HashSet<string> _compromisedAccounts = new HashSet<string>(); // 乗っ取られている社員番号
        readonly HashSet<string> _hostileIps = new HashSet<string>();   // 攻撃に使われた接続元（評価用）

        public IReadOnlyList<string> Blocked => _blocked;
        public event Action Changed;
        /// <summary>端末を隔離した（端末名）</summary>
        public event Action<string> DeviceIsolated;
        /// <summary>接続元を遮断した（接続元・範囲）</summary>
        public event Action<string> SourceBlocked;
        /// <summary>サブクエストの対象（不正な取得・転送をしている端末や接続先）か。止めたら正しい対応として数える</summary>
        public Func<string, bool> QuestTargetCheck;

        public SecurityOps(ShiftSession s) { _s = s; }

        // ---------------- 攻撃の記録（評価・判定用。プレイヤーには見せない） ----------------
        public void MarkHostile(string ip) { if (!string.IsNullOrEmpty(ip)) _hostileIps.Add(ip); }
        public bool IsHostile(string ip) => !string.IsNullOrEmpty(ip) && _hostileIps.Contains(ip);
        /// <summary>
        /// 感染させる。1台に同時に感染するのは1種類だけで、すでに感染している端末は先の感染をそのまま残す
        /// （別のウイルスで上書きして消さない）。戻り値はその端末の感染（新しい感染でなければ既存のもの）
        /// </summary>
        public Infection MarkInfected(string device, string virusId)
        {
            if (string.IsNullOrEmpty(device) || string.IsNullOrEmpty(virusId)) return null;
            if (_infected.TryGetValue(device, out var cur)) return cur;
            cur = new Infection { Device = device, VirusId = virusId, Serial = ++_infectionSerial, Since = _s.Time.CurrentMinute };
            _infected[device] = cur;
            Changed?.Invoke();
            return cur;
        }

        /// <summary>駆除。実際に感染を消したときだけ true（二重の駆除・加点を防ぐ）</summary>
        public bool ClearInfection(string device)
        {
            if (device == null || !_infected.Remove(device)) return false;
            Changed?.Invoke();
            return true;
        }

        public bool IsInfected(string device) => !string.IsNullOrEmpty(device) && _infected.ContainsKey(device);
        public string InfectionOf(string device) => device != null && _infected.TryGetValue(device, out var v) ? v.VirusId : null;
        public Infection InfectionRecord(string device) => device != null && _infected.TryGetValue(device, out var v) ? v : null;
        /// <summary>その感染が今も続いているか（駆除後の再感染は別の感染として扱う）</summary>
        public bool IsActive(Infection inf) => inf != null && _infected.TryGetValue(inf.Device, out var cur) && ReferenceEquals(cur, inf);
        public IEnumerable<KeyValuePair<string, string>> Infections
        {
            get { foreach (var kv in _infected) yield return new KeyValuePair<string, string>(kv.Key, kv.Value.VirusId); }
        }

        /// <summary>前の夜から隔離したままの端末（評価には数えない）</summary>
        public void RestoreIsolation(string device)
        {
            if (string.IsNullOrEmpty(device) || !_isolated.Add(device)) return;
            _s.Logs.Add(_s.Time.CurrentMinute, device, "-", "-", "ISOLATE", "CARRIED", "前の夜から隔離中");
            Changed?.Invoke();
        }
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
            SourceBlocked?.Invoke(target);

            // 評価: 攻撃に使われた接続元なら正しい対応。社内の端末・正規の接続元を止めたら業務妨害
            bool hostile = QuestTargetCheck != null && QuestTargetCheck(target);
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
            DeviceIsolated?.Invoke(device);
            if (IsInfected(device) || (QuestTargetCheck != null && QuestTargetCheck(device))) _s.Evaluation.SecurityActionsCorrect++;
            else _s.Evaluation.SecurityActionsWrong++;
            // その端末から届いている保留中の要求は、自動的に拒否（実際の要求の端末で照合する）
            foreach (var e in new List<EventInstance>(_s.Requests.Pending))
                if (e.Device == device) { e.AutoDecided = true; _s.Requests.Decide(e, PlayerAction.Block); }
            // リモートサポートの画面にも、通信できない状態として反映する（管理用の接続は残る）
            foreach (var t in _s.HelpDesk.Tickets)
                if (t.PC != null && t.PC.Hostname == device) t.PC.SetQuarantined(true);
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
