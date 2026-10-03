namespace NightShift.Systems
{
    /// <summary>運用担当者自身への作業依頼（パスワード変更など）。未完了の間はチュートリアルの次イベントを待たせる。</summary>
    public class OperatorTask
    {
        public const string PasswordChange = "PASSWORD_CHANGE";

        public string TaskId;
        public string Title;
        public float CreatedAt;
        public bool Done;
        public float DoneAt;
    }
}
