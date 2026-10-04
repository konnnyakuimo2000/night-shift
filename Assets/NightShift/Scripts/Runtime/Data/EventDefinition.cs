using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Data
{
    public enum EventCategory { Normal, HelpDesk, Threat }
    public enum EventKind { BackgroundTraffic, ConnectionRequest, LogEntry }
    public enum Severity { Info, Low, Medium, High }

    public enum UserType
    {
        Employee,       // 条件に合う実在社員
        Unknown,        // USER: UNKNOWN
        NamedAccount,   // 例: admin（社員ではないアカウント名）
        OperatorAccount // 夜間運用担当（プレイヤー自身）のID
    }

    public enum SourceType
    {
        RegisteredDevice,         // 社員の登録PC（社内）
        RegisteredLaptopExternal, // 社員の登録ノートPCから社外接続（VPN）
        UnregisteredDevice,       // 社内ネットワーク上の未登録端末
        ExternalUnknown,          // 外部の不明な端末
        AttackerSource,           // Threat Scenario の攻撃元
        AttackerRelatedSource,    // 攻撃元と関連する（同一レンジの）別IP
        OtherEmployeeDevice,      // 別の社員の登録端末（社内）… 本人の端末ではない
        RegisteredDeviceExternal, // 本人の社内PCを名乗る社外からの接続
        SpoofedSource             // 本人の登録端末名を名乗るが、接続元IPが端末IPと違う（なりすまし）
    }

    public enum DestinationMode { FixedNode, FromEmployeeNormalTargets }

    public enum EmploymentFilter { ActiveOnly, NotActive, Any }

    public enum ExpectedAction
    {
        PolicyDerived, // その日のルールと実際の社員データから算出（推奨）
        Allow,
        Block
    }

    /// <summary>イベントに使う社員の選択条件。社員データを編集してもシナリオが壊れないよう、具体的な人物ではなく条件で指定する。</summary>
    [Serializable]
    public class EmployeeCondition
    {
        public List<string> departments = new List<string>();
        public List<string> excludeDepartments = new List<string>();
        public List<PermissionDefinition> requiredPermissions = new List<PermissionDefinition>();
        public List<PermissionDefinition> excludedPermissions = new List<PermissionDefinition>();
        public List<string> requiredNormalTargets = new List<string>();
        public bool requireOnShift = true;
        public bool requireVpnEnabled;
        [Tooltip("VPN利用許可のない社員に限る")]
        public bool requireVpnDisabled;
        public bool requireLaptop;
        [Tooltip("退勤している社員に限る（勤務状態のルール用）")]
        public bool requireOffShift;
        [Tooltip("在籍状態の条件（既定は在籍中のみ）")]
        public EmploymentFilter employment = EmploymentFilter.ActiveOnly;
        [Tooltip("当日まだイベントに登場していない社員を優先")]
        public bool preferUnused = true;
    }

    [CreateAssetMenu(menuName = "NightShift/Event Definition", fileName = "Event")]
    public class EventDefinition : ScriptableObject
    {
        [Header("識別")]
        public string eventId;
        public EventCategory category = EventCategory.Normal;
        public EventKind kind = EventKind.ConnectionRequest;
        public int minimumDay = 1;
        public int maximumDay = 7;
        [Tooltip("発生時間帯（ゲーム内分。00:00=0, 04:00=240）")]
        public Vector2 triggerWindow = new Vector2(0, 240);
        [Min(0f)] public float weight = 1f;
        public Severity severity = Severity.Info;

        [Header("利用者")]
        public UserType userType = UserType.Employee;
        public EmployeeCondition employeeConditions = new EmployeeCondition();
        [Tooltip("NamedAccount の場合のアカウント名（例: admin、退職者の氏名）")]
        public string accountName;
        [Tooltip("NamedAccount の場合に表示する社員番号（社員名簿に存在しない番号など）。空なら -")]
        public string displayEmployeeId;

        [Header("接続元")]
        public SourceType sourceType = SourceType.RegisteredDevice;
        [Tooltip("端末名の上書き。{n} は乱数2桁、{hex} は16進4桁")]
        public string deviceNamePattern;

        [Header("接続先")]
        public DestinationMode destinationMode = DestinationMode.FixedNode;
        public NetworkNodeDefinition destination;
        public string accessType;

        [Header("判定")]
        public bool isMalicious;
        public ExpectedAction expectedAction = ExpectedAction.PolicyDerived;
        [Tooltip("未処理で破棄されるまでのゲーム内分")]
        public float timeoutMinutes = 25f;

        [Header("演出")]
        [Tooltip("Remote Support 中に優先して発生させる（窓の終端までに発生しなければ終端で発生）")]
        public bool preferWhileRemoteActive;
        public bool notifyAsSecurityAlert;
        [Tooltip("LogEntry の場合のイベント種別・結果・詳細")]
        public string logEventType;
        public string logResult;

        [Tooltip("備考欄に表示する文（AI向けの偽の指示 T15 など）")]
        [TextArea] public string note;

        [Header("後続")]
        public EventDefinition followUp;
        public float followUpDelayMinutes = 5f;

        public bool IsAvailableOn(int day) => day >= minimumDay && day <= maximumDay;
    }
}
