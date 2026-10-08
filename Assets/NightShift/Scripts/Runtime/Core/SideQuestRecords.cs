using System;
using System.Collections.Generic;

namespace NightShift.Core
{
    /// <summary>サブクエストの状態（SUBQUEST_SPEC.md §2.1）。Locked → Offered → Active → Resolved</summary>
    public static class QuestStatus
    {
        public const string Locked = "Locked", Offered = "Offered", Active = "Active", Resolved = "Resolved";
    }

    /// <summary>決着の種類</summary>
    public static class QuestResult
    {
        public const string Completed = "Completed", Declined = "Declined", Abandoned = "Abandoned", Failed = "Failed";
    }

    /// <summary>1本のサブクエストの、夜をまたいで残る状態</summary>
    [Serializable]
    public class QuestState
    {
        public string id;                 // SQ01 など
        public string status = QuestStatus.Locked;
        public string result;             // Resolved のときの決着
        public string stage;              // ルート内の段階 ID（E1, R2, H3 など）
        public int offerDay;              // 案内メールが届いた日
        public int nextTaskDay;           // 次の作業の夜（0 = 未定）
        public int ignoredNights;         // 案内に反応しなかった夜の数
        public string employeeId;         // 依頼者など、固定で割り当てた社員
        public string otherEmployeeId;    // 相手役（SQ01 の同僚など）
        public string endingId;           // 決まったサブエンド
        public int counter;               // 段階ごとの汎用カウンタ
        public float timer;               // 段階ごとの汎用タイマー（ゲーム内分）
        public List<string> marks = new List<string>();   // 起きた事実（E1_DONE, OFFER_VIEWED など）

        public bool Has(string m) => marks.Contains(m);
        public void Mark(string m) { if (!marks.Contains(m)) marks.Add(m); }
        public void Unmark(string m) => marks.Remove(m);
        public bool IsOpen => status == QuestStatus.Offered || status == QuestStatus.Active;
    }

    /// <summary>サブクエストのメール（夜をまたいで受信トレイに残る。返信の記録も持つ）</summary>
    [Serializable]
    public class QuestMailRecord
    {
        public string mailId;
        public string questId;
        public int day;                   // 届いた夜
        public float time = -1;           // 届いたゲーム内時刻（-1 = 業務開始前）
        public string from;
        public string address;
        public string subject;
        public string body;
        public string sentLabel;          // 本文に書かれた「元の送信時刻」など、表示用の補足
        public List<string> replies = new List<string>();   // 選べる返信（空なら返信なし）
        public bool freeReply;            // 自由入力の返信欄
        public int repliedIndex = -1;     // 送った返信（-1 = 未返信）
        public string repliedText;        // 送った文面
        public bool read;
        public bool reported;
        public bool viewed;               // 本文を表示した（自動プレビューでなく、開いて読んだ）
        public string actionKey;          // メール内のボタン
        public string actionLabel;
        public bool pinned;               // 後続のメールに埋もれさせない（重要な手がかり）
        public bool horror;               // 怪異メール（文字化けなどの演出対象）
        public string picture;            // 本文の上に出す絵（後日談のサブエンド ID）
    }

    /// <summary>給与とは別の入金・支出（個別入金・遊興口座など）。txId で一度だけ</summary>
    [Serializable]
    public class LedgerEntry
    {
        public string txId;
        public int day;
        public string source;             // SQ01 / SQ04 / LOAN など
        public string label;
        public int amount;                // 財布への増減（+ 入金 / − 支出）
    }

    /// <summary>SQ04/05 共通の遊興口座（GAMBLING_FX_SPEC.md §4）。財布とは別。給与・入金とは混ぜない</summary>
    [Serializable]
    public class LeisureState
    {
        public int free;                  // 空き資金（入出金・ベット・注文に使える）
        public int txSeq;                 // 入出金の通し番号（取引IDに使う）
        public int kingDeclinedDay, warriorDeclinedDay;   // 朝の進路選択を断った夜
        public int locked;                // 拘束資金（結果待ちの賭け金・建玉の証拠金）
        public int deposited, withdrawn;  // 財布との入出金の累計
        public int debt;                  // 借入残高
        public int loanDay, loanDue;      // 借入日・返済日（その夜の終わりに返済）
        public bool loanOffered;          // 追加資金の案内メールを送った
        // ---- ギャンブル ----
        public int betOrdinal;            // 受け付けた賭けの通し番号（抽選の位置）
        public int gamblePlays;
        public int gambleStaked, gamblePaid;
        public bool vip;
        public int gambleStopLoss;        // 指定損失（今夜の損失がこれ以上なら新規ベット停止。0 = 設定なし）
        public int gambleNightCap;        // 一夜の支出上限（0 = 設定なし）
        public List<int> gambleNights = new List<int>();
        public bool gambleFirstWin, gambleFirstLoss;
        // ---- FX ----
        public int fxOrdinal;
        public long fxPnl, fxFees;        // 確定損益・手数料の累計（円）
        public List<int> fxNights = new List<int>();
        public int fxFirstDay;            // 初めて注文した夜
        public int spikeDay;              // 予告した急変夜（0 = 未予告）
        public long spikePnl;             // 急変夜の確定損益
        public int spikeClosed;           // 急変夜に決済した件数
        public bool spikeCleared;         // 急変夜を勝ち越した
        public List<string> history = new List<string>();   // 入出金・決着の履歴（表示用）

        public void Log(string line) { history.Add(line); if (history.Count > 200) history.RemoveAt(0); }
    }

    /// <summary>SQ03 の機器保護札（SUBQUEST_SPEC.md §5.6）</summary>
    [Serializable]
    public class CharmState
    {
        public int stock;                 // 未貼付の枚数
        public int boughtDay, boughtToday;
        public int postedDay;             // 貼った夜（0 = 貼っていない）
        public bool consumed;             // 今夜の効果を使った
        public bool torn;                 // 側面に破れた札が残っている
        public int brokenDay;             // 翌朝の破損を処理した夜（二重に処理しない）
        public bool everTriggered;        // 一度でも防いだ（商品説明が変わる）
    }
}
