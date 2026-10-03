using NightShift.Core;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// ログインパスワードの変更。規程（PasswordPolicy）を満たせば弱いパスワードでも通る。
    /// 強度や「付箋に書いたか」は記録だけして表示しない（後の日の脆弱性要素）。
    /// </summary>
    public static class PasswordChangeView
    {
        public const string Id = "password";

        public static void Open(ShiftDesktop d)
        {
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "パスワードの変更", Icon = "lock", Width = 420, Height = 360, Minimizable = false, Maximizable = false,
                StatusCells = 1, Position = new Vector2(430, 160)
            }, out bool created);
            if (!created) return;
            var c = w.Content;
            var account = d.Game.Account;

            UIFactory.IconImage(c, "lock", 6, 6, 32);
            UIFactory.Label(c, "社内情報システムのログインパスワードを変更します。\n変更後は、次回のログインから新しいパスワードを使用します。", 48, 4, 350, 36, 12, TextAnchor.UpperLeft);

            UIFactory.Label(c, "ユーザーID:", 10, 50, 120, 20);
            UIFactory.ValueBox(c, 136, 50, 200, 20, account.UserId, false);
            UIFactory.Label(c, "現在のパスワード(C):", 10, 76, 124, 20);
            var current = UIFactory.Input(c, "", true);
            ((RectTransform)current.transform).Place(136, 76, 200, 20);
            UIFactory.Label(c, "新しいパスワード(N):", 10, 102, 124, 20);
            var next = UIFactory.Input(c, "", true);
            ((RectTransform)next.transform).Place(136, 102, 200, 20);
            UIFactory.Label(c, "確認のため再入力(R):", 10, 128, 124, 20);
            var confirm = UIFactory.Input(c, "", true);
            ((RectTransform)confirm.transform).Place(136, 128, 200, 20);

            var g = UIFactory.Group(c, "パスワード規程", 10, 154, 390, 92);
            UIFactory.Label(g, PasswordPolicy.PolicyText, 12, TextAnchor.UpperLeft).rectTransform.Stretch();

            var note = UIFactory.Check(c, "忘れないように付箋に書いてモニターに貼っておく", false, null);
            note.GetComponent<RectTransform>().Place(10, 252, 390, 20);

            var err = UIFactory.Label(c, "", 10, 274, 390, 18, 12);
            err.color = new Color(0.7f, 0, 0);

            void Submit()
            {
                if (current.text != account.Password)
                {
                    AudioFx.Error();
                    err.text = "現在のパスワードが正しくありません。";
                    current.text = "";
                    current.ActivateInputField();
                    return;
                }
                string error = PasswordPolicy.Validate(next.text, confirm.text, account.Password, account.UserId);
                if (error != null)
                {
                    AudioFx.Error();
                    err.text = error;
                    next.text = ""; confirm.text = "";
                    next.ActivateInputField();
                    return;
                }
                account.Apply(next.text, note.Value, d.S.Day.dayNumber);
                d.S.CompleteTask(OperatorTask.PasswordChange);
                d.S.Logs.Add(d.S.Time.CurrentMinute, "NIGHT-OPS", "AUTH-SRV01", account.UserId, "ACCOUNT", "CHANGED", "ログインパスワードを変更");
                AudioFx.Notify();
                w.Close();
                MessageBox.Show(d, "パスワードの変更", "パスワードを変更しました。\n次回のログインから新しいパスワードを使用してください。", "lock");
            }

            UIFactory.Button(c, "変更(O)", 200, 296, 96, 26, Submit, "check");
            UIFactory.Button(c, "キャンセル", 304, 296, 96, 26, w.Close);
            confirm.onEndEdit.AddListener(_ => { if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)) Submit(); });
            w.SetStatus(0, d.S.HasOpenTask(OperatorTask.PasswordChange) ? "情報システム部から変更を依頼されています。" : "");
            Screens.Delay.Run(w.gameObject, 0.05f, () => { if (current) current.ActivateInputField(); });
        }
    }
}
