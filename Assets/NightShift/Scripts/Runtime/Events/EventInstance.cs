using NightShift.Data;

namespace NightShift.Events
{
    public enum EventState { Pending, Allowed, Blocked, Expired }
    public enum PlayerAction { None, Allow, Block }
    public enum SourceZone { Internal, External }

    public enum DecisionOutcome
    {
        None,
        CorrectAllow,
        CorrectBlock,
        FalsePositive,     // 許可すべき要求を拒否
        MaliciousAllow,    // 悪意ある要求を許可
        PolicyViolation,   // 悪意はないがルール違反の要求を許可
        ExpiredLegit,
        ExpiredBlock
    }

    /// <summary>実行中のイベント1件。定義（EventDefinition）とは分離する。</summary>
    public class EventInstance
    {
        public string RequestId;          // REQ-0001（Connection Request の場合）
        public EventDefinition Definition;
        public EventCategory Category;
        public EventKind Kind;

        public EmployeeData Employee;     // 実在社員（UNKNOWN等の場合 null）
        public string DisplayUser;        // 表示上の利用者名
        public string DisplayEmployeeId;  // 表示上の社員番号
        public string Device;
        public SourceZone Zone;
        public string SourceIp;
        public NetworkNodeDefinition Destination;
        public string AccessType;
        public string ServiceId;

        public float GeneratedTime;
        public float TimeoutAt;
        public EventState State = EventState.Pending;
        public bool IsMalicious;
        public ExpectedAction Expected;   // 実際に評価に使う期待動作（Allow / Block）
        public string ExpectedReason;     // デバッグ用
        public PlayerAction PlayerAction;
        public float DecidedTime;
        public float ResponseTime;        // ゲーム内分
        public DecisionOutcome Outcome;

        public bool IsOperatorAccount;    // 利用者が運用担当者（プレイヤー）自身のID
        public string ExpectedDeviceIp;   // 社員の登録端末のIP（なりすましの判定用）
        public bool AutoDecided;          // 自動審査ツール・遮断リストで自動処理された
        public string Note;               // 備考（AI向けの偽の指示など）

        public object Owner;              // ThreatScenarioRun など
        public string QuestTag;           // サブクエストの対象要求（自動審査・AIの自動処理の対象外。null = 通常）
        public string QuestMailId;        // 対象要求に関係する依頼メール（「依頼メールを確認」の導線）
        public string QuestConfirm;       // 許可する前に確認を出す（全社停止を含む処理・永続接続など）
        public bool NotifyAsSecurityAlert;

        public string SourceLabel => (Zone == SourceZone.External ? "社外" : "社内") + " (" + SourceIp + ")";
        public string DestinationLabel => Destination != null ? Destination.nodeId : "-";
    }
}
