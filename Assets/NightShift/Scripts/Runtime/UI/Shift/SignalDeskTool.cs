using System.Text;
using NightShift.Core;
using NightShift.Systems.SideQuests;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>既に届いたメールと通常ログを整理するだけの、購入任意の案件ツール。</summary>
    public class SignalDeskTool : ToolWindow
    {
        Text _record;
        protected override void Build(RectTransform page)
        {
            float y = ToolStyle.Header(page, "SIGNAL DESK", "案件メール・関連ログ・作業時刻を整理します。操作は自動実行しません。", "network", ToolStyle.Teal);
            ToolStyle.Button(page, "依頼メールを開く", 0, y, 180, 28, () =>
            {
                var route = S.Quests.Route<Sq06Route>();
                var mail = S.Quests.Message("SQ06", route.CurrentMailId) ?? S.Quests.Message("SQ06", "INVITE");
                S.Ui?.OpenMailbox(mail);
            }, ToolStyle.Teal, false);
            ToolStyle.Button(page, "対象接続を開く", 190, y, 160, 28, () => S.Ui?.OpenConnectionMonitor(S.Quests.Route<Sq06Route>().TargetRequest), ToolStyle.Teal, false);
            ToolStyle.Button(page, "通常ログを開く", 360, y, 160, 28, () => S.Ui?.OpenLogViewer(), ToolStyle.Teal, false);
            _record = ToolStyle.ScrollText(UIFactory.Rect("Record", page).Stretch(0, y + 38, 0, 0));
        }

        protected override void Refresh()
        {
            var route = S.Quests.Route<Sq06Route>();
            var b = new StringBuilder(route.Memo());
            b.AppendLine("\n\n【受信済みメール】");
            foreach (var mail in CareerRecord.Current.questMails)
                if (mail.questId == "SQ06" && S.Mail.Find(mail.mailId) != null)
                    b.AppendLine($"DAY{mail.day} / {mail.from} / {mail.subject}（{(mail.viewed ? "確認済み" : "未確認")}）");
            b.AppendLine("\n【関連ログ】");
            foreach (var log in S.Logs.Entries)
                if (log.Source == Sq06Route.SourceIp || log.Source == Sq06Route.FakeDevice || log.Detail.Contains("SQ06") || log.Detail.Contains("NULL SIGNAL"))
                    b.AppendLine($"{TimeManager.Format(log.Time)} {log.Source} → {log.Destination} {log.Result}\n{LogViewerView.VisibleDetail(S, log)}");
            _record.text = b.ToString();
        }
    }
}
