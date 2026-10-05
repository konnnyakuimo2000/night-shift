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
        [Tooltip("利用システムに必要な権限が無ければ禁止")]
        public bool requirePermission = true;
        [Tooltip("VPN利用許可が必要な接続")]
        public bool requireVpnPermission = true;
        [Tooltip("接続元IPが社員の端末IPと一致すること（DAY9〜、なりすまし対策）")]
        public bool requireSourceIpMatch;
        [Tooltip("勤務中の社員だけ許可（DAY10〜）")]
        public bool requireOnShift;
        [Tooltip("在籍中の社員だけ許可（DAY12〜）")]
        public bool requireActiveEmployment;
        [Tooltip("運用担当者（自分）のIDからの接続要求は拒否（DAY14〜）")]
        public bool blockOperatorAccount;
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
        [Tooltip("接続要求の期限切れを有効にする（チュートリアル中は無効。04:00 後も未処理の要求をすべて処理するまで待つ）")]
        public bool requestsExpire = true;

        [Header("業務連絡")]
        public string briefingFrom = "情報システム部 課長 森田";
        [TextArea(4, 12)] public string briefingBody;
        public List<string> rules = new List<string>();
        [Tooltip("この番号以降のルールを「NEW」として強調（0始まり）。-1 で強調なし")]
        public int newRuleStartIndex = -1;
        [Tooltip("前日から内容が変わったルールの番号（0始まり）。「変更」として強調")]
        public List<int> changedRuleIndices = new List<int>();
        [TextArea] public string briefingFooter;
        [Tooltip("業務連絡に添付する文書（マニュアル等）")]
        public List<DocumentDefinition> attachments = new List<DocumentDefinition>();

        [Header("イベント")]
        public List<EventDefinition> fixedEvents = new List<EventDefinition>();
        public List<RandomEventSlot> randomSlots = new List<RandomEventSlot>();
        public List<HelpDeskDefinition> helpDesk = new List<HelpDeskDefinition>();
        public List<ThreatScenarioDefinition> threats = new List<ThreatScenarioDefinition>();
        [Range(0f, 1f)] public float backgroundLogChance = 0.3f;
        [Tooltip("会社全体の背景通信の倍率")]
        public float backgroundActivityScale = 1f;

        [Header("本番業務（DAY8〜）: ランダム生成")]
        [Tooltip("接続要求・問い合わせをランダムに生成する")]
        public bool randomEvents;
        [Tooltip("1夜の接続要求の数")]
        public int requestCount = 16;
        [Tooltip("そのうちルール違反（拒否すべき）要求の割合")]
        [Range(0f, 1f)] public float violationRatio = 0.3f;
        [Tooltip("要求が同時に重なる確率（近い時刻にもう1件）")]
        [Range(0f, 1f)] public float burstChance = 0.2f;
        public List<EventDefinition> routinePool = new List<EventDefinition>();
        public List<EventDefinition> violationPool = new List<EventDefinition>();
        [Tooltip("1夜の問い合わせ（二択・リモート）の数")]
        public int helpDeskCount = 2;
        public List<HelpDeskDefinition> helpDeskPool = new List<HelpDeskDefinition>();

        [Header("チュートリアル進行")]
        [Tooltip("この時刻（ゲーム内分）より前に予定されたプレイヤー向けイベントは、前のタスクが片付くまで発生を待つ。0 で無効")]
        public float tutorialGateUntilMinute = 0f;
        [Tooltip("タスクが片付いてから次のイベントを出すまでの間（ゲーム内分）")]
        public float tutorialGateBreathMinutes = 2f;
        [Tooltip("待機できる最大時間（ゲーム内分）。これを過ぎたら強制的に発生")]
        public float tutorialGateMaxDelayMinutes = 30f;

        [Header("運用担当者への作業依頼")]
        [Tooltip("ログインパスワードの変更依頼を出す時刻（ゲーム内分）。負の値で出さない")]
        public float passwordChangeAtMinute = -1f;

        [Header("ルール")]
        public PolicyRuleSet policy = new PolicyRuleSet();

        [Header("解放されている業務システム")]
        [Tooltip("Features の ID。ここに無いシステムはメニューに表示されても利用できない")]
        public List<string> unlockedFeatures = new List<string>();

        public bool IsUnlocked(string feature) => unlockedFeatures.Contains(feature);

        /// <summary>業務時間の表示（例: 00:00 - 06:00）</summary>
        public string ShiftHoursLabel => $"{Core.TimeManager.Format(startMinute)} - {Core.TimeManager.Format(endMinute)}";
        public string EndLabel => Core.TimeManager.Format(endMinute);
    }
}
