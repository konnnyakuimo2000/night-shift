using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Data
{
    public enum HelpDeskType
    {
        Binary, // TYPE A: 二択判断
        Remote  // TYPE B: Remote Support
    }

    /// <summary>VirtualPCState の値（キー名は VirtualPCState.GetValue と対応）</summary>
    [Serializable]
    public class StateValue
    {
        public string key;
        public string value;
    }

    [Serializable]
    public class FaultVariant
    {
        public string label;
        [Min(0f)] public float weight = 1f;
        public List<StateValue> faults = new List<StateValue>();
    }

    [CreateAssetMenu(menuName = "NightShift/Help Desk Definition", fileName = "HelpDesk")]
    public class HelpDeskDefinition : ScriptableObject
    {
        public string definitionId;
        public HelpDeskType type;
        public int minimumDay = 1;
        public int maximumDay = 7;
        public Vector2 triggerWindow = new Vector2(0, 240);
        public EmployeeCondition requester = new EmployeeCondition();

        [Header("問い合わせ ({name} {dept} {device} が置換される)")]
        public string subject;
        [TextArea(3, 8)] public string message;
        [Tooltip("未対応のまま放置された時の催促メッセージ（任意）")]
        [TextArea] public string reminderMessage;
        public float reminderAfterMinutes = 25f;

        [Header("TYPE A 二択")]
        [TextArea] public string correctOption;
        [TextArea] public string incorrectOption;
        [TextArea] public string replyAfterAnswer = "ありがとうございます。そのようにします。";

        [Header("TYPE B Remote")]
        public VirtualPCProfile pcProfile;
        public List<FaultVariant> faultVariants = new List<FaultVariant>();
        [Tooltip("すべて満たせば解決（最終的なPC状態で判定）")]
        public List<StateValue> resolutionConditions = new List<StateValue>();
        [TextArea] public string successReply = "直りました！ありがとうございます。";
        [TextArea] public string failureReply = "すみません、まだ直っていないようです…";
        [Min(1)] public int maxReports = 2;
    }
}
