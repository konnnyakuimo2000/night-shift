namespace NightShift.Data
{
    /// <summary>
    /// 日ごとに解放される業務システムのID。DayDefinition.unlockedFeatures に列挙されたものだけ利用できる。
    /// </summary>
    public static class Features
    {
        public const string ConnectionMonitor = "CONNECTION_MONITOR";
        public const string EmployeeDirectory = "EMPLOYEE_DIRECTORY";
        public const string DeviceCheck = "DEVICE_CHECK";               // 端末の確認（登録端末の表示・端末番号検索）
        public const string EmployeeDetail = "EMPLOYEE_DETAIL";         // 社員詳細（写真・端末など）
        public const string EmployeePermissions = "EMPLOYEE_PERMISSIONS"; // 社員詳細の権限欄
        public const string AdminPermissions = "ADMIN_PERMISSIONS";     // 社員詳細の【管理者権限】欄（DAY4 から）
        public const string PermissionHint = "PERMISSION_HINT";         // 接続要求に「必要な権限」を表示（DAY3 のみ）
        public const string ServerGuide = "SERVER_GUIDE";               // 社内サーバー構成資料（DAY4 から）
        public const string HelpDesk = "HELPDESK";
        public const string RemoteSupport = "REMOTE_SUPPORT";
        public const string SystemLog = "SYSTEM_LOG";
        public const string NetworkMap = "NETWORK_MAP";
        public const string SecurityInfo = "SECURITY_INFO";
        public const string ServerManagement = "SERVER_MANAGEMENT";
        // ---- 本番業務（DAY8〜） ----
        public const string Store = "STORE";                       // 社内購買システム
        public const string IpCheck = "IP_CHECK";                  // 接続元IPと端末IPの照合（DAY9）
        public const string WorkStatus = "WORK_STATUS";            // 勤務状態のルール（DAY10）
        public const string FirewallBlock = "FIREWALL_BLOCK";      // 接続元の遮断（DAY11）
        public const string EmploymentStatus = "EMPLOYMENT_STATUS"; // 在籍状態（DAY12）
        public const string Report = "REPORT";                     // 報告（DAY13）
        public const string MailSender = "MAIL_SENDER";            // メールの差出人アドレス表示（DAY13）
        public const string Isolation = "ISOLATION";               // 端末の隔離（DAY16）
        public const string UserManagement = "USER_MANAGEMENT";    // ユーザー管理（DAY19）

        public static string Label(string feature)
        {
            switch (feature)
            {
                case ConnectionMonitor: return "ネットワーク接続監視";
                case EmployeeDirectory: return "社員情報の検索・参照";
                case DeviceCheck: return "端末の確認";
                case EmployeeDetail: return "社員情報の詳細表示";
                case EmployeePermissions: return "社員の権限情報";
                case AdminPermissions: return "管理者権限の表示";
                case PermissionHint: return "必要な権限の表示";
                case ServerGuide: return "社内サーバー構成資料";
                case HelpDesk: return "ITサポート受付管理";
                case RemoteSupport: return "リモートサポート";
                case SystemLog: return "システムログの検索・参照";
                case NetworkMap: return "ネットワークマップ";
                case SecurityInfo: return "セキュリティ情報";
                case ServerManagement: return "サーバー運用管理";
                case Store: return "社内購買システム";
                case IpCheck: return "接続元IPの照合";
                case WorkStatus: return "勤務状態の確認";
                case FirewallBlock: return "接続元の遮断";
                case EmploymentStatus: return "在籍状態の確認";
                case Report: return "報告";
                case MailSender: return "メールの差出人表示";
                case Isolation: return "端末の隔離";
                case UserManagement: return "ユーザー管理";
                default: return feature;
            }
        }
    }
}
