namespace NightShift.Systems
{
    /// <summary>
    /// T5 担当者宛ての偽メール（DAY13〜）。「情報システム部」を名乗り、パスワードの再登録を求める。
    /// 見抜く手がかりは差出人のアドレス（社内は @nexus-corp.example）。AI が作るものは文面が自然で、アドレスも紛らわしい。
    /// </summary>
    public static class PhishingMail
    {
        public static MailMessage Send(ShiftSession s, bool ai)
        {
            string from = MailManager.Sender("情報システム部 セキュリティ担当");
            string address = ai ? "security@nexus-corp.exarnple" : "it-support@nexus-c0rp-helpdesk.example";
            string body = ai
                ? $"{s.OperatorName} 様\n\n" +
                  "夜間のご対応、いつもお疲れさまです。\n" +
                  "先日お知らせした認証サーバーの切り替えに伴い、夜間運用担当の皆様のアカウントのみ再登録が必要になりました。\n" +
                  "お手数ですが、業務終了までに下のボタンから再登録をお願いします。\n" +
                  "（再登録が済んでいないアカウントは、明朝から利用できなくなります）\n" +
                  MailManager.Signature("情報システム部 セキュリティ担当")
                : $"{s.OperatorName} 様\n\n" +
                  "<b>【至急】</b>セキュリティ強化のため、パスワードの再登録が必要です。\n" +
                  "本日中に下のボタンから再登録してください。再登録しない場合、アカウントは停止されます。\n\n" +
                  "情報システム部";
            var mail = s.Mail.Add(new MailMessage
            {
                MailId = "PHISH-" + s.Day.dayNumber + "-" + s.Rng.Next(1000, 9999),
                From = from,
                FromAddress = address,
                To = MailManager.Recipient(s.OperatorName),
                Subject = ai ? "認証サーバー切り替えに伴うアカウント再登録のお願い" : "【至急】パスワード再登録のお願い",
                DateLabel = $"{s.Day.dateLabel} {s.Time.Now}",
                Body = body,
                Day = s.Day.dayNumber,
                ActionKey = MailManager.ActionPhishForm,
                ActionLabel = "パスワード再登録ページを開く",
                IsPhish = true,
                ReceivedAt = s.Time.CurrentMinute
            });
            s.Notifications.Notify(NotificationType.Info, "新着メール", $"情報システム部: {mail.Subject}", s.Time.CurrentMinute, () => s.Ui?.OpenMailbox(mail));
            return mail;
        }

        /// <summary>偽のフォームにIDとパスワードを入力してしまった</summary>
        public static void Submitted(ShiftSession s, MailMessage m)
        {
            if (s.OperatorCompromised) return;
            s.OperatorCompromised = true;
            s.Logs.Add(s.Time.CurrentMinute, "NIGHT-OPS", "INTERNET", s.OperatorName, "HTTP", "OK", "POST /account/re-register (外部のサーバー)");
            s.AddDamage("運用担当者のパスワードが社外に送信された");
        }
    }
}
