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
    public static partial class DefaultDataFactory
    {
        /// <summary>初期データの版。内容を変えたら上げる（エディタが古いアセットの再生成を行う）。</summary>
        public const int DataVersion = 21;

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
            P("GENERAL_BUSINESS", "メール・Web", PermissionCategory.SystemUse, 10, "メール・社内Web・インターネット");
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
            internet.accessTypes.Add(A("Web閲覧", "WEB", "GENERAL_BUSINESS"));
            N("FIREWALL", NodeRole.Firewall, NodeZone.Perimeter, "10.10.0.254", new Vector2(0.29f, 0.5f), false, false);
            var pcs = N("EMPLOYEE PCs", NodeRole.ClientGroup, NodeZone.Client, "10.10.x.x", new Vector2(0.92f, 0.5f), false, false);
            pcs.manageableFromDay = 99;
            var web = N("WEB-SRV01", NodeRole.Server, NodeZone.DMZ, "172.16.0.10", new Vector2(0.6f, 0.1f), false, false);
            web.accessTypes.Add(A("公開Web", "CORP-WEB", ""));
            var mail = N("MAIL-SRV01", NodeRole.Server, NodeZone.Internal, "10.10.0.20", new Vector2(0.6f, 0.26f), true, false);
            mail.accessTypes.Add(A("メール", "MAIL", "GENERAL_BUSINESS"));
            var file = N("FILE-SRV01", NodeRole.Server, NodeZone.Internal, "10.10.0.30", new Vector2(0.6f, 0.42f), true, false);
            file.accessTypes.Add(A("共有ファイル（閲覧）", "FILE", "FILE_ACCESS"));
            file.accessTypes.Add(A("共有ファイル（更新）", "FILE", "FILE_ACCESS"));
            var app = N("APP-SRV01", NodeRole.Server, NodeZone.Internal, "10.10.0.40", new Vector2(0.6f, 0.58f), true, false);
            app.accessTypes.Add(A("開発環境", "DEV", "DEV_ACCESS"));
            app.accessTypes.Add(A("人事システム", "HR", "HR_ACCESS"));
            app.accessTypes.Add(A("経理システム", "ACCOUNTING", "ACCOUNTING_ACCESS"));
            var auth = N("AUTH-SRV01", NodeRole.Server, NodeZone.Internal, "10.10.0.5", new Vector2(0.6f, 0.74f), true, false);
            auth.accessTypes.Add(A("VPN接続", "VPN", "GENERAL_BUSINESS", true));
            var admin = N("ADMIN-SRV01", NodeRole.Server, NodeZone.Internal, "10.10.0.2", new Vector2(0.6f, 0.9f), true, true);
            admin.accessTypes.Add(A("管理ログイン", "ADMIN", "SERVER_ADMIN"));
            admin.accessTypes.Add(A("リモート管理", "ADMIN", "SERVER_ADMIN"));

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
                new[] { "WEB", "MAIL", "FILE", "ADMIN" }, new[] { "GENERAL_BUSINESS", "FILE_ACCESS", "LOCAL_ADMIN", "NETWORK_ADMIN", "SERVER_ADMIN" }, "NB-IT02");
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
                e.eventId = id; e.minimumDay = 1; e.maximumDay = 8; e.triggerWindow = window;
                e.timeoutMinutes = 30;
                return e;
            }

            // --- 共通 ---
            var routine = Ev("REQ_ROUTINE", Vector2.zero);
            routine.destinationMode = DestinationMode.FromEmployeeNormalTargets;
            routine.employeeConditions.preferUnused = false;
            routine.timeoutMinutes = 25;

            var routineVpn = Ev("REQ_ROUTINE_VPN", Vector2.zero);
            routineVpn.minimumDay = 6;
            routineVpn.sourceType = SourceType.RegisteredLaptopExternal;
            routineVpn.employeeConditions.requireVpnEnabled = true;
            routineVpn.employeeConditions.requireLaptop = true;
            routineVpn.employeeConditions.preferUnused = false;
            routineVpn.destination = auth; routineVpn.accessType = "VPN接続";
            routineVpn.weight = 0.5f;
            routineVpn.timeoutMinutes = 25;

            var firstNormal = Ev("REQ_FIRST_NORMAL", new Vector2(2, 4));
            firstNormal.employeeConditions.requiredNormalTargets.Add("MAIL");
            firstNormal.employeeConditions.requiredPermissions.Add(perms["GENERAL_BUSINESS"]);
            firstNormal.destination = mail; firstNormal.accessType = "メール";

            var scanLog = Ev("LOG_SCANNER_NOISE", Vector2.zero);
            scanLog.minimumDay = 6;
            scanLog.category = EventCategory.Threat;
            scanLog.kind = EventKind.LogEntry;
            scanLog.userType = UserType.Unknown;
            scanLog.sourceType = SourceType.ExternalUnknown;
            scanLog.destination = web; scanLog.accessType = "TCP";
            scanLog.logEventType = "FW";
            scanLog.logResult = "DENY";

            // --- ヘルパー ---
            EventDefinition Unregistered(string id, Vector2 w)
            {
                var e = Ev(id, w);
                e.severity = Severity.Low;
                e.sourceType = SourceType.UnregisteredDevice;
                e.deviceNamePattern = "NB-PRIV{n}";
                e.employeeConditions.requiredPermissions.Add(perms["FILE_ACCESS"]);
                e.employeeConditions.requiredNormalTargets.Add("FILE");
                e.destination = file; e.accessType = "共有ファイル（閲覧）";
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
            // 社員名簿に存在しない利用者（退職者・ゲストなど）
            EventDefinition NotAnEmployee(string id, Vector2 w, string name, string empId, SourceType source, string devicePattern, NetworkNodeDefinition dest, string access)
            {
                var e = Ev(id, w);
                e.severity = Severity.Medium;
                e.userType = UserType.NamedAccount;
                e.accountName = name;
                e.displayEmployeeId = empId;
                e.sourceType = source;
                e.deviceNamePattern = devicePattern;
                e.destination = dest; e.accessType = access;
                return e;
            }
            EventDefinition NoPermission(string id, Vector2 w, string dept, string missingPerm, NetworkNodeDefinition dest, string access)
            {
                var e = Ev(id, w);
                e.severity = Severity.Medium;
                if (!string.IsNullOrEmpty(dept)) e.employeeConditions.departments.Add(dept);
                e.employeeConditions.excludedPermissions.Add(perms[missingPerm]);
                e.destination = dest; e.accessType = access;
                return e;
            }
            EventDefinition ExternalUnknownAdmin(string id, Vector2 w)
            {
                var e = Ev(id, w);
                e.category = EventCategory.Threat;
                e.notifyAsSecurityAlert = true;
                e.severity = Severity.High;
                e.userType = UserType.Unknown;
                e.sourceType = SourceType.ExternalUnknown;
                e.deviceNamePattern = "UNKNOWN-{n}";
                e.destination = admin; e.accessType = "管理ログイン";
                e.isMalicious = true;
                e.timeoutMinutes = 40;
                return e;
            }

            // --- DAY1: 社員かどうか ---
            var d1Former = NotAnEmployee("D1_REQ_FORMER_EMPLOYEE", new Vector2(20, 26), "木村 進", "E-0009", SourceType.UnregisteredDevice, "SALES-PC1{n}", mail, "メール");
            var d1Unknown = ExternalUnknownAdmin("D1_REQ_UNKNOWN_USER", new Vector2(90, 105));
            d1Unknown.deviceNamePattern = "UNKNOWN-14";
            var d1Final = NotAnEmployee("D1_REQ_FINAL_GUEST", new Vector2(190, 205), "guest", "-", SourceType.UnregisteredDevice, "PC-GUEST{n}", file, "共有ファイル（閲覧）");

            // --- DAY2: 端末 ---
            var d2Unregistered = Unregistered("D2_REQ_UNREGISTERED_DEVICE", new Vector2(20, 28));
            var d2OtherDevice = OtherDevice("D2_REQ_OTHER_EMPLOYEES_PC", new Vector2(60, 75), mail, "メール", "MAIL");
            var d2Former = NotAnEmployee("D2_REQ_FORMER_EMPLOYEE", new Vector2(130, 140), "田村 洋子", "E-0099", SourceType.UnregisteredDevice, "HR-PC0{n}", app, "人事システム");
            var d2Final = OtherDevice("D2_REQ_FINAL_OTHER_DEVICE", new Vector2(195, 205), file, "共有ファイル（閲覧）", "FILE");
            d2Final.employeeConditions.requiredPermissions.Add(perms["FILE_ACCESS"]);

            // --- DAY3: 権限 ---
            var d3HR = Ev("D3_REQ_HR_SYSTEM", new Vector2(10, 14));
            d3HR.employeeConditions.requiredPermissions.Add(perms["HR_ACCESS"]);
            d3HR.employeeConditions.requiredNormalTargets.Add("HR");
            d3HR.destination = app; d3HR.accessType = "人事システム";
            var d3NoAccounting = NoPermission("D3_REQ_NO_ACCOUNTING", new Vector2(30, 36), "営業部", "ACCOUNTING_ACCESS", app, "経理システム");
            var d3LooksOdd = Ev("D3_REQ_DEV_FILE_LOOKS_ODD", new Vector2(70, 80));
            d3LooksOdd.employeeConditions.departments.Add("開発部");
            d3LooksOdd.employeeConditions.requiredPermissions.Add(perms["FILE_ACCESS"]);
            d3LooksOdd.employeeConditions.requiredNormalTargets.Add("FILE");
            d3LooksOdd.destination = file; d3LooksOdd.accessType = "共有ファイル（更新）";
            var d3Unregistered = Unregistered("D3_REQ_UNREGISTERED_DEVICE", new Vector2(130, 140));
            var d3Final = NoPermission("D3_REQ_FINAL_NO_DEV", new Vector2(195, 204), "カスタマーサポート部", "DEV_ACCESS", app, "開発環境");

            // --- DAY4: サーバー（構成資料で必要な権限を調べる） ---
            var d4MailOk = Ev("D4_REQ_MAIL_OK", new Vector2(10, 14));
            d4MailOk.employeeConditions.requiredPermissions.Add(perms["GENERAL_BUSINESS"]);
            d4MailOk.employeeConditions.requiredNormalTargets.Add("MAIL");
            d4MailOk.destination = mail; d4MailOk.accessType = "メール";
            var d4AccountingOk = Ev("D4_REQ_ACCOUNTING_OK", new Vector2(30, 36));
            d4AccountingOk.employeeConditions.requiredPermissions.Add(perms["ACCOUNTING_ACCESS"]);
            d4AccountingOk.destination = app; d4AccountingOk.accessType = "経理システム";
            var d4NoHR = NoPermission("D4_REQ_NO_HR", new Vector2(60, 70), "営業部", "HR_ACCESS", app, "人事システム");
            var d4DevOk = Ev("D4_REQ_DEV_OK", new Vector2(95, 105));
            d4DevOk.employeeConditions.requiredPermissions.Add(perms["DEV_ACCESS"]);
            d4DevOk.employeeConditions.requiredNormalTargets.Add("DEV");
            d4DevOk.destination = app; d4DevOk.accessType = "開発環境";
            // ローカル管理者はあるが、サーバー管理者ではない（管理者権限の種類に注意）
            var d4Final = NoPermission("D4_REQ_FINAL_LOCAL_ADMIN_ONLY", new Vector2(195, 204), "開発部", "SERVER_ADMIN", admin, "リモート管理");
            d4Final.employeeConditions.requiredPermissions.Add(perms["LOCAL_ADMIN"]);

            // --- DAY5: ITサポート ---
            var d5NoDev = NoPermission("D5_REQ_NO_DEV", new Vector2(70, 80), "カスタマーサポート部", "DEV_ACCESS", app, "開発環境");
            var d5OtherDevice = OtherDevice("D5_REQ_OTHER_EMPLOYEES_PC", new Vector2(130, 140), file, "共有ファイル（閲覧）", "FILE");
            var d5Final = NoPermission("D5_REQ_FINAL_NO_ACCOUNTING", new Vector2(195, 204), "営業部", "ACCOUNTING_ACCESS", app, "経理システム");

            // --- DAY6: 社外 ---
            var d6VpnLegit = Ev("D6_REQ_VPN_LEGIT", new Vector2(25, 32));
            d6VpnLegit.sourceType = SourceType.RegisteredLaptopExternal;
            d6VpnLegit.employeeConditions.requireVpnEnabled = true;
            d6VpnLegit.employeeConditions.requireLaptop = true;
            d6VpnLegit.destination = auth; d6VpnLegit.accessType = "VPN接続";
            // VPN を使わずに社外から社内のシステムへ（VPN許可のある社員でも拒否）
            var d6DirectFile = Ev("D6_REQ_DIRECT_FILE_FROM_OUTSIDE", new Vector2(75, 85));
            d6DirectFile.severity = Severity.Medium;
            d6DirectFile.sourceType = SourceType.RegisteredLaptopExternal;
            d6DirectFile.employeeConditions.requireVpnEnabled = true;
            d6DirectFile.employeeConditions.requireLaptop = true;
            d6DirectFile.employeeConditions.requiredPermissions.Add(perms["FILE_ACCESS"]);
            d6DirectFile.destination = file; d6DirectFile.accessType = "共有ファイル（閲覧）";
            // VPN利用が「不可」の社員
            var d6NoVpn = Ev("D6_REQ_NO_VPN", new Vector2(110, 120));
            d6NoVpn.severity = Severity.Medium;
            d6NoVpn.sourceType = SourceType.RegisteredDeviceExternal;
            d6NoVpn.employeeConditions.requireVpnDisabled = true;
            d6NoVpn.destination = auth; d6NoVpn.accessType = "VPN接続";
            // 最後の問題: サーバー管理者の社員が自宅から直接「管理ログイン」→ 管理者でも VPN接続 以外は拒否
            var d6Final = Ev("D6_REQ_FINAL_ADMIN_FROM_HOME", new Vector2(195, 204));
            d6Final.severity = Severity.Medium;
            d6Final.sourceType = SourceType.RegisteredLaptopExternal;
            d6Final.employeeConditions.requireOnShift = false; // 退勤後に自宅から
            d6Final.employeeConditions.requireVpnEnabled = true;
            d6Final.employeeConditions.requireLaptop = true;
            d6Final.employeeConditions.requiredPermissions.Add(perms["SERVER_ADMIN"]);
            d6Final.destination = admin; d6Final.accessType = "管理ログイン";
            // スキャンしてきた外部IPが、そのまま管理サーバーへログインを試みる（ログで同じIPと分かる）
            var d6ScanAdminReq = Ev("D6_REQ_SCANNER_ADMIN", Vector2.zero, "Threats");
            d6ScanAdminReq.category = EventCategory.Threat;
            d6ScanAdminReq.notifyAsSecurityAlert = true;
            d6ScanAdminReq.severity = Severity.High;
            d6ScanAdminReq.userType = UserType.Unknown;
            d6ScanAdminReq.sourceType = SourceType.AttackerSource;
            d6ScanAdminReq.deviceNamePattern = "UNKNOWN-{n}";
            d6ScanAdminReq.destination = admin; d6ScanAdminReq.accessType = "管理ログイン";
            d6ScanAdminReq.isMalicious = true;
            d6ScanAdminReq.timeoutMinutes = 40;

            // --- DAY7: 総仕上げ ---
            var d7Suspicious = ExternalUnknownAdmin("D7_REQ_SUSPICIOUS_DURING_REMOTE", new Vector2(30, 55));
            d7Suspicious.preferWhileRemoteActive = true; // 唯一の意図的な同時発生
            var d7Unregistered = Unregistered("D7_REQ_UNREGISTERED_DEVICE", new Vector2(150, 160));
            var d7Final = NotAnEmployee("D7_REQ_FINAL_FORMER_VPN", new Vector2(200, 206), "木村 進", "E-0009", SourceType.ExternalUnknown, "NB-KIMURA", auth, "VPN接続");

            // ---------------- Threat scenario ----------------
            var threatReq = Ev("REQ_THREAT_ADMIN", Vector2.zero, "Threats");
            threatReq.category = EventCategory.Threat;
            threatReq.userType = UserType.NamedAccount;
            threatReq.accountName = "admin";
            threatReq.sourceType = SourceType.AttackerRelatedSource;
            threatReq.deviceNamePattern = "WIN-{hex}";
            threatReq.destination = admin; threatReq.accessType = "管理ログイン";
            threatReq.isMalicious = true;
            threatReq.notifyAsSecurityAlert = true;
            threatReq.severity = Severity.High;
            threatReq.timeoutMinutes = 35;

            var threat = b.Add(ScriptableObject.CreateInstance<ThreatScenarioDefinition>(), "Threats", "THREAT_WebProbeToAdmin");
            threat.scenarioId = "THREAT_WEB_PROBE";
            threat.minimumDay = 7;
            threat.designNote = "外部の攻撃者（自動スクリプト＋手動操作）が公開Webを探索し、管理サーバーへのログインを試みる。ログを見れば同一レンジのIPと気付ける。";
            threat.startWindow = new Vector2(90, 97);
            threat.sourceIpPrefix = "198.51.100.";
            threat.steps.Add(new ThreatStep { label = "公開Webへのアクセス", offsetMinutes = 0, kind = ThreatStepKind.LogEntry, destination = web, eventType = "HTTP", result = "200", detail = "GET / (python-urllib/2.0)" });
            threat.steps.Add(new ThreatStep { label = "管理ページ探索", offsetMinutes = 4, kind = ThreatStepKind.LogEntry, destination = web, eventType = "HTTP", result = "404", detail = "GET /admin/|GET /manager/html|GET /login.cgi|GET /cgi-bin/admin.pl", repeat = 4, repeatIntervalMinutes = 0.6f });
            threat.steps.Add(new ThreatStep { label = "再アクセス", offsetMinutes = 9, kind = ThreatStepKind.LogEntry, destination = web, eventType = "HTTP", result = "200", detail = "GET /about/staff.html" });
            threat.steps.Add(new ThreatStep { label = "管理サーバーのポート探索", offsetMinutes = 12, kind = ThreatStepKind.LogEntry, useRelatedSource = true, destination = admin, eventType = "FW", result = "DENY", detail = "TCP 23|TCP 3389|TCP 22", repeat = 3, repeatIntervalMinutes = 0.4f });
            threat.steps.Add(new ThreatStep { label = "管理サーバーへの接続要求", offsetMinutes = 16, kind = ThreatStepKind.ConnectionRequest, useRelatedSource = true, destination = admin, request = threatReq });

            var noise = b.Add(ScriptableObject.CreateInstance<ThreatScenarioDefinition>(), "Threats", "THREAT_ScannerNoise");
            noise.scenarioId = "THREAT_SCANNER_NOISE";
            noise.minimumDay = 6;
            noise.designNote = "インターネット上の一般的な自動スキャン。ファイアウォールで拒否されるだけ。";
            noise.startWindow = new Vector2(40, 50);
            noise.sourceIpPrefix = "203.0.113.";
            noise.steps.Add(new ThreatStep { label = "ポートスキャン", offsetMinutes = 0, kind = ThreatStepKind.LogEntry, destination = web, eventType = "FW", result = "DENY", detail = "TCP 21|TCP 23|TCP 445|TCP 1433|TCP 3306", repeat = 5, repeatIntervalMinutes = 0.3f });

            // DAY6: スキャンしたIPが、そのまま管理サーバーへのログインを試みる（ログで同じIPと気付ける）
            var scanThenAdmin = b.Add(ScriptableObject.CreateInstance<ThreatScenarioDefinition>(), "Threats", "THREAT_ScanThenAdmin");
            scanThenAdmin.scenarioId = "THREAT_SCAN_THEN_ADMIN";
            scanThenAdmin.minimumDay = 6;
            scanThenAdmin.designNote = "外部からポートスキャン（ログに記録）→ 同じIPから ADMIN-SRV01 への管理ログイン要求。セキュリティ警告からログを開くと同じIPが見つかる。";
            scanThenAdmin.startWindow = new Vector2(45, 52);
            scanThenAdmin.sourceIpPrefix = "203.0.113.";
            scanThenAdmin.steps.Add(new ThreatStep { label = "ポートスキャン", offsetMinutes = 0, kind = ThreatStepKind.LogEntry, destination = admin, eventType = "FW", result = "DENY", detail = "TCP 22|TCP 23|TCP 3389|TCP 445", repeat = 4, repeatIntervalMinutes = 0.4f });
            scanThenAdmin.steps.Add(new ThreatStep { label = "管理サーバーへの接続要求", offsetMinutes = 6, kind = ThreatStepKind.ConnectionRequest, destination = admin, request = d6ScanAdminReq });

            // ---------------- Help Desk ----------------
            HelpDeskDefinition H(string id, HelpDeskType type, Vector2 window, int day)
            {
                var h = b.Add(ScriptableObject.CreateInstance<HelpDeskDefinition>(), "HelpDesk", "HD_" + id);
                h.definitionId = id; h.type = type; h.triggerWindow = window; h.minimumDay = day;
                return h;
            }

            var hdA1 = H("A1_PASSWORD_PHONE", HelpDeskType.Binary, new Vector2(10, 14), 5);
            hdA1.subject = "パスワードを教えてほしいと言われた";
            hdA1.message = "{dept}の{name}です。\nさきほど「本社の情報システムの者」と名乗る人から電話があり、緊急のメンテナンスなので私のパスワードを教えてほしいと言われました。\n折り返すと言って一度切ったのですが、教えても大丈夫でしょうか？";
            hdA1.correctOption = "パスワードは誰にも教えないよう伝え、電話の内容を記録して報告してもらう。";
            hdA1.incorrectOption = "情報システムの担当者なら問題ないので、折り返して伝えてよいと回答する。";
            hdA1.replyAfterAnswer = "わかりました。そのようにします。";
            hdA1.reminderMessage = "すみません、先ほどの件どうしたらいいでしょうか…？";
            hdA1.explanation = "社員や関係者を装って電話でパスワードなどを聞き出す手口は「ソーシャルエンジニアリング」の典型例。相手が誰であっても、パスワードは教えない。";
            hdA1.sourceNote = "平成28年度 春期 ITパスポート試験 問86（改変）";

            var hdR1 = H("R1_NO_INTERNET", HelpDeskType.Remote, new Vector2(40, 46), 5);
            hdR1.subject = "インターネットにつながらない";
            hdR1.message = "{dept}の{name}です。\nさっきから急にインターネットにつながらなくなりました。メールも送れません。\n見てもらえますか？（端末: {device}）";
            hdR1.pcProfile = profile;
            hdR1.faultVariants.Add(new FaultVariant { label = "Wi-Fi OFF", faults = { new StateValue { key = "WiFiEnabled", value = "false" } } });
            hdR1.resolutionConditions.Add(new StateValue { key = "InternetConnectivity", value = "true" });
            hdR1.successReply = "あ、つながりました！メールも送れました。ありがとうございます！";
            hdR1.failureReply = "うーん、まだつながらないみたいです…。";
            hdR1.reminderMessage = "まだつながらないです…。急ぎのメールがあって。";

            var hdA2 = H("A2_ATTACHMENT", HelpDeskType.Binary, new Vector2(12, 16), 6);
            hdA2.subject = "知らない会社からExcelファイルが届いた";
            hdA2.message = "{dept}の{name}です。\n知らない会社から「請求書の件」という件名でExcelファイルが届きました。\n取引先かもしれないのですが、開いて大丈夫ですか？";
            hdA2.correctOption = "開かずに、送信元や取引関係を確認してから判断するよう伝える。";
            hdA2.incorrectOption = "内容を確認しないと判断できないので、一度開いてみるよう伝える。";
            hdA2.replyAfterAnswer = "了解です。確認してみます。";
            hdA2.reminderMessage = "あの、さっきのファイル、開いちゃっていいですかね？";
            hdA2.explanation = "文書や表計算のファイルには、マクロ機能を悪用したウイルス（マクロウイルス）が仕込まれていることがある。心当たりのない添付は開かず、送信元を確かめる。";
            hdA2.sourceNote = "令和2年度 秋期 ITパスポート試験 問58（改変）";

            var hdR2 = H("R2_CANNOT_PRINT", HelpDeskType.Remote, new Vector2(25, 31), 7);
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
            // マニュアルは「業務ごとに1ページ」。新しい業務が解放されると、その日にページが増える。
            DocumentDefinition Doc(string id, string title, string file_, string icon, string requiredFeature)
            {
                var d = b.Add(ScriptableObject.CreateInstance<DocumentDefinition>(), "Documents", "DOC_" + id);
                d.documentId = id; d.title = title; d.fileName = file_; d.icon = icon; d.requiredFeature = requiredFeature; d.body = "";
                return d;
            }
            void Page(DocumentDefinition d, string title, string feature, params string[] lines) =>
                d.sections.Add(new DocumentSection { title = title, requiredFeature = feature, text = string.Join("\n", lines) });

            // ===== 業務システム操作マニュアル =====
            var manualSystem = Doc("SYSTEM_MANUAL", "社内情報システム 操作マニュアル（夜間運用担当向け）", "業務システム操作マニュアル.doc", "doc", "");
            Page(manualSystem, "はじめに（判断の流れ）", "",
                $"夜間運用担当の主な仕事は、社内ネットワークへの {B("接続要求")} を審査することです。",
                $"届いた要求を1件ずつ選び、社員情報と照らし合わせて {Col("許可", Allow)} か {Col("拒否", Deny)} を決めます。",
                "",
                "下の図は、本日のルールでの判断の流れです（担当範囲が広がると段階が増えます）。",
                "{FIG:REVIEW_FLOW}",
                $"・迷ったら、まず調べること。{B("ページの赤い枠")} が、各業務で見る場所です。");
            Page(manualSystem, "接続要求の審査", Features.ConnectionMonitor,
                "[ネットワーク接続監視] の画面です。要求が届くと画面右下に通知が出ます。",
                "{FIG:UI_CONNECTION}",
                $"・{B("①")} 一覧から要求を1件選びます。",
                $"・{B("②")} 要求内容の {B("利用者・社員番号")} を確認します。",
                $"・{B("③")} [社員情報を参照] で社員情報の画面が開きます。{B("社員番号や氏名を自分で入力して検索")}してください。",
                $"・{B("④")} 確認できたら {Col("接続を許可", Allow)} か {Col("接続を拒否", Deny)} を押します。{B("処理後の取り消しはできません。")}",
                $"・本番の業務では、要求に {B("受付からの期限")} があります（{B("研修期間中は期限切れになりません")}）。");
            Page(manualSystem, "社員の検索", Features.EmployeeDirectory,
                "[社員情報の検索・参照] の画面です。",
                "{FIG:UI_DIRECTORY}",
                $"・{B("①")} 社員番号や氏名を入力して [検索開始]。",
                $"・{B("②")} 結果の一覧に出てくれば社員です。{Col("見つからなければ、社員として登録されていません。", Deny)}");
            Page(manualSystem, "端末の確認", Features.DeviceCheck,
                $"要求の {B("端末")} と、社員の {B("登録端末")} が同じかを見比べます。",
                "{FIG:UI_DEVICE}",
                $"・{B("①")} 接続要求の「端末」",
                $"・{B("②")} 社員情報の「登録端末」",
                $"・{Col("社員本人でも、別の端末（他人のPC・私物のPC）からの接続は拒否です。", Deny)}");
            Page(manualSystem, "社員の詳細", Features.EmployeeDetail,
                "検索結果をダブルクリックするか [詳細表示] で、社員の詳細を開きます。",
                "{FIG:UI_DETAIL}",
                $"・{B("①")} 顔写真・所属・勤務状態",
                $"・{B("②")} 登録端末と VPN端末（社外から使う登録ノートPC）");
            Page(manualSystem, "権限の確認", Features.EmployeePermissions,
                $"要求の {B("必要な権限")} と {B("同じ名前の行")} を、社員の権限欄で探します。",
                "{FIG:UI_PERMISSION}",
                $"・{B("①")} 接続要求の「必要な権限」（例: 人事システム）",
                $"・{B("②")} 社員詳細の権限欄で同じ名前の行を探し、{Col("○ なら許可", Allow)}、{Col("× なら拒否", Deny)}。",
                $"・社員詳細の {B("【普段使うシステム】")} は参考情報です。判断には使いません。");
            Page(manualSystem, "管理者権限", Features.AdminPermissions,
                $"管理用のサーバー（ADMIN-SRV01）への接続には {B("サーバー管理者")} の権限が必要です。",
                "{FIG:UI_ADMIN}",
                $"・{B("①")} 必要な権限が「サーバー管理者」の要求",
                $"・{B("②")} 社員詳細の {B("【管理者権限】")} の欄で「サーバー管理者」を見ます。",
                $"・{Col("「ローカル管理者」は自分のPCの管理権限で、サーバー管理者とは別です。", Deny)}");
            Page(manualSystem, "ITサポート受付", Features.HelpDesk,
                "[ITサポート受付管理] の画面です。社員からの問い合わせが届きます。",
                "{FIG:UI_HELPDESK}",
                $"・{B("①")} 受付一覧。区分が {B("相談")} か {B("障害")} かを見ます。",
                $"・{B("②")} 問い合わせ内容（社員からのメッセージ）",
                $"・{B("③")} 相談 → 回答を選んで [回答を送信]。障害 → [リモート接続] で社員のPCを直します。",
                $"・{Col("リモート接続中も業務時間は進みます。", Deny)} 手順は「トラブル対応マニュアル」を参照してください。");
            Page(manualSystem, "社外からの接続", Features.SystemLog,
                $"接続要求の {B("接続元")} が {Col("「社外」", Check)} のときは、次の3つだけを確認します。",
                "{FIG:UI_EXTERNAL}",
                $"・{B("①")} 利用システムが {B("「VPN接続」")} か",
                $"・{B("②")} 社員情報の {B("「VPN利用」")} が {Col("許可", Allow)} か",
                $"・{B("③")} 接続要求の端末が、社員情報の {B("「VPN端末」")} と同じか",
                "{FIG:EXTERNAL_FLOW}",
                $"・{Col("管理者でも、社外からは「VPN接続」以外は拒否です。", Deny)}");
            Page(manualSystem, "システムログ", Features.SystemLog,
                "[システムログの検索・参照] の画面です。社内の通信・処理の記録が残ります。",
                "{FIG:UI_LOG}",
                $"・{B("①")} {B("セキュリティ警告")} が出たら、要求の接続元（IPアドレス）をキーワードに入れて [検索開始]。",
                $"・{B("②")} 同じIPが {B("前に何をしていたか")}（ポートスキャンなど）を確認します。",
                "・行をダブルクリックすると、その接続元で絞り込みます。ログは自動では分析されません。");
            Page(manualSystem, "ネットワークマップ", Features.NetworkMap,
                "通信の流れ（接続元 → 接続先）を表示します。サーバーは監視のみで、操作はできません。",
                "社内ネットワークの区分は「社内サーバー構成資料」を参照してください。");
            Page(manualSystem, "アカウントとキー操作", "",
                $"・ログインパスワードは [システム設定] → [パスワードの変更] で変更できます。{B("パスワードは他人に教えないこと。")}",
                "・ウィンドウ切替: {KEY:SWITCH} / Alt+PageUp　・閉じる: {KEY:CLOSE}　・一時停止: {KEY:PAUSE}　・メール: {KEY:MAIL}",
                "・接続を許可: {KEY:ALLOW}　・接続を拒否: {KEY:BLOCK}　・社員情報を参照: {KEY:LOOKUP}",
                "・メインメニューの各システムは 1〜7 のキーでも開けます。",
                "（キーの割り当ては [システム設定] → [キー設定] で変更できます）");
            Page(manualSystem, "リモートPCのキー操作", Features.RemoteSupport,
                "・スタート {KEY:REMOTE_START} / ファイル名を指定して実行 {KEY:REMOTE_RUN} / エクスプローラー {KEY:REMOTE_EXPLORER}",
                "・設定 {KEY:REMOTE_SETTINGS} / デスクトップ {KEY:REMOTE_DESKTOP} / タスク マネージャー {KEY:REMOTE_TASKMGR} / 更新 {KEY:REFRESH}",
                "（Windows キーや Alt+Tab は手元のPCが処理するため、上記のキーを使ってください）");

            // ===== 社内サーバー構成資料 =====
            var manualServer = Doc("SERVER_GUIDE", "社内サーバー構成資料", "社内サーバー構成資料.doc", "server", Features.ServerGuide);
            Page(manualServer, "早見表（必要な権限）", "",
                "社内のサーバーと、接続に必要な権限の一覧です。接続要求の「必要な権限」欄に表示される内容と同じです（参考資料）。",
                "{ACCESS_TABLE}",
                $"・{B("「管理者」とつく権限は1種類ではありません。")} {B("ローカル管理者")} は自分のPCの管理権限で、{Col("サーバー管理者", Check)} とは別です。",
                "・サーバーの管理操作（サービス停止・再起動など）は、まだ担当範囲ではありません。");
            Page(manualServer, "ネットワークの区分", Features.NetworkMap,
                "{FIG:NETWORK_ZONES}",
                $"・{B("社外 (INTERNET)")} … インターネット。社外からの通信は FIREWALL を通ります。",
                $"・{B("公開 (DMZ)")} … 社外に公開している Web サーバー。社外からのアクセスは日常的に発生します。",
                $"・{B("社内")} … 業務システム。社外から使うには {Col("VPN 接続", Check)} が必要です。",
                $"・{B("社員端末 (EMPLOYEE PCs)")} … 各社員の登録PC。",
                "",
                B("サーバー一覧"),
                "{SERVER_LIST}");

            // ===== トラブル対応マニュアル =====
            var manualTrouble = Doc("TROUBLE_MANUAL", "PCトラブル対応マニュアル（リモートサポート）", "トラブル対応マニュアル.doc", "helpdesk", Features.RemoteSupport);
            Page(manualTrouble, "基本の流れ", "",
                "社員PCの不具合にリモートで対応する手順です。",
                "{FIG:REMOTE_FLOW}",
                $"1. {B("症状を確認")} … 問い合わせ内容を読み、何ができないのかを把握する。",
                $"2. {B("リモート接続")} … 受付管理の [リモート接続] で社員PCに接続する。",
                $"3. {B("原因を調べる")} … 設定画面・通知領域のアイコン・コマンド プロンプトなどで状態を確認する。",
                $"4. {B("対処する")} … 原因に応じて設定を変更する。",
                $"5. {Col("動作を確認する", Check)} … 実際に使えるようになったか確かめる（Web ページの表示、ping、印刷の状態）。",
                $"6. {B("報告する")} … [対応完了を報告]。{Col("直っていない状態で報告すると、社員から再度連絡が来ます。", Deny)}");
            Page(manualTrouble, "リモート画面の見かた", "",
                "{FIG:UI_REMOTE}",
                $"・{B("①")} 通知領域（ネットワーク・プリンターのアイコン）",
                $"・{B("②")} [スタート] から設定・コマンド プロンプトなどを開けます。",
                $"・{B("③")} 直ったことを確認してから [対応完了を報告]。");
            Page(manualTrouble, "ネットワークにつながらない", "",
                "・画面右下の通知領域のネットワーク アイコン",
                "・[設定] → [ネットワーク]（Wi-Fi の状態、IP アドレス）",
                "・[ネットワーク接続]（ファイル名を指定して実行: ncpa.cpl）… アダプターの有効／無効",
                "・コマンド プロンプト（ファイル名を指定して実行: cmd）",
                "　　ipconfig / ipconfig /all … IP アドレス・ゲートウェイの確認",
                "　　ping <ホスト名> … 相手に届くかの確認（例: ping intranet）");
            Page(manualTrouble, "印刷できない", "",
                "・[設定] → [プリンター] … プリンターの状態",
                "・プリンターのキュー（通知領域のプリンター アイコン / control printers）… ドキュメントの状態と [プリンター] メニュー");
            Page(manualTrouble, "その他のツール", "",
                "・タスク マネージャー（{KEY:REMOTE_TASKMGR}）… 実行中のアプリケーション・プロセス・ネットワーク状況",
                "・エクスプローラー（{KEY:REMOTE_EXPLORER}）… ファイル・共有フォルダー",
                $"・{Col("システムのプロセスは終了しないでください。", Deny)}");

            // ---------------- Rules ----------------
            string rRegistered = $"{B("社員として登録されている利用者")}からの接続は {Col("許可", Allow)} する。";
            string rUnknown = $"{B("社員として登録されていない利用者")}からの接続は {Col("拒否", Deny)}。（社員番号・氏名で検索して確認）";
            string rOwnDevice = $"{B("社員")}が{B("自分の登録端末")}から接続する場合は {Col("許可", Allow)} する。";
            string rDevice = $"{B("その社員の登録端末ではない端末")}からの接続は、社員本人でも {Col("拒否", Deny)}。";
            string rPermission = $"接続要求の {Col("「必要な権限」", Check)} が、社員情報の権限欄で × なら {Col("拒否", Deny)}。";
            // 社外のルールは1つにまとめる（「VPN接続」以外は、管理者でも全部拒否）
            string rExternal = $"接続元が {B("社外")} の場合は {Col("「VPN接続」だけ", Check)}。社員の {B("VPN利用が「許可」")} で、{B("VPN端末")} からなら {Col("許可", Allow)}。それ以外は {Col("拒否", Deny)}。";

            // ---------------- Days ----------------
            var allFeatures = new List<string>();
            DayDefinition Day(int n, string title, string date, string clockIn, float speed, string[] rules, int newFrom, params string[] newFeatures)
            {
                var d = b.Add(ScriptableObject.CreateInstance<DayDefinition>(), "Days", $"DAY{n}_{title.Replace(" ", "")}");
                d.dayNumber = n; d.title = title; d.dateLabel = date; d.clockInDateLabel = clockIn;
                d.realSecondsPerGameMinute = speed;
                d.briefingFrom = "情報システム部 課長 森田";
                d.rules.AddRange(rules);
                d.newRuleStartIndex = newFrom;
                // チュートリアル: 終日「1件ずつ」。強制発生はしない
                d.tutorialGateUntilMinute = d.endMinute;
                d.tutorialGateBreathMinutes = 1.5f;
                d.tutorialGateMaxDelayMinutes = 9999f;
                d.requestsExpire = false; // チュートリアル中は期限切れなし
                d.backgroundLogChance = 0.3f;
                allFeatures.AddRange(newFeatures);
                d.unlockedFeatures.AddRange(allFeatures);
                d.attachments.AddRange(new[] { manualSystem, manualServer, manualTrouble });
                db.days.Add(d);
                return d;
            }
            void Slot(DayDefinition d, string label, float a, float c, params EventDefinition[] pool)
            {
                var s = new RandomEventSlot { label = label, window = new Vector2(a, c) };
                s.pool.AddRange(pool);
                d.randomSlots.Add(s);
            }
            PolicyRuleSet Policy(bool device, bool permission, bool external) => new PolicyRuleSet
            {
                blockUnknownUser = true,
                blockUnregisteredDevice = device,
                requirePermission = permission,
                blockExternalToAdministrative = external,
                blockUnregisteredToCritical = external,
                requireVpnPermission = external
            };
            const string Footer = "\n※ 操作方法などは添付のマニュアルを参照してください。この業務連絡は、タスクバーの [メール] の受信トレイからいつでも確認できます。";
            string Tasks(params string[] lines) => "■ 本日の業務\n" + string.Join("\n", lines) + "\n\n■ 接続審査のルール";

            // DAY1: 社員かどうか
            var day1 = Day(1, "FIRST SHIFT", "1998/05/18 (月)", "1998/05/17 (日)", 1.25f, new[] { rRegistered, rUnknown }, -1,
                Features.ConnectionMonitor, Features.EmployeeDirectory);
            day1.briefingBody =
                "夜間IT担当として、今夜からよろしくお願いします。\n" +
                $"業務時間は {B("00:00〜04:00")} です。{B("時間は止まりません")}（{{KEY:PAUSE}} で一時停止できます）。\n" +
                $"研修期間（7日間）は、業務に慣れてもらうため、{B("仕事を1件ずつ")}回します。片付けると次の仕事が届きます。\n\n" +
                Tasks($"・{B("接続要求の審査")}（ネットワーク接続監視）", "　接続要求の利用者が、社員として登録されているかを確認してください。");
            day1.briefingFooter =
                $"利用者は {Col("「社員情報の検索・参照」", Check)} で、社員番号や氏名から検索できます。\n" +
                "本日使うのは「ネットワーク接続監視」と「社員情報の検索・参照」だけです。\n" +
                $"{B("迷ったら、まず調べること。")}\n" + Footer;
            day1.policy = Policy(false, false, false);
            day1.fixedEvents.AddRange(new[] { firstNormal, d1Former, d1Unknown, d1Final });
            Slot(day1, "routine-1", 8, 14, routine);
            Slot(day1, "routine-2", 40, 55, routine);
            Slot(day1, "routine-3", 120, 140, routine);

            // DAY2: 端末 + パスワード変更
            var day2 = Day(2, "DEVICES", "1998/05/19 (火)", "1998/05/18 (月)", 1.4f, new[] { rOwnDevice, rUnknown, rDevice }, 2,
                Features.DeviceCheck, Features.EmployeeDetail);
            day2.briefingBody =
                "昨夜はお疲れさまでした。\n" +
                $"本日から {Col("端末の確認", Allow)} と {Col("社員情報の詳細表示", Allow)}（顔写真など）が使えます。\n" +
                $"接続要求の {B("端末")} と、社員情報の {B("登録端末")} が表示されるようになります。\n" +
                $"また、{B("ログインパスワードがまだ初期設定のまま")}です。本日中に変更をお願いします。\n\n" +
                Tasks($"・{B("接続要求の審査")} … 利用者だけでなく、{B("接続してきた端末")}も確認してください。",
                      $"・{B("パスワードの変更")} … [システム設定] → [パスワードの変更]");
            day2.briefingFooter =
                $"社員の登録端末は、{Col("「社員情報の検索・参照」", Check)} の検索結果や詳細表示で確認できます。\n" +
                $"ルール1は、本日から {B("「自分の登録端末から」")} の条件が加わりました。\n" +
                $"{B("社員本人でも、他人の端末や私物の端末からの接続は認められません。")}\n" + Footer;
            day2.policy = Policy(true, false, false);
            day2.changedRuleIndices.Add(0); // ルール1に「自分の登録端末から」が加わった
            day2.passwordChangeAtMinute = 6f;
            day2.fixedEvents.AddRange(new[] { firstNormal, d2Unregistered, d2OtherDevice, d2Former, d2Final });
            Slot(day2, "routine-1", 35, 45, routine);
            Slot(day2, "routine-2", 95, 110, routine);
            Slot(day2, "routine-3", 160, 175, routine);

            // DAY3: 権限
            var day3 = Day(3, "ACCESS RIGHTS", "1998/05/20 (水)", "1998/05/19 (火)", 1.5f, new[] { rOwnDevice, rUnknown, rDevice, rPermission }, 3,
                Features.EmployeePermissions, Features.PermissionHint);
            day3.briefingBody =
                $"本日から社員情報で {Col("権限情報", Allow)} を閲覧できます。\n" +
                $"また、接続要求に {Col("「必要な権限」", Allow)} 欄が表示されます。\n" +
                $"{B("社員本人で、本人の端末でも、権限がなければ拒否")}です。\n\n" +
                Tasks($"・{B("接続要求の審査")} … 利用者・端末に加えて、{B("権限")}を確認してください。",
                      $"　要求の {B("「必要な権限」")} を見て、社員情報の {B("権限欄で ○")} なら許可、× なら拒否です。");
            day3.briefingFooter =
                $"{B("怪しく見えても、ルールを満たしていれば許可です。")}\n" + Footer;
            day3.policy = Policy(true, true, false);
            day3.fixedEvents.AddRange(new[] { firstNormal, d3HR, d3NoAccounting, d3LooksOdd, d3Unregistered, d3Final });
            Slot(day3, "routine-1", 45, 55, routine);
            Slot(day3, "routine-2", 100, 115, routine);
            Slot(day3, "routine-3", 160, 175, routine);

            // DAY4: サーバー
            var day4 = Day(4, "SERVERS", "1998/05/21 (木)", "1998/05/20 (水)", 1.6f, new[] { rOwnDevice, rUnknown, rDevice, rPermission }, -1,
                Features.ServerGuide, Features.AdminPermissions);
            day4.briefingBody =
                $"本日から社員情報に {Col("【管理者権限】", Allow)} の欄が表示されます。\n" +
                $"管理用のサーバー（ADMIN-SRV01）への接続には、{B("「サーバー管理者」")} の権限が必要です。\n\n" +
                Tasks($"・{B("接続要求の審査")} … 昨日と同じく、要求の {B("「必要な権限」")} と同じ名前の権限を社員情報で探します。",
                      $"　{B("「サーバー管理者」")} は、社員情報の {B("【管理者権限】")} の欄にあります。");
            day4.briefingFooter =
                $"{B("「管理者」とつく権限は1種類ではありません。")} {B("ローカル管理者")} は自分のPCの管理権限で、サーバー管理者とは別です。\n" +
                $"サーバーの一覧は、添付の {Col("「社内サーバー構成資料」", Check)} で確認できます（参考資料）。\n" + Footer;
            day4.policy = Policy(true, true, false);
            day4.fixedEvents.AddRange(new[] { firstNormal, d4MailOk, d4AccountingOk, d4NoHR, d4DevOk, d4Final });
            Slot(day4, "routine-1", 45, 55, routine);
            Slot(day4, "routine-2", 130, 145, routine);
            Slot(day4, "routine-3", 165, 180, routine);

            // DAY5: ITサポート
            var day5 = Day(5, "HELP DESK", "1998/05/22 (金)", "1998/05/21 (木)", 1.75f, new[] { rOwnDevice, rUnknown, rDevice, rPermission }, -1,
                Features.HelpDesk, Features.RemoteSupport);
            day5.briefingBody =
                $"本日から {Col("ITサポート受付管理", Allow)}（社員からの相談・リモートサポート）も担当していただきます。\n\n" +
                Tasks($"・{B("接続要求の審査")}", $"・社員からの{B("IT問い合わせ")}への対応（相談への回答／リモートサポート）");
            day5.briefingFooter =
                $"リモートサポートの手順は {Col("「トラブル対応マニュアル」", Check)} を参照してください。\n" +
                $"{B("直ったことを自分で確認してから報告すること。")}\n" + Footer;
            day5.policy = Policy(true, true, false);
            day5.fixedEvents.AddRange(new[] { firstNormal, d5NoDev, d5OtherDevice, d5Final });
            day5.helpDesk.AddRange(new[] { hdA1, hdR1 });
            Slot(day5, "routine-1", 25, 30, routine);
            Slot(day5, "routine-2", 95, 110, routine);
            Slot(day5, "routine-3", 160, 175, routine);

            // DAY6: 社外
            var day6 = Day(6, "OUTSIDE THE WALL", "1998/05/25 (月)", "1998/05/24 (日)", 2f,
                new[] { rOwnDevice, rUnknown, rDevice, rPermission, rExternal }, 4,
                Features.SystemLog, Features.NetworkMap, Features.SecurityInfo);
            day6.briefingBody =
                $"本日から {B("社外からの接続")} も審査します。接続要求の {Col("接続元", Check)} が {B("「社外」")} なら、ルール5で判断してください。\n" +
                $"見るところは3つだけです: {B("① 利用システムが「VPN接続」か")}　{B("② 社員の「VPN利用」が許可か")}　{B("③ 端末がその社員の「VPN端末」か")}\n" +
                $"また、{Col("システムログ", Allow)}・{Col("ネットワークマップ", Allow)}・{Col("セキュリティ情報", Allow)} が使えるようになります。\n\n" +
                Tasks($"・{B("接続要求の審査")}（社外からの接続を含む）",
                      $"・{B("IT問い合わせ")}への対応",
                      $"・{B("セキュリティ警告")}が出たら、{Col("システムログ", Check)} でその接続元（IPアドレス）を検索し、前に何をしていたか確認する");
            day6.briefingFooter =
                $"{B("管理者でも、社外からは「VPN接続」以外は拒否です。")}\n" +
                $"判断の流れは、操作マニュアルの {Col("「社外からの接続」", Check)} の図を見てください。\n" + Footer;
            day6.policy = Policy(true, true, true);
            day6.fixedEvents.AddRange(new[] { firstNormal, d6VpnLegit, d6DirectFile, d6NoVpn, d6Final });
            day6.helpDesk.Add(hdA2);
            day6.threats.Add(scanThenAdmin);
            Slot(day6, "routine-1", 95, 105, routine);
            Slot(day6, "routine-2", 135, 150, routine, routineVpn);
            Slot(day6, "routine-3", 165, 180, routine, routineVpn);

            // DAY7: 総仕上げ
            var day7 = Day(7, "LONG NIGHT", "1998/05/26 (火)", "1998/05/25 (月)", 2f,
                new[] { rOwnDevice, rUnknown, rDevice, rPermission, rExternal }, -1);
            day7.briefingBody =
                "研修期間の最終日です。これまでの業務をすべて担当していただきます。\n" +
                $"本日はリモートサポート中に {B("別の業務が割り込むことがあります")}。いったん中断して対応し、あとで戻ってきてください。\n\n" +
                Tasks($"・{B("接続要求の審査")}", $"・{B("IT問い合わせ")}への対応", $"・{B("システムログ・ネットワークの監視")}");
            day7.briefingFooter =
                $"同じ接続元から何度もアクセスがある場合は、{Col("システムログ", Check)} で前後の記録を確認してください。\n" +
                "明日からはサーバー運用管理も担当範囲に加わる予定です。\n" + Footer;
            day7.policy = Policy(true, true, true);
            day7.fixedEvents.AddRange(new[] { firstNormal, d7Suspicious, d7Unregistered, d7Final });
            day7.helpDesk.Add(hdR2);
            day7.threats.Add(noise);
            day7.threats.Add(threat);
            Slot(day7, "routine-1", 60, 75, routine);
            Slot(day7, "routine-2", 120, 140, routine, routineVpn);
            Slot(day7, "routine-3", 170, 185, routine);

            // ---------------- DAY8〜30（本番業務） ----------------
            BuildMainDays(new MainContext
            {
                b = b, db = db, perms = perms,
                web = web, mail = mail, file = file, app = app, auth = auth, admin = admin,
                routine = routine, routineVpn = routineVpn, profile = profile,
                manualSystem = manualSystem, manualServer = manualServer, manualTrouble = manualTrouble,
                baseRules = new[] { rOwnDevice, rUnknown, rDevice, rPermission, rExternal },
                features = allFeatures, scannerNoise = noise, webProbe = threat, scanThenAdmin = scanThenAdmin,
                lastTutorialDate = new System.DateTime(1998, 5, 26)
            });

            return b;
        }
    }
}
