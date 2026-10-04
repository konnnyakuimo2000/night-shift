using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>偽メールのリンク先（パスワード再登録の偽フォーム）。ゲーム内の演出のみ。</summary>
    public class PhishFormView : MonoBehaviour
    {
        public static void Open(ShiftDesktop d, MailMessage m)
        {
            var w = d.OpenWindow(new WindowSpec
            {
                Id = "phish:" + m.MailId, Title = "アカウント再登録 - ブラウザー", Icon = "globe", Width = 520, Height = 330,
                StatusCells = 1
            }, out bool created);
            if (!created) return;
            var c = w.Content;
            // アドレス欄は実際のブラウザーのように白い枠で、はっきり読めるように（偽の綴りに気づけることが大事）
            UIFactory.Label(c, "アドレス:", 0, 0, 60, 20, 12, TextAnchor.MiddleLeft);
            UIFactory.ValueBox(c, 62, 0, 438, 20, "http://nexus-c0rp-helpdesk.example/account/re-register");
            var head = UIFactory.Solid(c, RetroTheme.Navy, "Head");
            head.rectTransform.Place(0, 24, 500, 34);
            UIFactory.Label(head.transform, "  NEXUS 社内アカウント 再登録", 16, TextAnchor.MiddleLeft, Color.white).rectTransform.Stretch();
            UIFactory.Label(c, "ユーザーID と 現在のパスワード を入力してください。", 20, 70, 460, 20, 12);
            UIFactory.Label(c, "ユーザーID:", 40, 104, 100, 20);
            var id = UIFactory.Input(c, 140, 104, 220);
            id.text = d.S.OperatorName;
            UIFactory.Label(c, "パスワード:", 40, 132, 100, 20);
            var pw = UIFactory.Input(c, 140, 132, 220);
            pw.contentType = InputField.ContentType.Password;
            UIFactory.Button(c, "登録", 140, 170, 100, 26, () =>
            {
                if (string.IsNullOrEmpty(pw.text)) { AudioFx.Error(); w.SetStatus(0, "パスワードを入力してください。"); return; }
                PhishingMail.Submitted(d.S, m);
                w.Close();
                MessageBox.Show(d, "アカウント再登録", "再登録を受け付けました。", "info");
            });
            UIFactory.Button(c, "閉じる", 250, 170, 100, 26, w.Close);
            w.SetStatus(0, "");
        }
    }
}
