using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Core
{
    /// <summary>
    /// プレイヤー（夜間運用担当）の社内システムアカウント。
    /// パスワードの強度・付箋へのメモ・初期化回数などを記録し、後の日で「脆弱性」として利用する。
    /// </summary>
    [Serializable]
    public class OperatorAccount
    {
        public const string InitialPassword = "nexus98";

        public string UserId = GameManager.DefaultOperatorName;
        public string Password = InitialPassword;
        public bool Changed;             // 初期パスワードから変更したか
        public bool WrittenDown;         // 付箋に書いてモニターに貼ったか
        public int ResetCount;           // 「パスワードを忘れた」で初期化した回数
        public int Strength;             // 0-4（PasswordPolicy.Evaluate）
        public List<string> Weaknesses = new List<string>();

        const string Key = "NightShift.OperatorAccount";

        public static OperatorAccount Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var a = JsonUtility.FromJson<OperatorAccount>(json);
                    if (a != null && !string.IsNullOrEmpty(a.Password)) return a;
                }
            }
            catch { }
            return new OperatorAccount();
        }

        public string Snapshot() => JsonUtility.ToJson(this);

        /// <summary>セーブ地点の内容に戻して保存する</summary>
        public static OperatorAccount Restore(string json)
        {
            try
            {
                var a = JsonUtility.FromJson<OperatorAccount>(json);
                if (a != null && !string.IsNullOrEmpty(a.Password)) { a.Save(); return a; }
            }
            catch { }
            return Load();
        }

        public void Save()
        {
            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
                PlayerPrefs.Save();
            }
            catch { }
        }

        public bool Matches(string id, string password) =>
            string.Equals((id ?? "").Trim(), UserId, StringComparison.OrdinalIgnoreCase) && password == Password;

        /// <summary>パスワードを忘れた場合の初期化（情報システム部に依頼した扱い）</summary>
        public void ResetToInitial()
        {
            Password = InitialPassword;
            Changed = false;
            WrittenDown = false;
            ResetCount++;
            Strength = PasswordPolicy.Evaluate(Password, UserId, out Weaknesses);
            Save();
        }

        /// <summary>パスワードを付箋に書いてモニターに貼る／はがす（変更した後はいつでも選べる）</summary>
        public void SetWrittenDown(bool v)
        {
            if (!Changed) return;
            WrittenDown = v;
            Save();
        }

        public void Apply(string newPassword, bool writtenDown, int day)
        {
            Password = newPassword;
            Changed = true;
            WrittenDown = writtenDown;
            Strength = PasswordPolicy.Evaluate(newPassword, UserId, out Weaknesses);
            Save();
        }

        /// <summary>新しいゲームを始めた時（タイトルの「出勤する」）</summary>
        public static OperatorAccount NewGame(string userId)
        {
            var a = new OperatorAccount { UserId = userId };
            a.Strength = PasswordPolicy.Evaluate(a.Password, userId, out a.Weaknesses);
            return a;
        }
    }

    /// <summary>
    /// 社内のパスワード規程（1998年当時の緩めの規程）。
    /// 規程を満たしても「弱い」パスワードは作れる。その弱さは後の日の脆弱性要素になる。
    /// </summary>
    public static class PasswordPolicy
    {
        public const int MinLength = 6;

        static readonly string[] Common =
        {
            "password", "passw0rd", "123456", "12345678", "qwerty", "abc123", "nexus", "admin", "letmein", "welcome", "night", "shift", "1998"
        };

        public static string PolicyText =>
            $"・{MinLength} 文字以上\n・英字と数字を両方含む\n・ユーザーIDと同じ文字列は不可\n・現在のパスワードと同じものは不可";

        /// <summary>規程違反があればエラーメッセージ。null なら規程を満たす。</summary>
        public static string Validate(string newPassword, string confirm, string currentPassword, string userId)
        {
            if (string.IsNullOrEmpty(newPassword)) return "新しいパスワードを入力してください。";
            if (newPassword != confirm) return "確認用のパスワードが一致しません。";
            if (newPassword.Length < MinLength) return $"パスワードは {MinLength} 文字以上にしてください。";
            bool hasLetter = false, hasDigit = false;
            foreach (char c in newPassword)
            {
                if (char.IsWhiteSpace(c)) return "パスワードに空白は使えません。";
                if (c < 0x21 || c > 0x7E) return "パスワードには半角の英数字・記号を使ってください。";
                if (char.IsLetter(c)) hasLetter = true;
                if (char.IsDigit(c)) hasDigit = true;
            }
            if (!hasLetter || !hasDigit) return "英字と数字を両方含めてください。";
            if (string.Equals(newPassword, userId, StringComparison.OrdinalIgnoreCase)) return "ユーザーIDと同じパスワードは使えません。";
            if (newPassword == currentPassword) return "現在のパスワードと同じものは使えません。";
            return null;
        }

        /// <summary>強度 0(非常に弱い)〜4(強い)。弱点の一覧も返す（後の日の脆弱性判定用）。</summary>
        public static int Evaluate(string password, string userId, out List<string> weaknesses)
        {
            weaknesses = new List<string>();
            if (string.IsNullOrEmpty(password)) { weaknesses.Add("EMPTY"); return 0; }
            string lower = password.ToLowerInvariant();
            int score = 0;
            if (password.Length >= 8) score++;
            if (password.Length >= 12) score++;
            bool upper = false, low = false, digit = false, symbol = false;
            foreach (char c in password)
            {
                if (char.IsUpper(c)) upper = true;
                else if (char.IsLower(c)) low = true;
                else if (char.IsDigit(c)) digit = true;
                else symbol = true;
            }
            int classes = (upper ? 1 : 0) + (low ? 1 : 0) + (digit ? 1 : 0) + (symbol ? 1 : 0);
            if (classes >= 3) score++;
            if (symbol) score++;
            if (password.Length < 8) weaknesses.Add("SHORT");
            foreach (var w in Common)
                if (lower.Contains(w)) { weaknesses.Add("COMMON_WORD"); score -= 2; break; }
            if (!string.IsNullOrEmpty(userId) && lower.Contains(userId.ToLowerInvariant())) { weaknesses.Add("CONTAINS_USER_ID"); score -= 2; }
            if (password == OperatorAccount.InitialPassword) { weaknesses.Add("INITIAL_PASSWORD"); score = 0; }
            if (classes <= 2) weaknesses.Add("LOW_VARIETY");
            return Mathf.Clamp(score, 0, 4);
        }

        public static string StrengthLabel(int s)
        {
            switch (s)
            {
                case 0: return "非常に弱い";
                case 1: return "弱い";
                case 2: return "普通";
                case 3: return "強い";
                default: return "非常に強い";
            }
        }
    }
}
