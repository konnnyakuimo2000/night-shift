using System.Collections.Generic;
using NightShift.Data;
using NightShift.Systems;

namespace NightShift.Events
{
    /// <summary>
    /// 本番業務の攻撃シナリオの「台本」。ThreatStep.script の名前で呼ばれる。
    /// どの処理も、プレイヤーが気付ける手がかり（ログ・プロセス・問い合わせ）を必ず残す。
    /// </summary>
    public static class ThreatScripts
    {
        public static void Run(ThreatScenarioManager m, ThreatScenarioRun run, ThreatStep step, int index)
        {
            var s = m.Session;
            float now = s.Time.CurrentMinute;
            string name = step.script ?? "";
            string arg = "";
            int colon = name.IndexOf(':');
            if (colon >= 0) { arg = name.Substring(colon + 1); name = name.Substring(0, colon); }

            switch (name)
            {
                // ---------- 共通 ----------
                case "PICK_VICTIM":
                    if (run.Victim == null) run.Victim = PickVictim(s, arg);
                    break;
                case "INFECT":
                    if (Victim(s, run) is EmployeeData v1)
                    {
                        s.Security.MarkInfected(v1.deviceId, arg);
                        if (arg == "V6") s.RequestHorror("MUSICBOX");
                        s.Logs.Add(now, s.Employees.GetIp(v1), "-", v1.fullName, "PROCESS", "START", NewProcessName(arg) + " (" + v1.deviceId + ")");
                    }
                    break;
                case "TICKET":
                    if (step.helpDesk != null && Victim(s, run) is EmployeeData v2 && !Disconnected(s, v2))
                        s.HelpDesk.Create(step.helpDesk, v2);
                    break;

                // ---------- T7 感染した社員PC（V3 NIGHTCRY） ----------
                case "BEACON":
                    if (Victim(s, run) is EmployeeData v3 && !Disconnected(s, v3))
                    {
                        s.Network.Emit(true, null, TrafficResult.Delivered);
                        s.Logs.Add(now, s.Employees.GetIp(v3), run.C2Ip, v3.fullName, "HTTPS", "OK", "POST /gate.php (" + v3.deviceId + ")");
                    }
                    break;
                case "SPREAD_CHECK":
                    if (Victim(s, run) is EmployeeData v4 && !Disconnected(s, v4))
                    {
                        int spread = 0;
                        foreach (var e in s.Employees.All)
                        {
                            if (spread >= 2 || e == v4 || !e.IsActive || !e.onShift || e.department != v4.department && spread > 0) continue;
                            if (s.Security.IsInfected(e.deviceId)) continue;
                            s.Security.MarkInfected(e.deviceId, "V3");
                            spread++;
                        }
                        run.Compromised = true;
                        s.AddDamage("社内のPCでウイルス感染が広がった（NIGHTCRY）");
                    }
                    break;

                // ---------- T8 内部からの大量持ち出し ----------
                case "FILE_READ":
                    if (Victim(s, run) is EmployeeData v5 && !Stopped(s, v5))
                    {
                        string ip = s.Employees.GetIp(v5);
                        s.Security.MarkHostile(ip);
                        string[] files = { "顧客名簿_関東.xls", "顧客名簿_関西.xls", "取引先一覧.xls", "見積単価表.xls", "社員連絡先.xls", "顧客名簿_全国.mdb" };
                        s.Logs.Add(now, ip, "FILE-SRV01", $"{v5.fullName} ({v5.employeeId})", "SMB", "OK", "FILE READ " + files[index % files.Length]);
                    }
                    break;
                case "EXFIL_CHECK":
                    if (Victim(s, run) is EmployeeData v6 && !Stopped(s, v6) && !s.Security.WasReported(s.Employees.GetIp(v6)))
                    {
                        run.Compromised = true;
                        s.Logs.Add(now, s.Employees.GetIp(v6), "INTERNET", v6.fullName, "HTTP", "OK", "大容量のファイル送信 (48 MB)");
                        s.AddDamage("顧客情報が社外へ持ち出された");
                    }
                    break;

                // ---------- T9 公開Webの改ざん（V2 GARBLE） ----------
                case "WEB_IMPLANT":
                    s.Servers.Implant("WEB-SRV01", "httpd_cache.exe", "T9");
                    s.Logs.Add(now, run.AttackerIp, "WEB-SRV01", "-", "HTTP", "200", "POST /cgi-bin/upload.cgi (16 KB)");
                    s.Logs.Add(now + 0.3f, "WEB-SRV01", "-", "SYSTEM", "PROCESS", "START", "httpd_cache.exe");
                    break;
                case "WEB_DEFACE_CHECK":
                    if (s.Servers.HasTag("WEB-SRV01", "T9"))
                    {
                        s.Servers.SetDefaced("WEB-SRV01", true);
                        run.Compromised = true;
                        s.AddDamage("公開Webのページが書き換えられた（GARBLE）");
                    }
                    break;

                // ---------- T10 管理者アカウントの乗っ取り（V5 REDACT） ----------
                case "UPS_CHANGE":
                    if (Victim(s, run) is EmployeeData v7)
                    {
                        s.Servers.Implant("ADMIN-SRV01", "upsctl.exe", "T10");
                        s.Logs.Add(now, s.Employees.GetIp(v7), "ADMIN-SRV01", $"{v7.fullName} ({v7.employeeId})", "CONFIG", "CHANGED", "UPS: 停電時の動作設定を変更");
                    }
                    break;
                case "BLACKOUT_CHECK":
                    if (Victim(s, run) is EmployeeData v8 && !s.Security.IsSuspended(v8.employeeId) && s.Servers.HasTag("ADMIN-SRV01", "T10"))
                    {
                        run.Compromised = true;
                        s.AddDamage("フロアの電源が落ちた（REDACT）");
                        s.RequestBlackout();
                    }
                    break;

                case "ADMIN_FATAL_CHECK":
                    // 要求を許可したうえに、アカウントを止めていない
                    if (run.RequestAllowed && Victim(s, run) is EmployeeData v11 && !s.Security.IsSuspended(v11.employeeId))
                        s.AddDamage("管理者アカウントを乗っ取られ、社内システムが停止した", true);
                    break;

                // ---------- T11 身代金ウイルス（V6 BRIAR ROSE） ----------
                case "RANSOM_WRITE":
                    if (Victim(s, run) is EmployeeData v9 && !Disconnected(s, v9))
                    {
                        string[] docs = { "見積書_0520.doc", "営業日報_5月.xls", "請求書控え.xls", "会議メモ.txt", "顧客名簿_関東.xls", "契約書_雛形.doc" };
                        string f = docs[index % docs.Length];
                        s.Logs.Add(now, s.Employees.GetIp(v9), "FILE-SRV01", v9.fullName, "SMB", "OK", $"FILE WRITE {f} → {f}.briar");
                        s.RansomEncryptedFiles++;
                        // ファイルサーバーの監視が、書き込みの急増を知らせる（どの端末かはログで調べる）
                        if (index == 3)
                            s.Notifications.Notify(NotificationType.SecurityAlert, "ファイルサーバー監視", "FILE-SRV01 への書き込みが急増しています。", now, () => s.Ui?.OpenLogViewer());
                    }
                    break;
                case "RANSOM_CHECK":
                    if (Victim(s, run) is EmployeeData v10 && !Disconnected(s, v10))
                    {
                        run.Compromised = true;
                        s.AddDamage("全社のファイルが暗号化された（BRIAR ROSE）", true);
                    }
                    break;

                // ---------- T13 陽動 ----------
                case "DIVERSION":
                {
                    int n = 0;
                    foreach (var def in s.Day.helpDeskPool)
                    {
                        if (n >= 4) break;
                        if (def == null || def.type != HelpDeskType.Binary || !HelpDeskGenerator.IsAvailable(def, s.Day.dayNumber)) continue;
                        if (s.Rng.NextDouble() < 0.5) continue;
                        if (s.Director.HelpDeskGen.Spawn(def) != null) n++;
                    }
                    break;
                }

                // ---------- T5 担当者宛ての偽メール ----------
                case "PHISH_MAIL":
                    if (arg == "ATTACH") PhishingMail.SendAttachment(s);
                    else PhishingMail.Send(s, arg == "AI");
                    break;
            }
        }

        static EmployeeData Victim(ShiftSession s, ThreatScenarioRun run) => run.Victim;

        /// <summary>隔離された・ネットワークから切り離された端末か</summary>
        public static bool Disconnected(ShiftSession s, EmployeeData e)
        {
            if (e == null) return true;
            if (s.Security.IsIsolated(e.deviceId)) return true;
            foreach (var t in s.HelpDesk.Tickets)
                if (t.Employee == e && t.PC != null && !t.PC.WifiConnected && !t.PC.EthernetCableConnected) return true;
            return false;
        }

        static bool Stopped(ShiftSession s, EmployeeData e) =>
            e == null || s.Security.IsSuspended(e.employeeId) || s.Security.IsIsolated(e.deviceId) || s.Security.IsBlocked(s.Employees.GetIp(e));

        static EmployeeData PickVictim(ShiftSession s, string kind)
        {
            var list = new List<EmployeeData>();
            foreach (var e in s.Employees.All)
            {
                if (!e.IsActive || !e.onShift || e.department == "情報システム部") continue;
                if (kind == "FILE" && !e.HasPermission("FILE_ACCESS")) continue;
                list.Add(e);
            }
            return list.Count == 0 ? null : list[s.Rng.Next(list.Count)];
        }

        static string NewProcessName(string virus)
        {
            switch (virus)
            {
                case "V3": return "svch0st.exe";
                case "V6": return "briar.exe";
                case "V1": return "tinnitus.scr";
                default: return "update32.exe";
            }
        }
    }
}
