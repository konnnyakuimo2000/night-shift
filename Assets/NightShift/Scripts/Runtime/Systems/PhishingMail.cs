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
                  "夜は長いので、全部を一度に済ませようとせず、手が空いたときで構いません。\n" +
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

        /// <summary>添付ファイル付きの偽メール（経路 C）。開くと担当者のワークステーションが感染してブルースクリーン（V4 BLUESTONE）</summary>
        public static MailMessage SendAttachment(ShiftSession s)
        {
            var mail = s.Mail.Add(new MailMessage
            {
                MailId = "PHISH-ATT-" + s.Day.dayNumber + "-" + s.Rng.Next(1000, 9999),
                From = MailManager.Sender("総務部 給与担当"),
                FromAddress = "kyuyo@nexus-corp-payroll.example",
                To = MailManager.Recipient(s.OperatorName),
                Subject = "【給与明細】訂正のお知らせ",
                DateLabel = $"{s.Day.dateLabel} {s.Time.Now}",
                Body = $"{s.OperatorName} 様\n\n先日お送りした給与明細に誤りがありました。\n訂正版を添付しますので、ご確認ください。\n\n総務部 給与担当",
                Day = s.Day.dayNumber,
                ActionKey = MailManager.ActionMalwareAttachment,
                ActionLabel = "添付: 給与明細.txt.exe を開く",
                IsPhish = true,
                ReceivedAt = s.Time.CurrentMinute
            });
            s.Notifications.Notify(NotificationType.Info, "新着メール", $"総務部: {mail.Subject}", s.Time.CurrentMinute, () => s.Ui?.OpenMailbox(mail));
            return mail;
        }

        /// <summary>添付を開いた。VACCINE 99 の常駐監視（定義ファイルが新しければ）が止める</summary>
        public static void OpenAttachment(ShiftSession s, MailMessage m, bool vaccine)
        {
            if (m.Reported) return;
            if (vaccine && Core.CareerRecord.Current.vaccineDefinitionDay >= 13)
            {
                s.Logs.Add(s.Time.CurrentMinute, "NIGHT-OPS", "-", s.OperatorName, "VACCINE", "BLOCKED", "給与明細.txt.exe（BLUESTONE を検出・隔離）");
                s.Notifications.Notify(NotificationType.SecurityAlert, "VACCINE 99", "ウイルスを検出しました: BLUESTONE（給与明細.txt.exe）", s.Time.CurrentMinute);
                return;
            }
            m.Reported = true; // 二度は開けない
            s.Security.MarkInfected("NIGHT-OPS-WS", "V4");
            s.Logs.Add(s.Time.CurrentMinute, "NIGHT-OPS", "-", s.OperatorName, "PROCESS", "START", "給与明細.txt.exe");
            s.AddDamage("担当者のワークステーションが感染した（BLUESTONE）");
            s.RequestHorror("BSOD");
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
