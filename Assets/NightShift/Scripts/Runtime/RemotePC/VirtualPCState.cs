using System;
using System.Collections.Generic;
using NightShift.Data;

namespace NightShift.RemotePC
{
    public class VirtualProcess
    {
        public string Name;
        public int Pid;
        public int MemoryKb;
        public string User;
        public bool Critical;
        public bool System;
    }

    public class PrintJob
    {
        public string Document;
        public string Owner;
        public int Pages;
        public string Submitted;
        public float Progress; // 0-1
        public bool Printing;
    }

    /// <summary>
    /// リモートPCの状態。GUIアプリ・疑似Terminalはすべてこの状態を参照・変更する。
    /// 解決判定は「どのボタンを押したか」ではなく最終状態（GetValue）で行う。
    /// </summary>
    public class VirtualPCState
    {
        public string Hostname;
        public string UserName;      // nexus\e0031
        public string UserDisplay;
        public string OsName;
        public string DeviceIp;

        // ---- network ----
        public bool NetworkAdapterEnabled = true;  // Wi-Fi アダプター（デバイス）の有効/無効
        public bool WiFiEnabled = true;            // Wi-Fi スイッチ（設定画面のオン/オフ）
        public bool EthernetCableConnected = false;
        public bool DHCPEnabled = true;
        public string Ssid;
        public string WifiAdapterName;
        public string EthernetAdapterName;
        public string SubnetMask;
        public string DefaultGateway;
        public string ProfileDns;
        public string Domain;
        public string MacAddress;

        // ---- printer ----
        public string PrinterName;
        public bool PrinterOnline = true;
        public bool PrintQueuePaused;
        public readonly List<PrintJob> PrintQueue = new List<PrintJob>();
        public int PagesPrinted;

        // ---- system ----
        public int DiskUsage;
        public readonly List<VirtualProcess> RunningProcesses = new List<VirtualProcess>();
        public readonly List<FileEntry> Files = new List<FileEntry>();
        public string MalwareState = "None"; // 感染しているウイルス（V3 など）

        // ---- 本番業務の問い合わせ（R3〜R11） ----
        public bool DnsAuto = true;                 // DNS サーバーのアドレスを自動取得
        public string StaticIp = "192.168.1.25";    // 手で入れられた固定アドレス（DHCP オフのとき）
        public bool Rebooted;                       // 対応中に再起動した（痕跡が消える）
        public int PendingPatches;                  // 未適用の修正プログラム
        public bool ScreenLockEnabled;              // スクリーンセーバーのパスワード保護
        public bool FakeAlertOpen;                  // 偽の警告画面が出ている
        public bool UnknownToolInstalled;           // 偽の警告に出たツールを入れてしまった
        public bool LogsSaved, LogsCleared;         // イベントログの保存・消去
        public bool BackupDone;                     // MO にバックアップして取り外した
        public bool FileServerReachable = true;     // ファイルサーバーの障害（R9）
        public string ReportKind = "PC";            // 報告の種類（PC側で解決 / サーバー側の障害）
        public bool Quarantined;                    // 端末隔離ツールで社内ネットワークから切り離されている（リモート管理の接続だけ残る）
        public string BaitFile;                     // デスクトップの見慣れないファイル（実行すると感染。実際には何も実行しない）
        public bool ExecutedBait;
        public const string BrowserProcess = "netvoyager.exe";

        public event Action Changed;
        int _nextPid = 400;

        // ---- derived ----
        public bool WifiConnected => NetworkAdapterEnabled && WiFiEnabled;
        public string IPAddress => WifiConnected ? (DHCPEnabled ? DeviceIp : StaticIp) : "";
        public string Gateway => WifiConnected ? DefaultGateway : "";
        public string DNS => WifiConnected ? (DnsAuto ? ProfileDns : "10.10.99.99") : "";
        /// <summary>手で入れた固定アドレスは社内のネットワークと合わないため、DHCP がオフだとつながらない</summary>
        public bool InternetConnectivity => WifiConnected && DHCPEnabled && !Quarantined && !string.IsNullOrEmpty(IPAddress) && !string.IsNullOrEmpty(Gateway);
        public bool CanPrint => PrinterOnline && !PrintQueuePaused && InternetConnectivity;

        public static VirtualPCState Create(VirtualPCProfile p, EmployeeData employee, string ip, Random rng)
        {
            var s = new VirtualPCState
            {
                Hostname = employee.deviceId,
                UserName = employee.LoginName,
                UserDisplay = employee.fullName,
                OsName = p != null ? p.osName : "NEXUS Workstation 2000",
                DeviceIp = ip,
                Ssid = p != null ? p.ssid : "NEXUS-CORP",
                WifiAdapterName = p != null ? p.wifiAdapterName : "Wireless Adapter",
                EthernetAdapterName = p != null ? p.ethernetAdapterName : "Ethernet Adapter",
                SubnetMask = p != null ? p.subnetMask : "255.255.0.0",
                DefaultGateway = p != null ? p.gateway : "10.10.0.1",
                ProfileDns = p != null ? p.dns : "10.10.0.10",
                Domain = p != null ? p.domain : "nexus.local",
                DHCPEnabled = p == null || p.dhcpEnabled,
                PrinterName = p != null ? p.printerName : "Printer",
                DiskUsage = (p != null ? p.diskUsagePercent : 60) + rng.Next(-8, 9),
            };
            s.MacAddress = $"00-0C-{rng.Next(16, 255):X2}-{rng.Next(16, 255):X2}-{rng.Next(16, 255):X2}-{rng.Next(16, 255):X2}";
            if (p != null)
            {
                foreach (var pr in p.processes)
                    s.RunningProcesses.Add(new VirtualProcess
                    {
                        Name = pr.name, Pid = s._nextPid += rng.Next(4, 120), MemoryKb = pr.memoryKb + rng.Next(-200, 400),
                        User = pr.system ? "SYSTEM" : employee.LoginName.Replace("nexus\\", ""), Critical = pr.critical, System = pr.system
                    });
                s.Files.AddRange(p.files);
                int hour = 23;
                int min = rng.Next(10, 58);
                foreach (var j in p.printJobs)
                {
                    s.PrintQueue.Add(new PrintJob { Document = j.document, Pages = j.pages, Owner = s.UserName.Replace("nexus\\", ""), Submitted = $"{hour}:{min:00}" });
                    min = Math.Min(59, min + rng.Next(1, 4));
                }
            }
            return s;
        }

        public void NotifyChanged() => Changed?.Invoke();

        public void SetWiFi(bool on) { WiFiEnabled = on; NotifyChanged(); }
        public void SetQuarantined(bool on) { Quarantined = on; NotifyChanged(); }
        public void SetAdapter(bool on) { NetworkAdapterEnabled = on; NotifyChanged(); }
        public void SetQueuePaused(bool paused) { PrintQueuePaused = paused; NotifyChanged(); }
        public void SetPrinterOnline(bool online) { PrinterOnline = online; NotifyChanged(); }
        public void SetDhcp(bool on) { DHCPEnabled = on; NotifyChanged(); }
        public void SetDnsAuto(bool on) { DnsAuto = on; NotifyChanged(); }
        public void SetScreenLock(bool on) { ScreenLockEnabled = on; NotifyChanged(); }
        public void ApplyPatches() { PendingPatches = 0; NotifyChanged(); }
        public void SaveLogs() { LogsSaved = true; NotifyChanged(); }
        public void ClearLogs() { LogsCleared = true; NotifyChanged(); }
        public void Backup() { BackupDone = true; NotifyChanged(); }
        public void InstallUnknownTool() { UnknownToolInstalled = true; NotifyChanged(); }

        /// <summary>再起動: 利用者のプログラムは終了し、メモリ上の痕跡も消える</summary>
        public void Reboot()
        {
            Rebooted = true;
            RunningProcesses.RemoveAll(p => !p.System && !p.Critical && p.Name != "explorer.exe");
            FakeAlertOpen = false;
            NotifyChanged();
        }

        public void CancelJob(PrintJob j) { PrintQueue.Remove(j); NotifyChanged(); }
        public void CancelAllJobs() { PrintQueue.Clear(); NotifyChanged(); }

        public VirtualProcess StartProcess(string name, bool critical = false)
        {
            var p = new VirtualProcess { Name = name, Pid = _nextPid += 8, MemoryKb = 2000 + name.Length * 300, User = UserName.Replace("nexus\\", ""), Critical = critical };
            RunningProcesses.Add(p);
            NotifyChanged();
            return p;
        }

        public void StopProcessByName(string name)
        {
            var p = RunningProcesses.Find(x => x.Name == name && !x.Critical);
            if (p != null) { RunningProcesses.Remove(p); NotifyChanged(); }
        }

        /// <summary>false = アクセス拒否</summary>
        public bool KillProcess(VirtualProcess p)
        {
            if (p.Critical) return false;
            RunningProcesses.Remove(p);
            if (p.Name == BrowserProcess) FakeAlertOpen = false;
            NotifyChanged();
            return true;
        }

        /// <summary>印刷キューをゲーム時間で進める</summary>
        public void Tick(float deltaMinutes)
        {
            if (!CanPrint || PrintQueue.Count == 0) return;
            var job = PrintQueue[0];
            job.Printing = true;
            job.Progress += deltaMinutes / Math.Max(0.5f, job.Pages * 0.6f);
            if (job.Progress >= 1f)
            {
                PagesPrinted += job.Pages;
                PrintQueue.RemoveAt(0);
                NotifyChanged();
            }
        }

        // ---- state access by key (ResolutionCondition / Fault) ----
        public string GetValue(string key)
        {
            switch (key)
            {
                case "NetworkAdapterEnabled": return B(NetworkAdapterEnabled);
                case "WiFiEnabled": return B(WiFiEnabled);
                case "DHCPEnabled": return B(DHCPEnabled);
                case "IPAddress": return IPAddress;
                case "Gateway": return Gateway;
                case "DNS": return DNS;
                case "InternetConnectivity": return B(InternetConnectivity);
                case "PrinterOnline": return B(PrinterOnline);
                case "PrintQueuePaused": return B(PrintQueuePaused);
                case "CanPrint": return B(CanPrint);
                case "DiskUsage": return DiskUsage.ToString();
                case "MalwareState": return MalwareState;
                case "DnsAuto": return B(DnsAuto);
                case "Rebooted": return B(Rebooted);
                case "PendingPatches": return PendingPatches.ToString();
                case "ScreenLockEnabled": return B(ScreenLockEnabled);
                case "FakeAlertOpen": return B(FakeAlertOpen);
                case "UnknownToolInstalled": return B(UnknownToolInstalled);
                case "LogsSaved": return B(LogsSaved);
                case "LogsCleared": return B(LogsCleared);
                case "BackupDone": return B(BackupDone);
                case "FileServerReachable": return B(FileServerReachable);
                case "ReportKind": return ReportKind;
                case "Quarantined": return B(Quarantined);
                default: return null;
            }
        }

        public bool SetValue(string key, string value)
        {
            bool b = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            switch (key)
            {
                case "NetworkAdapterEnabled": NetworkAdapterEnabled = b; break;
                case "WiFiEnabled": WiFiEnabled = b; break;
                case "DHCPEnabled": DHCPEnabled = b; break;
                case "PrinterOnline": PrinterOnline = b; break;
                case "PrintQueuePaused": PrintQueuePaused = b; break;
                case "DiskUsage": int.TryParse(value, out DiskUsage); break;
                case "MalwareState": MalwareState = value; break;
                case "DnsAuto": DnsAuto = b; break;
                case "PendingPatches": int.TryParse(value, out PendingPatches); break;
                case "ScreenLockEnabled": ScreenLockEnabled = b; break;
                case "FakeAlertOpen":
                    FakeAlertOpen = b;
                    if (b && RunningProcesses.Find(x => x.Name == BrowserProcess) == null)
                        RunningProcesses.Add(new VirtualProcess { Name = BrowserProcess, Pid = _nextPid += 8, MemoryKb = 24800, User = UserName.Replace("nexus\\", "") });
                    break;
                case "FileServerReachable": FileServerReachable = b; break;
                default: return false;
            }
            NotifyChanged();
            return true;
        }

        public bool Satisfies(List<StateValue> conditions)
        {
            foreach (var c in conditions)
            {
                var v = GetValue(c.key);
                if (v == null || !string.Equals(v, c.value, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        static string B(bool b) => b ? "true" : "false";
    }
}
