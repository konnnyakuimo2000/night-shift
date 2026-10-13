using System;
using System.Collections.Generic;
using NightShift.Data;

namespace NightShift.Systems
{
    public class ServerProcess
    {
        public string Name;
        public string Publisher;
        public int MemoryKb;
        public bool Suspicious;   // 攻撃者が置いたもの（プレイヤーには見せない）
        public bool Critical;     // 止めるとサービスが止まる
        public string Tag;        // 脅威スクリプトが参照する目印
    }

    public class ServiceState
    {
        public string Name;
        public bool Running = true;
    }

    public class ServerState
    {
        public NetworkNodeDefinition Node;
        public readonly List<ServiceState> Services = new List<ServiceState>();
        public readonly List<ServerProcess> Processes = new List<ServerProcess>();
        public readonly List<ServerProcess> Killed = new List<ServerProcess>(); // 終了させた正規のプロセス（再起動で戻る）
        public bool Defaced;          // 公開Webの改ざん
        public bool Garbled;          // ファイル・ログの書き換え（V2 GARBLE）
        public bool Restoring;
        public float RestoreDoneAt;
        public string Id => Node.nodeId;

        public bool AllRunning
        {
            get { foreach (var s in Services) if (!s.Running) return false; return true; }
        }

        public string StatusLabel => Restoring ? "復元中" : Defaced ? "改ざん" : !AllRunning ? "停止あり" : "稼働中";
    }

    /// <summary>
    /// サーバー運用管理（DAY15〜）。サービスの状態・プロセス・バックアップからの復元。
    /// 不審かどうかは表示しない（発行元・名前を見て判断するのはプレイヤー）。
    /// </summary>
    public class ServerOps
    {
        readonly ShiftSession _s;
        readonly List<ServerState> _servers = new List<ServerState>();
        public IReadOnlyList<ServerState> Servers => _servers;
        public event Action Changed;
        public const float RestoreMinutes = 15f;

        public ServerOps(ShiftSession s)
        {
            _s = s;
            foreach (var n in s.Network.Nodes)
            {
                if (n == null || n.role != NodeRole.Server) continue;
                var st = new ServerState { Node = n };
                var seen = new HashSet<string>();
                foreach (var a in n.accessTypes)
                    if (!string.IsNullOrEmpty(a.serviceId) && seen.Add(a.serviceId))
                        st.Services.Add(new ServiceState { Name = ServiceLabel(a.serviceId) });
                void P(string name, string pub, int kb, bool crit) => st.Processes.Add(new ServerProcess { Name = name, Publisher = pub, MemoryKb = kb, Critical = crit });
                P("System", "OS", 220, true);
                P("services.exe", "OS", 4100, true);
                P("svchost.exe", "OS", 8800, true);
                P("nxagent.exe", "NEXUS Systems", 3300, false);
                switch (n.nodeId)
                {
                    case "WEB-SRV01": P("httpd.exe", "NEXUS Systems", 22100, true); break;
                    case "MAIL-SRV01": P("smtpd.exe", "NEXUS Systems", 15400, true); P("pop3d.exe", "NEXUS Systems", 6200, true); break;
                    case "FILE-SRV01": P("smbd.exe", "NEXUS Systems", 18800, true); P("backupd.exe", "NEXUS Systems", 5100, false); break;
                    case "APP-SRV01": P("appsrv.exe", "NEXUS Systems", 41200, true); P("dbsrv.exe", "NEXUS Systems", 61000, true); break;
                    case "AUTH-SRV01": P("authsvc.exe", "NEXUS Systems", 9900, true); P("vpngw.exe", "NEXUS Systems", 7300, true); break;
                    case "ADMIN-SRV01": P("admsvc.exe", "NEXUS Systems", 12000, true); P("upsmon.exe", "PowerGuard", 2100, false); break;
                }
                _servers.Add(st);
            }
        }

        static string ServiceLabel(string svc)
        {
            switch (svc)
            {
                case "CORP-WEB": return "公開Web (HTTP)";
                case "MAIL": return "メール (SMTP/POP3)";
                case "FILE": return "共有ファイル (SMB)";
                case "DEV": return "開発環境";
                case "HR": return "人事システム";
                case "ACCOUNTING": return "経理システム";
                case "VPN": return "VPN";
                case "ADMIN": return "管理コンソール";
                default: return svc;
            }
        }

        public ServerState Find(string nodeId) => _servers.Find(x => x.Id == nodeId);

        // ---------------- 脅威スクリプトから ----------------
        public ServerProcess Implant(string nodeId, string name, string tag, int kb = 2600)
        {
            var st = Find(nodeId);
            if (st == null) return null;
            var p = new ServerProcess { Name = name, Publisher = "(不明)", MemoryKb = kb, Suspicious = true, Tag = tag };
            st.Processes.Add(p);
            Changed?.Invoke();
            return p;
        }

        public bool HasTag(string nodeId, string tag)
        {
            var st = Find(nodeId);
            if (st == null) return false;
            foreach (var p in st.Processes) if (p.Tag == tag) return true;
            return false;
        }

        public void SetDefaced(string nodeId, bool garble)
        {
            var st = Find(nodeId);
            if (st == null) return;
            st.Defaced = true;
            if (garble) st.Garbled = true;
            Changed?.Invoke();
        }

        // ---------------- プレイヤーの操作 ----------------
        public string KillProcess(ServerState st, ServerProcess p)
        {
            if (st == null || p == null || !st.Processes.Contains(p)) return "";
            st.Processes.Remove(p);
            _s.Logs.Add(_s.Time.CurrentMinute, st.Id, "-", _s.OperatorName, "PROCESS", "KILL", p.Name);
            if (p.Suspicious) _s.Evaluation.SecurityActionsCorrect++;
            else
            {
                _s.Evaluation.SecurityActionsWrong++;
                st.Killed.Add(p);
                if (p.Critical)
                {
                    foreach (var svc in st.Services) svc.Running = false;
                    _s.Logs.Add(_s.Time.CurrentMinute, st.Id, "-", "-", "SERVICE", "STOPPED", $"{p.Name} の終了によりサービスが停止");
                    _s.Evaluation.FalsePositive++; // 正常業務を止めた
                }
            }
            Changed?.Invoke();
            return $"{st.Id} の {p.Name} を終了しました。";
        }

        public string RestartService(ServerState st, ServiceState svc)
        {
            if (st == null || svc == null) return "";
            if (st.Restoring) return "復元中は操作できません。";
            svc.Running = true;
            // 終了させた正規のプロセスを戻す
            ReturnKilled(st);
            _s.Logs.Add(_s.Time.CurrentMinute, st.Id, "-", _s.OperatorName, "SERVICE", "RESTART", svc.Name);
            Changed?.Invoke();
            return $"{svc.Name} を再起動しました。";
        }

        public string StartRestore(ServerState st)
        {
            if (st == null) return "";
            if (st.Restoring) return "すでに復元中です。";
            st.Restoring = true;
            st.RestoreDoneAt = _s.Time.CurrentMinute + RestoreMinutes;
            foreach (var svc in st.Services) svc.Running = false;
            _s.Logs.Add(_s.Time.CurrentMinute, st.Id, "-", _s.OperatorName, "BACKUP", "RESTORE", "バックアップからの復元を開始");
            Changed?.Invoke();
            return $"{st.Id} の復元を開始しました（約{RestoreMinutes:0}分、その間サービスは停止します）。";
        }

        /// <summary>終了させた正規のプロセスを戻す（同じものを二重に戻さない）</summary>
        static void ReturnKilled(ServerState st)
        {
            foreach (var p in st.Killed) if (!st.Processes.Contains(p)) st.Processes.Add(p);
            st.Killed.Clear();
        }

        public void Tick(float now)
        {
            foreach (var st in _servers)
            {
                if (!st.Restoring || now < st.RestoreDoneAt) continue;
                st.Restoring = false;
                bool wasBad = st.Defaced || st.Garbled;
                st.Defaced = false; st.Garbled = false;
                st.Processes.RemoveAll(p => p.Suspicious);
                ReturnKilled(st); // バックアップの状態に戻るので、終了させた正規のプロセスも動いている
                foreach (var svc in st.Services) svc.Running = true;
                if (wasBad) _s.Evaluation.SecurityActionsCorrect++;
                else _s.Evaluation.SecurityActionsWrong++;
                _s.Logs.Add(now, st.Id, "-", _s.OperatorName, "BACKUP", "DONE", "バックアップからの復元が完了");
                Changed?.Invoke();
            }
        }
    }
}
