namespace NightShift.Data
{
    /// <summary>
    /// 日ごとに解放される業務システムのID。DayDefinition.unlockedFeatures に列挙されたものだけ利用できる。
    /// </summary>
    public static class Features
    {
        public const string ConnectionMonitor = "CONNECTION_MONITOR";
        public const string EmployeeDirectory = "EMPLOYEE_DIRECTORY";
        public const string HelpDesk = "HELPDESK";
        public const string RemoteSupport = "REMOTE_SUPPORT";
        public const string SystemLog = "SYSTEM_LOG";
        public const string NetworkMap = "NETWORK_MAP";
        public const string SecurityInfo = "SECURITY_INFO";
        public const string ServerManagement = "SERVER_MANAGEMENT";

        public static string Label(string feature)
        {
            switch (feature)
            {
                case ConnectionMonitor: return "ネットワーク接続監視";
                case EmployeeDirectory: return "社員情報の検索・参照";
                case HelpDesk: return "ITサポート受付管理";
                case RemoteSupport: return "リモートサポート";
                case SystemLog: return "システムログの検索・参照";
                case NetworkMap: return "ネットワークマップ";
                case SecurityInfo: return "セキュリティ情報";
                case ServerManagement: return "サーバー運用管理";
                default: return feature;
            }
        }
    }
}
