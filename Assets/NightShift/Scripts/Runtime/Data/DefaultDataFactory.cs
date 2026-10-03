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
            P("SERVER_ADMIN", "サーバー管理者", PermissionCategory.Administrator, 120, "サーバーへの管理ログオン");
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
            EventDefinition Ev(string id, string folder = "Events")
            {
                var e = b.Add(ScriptableObject.CreateInstance<EventDefinition>(), folder, "EVT_" + id);
                e.eventId = id; e.minimumDay = 1; e.maximumDay = 7;
                return e;
            }

            var firstNormal = Ev("REQ_FIRST_NORMAL");
            firstNormal.triggerWindow = new Vector2(2, 4);
            firstNormal.employeeConditions.requiredNormalTargets.Add("MAIL");
            firstNormal.employeeConditions.requiredPermissions.Add(perms["GENERAL_BUSINESS"]);
            firstNormal.destination = mail; firstNormal.accessType = "MAIL";
            firstNormal.timeoutMinutes = 30;

            var dirCheck = Ev("REQ_DIRECTORY_HR");
            dirCheck.triggerWindow = new Vector2(10, 13);
            dirCheck.employeeConditions.requiredPermissions.Add(perms["HR_ACCESS"]);
            dirCheck.employeeConditions.requiredNormalTargets.Add("HR");
            dirCheck.destination = app; dirCheck.accessType = "HR SYSTEM";
            dirCheck.timeoutMinutes = 30;

            var unregistered = Ev("REQ_UNREGISTERED_DEVICE");
            unregistered.triggerWindow = new Vector2(27, 31);
            unregistered.severity = Severity.Low;
            unregistered.sourceType = SourceType.UnregisteredDevice;
            unregistered.deviceNamePattern = "NB-PRIV{n}";
            unregistered.employeeConditions.requiredPermissions.Add(perms["FILE_ACCESS"]);
            unregistered.destination = file; unregistered.accessType = "FILE READ";
            unregistered.timeoutMinutes = 30;

            var firstSuspicious = Ev("REQ_FIRST_SUSPICIOUS");
            firstSuspicious.category = EventCategory.Threat;
            firstSuspicious.triggerWindow = new Vector2(46, 64);
            firstSuspicious.preferWhileRemoteActive = true;
            firstSuspicious.notifyAsSecurityAlert = true;
            firstSuspicious.severity = Severity.High;
            firstSuspicious.userType = UserType.Unknown;
            firstSuspicious.sourceType = SourceType.ExternalUnknown;
            firstSuspicious.deviceNamePattern = "UNKNOWN-14";
            firstSuspicious.destination = admin; firstSuspicious.accessType = "ADMIN LOGIN";
            firstSuspicious.isMalicious = true;
            firstSuspicious.timeoutMinutes = 40;

            var looksOdd = Ev("REQ_DEV_FILE_LOOKS_ODD");
            looksOdd.triggerWindow = new Vector2(83, 88);
            looksOdd.employeeConditions.departments.Add("開発部");
            looksOdd.employeeConditions.requiredPermissions.Add(perms["FILE_ACCESS"]);
            looksOdd.employeeConditions.requiredNormalTargets.Add("FILE");
            looksOdd.destination = file; looksOdd.accessType = "FILE WRITE";
            looksOdd.timeoutMinutes = 30;

            var vpnLegit = Ev("REQ_VPN_LEGIT");
            vpnLegit.triggerWindow = new Vector2(148, 156);
            vpnLegit.sourceType = SourceType.RegisteredLaptopExternal;
            vpnLegit.employeeConditions.requireVpnEnabled = true;
            vpnLegit.employeeConditions.requireLaptop = true;
            vpnLegit.destination = auth; vpnLegit.accessType = "VPN LOGIN";
            vpnLegit.timeoutMinutes = 30;

            var finalQ = Ev("REQ_FINAL_NO_ADMIN");
            finalQ.triggerWindow = new Vector2(198, 204);
            finalQ.severity = Severity.Medium;
            finalQ.employeeConditions.departments.Add("営業部");
            finalQ.employeeConditions.excludedPermissions.Add(perms["SERVER_ADMIN"]);
            finalQ.employeeConditions.excludedPermissions.Add(perms["LOCAL_ADMIN"]);
            finalQ.destination = admin; finalQ.accessType = "ADMIN LOGIN";
            finalQ.timeoutMinutes = 30;

            var routine = Ev("REQ_ROUTINE");
            routine.destinationMode = DestinationMode.FromEmployeeNormalTargets;
            routine.employeeConditions.preferUnused = false;
            routine.timeoutMinutes = 25;

            var routineVpn = Ev("REQ_ROUTINE_VPN");
            routineVpn.sourceType = SourceType.RegisteredLaptopExternal;
            routineVpn.employeeConditions.requireVpnEnabled = true;
            routineVpn.employeeConditions.requireLaptop = true;
            routineVpn.employeeConditions.preferUnused = false;
            routineVpn.destination = auth; routineVpn.accessType = "VPN LOGIN";
            routineVpn.weight = 0.4f;
            routineVpn.timeoutMinutes = 25;

            var scanLog = Ev("LOG_SCANNER_NOISE");
            scanLog.category = EventCategory.Threat;
            scanLog.kind = EventKind.LogEntry;
            scanLog.userType = UserType.Unknown;
            scanLog.sourceType = SourceType.ExternalUnknown;
            scanLog.destination = web; scanLog.accessType = "TCP";
            scanLog.logEventType = "FW";
            scanLog.logResult = "DENY";

            // ---------------- Threat scenario ----------------
            var threatReq = Ev("REQ_THREAT_ADMIN", "Threats");
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
            threat.designNote = "外部の攻撃者（自動スクリプト＋手動操作）が公開Webを探索し、管理サーバーへのログオンを試みる。ログを見れば同一レンジのIPと気付ける。";
            threat.startWindow = new Vector2(128, 135);
            threat.sourceIpPrefix = "198.51.100.";
            threat.steps.Add(new ThreatStep { label = "公開Webへのアクセス", offsetMinutes = 0, kind = ThreatStepKind.LogEntry, destination = web, eventType = "HTTP", result = "200", detail = "GET / (python-urllib/2.0)" });
            threat.steps.Add(new ThreatStep { label = "管理ページ探索", offsetMinutes = 4, kind = ThreatStepKind.LogEntry, destination = web, eventType = "HTTP", result = "404", detail = "GET /admin/|GET /manager/html|GET /login.cgi|GET /cgi-bin/admin.pl", repeat = 4, repeatIntervalMinutes = 0.6f });
            threat.steps.Add(new ThreatStep { label = "再アクセス", offsetMinutes = 9, kind = ThreatStepKind.LogEntry, destination = web, eventType = "HTTP", result = "200", detail = "GET /about/staff.html" });
            threat.steps.Add(new ThreatStep { label = "管理サーバーのポート探索", offsetMinutes = 12, kind = ThreatStepKind.LogEntry, useRelatedSource = true, destination = admin, eventType = "FW", result = "DENY", detail = "TCP 23|TCP 3389|TCP 22", repeat = 3, repeatIntervalMinutes = 0.4f });
            threat.steps.Add(new ThreatStep { label = "管理サーバーへの接続要求", offsetMinutes = 16, kind = ThreatStepKind.ConnectionRequest, useRelatedSource = true, destination = admin, request = threatReq });

            var noise = b.Add(ScriptableObject.CreateInstance<ThreatScenarioDefinition>(), "Threats", "THREAT_ScannerNoise");
            noise.scenarioId = "THREAT_SCANNER_NOISE";
            noise.designNote = "インターネット上の一般的な自動スキャン。ファイアウォールで拒否されるだけ。";
            noise.startWindow = new Vector2(66, 74);
            noise.sourceIpPrefix = "203.0.113.";
            noise.steps.Add(new ThreatStep { label = "ポートスキャン", offsetMinutes = 0, kind = ThreatStepKind.LogEntry, destination = web, eventType = "FW", result = "DENY", detail = "TCP 21|TCP 23|TCP 445|TCP 1433|TCP 3306", repeat = 5, repeatIntervalMinutes = 0.3f });

            // ---------------- Help Desk ----------------
            HelpDeskDefinition H(string id, HelpDeskType type, Vector2 window)
            {
                var h = b.Add(ScriptableObject.CreateInstance<HelpDeskDefinition>(), "HelpDesk", "HD_" + id);
                h.definitionId = id; h.type = type; h.triggerWindow = window;
                return h;
            }

            var hdA1 = H("A1_PASSWORD_PHONE", HelpDeskType.Binary, new Vector2(18, 21));
            hdA1.subject = "パスワードを教えてほしいと言われた";
            hdA1.message = "{dept}の{name}です。\nさきほど「本社の情報システムの者」と名乗る人から電話があり、緊急のメンテナンスなので私のパスワードを教えてほしいと言われました。\n折り返すと言って一度切ったのですが、教えても大丈夫でしょうか？";
            hdA1.correctOption = "パスワードは誰にも教えないよう伝え、電話の内容を記録して報告してもらう。";
            hdA1.incorrectOption = "情報システムの担当者なら問題ないので、折り返して伝えてよいと回答する。";
            hdA1.replyAfterAnswer = "わかりました。そのようにします。";
            hdA1.reminderMessage = "すみません、先ほどの件どうしたらいいでしょうか…？";

            var hdR1 = H("R1_NO_INTERNET", HelpDeskType.Remote, new Vector2(37, 41));
            hdR1.subject = "インターネットにつながらない";
            hdR1.message = "{dept}の{name}です。\nさっきから急にインターネットにつながらなくなりました。メールも送れません。\n見てもらえますか？（端末: {device}）";
            hdR1.pcProfile = profile;
            hdR1.faultVariants.Add(new FaultVariant { label = "Wi-Fi OFF", faults = { new StateValue { key = "WiFiEnabled", value = "false" } } });
            hdR1.resolutionConditions.Add(new StateValue { key = "InternetConnectivity", value = "true" });
            hdR1.successReply = "あ、つながりました！メールも送れました。ありがとうございます！";
            hdR1.failureReply = "うーん、まだつながらないみたいです…。";
            hdR1.reminderMessage = "まだつながらないです…。急ぎのメールがあって。";

            var hdA2 = H("A2_ATTACHMENT", HelpDeskType.Binary, new Vector2(96, 101));
            hdA2.subject = "知らない会社からExcelファイルが届いた";
            hdA2.message = "{dept}の{name}です。\n知らない会社から「請求書の件」という件名でExcelファイルが届きました。\n取引先かもしれないのですが、開いて大丈夫ですか？";
            hdA2.correctOption = "開かずに、送信元や取引関係を確認してから判断するよう伝える。";
            hdA2.incorrectOption = "内容を確認しないと判断できないので、一度開いてみるよう伝える。";
            hdA2.replyAfterAnswer = "了解です。確認してみます。";
            hdA2.reminderMessage = "あの、さっきのファイル、開いちゃっていいですかね？";

            var hdR2 = H("R2_CANNOT_PRINT", HelpDeskType.Remote, new Vector2(118, 123));
            hdR2.subject = "印刷できない";
            hdR2.message = "{dept}の{name}です。\n見積書を印刷しようとしても、プリンターから何も出てきません。\nプリンターの電源は入っています。朝イチで使いたいので、見てもらえますか？（端末: {device}）";
            hdR2.pcProfile = profile;
            hdR2.faultVariants.Add(new FaultVariant { label = "Print Queue Paused", faults = { new StateValue { key = "PrintQueuePaused", value = "true" } } });
            hdR2.resolutionConditions.Add(new StateValue { key = "PrinterOnline", value = "true" });
            hdR2.resolutionConditions.Add(new StateValue { key = "PrintQueuePaused", value = "false" });
            hdR2.successReply = "出てきました！助かりました。";
            hdR2.failureReply = "まだ何も出てこないです…。";
            hdR2.reminderMessage = "プリンターの件、どうでしょうか…？";

            // ---------------- Day 1 ----------------
            var day1 = b.Add(ScriptableObject.CreateInstance<DayDefinition>(), "Days", "DAY1_FirstShift");
            day1.dayNumber = 1;
            day1.title = "FIRST SHIFT";
            day1.dateLabel = "1998/05/20 (水)";
            day1.briefingFrom = "情報システム部 課長 森田";
            day1.briefingBody =
                "夜間IT担当として、今夜からよろしくお願いします。\n" +
                "業務時間は 00:00〜04:00 です。今夜は営業部・開発部・人事部・経理部の一部と、\n" +
                "カスタマーサポート部（夜勤）が勤務しています。\n\n" +
                "■ 主な業務\n" +
                "・社内ネットワークへの接続要求の審査\n" +
                "・社員からのIT問い合わせ（相談・リモートサポート）\n" +
                "・システムログの確認\n\n" +
                "■ 接続審査の基本ルール";
            day1.rules.Add("登録済みの社員端末から、通常業務システムへの通信は原則許可する。");
            day1.rules.Add("未登録の端末から、社内の重要システムへの接続は禁止。");
            day1.rules.Add("社外から管理系システムへの接続は禁止。");
            day1.rules.Add("管理系システムへの接続は、利用者の管理者権限を確認すること。");
            day1.rules.Add("社外からの接続(VPN)は、VPN利用を許可された社員の登録端末に限る。");
            day1.briefingFooter =
                "社員の所属・登録端末・権限は「社員情報の検索・参照」で確認できます。\n" +
                "サーバー運用管理は明日から担当範囲に加わります（本日は監視のみ）。\n" +
                "迷ったら、まず調べること。";
            day1.fixedEvents.AddRange(new[] { firstNormal, dirCheck, unregistered, firstSuspicious, looksOdd, vpnLegit, finalQ });
            void Slot(string label, float a, float c, params EventDefinition[] pool)
            {
                var s = new RandomEventSlot { label = label, window = new Vector2(a, c) };
                s.pool.AddRange(pool);
                day1.randomSlots.Add(s);
            }
            Slot("routine-1", 6, 8, routine);
            Slot("routine-2", 22, 25, routine);
            Slot("routine-3", 52, 70, routine, routineVpn);
            Slot("routine-4", 75, 80, routine);
            Slot("routine-5", 105, 114, routine, routineVpn);
            Slot("routine-6", 140, 147, routine);
            Slot("routine-7", 166, 180, routine, routineVpn);
            Slot("routine-8", 184, 194, routine);
            Slot("routine-9", 212, 224, routine);
            Slot("scan-noise", 20, 40, scanLog);
            Slot("scan-noise-2", 160, 190, scanLog);
            day1.helpDesk.AddRange(new[] { hdA1, hdR1, hdA2, hdR2 });
            day1.threats.Add(noise);
            day1.threats.Add(threat);
            day1.backgroundLogChance = 0.3f;
            db.days.Add(day1);

            return b;
        }
    }
}
