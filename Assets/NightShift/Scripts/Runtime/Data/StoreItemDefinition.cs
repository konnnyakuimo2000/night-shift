using UnityEngine;

namespace NightShift.Data
{
    public enum StoreCategory { Hardware, Tool, Security, AI, Consumable }

    /// <summary>社内購買システム（DAY8〜）の商品。購入すると Features の機能が使えるようになる。</summary>
    [CreateAssetMenu(menuName = "NightShift/Store Item", fileName = "StoreItem")]
    public class StoreItemDefinition : ScriptableObject
    {
        public string itemId;
        public string displayName;
        public StoreCategory category;
        public int price;
        [TextArea(2, 6)] public string description;
        [Tooltip("店頭に並ぶ日")]
        public int unlockDay = 8;
        [Tooltip("先に持っている必要がある商品（例: ツールはサブモニターが前提）")]
        public string prerequisite;
        [Tooltip("使うとなくなる（缶コーヒー・追加トークン・定義ファイル更新）")]
        public bool consumable;
        [Tooltip("消耗品を持てる数")]
        public int maxStock = 5;
        public string icon = "pc";
        [Tooltip("広告の一言（ネットニュースのバナー）")]
        public string adCopy;
        [Tooltip("サブクエストで解禁される商品（この記録フラグが立つまで店頭に並ばない）")]
        public string questFlag;
        [Tooltip("販売終了（業務システムに統合した）。店頭に並ばない。所有の記録・ID は残す")]
        public bool retired;
    }

    /// <summary>ツール・機材の ID（Features と同じ仕組みで有効/無効を判定する）</summary>
    public static class Items
    {
        public const string Monitor1 = "HW_MONITOR_2";
        public const string Monitor2 = "HW_MONITOR_3";
        public const string ProfilePanel = "TOOL_PROFILE_PANEL";
        public const string AutoReview = "TOOL_AUTO_REVIEW";
        public const string LogAnalyzer = "TOOL_LOG_ANALYZER";
        public const string FwConsole = "TOOL_FW_CONSOLE";
        public const string MailInspector = "TOOL_MAIL_INSPECTOR";
        public const string RemoteKit = "TOOL_REMOTE_KIT";
        public const string Isolator = "TOOL_ISOLATOR";
        public const string ServerMonitor = "TOOL_SERVER_MONITOR";
        public const string Correlator = "TOOL_CORRELATOR";
        public const string Vaccine = "TOOL_VACCINE";
        public const string VaccineServer = "TOOL_VACCINE_SERVER";
        public const string VaccineUpdate = "ITEM_VACCINE_UPDATE";
        public const string SocAi = "AI_CYBER_ASSISTANT";
        public const string TokenPack = "AI_TOKEN_PACK";
        public const string Coffee = "ITEM_COFFEE";
        // サブクエスト（招待・案内のあとにだけ店頭に並ぶ）
        public const string GambleTerminal = "TOOL_GAMBLE_TERMINAL";
        public const string FxTerminal = "TOOL_FX_TERMINAL";
        public const string SignalDesk = "TOOL_SIGNAL_DESK";
        public const string Charm = "ITEM_DEVICE_CHARM";
    }
}
