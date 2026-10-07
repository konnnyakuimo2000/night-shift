namespace NightShift.Systems
{
    /// <summary>
    /// T5 担当者宛ての偽メール（DAY13〜）。「情報システム部」を名乗り、パスワードの再登録を求める。
    /// 見抜く手がかりは差出人のアドレス（社内は @nexus-corp.example）。AI が作るものは文面が自然で、アドレスも紛らわしい。
    /// </summary>
    public static class PhishingMail
    {
        /// <summary>偽メールの文面（差出人・アドレス・件名・本文）。どれも差出人アドレスが社内（@nexus-corp.example）ではない</summary>
        struct Template
        {
            public string From, Address, Subject, Body, Attachment;
            public Template(string from, string address, string subject, string body, string attachment = null)
            { From = from; Address = address; Subject = subject; Body = body; Attachment = attachment; }
        }

        // 下手な偽メール（日本語が少しおかしい・脅し文句・社外のアドレス）
        static Template[] Crude(string name) => new[]
        {
            new Template("情報システム部", "it-support@nexus-c0rp-helpdesk.example", "【至急】パスワード再登録のお願い",
                $"{name} 様\n\n<b>【至急】</b>セキュリティ強化のため、パスワードの再登録が必要です。\n本日中に下のボタンから再登録してください。再登録しない場合、アカウントは停止されます。\n\n情報システム部"),
            new Template("システム管理者", "admin@nexus-corp-support.example", "【重要】アカウント停止のお知らせ",
                $"{name} 様\n\nあなたのアカウントに不正なアクセスが検出されたので、一時的に停止されました。\n下のボタンから本人確認をして下さい。\n<b>24時間以内に確認がない場合、アカウントは削除されます。</b>\n\nシステム管理者"),
            new Template("ヘルプデスク", "helpdesk@nexus-corp.example.net", "パスワードの有効期限が切れます",
                $"{name} 様\n\nあなたのパスワードは、まもなく有効期限が切れます。\n今すぐ下のボタンから更新して下さい。更新しないと、メールが受信できなくなります。\n\nヘルプデスク"),
        };

        // AI が作った自然な偽メール（社内の言い回し・本物の人名・紛らわしいアドレス）
        static Template[] Natural(string name) => new[]
        {
            new Template("情報システム部 セキュリティ担当", "security@nexus-corp.exarnple", "認証サーバー切り替えに伴うアカウント再登録のお願い",
                $"{name} 様\n\n夜間のご対応、いつもお疲れさまです。\n先日お知らせした認証サーバーの切り替えに伴い、夜間運用担当の皆様のアカウントのみ再登録が必要になりました。\n" +
                "お手数ですが、業務終了までに下のボタンから再登録をお願いします。\n夜は長いので、全部を一度に済ませようとせず、手が空いたときで構いません。\n" +
                "（再登録が済んでいないアカウントは、明朝から利用できなくなります）\n" + MailManager.Signature("情報システム部 セキュリティ担当")),
            new Template("情報システム部 課長 森田", "morita@nexus-corp.exampIe", "年末のアカウント棚卸しについて（夜間担当分）",
                $"{name} 様\n\nお疲れさまです。森田です。\n年末のアカウント棚卸しで、夜間担当のIDだけ照合が取れていないと監査から指摘がありました。\n" +
                "朝の会議で報告するので、今夜中に下のページでIDとパスワードを確認しておいてください。\n面倒をかけますが、よろしく。\n" + MailManager.Signature("情報システム部 課長 森田")),
            new Template("総務部 勤怠担当", "kintai@nexus-corp.example.co", "【夜間勤務の方へ】新勤怠システムへの移行について",
                $"{name} 様\n\nいつもお疲れさまです。総務部 勤怠担当です。\n来月から勤怠の記録が新しいシステムに変わります。\n" +
                "夜間勤務の方は日中の説明会に参加できないため、下のページから社内IDで先に登録をお願いしております。\n登録が済むと、深夜手当の計算も自動になります。\n" + MailManager.Signature("総務部 勤怠担当")),
        };

        // 添付ファイル付き（二重の拡張子・社外のアドレス）
        static Template[] Attachments(string name) => new[]
        {
            new Template("総務部 給与担当", "kyuyo@nexus-corp-payroll.example", "【給与明細】訂正のお知らせ",
                $"{name} 様\n\n先日お送りした給与明細に誤りがありました。\n訂正版を添付しますので、ご確認ください。\n\n総務部 給与担当", "給与明細.txt.exe"),
            new Template("総務部 健康管理室", "kenko@nexus-corp-health.example", "健康診断の結果について（要再検査）",
                $"{name} 様\n\n先日の健康診断の結果、再検査が必要な項目がありました。\n詳しくは添付の結果をご確認のうえ、今週中に病院を予約してください。\n\n総務部 健康管理室", "健康診断結果.doc.exe"),
            new Template("総務部", "hr@nexus-corp.exarnple", "年末調整の書類（記入のお願い）",
                $"{name} 様\n\n年末調整の書類を添付します。\n記入のうえ、今月末までに総務部へ提出してください。提出がない場合、還付金は受け取れません。\n\n総務部", "年末調整_記入用.xls.exe"),
            new Template("総務部 イベント係", "ryoko@nexus-c0rp.example", "社員旅行のしおり（熱海・1泊2日）",
                $"{name} 様\n\n今年の社員旅行のしおりができました！\n部屋割りとバスの座席も載っていますので、添付を開いて確認してください。\n夜勤の方も参加できるよう調整中です。\n\n総務部 イベント係", "社員旅行のしおり.exe"),
        };

        static System.Random Rng(ShiftSession s, string salt) => new System.Random(s.Seed * 31 + s.Day.dayNumber * 7 + salt.Length * 101 + salt[0] + s.Mail.Inbox.Count);

        public static MailMessage Send(ShiftSession s, bool ai)
        {
            var list = ai ? Natural(s.OperatorName) : Crude(s.OperatorName);
            var t = list[Rng(s, ai ? "ai" : "crude").Next(list.Length)];
            var mail = s.Mail.Add(new MailMessage
            {
                MailId = "PHISH-" + s.Day.dayNumber + "-" + s.Rng.Next(1000, 9999),
                From = MailManager.Sender(t.From),
                FromAddress = t.Address,
                To = MailManager.Recipient(s.OperatorName),
                Subject = t.Subject,
                DateLabel = $"{s.Day.dateLabel} {s.Time.Now}",
                Body = t.Body,
                Day = s.Day.dayNumber,
                ActionKey = MailManager.ActionPhishForm,
                ActionLabel = "パスワード再登録ページを開く",
                IsPhish = true,
                ReceivedAt = s.Time.CurrentMinute
            });
            s.Notifications.Notify(NotificationType.Info, "新着メール", $"{t.From}: {mail.Subject}", s.Time.CurrentMinute, () => s.Ui?.OpenMailbox(mail));
            return mail;
        }

        /// <summary>添付ファイル付きの偽メール（経路 C）。開くと担当者のワークステーションが感染してブルースクリーン（V4 BLUESTONE）</summary>
        public static MailMessage SendAttachment(ShiftSession s)
        {
            var list = Attachments(s.OperatorName);
            var t = list[Rng(s, "att").Next(list.Length)];
            var mail = s.Mail.Add(new MailMessage
            {
                MailId = "PHISH-ATT-" + s.Day.dayNumber + "-" + s.Rng.Next(1000, 9999),
                From = MailManager.Sender(t.From),
                FromAddress = t.Address,
                To = MailManager.Recipient(s.OperatorName),
                Subject = t.Subject,
                DateLabel = $"{s.Day.dateLabel} {s.Time.Now}",
                Body = t.Body,
                Day = s.Day.dayNumber,
                ActionKey = MailManager.ActionMalwareAttachment,
                ActionLabel = $"添付: {t.Attachment} を開く",
                IsPhish = true,
                ReceivedAt = s.Time.CurrentMinute
            });
            s.Notifications.Notify(NotificationType.Info, "新着メール", $"{t.From}: {mail.Subject}", s.Time.CurrentMinute, () => s.Ui?.OpenMailbox(mail));
            return mail;
        }

        /// <summary>添付の名前（「添付: ○○ を開く」から取り出す）</summary>
        static string AttachmentName(MailMessage m)
        {
            string label = m?.ActionLabel ?? "";
            if (label.StartsWith("添付: ") && label.EndsWith(" を開く")) return label.Substring(4, label.Length - 4 - 4);
            return "給与明細.txt.exe";
        }

        /// <summary>添付を開いた。VACCINE 99 の常駐監視（定義ファイルが新しければ）が止める</summary>
        public static void OpenAttachment(ShiftSession s, MailMessage m, bool vaccine)
        {
            if (m.Reported) return;
            if (vaccine && Core.CareerRecord.Current.vaccineDefinitionDay >= 13)
            {
                s.Logs.Add(s.Time.CurrentMinute, "NIGHT-OPS", "-", s.OperatorName, "VACCINE", "BLOCKED", $"{AttachmentName(m)}（BLUESTONE を検出・隔離）");
                s.Notifications.Notify(NotificationType.SecurityAlert, "VACCINE 99", $"ウイルスを検出しました: BLUESTONE（{AttachmentName(m)}）", s.Time.CurrentMinute);
                return;
            }
            m.Reported = true; // 二度は開けない
            s.Security.MarkInfected("NIGHT-OPS-WS", "V4");
            s.Logs.Add(s.Time.CurrentMinute, "NIGHT-OPS", "-", s.OperatorName, "PROCESS", "START", AttachmentName(m));
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
