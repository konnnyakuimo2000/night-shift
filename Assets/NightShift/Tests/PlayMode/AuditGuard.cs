using System;
using UnityEngine;

namespace NightShift.Tests
{
    /// <summary>
    /// PlayMode の監査テストは PlayerPrefs（セーブ）を消すので、普段は隔離したコピー（会社名 NightShiftPlayModeAudit）でだけ動かす。
    /// コマンドラインに -nightShiftAllowSaveReset を付けたときは、本体のプロジェクトでも動かす（セーブは消える）。
    /// </summary>
    public static class AuditGuard
    {
        public const string AllowFlag = "-nightShiftAllowSaveReset";
        public static bool Allowed =>
            Application.companyName == "NightShiftPlayModeAudit" || Array.IndexOf(Environment.GetCommandLineArgs(), AllowFlag) >= 0;
    }
}
