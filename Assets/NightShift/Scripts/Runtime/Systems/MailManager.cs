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
    }

    /// <summary>運用担当者の受信トレイ。業務連絡・作業依頼などはすべてメールで届く。</summary>
    public class MailManager
    {
        public const string ActionPasswordChange = "PASSWORD";

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
                From = day.briefingFrom,
                To = $"夜間IT運用担当 {operatorName} 様",
                Subject = $"【業務連絡】DAY {day.dayNumber} 夜間業務について",
                DateLabel = (string.IsNullOrEmpty(day.clockInDateLabel) ? day.dateLabel : day.clockInDateLabel) + " 21:30",
                Body = ComposeBriefing(day),
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
            sb.AppendLine("{FIG:REVIEW_FLOW}");
            sb.Append(day.briefingFooter);
            return sb.ToString();
        }
    }
}
