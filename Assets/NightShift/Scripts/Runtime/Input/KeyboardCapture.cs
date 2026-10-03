using System.Collections.Generic;

namespace NightShift.Controls
{
    /// <summary>
    /// OS予約ショートカット（Win+R / Alt+Tab / Ctrl+Shift+Esc / Alt+F4 等）は OS が先に処理するため、
    /// ゲームでは取り込まない。代わりに ShortcutInput の RDP互換代替キーで同じ機能に到達できる。
    /// （OS全体のキー入力を横取りする仕組みは使用しない方針）
    /// 将来、別の安全な入力経路を追加する場合の拡張点としてAPIのみ残す。
    /// </summary>
    public static class KeyboardCapture
    {
        public static bool Enabled = false;
        public static volatile bool AppFocused = true;
        public static bool Installed => false;
        public static bool Supported => false;

        public static void Drain(List<Shortcut> into) { }
        public static void Install() { }
        public static void Uninstall() { }
    }
}
