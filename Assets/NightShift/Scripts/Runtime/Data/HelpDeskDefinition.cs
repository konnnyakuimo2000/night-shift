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
        [Min(0f)] public float weight = 1f;

        [Header("本番業務（DAY8〜）のランダム出題")]
        [Tooltip("「はい」が正解の二択か（毎晩 4割程度を「はい」にする）")]
        public bool correctIsYes;
        [Tooltip("解禁日をプレイごとにランダムに決める（randomUnlockMin〜Max）")]
        public bool randomUnlock;
        public int randomUnlockMin = 8;
        public int randomUnlockMax = 25;
        [Tooltip("30日間で出題できる回数")]
        public int maxUses = 2;
        [Tooltip("間違えた（失敗した）場合に立てるフラグ（後日の脅威・ウイルスのきっかけ）")]
        public string flagOnWrong;

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

        [Header("解説・出典（対応の後に表示）")]
        [Tooltip("正しい対応の理由（短く）")]
        [TextArea] public string explanation;
        [Tooltip("参考にしたIPA試験の過去問題の出典。例: 平成28年度 春期 ITパスポート試験 問86（改変）")]
        public string sourceNote;
    }
}
