using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Data
{
    /// <summary>
    /// DAY1の初期データ。
    /// ・エディタ: NightShift > Create DAY1 Data Assets でこの内容を .asset として書き出し、以後はアセットを編集する。
    /// ・ランタイム: アセットが存在しない場合のフォールバック。
    /// ゲームロジックはここで作られた「データ」だけを参照し、特定の社員名などをハードコードしない。
    /// </summary>
    public static class DefaultDataFactory
    {
        /// <summary>初期データの版。内容を変えたら上げる（エディタが古いアセットの再生成を行う）。</summary>
        public const int DataVersion = 5;

        // 業務連絡で使う強調（Text のリッチテキスト）
        const string Allow = "#0030A0";   // 許可系
        const string Deny = "#B00000";    // 禁止系
        const string Check = "#8A4B00";   // 確認すること
        static string B(string t) => $"<b>{t}</b>";
        static string Col(string t, string c) => $"<color={c}><b>{t}</b></color>";
        public class Bundle
        {
            public GameDatabase database;
            public readonly List<KeyValuePair<ScriptableObject, string>> objects = new List<KeyValuePair<ScriptableObject, string>>();

            public T Add<T>(T obj, string folder, string assetName) where T : ScriptableObject
            {
                obj.name = assetName;
                objects.Add(new KeyValuePair<ScriptableObject, string>(obj, folder));
                return obj;
            }
        }

        public static Bundle Build()
        {
            var b = new Bundle();
            var db = b.Add(ScriptableObject.CreateInstance<GameDatabase>(), "", "GameDatabase");
            db.dataVersion = DataVersion;
            b.database = db;

            // ---------------- Permissions ----------------
            var perms = new Dictionary<string, PermissionDefinition>();
            PermissionDefinition P(string id, string label, PermissionCategory cat, int order, string desc)
            {
                var p = b.Add(ScriptableObject.CreateInstance<PermissionDefinition>(), "Permissions", "PERM_" + id);
                p.permissionId = id; p.displayName = label; p.category = cat; p.sortOrder = order; p.description = desc;
                perms[id] = p; db.permissions.Add(p);
                return p;
            }
            P("GENERAL_BUSINESS", "一般業務", PermissionCategory.SystemUse, 10, "メール・社内Web・インターネット");
            P("FILE_ACCESS", "共有ファイル", PermissionCategory.SystemUse, 20, "FILE-SRV01 の共有フォルダ");
            P("DEV_ACCESS", "開発環境", PermissionCategory.SystemUse, 30, "開発リポジトリ");
            P("HR_ACCESS", "人事システム", PermissionCategory.SystemUse, 40, "人事・勤怠システム");
            P("ACCOUNTING_ACCESS", "経理システム", PermissionCategory.SystemUse, 50, "会計・経費システム");
            P("LOCAL_ADMIN", "ローカル管理者", PermissionCategory.Administrator, 110, "自分のPCの管理者権限");
            P("SERVER_ADMIN", "サーバー管理者", PermissionCategory.Administrator, 120, "サーバーへの管理ログイン");
            P("NETWORK_ADMIN", "ネットワーク管理", PermissionCategory.Administrator, 130, "ネットワーク機器の管理");
            P("USER_ADMIN", "ユーザー管理", PermissionCategory.Administrator, 140, "アカウントの作成・変更");
            P("SECURITY_ADMIN", "セキュリティ管理", PermissionCategory.Administrator, 150, "セキュリティ設定の変更");
            P("DOMAIN_ADMIN", "全社管理者", PermissionCategory.Administrator, 160, "ドメイン全体の管理");

            // ---------------- Network nodes ----------------
            NetworkNodeDefinition N(string id, NodeRole role, NodeZone zone, string ip, Vector2 pos, bool critical, bool admin)
            {
                var n = b.Add(ScriptableObject.CreateInstance<NetworkNodeDefinition>(), "Network", "NODE_" + id);
                n.nodeId = id; n.displayName = id; n.role = role; n.zone = zone; n.ipAddress = ip;
                n.mapPosition = pos; n.isCritical = critical; n.isAdministrative = admin; n.manageableFromDay = 2;
                db.nodes.Add(n);
                return n;
            }
            AccessTypeEntry A(string type, string service, string perm, bool vpn = false) =>
                new AccessTypeEntry { accessType = type, serviceId = service, requiredPermission = perm, requiresVpnEnabled = vpn };

            var internet = N("INTERNET", NodeRole.Internet, NodeZone.External, "-", new Vector2(0.07f, 0.5f), false, false);
            internet.accessTypes.Add(A("WEB", "WEB", "GENERAL_BUSINESS"));
            N("FIREWALL", NodeRole.Firewall, NodeZone.Perimeter, "10.10.0.254", new Vector2(0.29f, 0.5f), false, false);
            var pcs = N("EMPLOYEE PCs", NodeRole.ClientGroup, NodeZone.Client, "10.10.x.x", new Vector2(0.92f, 0.5f), false, false);
            pcs.manageableFromDay = 99;
            var web = N("WEB-SRV01", NodeRole.Server, NodeZone.DMZ, "172.16.0.10", new Vector2(0.6f, 0.1f), false, false);
            web.accessTypes.Add(A("HTTP", "CORP-WEB", ""));
            var mail = N("MAIL-SRV01", NodeRole.Server, NodeZone.Internal, "10.10.0.20", new Vector2(0.6f, 0.26f), true, false);
            mail.accessTypes.Add(A("MAIL", "MAIL", "GENERAL_BUSINESS"));
            var file = N("FILE-SRV01", NodeRole.Server, NodeZone.Internal, "10.10.0.30", new Vector2(0.6f, 0.42f), true, false);
            file.accessTypes.Add(A("FILE READ", "FILE", "FILE_ACCESS"));
            file.accessTypes.Add(A("FILE WRITE", "FILE", "FILE_ACCESS"));
            var app = N("APP-SRV01", NodeRole.Server, NodeZone.Internal, "10.10.0.40", new Vector2(0.6f, 0.58f), true, false);
            app.accessTypes.Add(A("DEV REPOSITORY", "DEV", "DEV_ACCESS"));
            app.accessTypes.Add(A("HR SYSTEM", "HR", "HR_ACCESS"));
            app.accessTypes.Add(A("ACCOUNTING SYSTEM", "ACCOUNTING", "ACCOUNTING_ACCESS"));
            var auth = N("AUTH-SRV01", NodeRole.Server, NodeZone.Internal, "10.10.0.5", new Vector2(0.6f, 0.74f), true, false);
            auth.accessTypes.Add(A("VPN LOGIN", "VPN", "GENERAL_BUSINESS", true));
            var admin = N("ADMIN-SRV01", NodeRole.Server, NodeZone.Internal, "10.10.0.2", new Vector2(0.6f, 0.9f), true, true);
            admin.accessTypes.Add(A("ADMIN LOGIN", "ADMIN", "SERVER_ADMIN"));
            admin.accessTypes.Add(A("REMOTE CONSOLE", "ADMIN", "SERVER_ADMIN"));

            // ---------------- Employees ----------------
            EmployeeData E(string id, string name, string kana, int age, string dept, string pos, string device, string ip,
                string hours, bool onShift, int lit, int sec, string[] targets, string[] permIds, string laptop = null)
            {
                var e = b.Add(ScriptableObject.CreateInstance<EmployeeData>(), "Employees", "EMP_" + id);
                e.employeeId = id; e.fullName = name; e.kana = kana; e.age = age; e.department = dept; e.position = pos;
                e.deviceId = device; e.deviceIp = ip; e.workHours = hours; e.onShift = onShift;
                e.itLiteracy = lit; e.securityAwareness = sec;
                e.laptopDeviceId = laptop ?? "";
                e.vpnEnabled = !string.IsNullOrEmpty(laptop);
                e.normalAccessTargets.AddRange(targets);
                e.normalBehavior.usesVpn = e.vpnEnabled;
                e.normalBehavior.activityPerHour = 4f + lit;
                foreach (var t in targets)
                    e.normalBehavior.serviceWeights.Add(new ServiceWeight { serviceId = t, weight = t == "WEB" || t == "MAIL" ? 3f : 1.5f });
                foreach (var pid in permIds) e.permissions.Add(perms[pid]);
                db.employees.Add(e);
                return e;
            }
            string[] GB_FILE = { "GENERAL_BUSINESS", "FILE_ACCESS" };
            E("E-0012", "田中 美咲", "タナカ ミサキ", 34, "営業部", "主任", "SALES-PC02", "10.10.2.12", "09:00-18:00 (残業)", true, 3, 3,
                new[] { "WEB", "MAIL", "FILE" }, GB_FILE, "NB-SALES02");
            E("E-0018", "鈴木 健太", "スズキ ケンタ", 28, "営業部", "営業", "SALES-PC05", "10.10.2.15", "09:00-18:00 (残業)", true, 2, 2,
                new[] { "WEB", "MAIL", "FILE" }, GB_FILE);
            E("E-0026", "吉田 翔", "ヨシダ ショウ", 27, "営業部", "営業", "SALES-PC08", "10.10.2.18", "09:00-18:00 (海外対応)", true, 3, 2,
                new[] { "WEB", "MAIL", "FILE" }, GB_FILE, "NB-SALES08");
            E("E-0031", "佐藤 蓮", "サトウ レン", 26, "開発部", "エンジニア", "DEV-PC03", "10.10.3.21", "フレックス", true, 5, 4,
                new[] { "WEB", "MAIL", "FILE", "DEV" }, new[] { "GENERAL_BUSINESS", "FILE_ACCESS", "DEV_ACCESS", "LOCAL_ADMIN" }, "NB-DEV03");
            E("E-0035", "高橋 優斗", "タカハシ ユウト", 31, "開発部", "シニアエンジニア", "DEV-PC07", "10.10.3.27", "フレックス", true, 5, 4,
                new[] { "WEB", "MAIL", "FILE", "DEV" }, new[] { "GENERAL_BUSINESS", "FILE_ACCESS", "DEV_ACCESS", "LOCAL_ADMIN" });
            E("E-0044", "伊藤 さくら", "イトウ サクラ", 29, "人事部", "人事担当", "HR-PC01", "10.10.4.11", "09:00-18:00 (月末処理)", true, 3, 4,
                new[] { "WEB", "MAIL", "FILE", "HR" }, new[] { "GENERAL_BUSINESS", "FILE_ACCESS", "HR_ACCESS" });
            E("E-0052", "渡辺 隆", "ワタナベ タカシ", 45, "経理部", "課長", "ACC-PC02", "10.10.5.12", "09:00-18:00 (決算対応)", true, 2, 3,
                new[] { "MAIL", "FILE", "ACCOUNTING" }, new[] { "GENERAL_BUSINESS", "FILE_ACCESS", "ACCOUNTING_ACCESS" });
            E("E-0060", "山本 浩二", "ヤマモト コウジ", 52, "総務部", "部長", "GA-PC01", "10.10.6.11", "09:00-18:00 (残業)", true, 1, 2,
                new[] { "WEB", "MAIL" }, GB_FILE);
            E("E-0067", "中村 彩", "ナカムラ アヤ", 24, "カスタマーサポート部", "オペレーター", "CS-PC04", "10.10.7.14", "22:00-06:00 (夜勤)", true, 2, 3,
                new[] { "WEB", "MAIL", "FILE" }, GB_FILE);
            E("E-0071", "小林 誠", "コバヤシ マコト", 38, "情報システム部", "ネットワーク管理者", "IT-PC02", "10.10.8.12", "09:00-18:00", false, 5, 5,
                new[] { "WEB", "MAIL", "FILE", "ADMIN" }, new[] { "GENERAL_BUSINESS", "FILE_ACCESS", "LOCAL_ADMIN", "NETWORK_ADMIN", "SERVER_ADMIN" });
            E("E-0078", "加藤 由美", "カトウ ユミ", 41, "情報システム部", "サーバー管理者", "IT-PC03", "10.10.8.13", "09:00-18:00", false, 5, 5,
                new[] { "WEB", "MAIL", "FILE", "ADMIN" }, new[] { "GENERAL_BUSINESS", "FILE_ACCESS", "LOCAL_ADMIN", "SERVER_ADMIN", "USER_ADMIN" });

            // ---------------- PC profile ----------------
            var profile = b.Add(ScriptableObject.CreateInstance<VirtualPCProfile>(), "PCProfiles", "PC_StandardOffice");
            void Proc(string n, int kb, bool sys, bool crit) => profile.processes.Add(new ProcessEntry { name = n, memoryKb = kb, system = sys, critical = crit });
            Proc("System", 220, true, true);
            Proc("smss.exe", 360, true, true);
            Proc("csrss.exe", 2840, true, true);
            Proc("winlogon.exe", 3120, true, true);
            Proc("services.exe", 4510, true, true);
            Proc("lsass.exe", 6020, true, true);
            Proc("svchost.exe", 4380, true, true);
            Proc("svchost.exe", 12850, true, true);
            Proc("spoolsv.exe", 4920, true, true);
            Proc("nxagent.exe", 3310, true, true);
            Proc("explorer.exe", 18440, false, false);
            Proc("nxmail.exe", 15200, false, false);
            Proc("taskmgr.exe", 2900, false, false);
            Proc("imejp.exe", 1980, false, false);
            void F(string folder, string n, int kb) => profile.files.Add(new FileEntry { folder = folder, name = n, sizeKb = kb });
            F("マイ ドキュメント", "見積書_0520.doc", 48);
            F("マイ ドキュメント", "営業日報_5月.xls", 112);
            F("マイ ドキュメント", "会議メモ.txt", 3);
            F("デスクトップ", "社内電話帳.xls", 64);
            F("デスクトップ", "ショートカット - 共有フォルダ.lnk", 1);
            profile.printJobs.Add(new PrintJobEntry { document = "見積書_0520.doc", pages = 2 });
            profile.printJobs.Add(new PrintJobEntry { document = "請求書控え.xls", pages = 1 });

            // ---------------- Events ----------------
            EventDefinition Ev(string id, Vector2 window, string folder = "Events")
            {
                var e = b.Add(ScriptableObject.CreateInstance<EventDefinition>(), folder, "EVT_" + id);
                e.eventId = id; e.minimumDay = 1; e.maximumDay = 7; e.triggerWindow = window;
                e.timeoutMinutes = 30;
                return e;
            }

            // --- 共通 ---
            var routine = Ev("REQ_ROUTINE", Vector2.zero);
            routine.destinationMode = DestinationMode.FromEmployeeNormalTargets;
            routine.employeeConditions.preferUnused = false;
            routine.timeoutMinutes = 25;

            var routineVpn = Ev("REQ_ROUTINE_VPN", Vector2.zero);
            routineVpn.minimumDay = 3;
            routineVpn.sourceType = SourceType.RegisteredLaptopExternal;
            routineVpn.employeeConditions.requireVpnEnabled = true;
            routineVpn.employeeConditions.requireLaptop = true;
            routineVpn.employeeConditions.preferUnused = false;
            routineVpn.destination = auth; routineVpn.accessType = "VPN LOGIN";
            routineVpn.weight = 0.5f;
            routineVpn.timeoutMinutes = 25;

            var firstNormal = Ev("REQ_FIRST_NORMAL", new Vector2(2, 4));
            firstNormal.employeeConditions.requiredNormalTargets.Add("MAIL");
            firstNormal.employeeConditions.requiredPermissions.Add(perms["GENERAL_BUSINESS"]);
            firstNormal.destination = mail; firstNormal.accessType = "MAIL";

            var scanLog = Ev("LOG_SCANNER_NOISE", Vector2.zero);
            scanLog.minimumDay = 3;
            scanLog.category = EventCategory.Threat;
            scanLog.kind = EventKind.LogEntry;
            scanLog.userType = UserType.Unknown;
            scanLog.sourceType = SourceType.ExternalUnknown;
            scanLog.destination = web; scanLog.accessType = "TCP";
            scanLog.logEventType = "FW";
            scanLog.logResult = "DENY";

            // --- DAY1: 社員と端末 ---
            EventDefinition Unregistered(string id, Vector2 w)
            {
                var e = Ev(id, w);
                e.severity = Severity.Low;
                e.sourceType = SourceType.UnregisteredDevice;
                e.deviceNamePattern = "NB-PRIV{n}";
                e.employeeConditions.requiredPermissions.Add(perms["FILE_ACCESS"]);
                e.employeeConditions.requiredNormalTargets.Add("FILE");
                e.destination = file; e.accessType = "FILE READ";
                return e;
            }
            EventDefinition OtherDevice(string id, Vector2 w, NetworkNodeDefinition dest, string access, string service)
            {
                var e = Ev(id, w);
                e.severity = Severity.Low;
                e.sourceType = SourceType.OtherEmployeeDevice;
                e.employeeConditions.requiredNormalTargets.Add(service);
                e.destination = dest; e.accessType = access;
                return e;
            }

            var d1Unregistered = Unregistered("D1_REQ_UNREGISTERED_DEVICE", new Vector2(22, 28));
            var d1OtherDevice = OtherDevice("D1_REQ_OTHER_EMPLOYEES_PC", new Vector2(70, 80), mail, "MAIL", "MAIL");
            var d1Unknown = Ev("D1_REQ_UNKNOWN_USER", new Vector2(120, 135));
            d1Unknown.category = EventCategory.Threat;
            d1Unknown.notifyAsSecurityAlert = true;
            d1Unknown.severity = Severity.High;
            d1Unknown.userType = UserType.Unknown;
            d1Unknown.sourceType = SourceType.ExternalUnknown;
            d1Unknown.deviceNamePattern = "UNKNOWN-14";
            d1Unknown.destination = admin; d1Unknown.accessType = "ADMIN LOGIN";
            d1Unknown.isMalicious = true;
            d1Unknown.timeoutMinutes = 40;
            var d1Final = OtherDevice("D1_REQ_FINAL_OTHER_DEVICE", new Vector2(195, 205), file, "FILE READ", "FILE");
            d1Final.employeeConditions.requiredPermissions.Add(perms["FILE_ACCESS"]);

            // --- DAY2: 権限 ---
            var d2HR = Ev("D2_REQ_HR_SYSTEM", new Vector2(10, 14));
            d2HR.employeeConditions.requiredPermissions.Add(perms["HR_ACCESS"]);
            d2HR.employeeConditions.requiredNormalTargets.Add("HR");
            d2HR.destination = app; d2HR.accessType = "HR SYSTEM";

            var d2NoAccounting = Ev("D2_REQ_NO_ACCOUNTING", new Vector2(30, 36));
            d2NoAccounting.severity = Severity.Medium;
            d2NoAccounting.employeeConditions.departments.Add("営業部");
            d2NoAccounting.employeeConditions.excludedPermissions.Add(perms["ACCOUNTING_ACCESS"]);
            d2NoAccounting.destination = app; d2NoAccounting.accessType = "ACCOUNTING SYSTEM";

            var d2LooksOdd = Ev("D2_REQ_DEV_FILE_LOOKS_ODD", new Vector2(80, 88));
            d2LooksOdd.employeeConditions.departments.Add("開発部");
            d2LooksOdd.employeeConditions.requiredPermissions.Add(perms["FILE_ACCESS"]);
            d2LooksOdd.employeeConditions.requiredNormalTargets.Add("FILE");
            d2LooksOdd.destination = file; d2LooksOdd.accessType = "FILE WRITE";

            var d2OtherDevice = OtherDevice("D2_REQ_OTHER_EMPLOYEES_PC", new Vector2(100, 112), file, "FILE READ", "FILE");

            var d2Final = Ev("D2_REQ_FINAL_NO_ADMIN", new Vector2(198, 204));
            d2Final.severity = Severity.Medium;
            d2Final.employeeConditions.departments.Add("営業部");
            d2Final.employeeConditions.excludedPermissions.Add(perms["SERVER_ADMIN"]);
            d2Final.employeeConditions.excludedPermissions.Add(perms["LOCAL_ADMIN"]);
            d2Final.destination = admin; d2Final.accessType = "ADMIN LOGIN";

            // --- DAY3: 社外と攻撃 ---
            var d3Suspicious = Ev("D3_REQ_FIRST_SUSPICIOUS", new Vector2(44, 62));
            d3Suspicious.category = EventCategory.Threat;
            d3Suspicious.preferWhileRemoteActive = true;
            d3Suspicious.notifyAsSecurityAlert = true;
            d3Suspicious.severity = Severity.High;
            d3Suspicious.userType = UserType.Unknown;
            d3Suspicious.sourceType = SourceType.ExternalUnknown;
            d3Suspicious.deviceNamePattern = "UNKNOWN-{n}";
            d3Suspicious.destination = admin; d3Suspicious.accessType = "ADMIN LOGIN";
            d3Suspicious.isMalicious = true;
            d3Suspicious.timeoutMinutes = 40;

            var d3VpnLegit = Ev("D3_REQ_VPN_LEGIT", new Vector2(130, 140));
            d3VpnLegit.sourceType = SourceType.RegisteredLaptopExternal;
            d3VpnLegit.employeeConditions.requireVpnEnabled = true;
            d3VpnLegit.employeeConditions.requireLaptop = true;
            d3VpnLegit.destination = auth; d3VpnLegit.accessType = "VPN LOGIN";

            var d3NoVpn = Ev("D3_REQ_FINAL_NO_VPN", new Vector2(196, 204));
            d3NoVpn.severity = Severity.Medium;
            d3NoVpn.sourceType = SourceType.RegisteredDeviceExternal;
            d3NoVpn.employeeConditions.requireVpnDisabled = true;
            d3NoVpn.destination = auth; d3NoVpn.accessType = "VPN LOGIN";

            // ---------------- Threat scenario (DAY3) ----------------
            var threatReq = Ev("REQ_THREAT_ADMIN", Vector2.zero, "Threats");
            threatReq.category = EventCategory.Threat;
            threatReq.userType = UserType.NamedAccount;
            threatReq.accountName = "admin";
            threatReq.sourceType = SourceType.AttackerRelatedSource;
            threatReq.deviceNamePattern = "WIN-{hex}";
            threatReq.destination = admin; threatReq.accessType = "ADMIN LOGIN";
            threatReq.isMalicious = true;
            threatReq.notifyAsSecurityAlert = true;
            threatReq.severity = Severity.High;
            threatReq.timeoutMinutes = 35;

            var threat = b.Add(ScriptableObject.CreateInstance<ThreatScenarioDefinition>(), "Threats", "THREAT_WebProbeToAdmin");
            threat.scenarioId = "THREAT_WEB_PROBE";
            threat.minimumDay = 3;
            threat.designNote = "外部の攻撃者（自動スクリプト＋手動操作）が公開Webを探索し、管理サーバーへのログインを試みる。ログを見れば同一レンジのIPと気付ける。";
            threat.startWindow = new Vector2(100, 107);
            threat.sourceIpPrefix = "198.51.100.";
            threat.steps.Add(new ThreatStep { label = "公開Webへのアクセス", offsetMinutes = 0, kind = ThreatStepKind.LogEntry, destination = web, eventType = "HTTP", result = "200", detail = "GET / (python-urllib/2.0)" });
            threat.steps.Add(new ThreatStep { label = "管理ページ探索", offsetMinutes = 4, kind = ThreatStepKind.LogEntry, destination = web, eventType = "HTTP", result = "404", detail = "GET /admin/|GET /manager/html|GET /login.cgi|GET /cgi-bin/admin.pl", repeat = 4, repeatIntervalMinutes = 0.6f });
            threat.steps.Add(new ThreatStep { label = "再アクセス", offsetMinutes = 9, kind = ThreatStepKind.LogEntry, destination = web, eventType = "HTTP", result = "200", detail = "GET /about/staff.html" });
            threat.steps.Add(new ThreatStep { label = "管理サーバーのポート探索", offsetMinutes = 12, kind = ThreatStepKind.LogEntry, useRelatedSource = true, destination = admin, eventType = "FW", result = "DENY", detail = "TCP 23|TCP 3389|TCP 22", repeat = 3, repeatIntervalMinutes = 0.4f });
            threat.steps.Add(new ThreatStep { label = "管理サーバーへの接続要求", offsetMinutes = 16, kind = ThreatStepKind.ConnectionRequest, useRelatedSource = true, destination = admin, request = threatReq });

            var noise = b.Add(ScriptableObject.CreateInstance<ThreatScenarioDefinition>(), "Threats", "THREAT_ScannerNoise");
            noise.scenarioId = "THREAT_SCANNER_NOISE";
            noise.minimumDay = 3;
            noise.designNote = "インターネット上の一般的な自動スキャン。ファイアウォールで拒否されるだけ。";
            noise.startWindow = new Vector2(66, 74);
            noise.sourceIpPrefix = "203.0.113.";
            noise.steps.Add(new ThreatStep { label = "ポートスキャン", offsetMinutes = 0, kind = ThreatStepKind.LogEntry, destination = web, eventType = "FW", result = "DENY", detail = "TCP 21|TCP 23|TCP 445|TCP 1433|TCP 3306", repeat = 5, repeatIntervalMinutes = 0.3f });

            // ---------------- Help Desk ----------------
            HelpDeskDefinition H(string id, HelpDeskType type, Vector2 window, int day)
            {
                var h = b.Add(ScriptableObject.CreateInstance<HelpDeskDefinition>(), "HelpDesk", "HD_" + id);
                h.definitionId = id; h.type = type; h.triggerWindow = window; h.minimumDay = day;
                return h;
            }

            var hdA1 = H("A1_PASSWORD_PHONE", HelpDeskType.Binary, new Vector2(18, 22), 2);
            hdA1.subject = "パスワードを教えてほしいと言われた";
            hdA1.message = "{dept}の{name}です。\nさきほど「本社の情報システムの者」と名乗る人から電話があり、緊急のメンテナンスなので私のパスワードを教えてほしいと言われました。\n折り返すと言って一度切ったのですが、教えても大丈夫でしょうか？";
            hdA1.correctOption = "パスワードは誰にも教えないよう伝え、電話の内容を記録して報告してもらう。";
            hdA1.incorrectOption = "情報システムの担当者なら問題ないので、折り返して伝えてよいと回答する。";
            hdA1.replyAfterAnswer = "わかりました。そのようにします。";
            hdA1.reminderMessage = "すみません、先ほどの件どうしたらいいでしょうか…？";

            var hdR1 = H("R1_NO_INTERNET", HelpDeskType.Remote, new Vector2(40, 46), 2);
            hdR1.subject = "インターネットにつながらない";
            hdR1.message = "{dept}の{name}です。\nさっきから急にインターネットにつながらなくなりました。メールも送れません。\n見てもらえますか？（端末: {device}）";
            hdR1.pcProfile = profile;
            hdR1.faultVariants.Add(new FaultVariant { label = "Wi-Fi OFF", faults = { new StateValue { key = "WiFiEnabled", value = "false" } } });
            hdR1.resolutionConditions.Add(new StateValue { key = "InternetConnectivity", value = "true" });
            hdR1.successReply = "あ、つながりました！メールも送れました。ありがとうございます！";
            hdR1.failureReply = "うーん、まだつながらないみたいです…。";
            hdR1.reminderMessage = "まだつながらないです…。急ぎのメールがあって。";

            var hdA2 = H("A2_ATTACHMENT", HelpDeskType.Binary, new Vector2(15, 20), 3);
            hdA2.subject = "知らない会社からExcelファイルが届いた";
            hdA2.message = "{dept}の{name}です。\n知らない会社から「請求書の件」という件名でExcelファイルが届きました。\n取引先かもしれないのですが、開いて大丈夫ですか？";
            hdA2.correctOption = "開かずに、送信元や取引関係を確認してから判断するよう伝える。";
            hdA2.incorrectOption = "内容を確認しないと判断できないので、一度開いてみるよう伝える。";
            hdA2.replyAfterAnswer = "了解です。確認してみます。";
            hdA2.reminderMessage = "あの、さっきのファイル、開いちゃっていいですかね？";

            var hdR2 = H("R2_CANNOT_PRINT", HelpDeskType.Remote, new Vector2(34, 40), 3);
            hdR2.subject = "印刷できない";
            hdR2.message = "{dept}の{name}です。\n見積書を印刷しようとしても、プリンターから何も出てきません。\nプリンターの電源は入っています。朝イチで使いたいので、見てもらえますか？（端末: {device}）";
            hdR2.pcProfile = profile;
            hdR2.faultVariants.Add(new FaultVariant { label = "Print Queue Paused", faults = { new StateValue { key = "PrintQueuePaused", value = "true" } } });
            hdR2.resolutionConditions.Add(new StateValue { key = "PrinterOnline", value = "true" });
            hdR2.resolutionConditions.Add(new StateValue { key = "PrintQueuePaused", value = "false" });
            hdR2.successReply = "出てきました！助かりました。";
            hdR2.failureReply = "まだ何も出てこないです…。";
            hdR2.reminderMessage = "プリンターの件、どうでしょうか…？";

            // ---------------- Attachments (manuals) ----------------
            DocumentDefinition Doc(string id, string title, string file_, string icon, string requiredFeature, string intro)
            {
                var d = b.Add(ScriptableObject.CreateInstance<DocumentDefinition>(), "Documents", "DOC_" + id);
                d.documentId = id; d.title = title; d.fileName = file_; d.icon = icon; d.requiredFeature = requiredFeature; d.body = intro;
                return d;
            }
            void Sec(DocumentDefinition d, string feature, params string[] lines) =>
                d.sections.Add(new DocumentSection { requiredFeature = feature, text = string.Join("\n", lines) });

            var manualSystem = Doc("SYSTEM_MANUAL", "社内情報システム 操作マニュアル（夜間運用担当向け）", "業務システム操作マニュアル.doc", "doc", "",
                "本書は社内情報システムの操作方法をまとめたものです。担当範囲が広がると、章が追加されます。");
            Sec(manualSystem, Features.ConnectionMonitor,
                B("■ ネットワーク接続監視（接続要求の審査）"),
                "社内ネットワークへの接続要求が届くと、画面右下に通知が表示されます。一覧から要求を選び、内容を確認してから処理してください。",
                $"・{Col("接続を許可(A)", Allow)} / {Col("接続を拒否(B)", Deny)} … 選択中の要求を処理します。{B("処理後の取り消しはできません。")}",
                "・社員情報を参照(R) … 利用者の社員番号（または端末番号）で社員情報を検索します。",
                $"・要求には {B("受付からの期限")} があります。期限を過ぎた要求は自動的に破棄されます。",
                "・確認する項目: 利用者 / 社員番号 / 端末 / 接続元（社内・社外） / 接続先 / 接続種別");
            Sec(manualSystem, Features.EmployeeDirectory,
                B("■ 社員情報の検索・参照"),
                "社員番号・氏名・部署・端末番号で検索できます（部分一致）。ダブルクリックまたは [詳細表示] で詳細を表示します。",
                $"・{B("登録端末")} … その社員に割り当てられたPC。{B("VPN端末")} は社外接続用の登録ノートPCです。",
                $"・{Col("該当する社員が見つからない", Deny)} 場合、その利用者・端末は社員として登録されていません。",
                $"・{B("システム利用権限 / 管理者権限")} … ○ は権限あり、× は権限なし。",
                $"・{B("通常利用システム")} … その社員が普段使うシステム。{Col("権限とは別の情報", Check)} です。");
            Sec(manualSystem, Features.HelpDesk,
                B("■ ITサポート受付管理"),
                $"・{B("相談")} … 社員からの質問です。回答を選んで [回答を送信(S)] します。",
                $"・{B("障害")} … 社員PCの不具合です。[リモート接続(R)] で社員PCを操作し、直ったことを確認してから [対応完了を報告(P)] してください。",
                $"・{Col("リモート接続中も業務時間は進みます。", Deny)} 他の業務が入ったら、いったん中断して戻ってきても構いません。");
            Sec(manualSystem, Features.SystemLog,
                B("■ システムログの検索・参照"),
                "社内システムの主な通信・処理の記録です。キーワード（IPアドレス、サーバー名、利用者など）と種別で絞り込めます。",
                "・行をダブルクリックすると、その接続元で絞り込みます。",
                $"・ログは自動では分析されません。{B("同じ接続元・同じ時間帯の記録")} に注目してください。");
            Sec(manualSystem, Features.NetworkMap,
                B("■ ネットワークマップ"),
                "通信の流れ（接続元 → 接続先）を表示します。サーバーは監視のみで、操作はできません。");
            Sec(manualSystem, "",
                B("■ キーボード操作"),
                "・ウィンドウ切替: Ctrl+Tab / Alt+PageUp　・ウィンドウを閉じる: Ctrl+F4　・一時停止: Esc",
                "・業務メニューの各システムは、メインメニューで 1〜7 のキーでも開けます。");
            Sec(manualSystem, Features.RemoteSupport,
                B("■ リモートPCのキーボード操作"),
                "・スタート Alt+Home / ファイル名を指定して実行 Ctrl+Alt+R / エクスプローラー Ctrl+Alt+E",
                "・設定 Ctrl+Alt+I / デスクトップ Ctrl+Alt+D / タスク マネージャー Ctrl+Alt+End / 更新 F5",
                "（Windows キーや Alt+Tab は手元のPCが処理するため、上記の代替キーを使ってください）");

            var manualServer = Doc("SERVER_GUIDE", "社内サーバー構成資料", "社内サーバー構成資料.doc", "server", "",
                $"本資料は社内ネットワークとサーバーの概要です。サーバーの管理操作（サービス停止・再起動など）は、まだ担当範囲ではありません。");
            Sec(manualServer, "",
                B("■ ネットワークの区分"),
                $"・{B("社外 (INTERNET)")} … インターネット。すべての社外からの通信は FIREWALL を通ります。",
                $"・{B("公開 (DMZ)")} … 社外に公開している Web サーバー。社外からのアクセスは日常的に発生します。",
                $"・{B("社内")} … 業務システム。社外から利用する場合は {Col("VPN 経由", Check)} が必要です。",
                $"・{B("社員端末 (EMPLOYEE PCs)")} … 各社員の登録PC。",
                "",
                B("■ サーバー一覧（接続種別と必要な権限）"),
                "{SERVER_LIST}",
                "",
                B("■ 補足"),
                $"・{Col("管理系", Deny)} のサーバーへのログインには、{Col("サーバー管理者", Check)} などの管理者権限が必要です。",
                $"・{B("重要")} のサーバーは業務データを扱います。登録されていない端末からの利用は認められていません。");

            var manualTrouble = Doc("TROUBLE_MANUAL", "PCトラブル対応マニュアル（リモートサポート）", "トラブル対応マニュアル.doc", "helpdesk", Features.RemoteSupport,
                "社員PCの不具合にリモートで対応するための手順です。");
            Sec(manualTrouble, "",
                B("■ 基本の流れ"),
                $"1. {B("症状を確認")} … 社員からの問い合わせ内容を読み、何ができないのかを把握する。",
                $"2. {B("リモート接続")} … 受付管理から社員PCに接続する。",
                $"3. {B("原因を調べる")} … 設定画面・通知領域のアイコン・コマンド プロンプトなどで状態を確認する。",
                $"4. {B("対処する")} … 原因に応じて設定を変更する。",
                $"5. {Col("動作を確認する", Check)} … 実際に使えるようになったか確かめる（例: Web ページの表示、ping、印刷の状態）。",
                $"6. {B("報告する")} … [対応完了を報告] で社員に確認してもらう。{Col("直っていない状態で報告すると、社員から再度連絡が来ます。", Deny)}",
                "",
                B("■ ネットワークにつながらない場合の確認箇所"),
                "・画面右下の通知領域のネットワーク アイコン",
                "・[設定] → [ネットワーク]（Wi-Fi の状態、IP アドレス）",
                "・[ネットワーク接続]（ファイル名を指定して実行: ncpa.cpl）… アダプターの有効／無効",
                "・コマンド プロンプト（ファイル名を指定して実行: cmd）",
                "　　ipconfig / ipconfig /all … IP アドレス・ゲートウェイの確認",
                "　　ping <ホスト名> … 相手に届くかの確認（例: ping intranet）",
                "",
                B("■ 印刷できない場合の確認箇所"),
                "・[設定] → [プリンター] … プリンターの状態",
                "・プリンターのキュー（通知領域のプリンター アイコン / control printers）… ドキュメントの状態と [プリンター] メニュー",
                "",
                B("■ その他のツール"),
                "・タスク マネージャー（Ctrl+Alt+End）… 実行中のアプリケーション・プロセス・ネットワーク状況",
                "・エクスプローラー（Ctrl+Alt+E）… ファイル・共有フォルダー（\\\\FILE-SRV01）",
                $"・{Col("システムのプロセスは終了しないでください。", Deny)}");

            // ---------------- Days ----------------
            string[] ruleTexts =
            {
                $"{B("社員")}が{B("自分の登録端末")}から接続する場合は {Col("許可", Allow)} する。",
                $"{B("社員として登録されていない利用者")}、または {B("その社員の登録端末ではない端末")} からの接続は {Col("拒否", Deny)}。",
                $"接続先のシステムを使うには {B("権限")} が必要。社員情報で {Col("接続種別に必要な権限", Check)} を確認し、× なら {Col("拒否", Deny)}。",
                $"{B("社外")}から{B("管理系システム")}（ADMIN-SRV01 等）への接続は {Col("禁止", Deny)}。",
                $"社外からの接続 (VPN) は、{Col("VPN利用を許可された社員の登録端末", Check)} に限る。",
            };

            DayDefinition Day(int n, string title, string date, string clockIn, float speed, float gateUntil, int ruleCount, int newFrom)
            {
                var d = b.Add(ScriptableObject.CreateInstance<DayDefinition>(), "Days", $"DAY{n}_{title.Replace(" ", "")}");
                d.dayNumber = n; d.title = title; d.dateLabel = date; d.clockInDateLabel = clockIn;
                d.realSecondsPerGameMinute = speed;
                d.briefingFrom = "情報システム部 課長 森田";
                for (int i = 0; i < ruleCount; i++) d.rules.Add(ruleTexts[i]);
                d.newRuleStartIndex = newFrom;
                d.tutorialGateUntilMinute = gateUntil;
                d.tutorialGateBreathMinutes = 2f;
                d.tutorialGateMaxDelayMinutes = 30f;
                d.backgroundLogChance = 0.3f;
                db.days.Add(d);
                return d;
            }
            void Slot(DayDefinition d, string label, float a, float c, params EventDefinition[] pool)
            {
                var s = new RandomEventSlot { label = label, window = new Vector2(a, c) };
                s.pool.AddRange(pool);
                d.randomSlots.Add(s);
            }
            const string Footer = "\n※ 操作方法などは添付のマニュアルを参照してください。この業務連絡は、タスクバーの [業務連絡] からいつでも確認できます。";

            // DAY1
            var day1 = Day(1, "FIRST SHIFT", "1998/05/20 (水)", "1998/05/19 (火)", 1.5f, 240f, 2, -1);
            day1.briefingBody =
                "夜間IT担当として、今夜からよろしくお願いします。\n" +
                $"業務時間は {B("00:00〜04:00")} です。{B("時間は止まりません")}（Esc で一時停止できます）。\n\n" +
                "■ 本日の業務\n" +
                $"・{B("接続要求の審査")}（ネットワーク接続監視）\n" +
                "　社内ネットワークへの接続要求を、社員情報と照らし合わせて許可・拒否してください。\n\n" +
                "■ 接続審査のルール";
            day1.briefingFooter =
                $"利用者と端末は {Col("「社員情報の検索・参照」", Check)} で確認できます。\n" +
                "本日使うのは「ネットワーク接続監視」と「社員情報の検索・参照」だけです。ほかのシステムは、慣れてきたら順に担当していただきます。\n" +
                $"{B("迷ったら、まず調べること。")}\n" + Footer;
            day1.policy = new PolicyRuleSet
            {
                blockUnknownUser = true, blockUnregisteredDevice = true,
                blockExternalToAdministrative = false, blockUnregisteredToCritical = false, requirePermission = false, requireVpnPermission = false
            };
            day1.unlockedFeatures.AddRange(new[] { Features.ConnectionMonitor, Features.EmployeeDirectory });
            day1.attachments.Add(manualSystem);
            day1.fixedEvents.AddRange(new[] { firstNormal, d1Unregistered, d1OtherDevice, d1Unknown, d1Final });
            Slot(day1, "routine-1", 8, 14, routine);
            Slot(day1, "routine-2", 40, 55, routine);
            Slot(day1, "routine-3", 95, 110, routine);
            Slot(day1, "routine-4", 150, 170, routine);

            // DAY2
            var day2 = Day(2, "ACCESS RIGHTS", "1998/05/21 (木)", "1998/05/20 (水)", 1.75f, 120f, 3, 2);
            day2.briefingBody =
                "昨夜はお疲れさまでした。\n" +
                $"本日から {Col("ITサポート受付管理", Allow)}（社員からの相談・リモートサポート）も担当していただきます。\n\n" +
                "■ 本日の業務\n" +
                $"・{B("接続要求の審査")}\n" +
                $"・社員からの{B("IT問い合わせ")}への対応（相談への回答／リモートサポート）\n\n" +
                "■ 接続審査のルール";
            day2.briefingFooter =
                $"接続種別ごとに必要な権限は、添付の {Col("「社内サーバー構成資料」", Check)} で確認できます。\n" +
                $"リモートサポートの手順は {Col("「トラブル対応マニュアル」", Check)} を参照してください。\n" +
                $"{B("社員本人で、本人の端末でも、権限がなければ拒否です。")}\n" + Footer;
            day2.policy = new PolicyRuleSet
            {
                blockUnknownUser = true, blockUnregisteredDevice = true, requirePermission = true,
                blockExternalToAdministrative = false, blockUnregisteredToCritical = false, requireVpnPermission = false
            };
            day2.unlockedFeatures.AddRange(new[] { Features.ConnectionMonitor, Features.EmployeeDirectory, Features.HelpDesk, Features.RemoteSupport });
            day2.attachments.AddRange(new[] { manualSystem, manualServer, manualTrouble });
            day2.fixedEvents.AddRange(new[] { firstNormal, d2HR, d2NoAccounting, d2LooksOdd, d2OtherDevice, d2Final });
            day2.helpDesk.AddRange(new[] { hdA1, hdR1 });
            Slot(day2, "routine-1", 55, 65, routine);
            Slot(day2, "routine-2", 130, 150, routine);
            Slot(day2, "routine-3", 170, 185, routine);

            // DAY3
            var day3 = Day(3, "OUTSIDE THE WALL", "1998/05/22 (金)", "1998/05/21 (木)", 2f, 60f, 5, 3);
            day3.briefingBody =
                $"本日から {Col("システムログ", Allow)}・{Col("ネットワークマップ", Allow)}・{Col("セキュリティ情報", Allow)} も利用できます。\n" +
                $"{B("社外からの接続")}や、{B("不審な通信")}にも注意してください。\n\n" +
                "■ 本日の業務\n" +
                $"・{B("接続要求の審査")}\n" +
                $"・{B("IT問い合わせ")}への対応\n" +
                $"・{B("システムログ・ネットワークの監視")}\n\n" +
                "■ 接続審査のルール";
            day3.briefingFooter =
                $"不審な要求があったら、{Col("システムログ", Check)} で同じ接続元の記録がないか確認してください。\n" +
                "サーバー運用管理は来週から担当範囲に加わる予定です。\n" +
                $"{B("迷ったら、まず調べること。")}\n" + Footer;
            day3.policy = new PolicyRuleSet
            {
                blockUnknownUser = true, blockUnregisteredDevice = true, requirePermission = true,
                blockExternalToAdministrative = true, blockUnregisteredToCritical = true, requireVpnPermission = true
            };
            day3.unlockedFeatures.AddRange(new[] { Features.ConnectionMonitor, Features.EmployeeDirectory, Features.HelpDesk, Features.RemoteSupport,
                Features.SystemLog, Features.NetworkMap, Features.SecurityInfo });
            day3.attachments.AddRange(new[] { manualSystem, manualServer, manualTrouble });
            day3.fixedEvents.AddRange(new[] { firstNormal, d3Suspicious, d3VpnLegit, d3NoVpn });
            day3.helpDesk.AddRange(new[] { hdA2, hdR2 });
            day3.threats.Add(noise);
            day3.threats.Add(threat);
            Slot(day3, "routine-1", 24, 32, routine);
            Slot(day3, "routine-2", 78, 92, routine, routineVpn);
            Slot(day3, "routine-3", 150, 170, routine, routineVpn);
            Slot(day3, "routine-4", 210, 224, routine);
            Slot(day3, "scan-noise", 60, 75, scanLog);

            return b;
        }
    }
}
