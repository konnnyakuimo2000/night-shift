using System;
using System.Collections.Generic;
using System.Text;
using NightShift.Data;

namespace NightShift.Systems
{
    public class MailMessage
    {
        public string MailId;
        public string From;
        public string To;
        public string Subject;
        public string DateLabel;
        public string Body;                 // リッチテキスト。{FIG:ID} で図を埋め込める
        public List<DocumentDefinition> Attachments = new List<DocumentDefinition>();
        public bool Read;
        public int Day;
        public bool IsBriefing;
        public string ActionKey;            // UI 側で処理する操作（例: PASSWORD）
        public string ActionLabel;
        public float ReceivedAt = -1;       // シフト中に届いたメールのゲーム内時刻
        public string FromAddress;          // 差出人のメールアドレス（DAY13〜表示）
        public bool IsPhish;                // 偽メール（報告すべきもの）
        public bool Reported;
    }

    /// <summary>運用担当者の受信トレイ。業務連絡・作業依頼などはすべてメールで届く。</summary>
    public class MailManager
    {
        public const string ActionPasswordChange = "PASSWORD";
        public const string ActionPhishForm = "PHISH_FORM";
        public const string ActionMalwareAttachment = "MALWARE_ATTACHMENT";
        public const string CompanyDomain = "nexus-corp.example";

        /// <summary>社内メールに表示する社名（GameManager が GameDatabase から設定する）</summary>
        public static string CompanyName = "ネクサスコーポレーション";

        /// <summary>社内の差出人（例: ネクサスコーポレーション 総務部 給与担当）</summary>
        public static string Sender(string dept) => string.IsNullOrEmpty(CompanyName) ? dept : $"{CompanyName} {dept}";

        /// <summary>運用担当者宛ての宛先</summary>
        public static string Recipient(string operatorName) => $"{CompanyName} 夜間IT運用担当 {operatorName} 様";

        /// <summary>社内メールの署名</summary>
        public static string Signature(string dept) => $"\n――――――――――――――――\n{CompanyName}\n{dept}";

        readonly List<MailMessage> _inbox = new List<MailMessage>();
        public IReadOnlyList<MailMessage> Inbox => _inbox;
        public event Action<MailMessage> Received;
        public event Action Changed;

        public int UnreadCount
        {
            get { int n = 0; foreach (var m in _inbox) if (!m.Read) n++; return n; }
        }

        public MailMessage Add(MailMessage m, bool notify = true)
        {
            _inbox.Add(m);
            if (notify) Received?.Invoke(m);
            Changed?.Invoke();
            return m;
        }

        public void MarkRead(MailMessage m)
        {
            if (m == null || m.Read) return;
            m.Read = true;
            Changed?.Invoke();
        }

        public MailMessage Find(string id) => _inbox.Find(m => m.MailId == id);

        /// <summary>今日の業務連絡</summary>
        public MailMessage TodaysBriefing(int day) => _inbox.Find(m => m.IsBriefing && m.Day == day);

        /// <summary>業務連絡メールを DayDefinition から作る（本文＋ルール＋結び）</summary>
        public static MailMessage FromBriefing(DayDefinition day, string operatorName, bool read)
        {
            return new MailMessage
            {
                MailId = "BRIEFING-" + day.dayNumber,
                From = Sender(day.briefingFrom),
                FromAddress = "morita@" + CompanyDomain,
                To = Recipient(operatorName),
                Subject = $"【業務連絡】DAY {day.dayNumber} 夜間業務について",
                DateLabel = (string.IsNullOrEmpty(day.clockInDateLabel) ? day.dateLabel : day.clockInDateLabel) + " 21:30",
                Body = ComposeBriefing(day) + "\n" + Signature(day.briefingFrom),
                Attachments = new List<DocumentDefinition>(day.attachments),
                Read = read,
                Day = day.dayNumber,
                IsBriefing = true
            };
        }

        public static string ComposeBriefing(DayDefinition day)
        {
            var sb = new StringBuilder();
            sb.AppendLine(day.briefingBody);
            for (int i = 0; i < day.rules.Count; i++)
            {
                bool isNew = day.newRuleStartIndex >= 0 && i >= day.newRuleStartIndex;
                bool changed = !isNew && day.changedRuleIndices.Contains(i);
                string mark = isNew ? "  <color=#D04000><b>【NEW】</b></color>" : changed ? "  <color=#D04000><b>【変更】</b></color>" : "";
                sb.AppendLine($"  <b>{i + 1}.</b> {day.rules[i]}{mark}");
            }
            sb.AppendLine();
            sb.Append(day.briefingFooter);
            return sb.ToString();
        }
    }
}
