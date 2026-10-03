using NightShift.Data;

namespace NightShift.Events
{
    public enum EventState { Pending, Allowed, Blocked, Expired, Completed }
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
        public string InstanceId;
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

        public object Owner;              // ThreatScenarioRun など
        public bool NotifyAsSecurityAlert;

        public string SourceLabel => (Zone == SourceZone.External ? "社外" : "社内") + " (" + SourceIp + ")";
        public string DestinationLabel => Destination != null ? Destination.nodeId : "-";
    }
}
