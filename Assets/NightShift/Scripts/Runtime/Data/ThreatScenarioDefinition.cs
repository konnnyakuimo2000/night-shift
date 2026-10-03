using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Data
{
    public enum ThreatStepKind
    {
        Traffic,           // 通信のみ（Network Monitor / 一部Log）
        LogEntry,          // ログに記録
        ConnectionRequest  // プレイヤーに判断させる接続要求
    }

    [Serializable]
    public class ThreatStep
    {
        public string label;
        [Tooltip("シナリオ開始からのゲーム内分")]
        public float offsetMinutes;
        public ThreatStepKind kind;
        public bool useRelatedSource;
        public NetworkNodeDefinition destination;
        public string eventType = "HTTP";
        public string result = "OK";
        [Tooltip("ログ詳細（{src} が攻撃元IPに置換）")]
        public string detail;
        [Min(1)] public int repeat = 1;
        public float repeatIntervalMinutes = 0.5f;
        [Tooltip("ConnectionRequest の場合の定義")]
        public EventDefinition request;
    }

    /// <summary>
    /// 小規模な攻撃チェーン。DAY1の攻撃者は人間または一般的な自動スキャン/攻撃スクリプト。
    /// </summary>
    [CreateAssetMenu(menuName = "NightShift/Threat Scenario", fileName = "Threat")]
    public class ThreatScenarioDefinition : ScriptableObject
    {
        public string scenarioId;
        [TextArea] public string designNote;
        public int minimumDay = 1;
        public int maximumDay = 7;
        public Vector2 startWindow = new Vector2(130, 140);
        [Tooltip("攻撃元IPのプレフィックス。末尾オクテットは乱数")]
        public string sourceIpPrefix = "198.51.100.";
        public List<ThreatStep> steps = new List<ThreatStep>();
        [Tooltip("要求を許可してしまった場合のログ")]
        public string compromiseLog = "管理コンソールへのログインに成功";
        [Tooltip("要求を拒否した場合、以降のステップを停止する")]
        public bool stopOnBlock = true;
    }
}
