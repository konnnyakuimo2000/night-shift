using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Data
{
    /// <summary>ランダム枠: 時間帯内にプールから重み付きで1件選ぶ（骨格固定＋細部ランダム）</summary>
    [Serializable]
    public class RandomEventSlot
    {
        public string label;
        public Vector2 window;
        public List<EventDefinition> pool = new List<EventDefinition>();
    }

    [Serializable]
    public class PolicyRuleSet
    {
        [Tooltip("外部から管理系システムへの接続は禁止")]
        public bool blockExternalToAdministrative = true;
        [Tooltip("本人の登録端末ではない端末からの接続は、接続先に関わらず禁止（DAY1の簡易ルール）")]
        public bool blockUnregisteredDevice;
        [Tooltip("未登録端末から内部重要システムへの接続は禁止")]
        public bool blockUnregisteredToCritical = true;
        [Tooltip("不明な利用者は禁止")]
        public bool blockUnknownUser = true;
        [Tooltip("接続種別に必要な権限が無ければ禁止")]
        public bool requirePermission = true;
        [Tooltip("VPN利用許可が必要な接続")]
        public bool requireVpnPermission = true;
    }

    [CreateAssetMenu(menuName = "NightShift/Day Definition", fileName = "Day")]
    public class DayDefinition : ScriptableObject
    {
        public int dayNumber = 1;
        public string title = "FIRST SHIFT";
        public string dateLabel = "1998/05/20 (水)";
        [Tooltip("タイムカード打刻時（前日夜）の日付")]
        public string clockInDateLabel = "1998/05/19 (火)";

        [Header("時間")]
        public float startMinute = 0f;
        public float endMinute = 240f;
        [Tooltip("1ゲーム分あたりの実秒（2.0 = 4時間を8分）")]
        public float realSecondsPerGameMinute = 2f;
        public float endWarningMinute = 230f;
        [Tooltip("終了後、未処理要求を処理できる猶予（実秒）")]
        public float graceRealSeconds = 20f;

        [Header("業務連絡")]
        public string briefingFrom = "情報システム部 課長 森田";
        [TextArea(4, 12)] public string briefingBody;
        public List<string> rules = new List<string>();
        [Tooltip("この番号以降のルールを「NEW」として強調（0始まり）。-1 で強調なし")]
        public int newRuleStartIndex = -1;
        [TextArea] public string briefingFooter;
        [Tooltip("業務連絡に添付する文書（マニュアル等）")]
        public List<DocumentDefinition> attachments = new List<DocumentDefinition>();

        [Header("イベント")]
        public List<EventDefinition> fixedEvents = new List<EventDefinition>();
        public List<RandomEventSlot> randomSlots = new List<RandomEventSlot>();
        public List<HelpDeskDefinition> helpDesk = new List<HelpDeskDefinition>();
        public List<ThreatScenarioDefinition> threats = new List<ThreatScenarioDefinition>();
        [Tooltip("背景イベント（ログに残らない通信も含む）として使う定義。社員の NormalBehaviorProfile から生成される")]
        public EventDefinition backgroundTrafficTemplate;
        [Range(0f, 1f)] public float backgroundLogChance = 0.3f;
        [Tooltip("会社全体の背景通信の倍率")]
        public float backgroundActivityScale = 1f;

        [Header("チュートリアル進行")]
        [Tooltip("この時刻（ゲーム内分）より前に予定されたプレイヤー向けイベントは、前のタスクが片付くまで発生を待つ。0 で無効")]
        public float tutorialGateUntilMinute = 0f;
        [Tooltip("タスクが片付いてから次のイベントを出すまでの間（ゲーム内分）")]
        public float tutorialGateBreathMinutes = 2f;
        [Tooltip("待機できる最大時間（ゲーム内分）。これを過ぎたら強制的に発生")]
        public float tutorialGateMaxDelayMinutes = 30f;

        [Header("ルール")]
        public PolicyRuleSet policy = new PolicyRuleSet();

        [Header("解放されている業務システム")]
        [Tooltip("Features の ID。ここに無いシステムはメニューに表示されても利用できない")]
        public List<string> unlockedFeatures = new List<string>();

        public bool IsUnlocked(string feature) => unlockedFeatures.Contains(feature);
    }
}
