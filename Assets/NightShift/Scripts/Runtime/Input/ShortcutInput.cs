using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift.Controls
{
    public enum Shortcut
    {
        StartMenu,    // Win
        Run,          // Win+R
        Explorer,     // Win+E
        Settings,     // Win+I
        ShowDesktop,  // Win+D
        TaskManager,  // Ctrl+Shift+Esc
        SwitchNext,   // Alt+Tab
        SwitchPrev,   // Alt+Shift+Tab
        CloseWindow,  // Alt+F4
        Refresh,      // F5
        Escape,
        Copy, Paste, Cut, SelectAll, Rename, Delete, // 拡張用
        ToggleDebug,  // F12
        ToggleOverlay // F11
    }

    /// <summary>
    /// ショートカット入力の統合。
    /// ・OSキーボードフック（スタンドアロン）で取り込んだ実キー
    /// ・Unityが受け取れたキー
    /// ・エディタ用のRDP互換代替キー（Alt+Home=スタート, Ctrl+Alt+End=タスクマネージャー, Alt+PageUp/Down=切替, Ctrl+Alt+R/E/I/D, Ctrl+F4, Ctrl+Tab）
    /// ショートカット使用自体にスコア加点はしない。
    /// </summary>
    public static class ShortcutInput
    {
        static readonly List<Shortcut> _frame = new List<Shortcut>();
        static int _frameNo = -1;

        public static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        public static bool Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
        public static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        static bool Win => Input.GetKey(KeyCode.LeftWindows) || Input.GetKey(KeyCode.RightWindows) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

        /// <summary>Alt / Ctrl のどちらかで切替を開始した場合、その修飾キーが押され続けているか</summary>
        public static bool SwitchModifierHeld => Alt || Ctrl;

        public static bool TextFieldFocused
        {
            get
            {
                var es = EventSystem.current;
                if (es == null || es.currentSelectedGameObject == null) return false;
                var f = es.currentSelectedGameObject.GetComponent<InputField>();
                return f != null && f.isFocused;
            }
        }

        public static List<Shortcut> Poll()
        {
            if (_frameNo == Time.frameCount) return _frame;
            _frameNo = Time.frameCount;
            _frame.Clear();
            KeyboardCapture.Drain(_frame);

            bool ctrl = Ctrl, alt = Alt, shift = Shift, win = Win;
            bool text = TextFieldFocused;

            // Win 系
            if (win && Input.GetKeyDown(KeyCode.R)) _frame.Add(Shortcut.Run);
            if (win && Input.GetKeyDown(KeyCode.E)) _frame.Add(Shortcut.Explorer);
            if (win && Input.GetKeyDown(KeyCode.I)) _frame.Add(Shortcut.Settings);
            if (win && Input.GetKeyDown(KeyCode.D)) _frame.Add(Shortcut.ShowDesktop);
            if (ctrl && alt && Input.GetKeyDown(KeyCode.R)) _frame.Add(Shortcut.Run);
            if (ctrl && alt && Input.GetKeyDown(KeyCode.E)) _frame.Add(Shortcut.Explorer);
            if (ctrl && alt && Input.GetKeyDown(KeyCode.I)) _frame.Add(Shortcut.Settings);
            if (ctrl && alt && Input.GetKeyDown(KeyCode.D)) _frame.Add(Shortcut.ShowDesktop);
            if (alt && Input.GetKeyDown(KeyCode.Home)) _frame.Add(Shortcut.StartMenu);
            if (ctrl && Input.GetKeyDown(KeyCode.Escape) && !shift) _frame.Add(Shortcut.StartMenu);

            // タスクマネージャー
            if (ctrl && shift && Input.GetKeyDown(KeyCode.Escape)) _frame.Add(Shortcut.TaskManager);
            else if (ctrl && alt && Input.GetKeyDown(KeyCode.End)) _frame.Add(Shortcut.TaskManager);

            // 切替
            if (Input.GetKeyDown(KeyCode.Tab) && (alt || ctrl)) _frame.Add(shift ? Shortcut.SwitchPrev : Shortcut.SwitchNext);
            if (alt && Input.GetKeyDown(KeyCode.PageUp)) _frame.Add(Shortcut.SwitchNext);
            if (alt && Input.GetKeyDown(KeyCode.PageDown)) _frame.Add(Shortcut.SwitchPrev);

            // 閉じる
            if (Input.GetKeyDown(KeyCode.F4) && (alt || ctrl)) _frame.Add(Shortcut.CloseWindow);

            if (Input.GetKeyDown(KeyCode.F5)) _frame.Add(Shortcut.Refresh);
            if (Input.GetKeyDown(KeyCode.Escape) && !ctrl) _frame.Add(Shortcut.Escape);
            if (Input.GetKeyDown(KeyCode.F12)) _frame.Add(Shortcut.ToggleDebug);
            if (Input.GetKeyDown(KeyCode.F11)) _frame.Add(Shortcut.ToggleOverlay);

            if (!text)
            {
                if (ctrl && !alt && Input.GetKeyDown(KeyCode.C)) _frame.Add(Shortcut.Copy);
                if (ctrl && !alt && Input.GetKeyDown(KeyCode.V)) _frame.Add(Shortcut.Paste);
                if (ctrl && !alt && Input.GetKeyDown(KeyCode.X)) _frame.Add(Shortcut.Cut);
                if (ctrl && !alt && Input.GetKeyDown(KeyCode.A)) _frame.Add(Shortcut.SelectAll);
                if (Input.GetKeyDown(KeyCode.F2)) _frame.Add(Shortcut.Rename);
                if (Input.GetKeyDown(KeyCode.Delete)) _frame.Add(Shortcut.Delete);
            }
            return _frame;
        }

        public static bool Has(List<Shortcut> list, Shortcut s) => list.Contains(s);
    }
}
