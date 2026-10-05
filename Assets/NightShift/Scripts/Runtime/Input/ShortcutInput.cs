using System.Collections.Generic;
using NightShift.Core;
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
        ToggleOverlay, // F11
        OpenMail
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

            bool ctrl = Ctrl, alt = Alt, shift = Shift, win = Win;
            bool text = TextFieldFocused;
            if (Capturing) return _frame; // キー設定の入力待ち中は何もしない

            void Add(Shortcut sc) { if (!_frame.Contains(sc)) _frame.Add(sc); }

            // ---- キー設定（GameSettings）で割り当てたキー ----
            if (GameSettings.Pressed(GameSettings.RemoteStart)) Add(Shortcut.StartMenu);
            if (GameSettings.Pressed(GameSettings.RemoteRun)) Add(Shortcut.Run);
            if (GameSettings.Pressed(GameSettings.RemoteExplorer)) Add(Shortcut.Explorer);
            if (GameSettings.Pressed(GameSettings.RemoteSettings)) Add(Shortcut.Settings);
            if (GameSettings.Pressed(GameSettings.RemoteDesktop)) Add(Shortcut.ShowDesktop);
            if (GameSettings.Pressed(GameSettings.RemoteTaskManager)) Add(Shortcut.TaskManager);
            if (GameSettings.Pressed(GameSettings.CloseWindow)) Add(Shortcut.CloseWindow);
            if (GameSettings.Pressed(GameSettings.Refresh)) Add(Shortcut.Refresh);
            if (GameSettings.Pressed(GameSettings.Pause)) Add(Shortcut.Escape);
            if (GameSettings.Pressed(GameSettings.DebugPanel)) Add(Shortcut.ToggleDebug);
            if (GameSettings.Pressed(GameSettings.DebugOverlay)) Add(Shortcut.ToggleOverlay);
            if (GameSettings.Pressed(GameSettings.OpenMail)) Add(Shortcut.OpenMail);
            var sw = GameSettings.Current.Get(GameSettings.SwitchWindow);
            if (sw != null && sw.key != KeyCode.None && Input.GetKeyDown(sw.key) && ctrl == sw.ctrl && alt == sw.alt)
                Add(shift && !sw.shift ? Shortcut.SwitchPrev : Shortcut.SwitchNext);

            // ---- 実機と同じキー（OS が先に処理しなかった場合に届く）----
            if (win && Input.GetKeyDown(KeyCode.R)) Add(Shortcut.Run);
            if (win && Input.GetKeyDown(KeyCode.E)) Add(Shortcut.Explorer);
            if (win && Input.GetKeyDown(KeyCode.I)) Add(Shortcut.Settings);
            if (win && Input.GetKeyDown(KeyCode.D)) Add(Shortcut.ShowDesktop);
            if (ctrl && shift && Input.GetKeyDown(KeyCode.Escape)) Add(Shortcut.TaskManager);
            if (alt && Input.GetKeyDown(KeyCode.Tab)) Add(shift ? Shortcut.SwitchPrev : Shortcut.SwitchNext);
            if (alt && Input.GetKeyDown(KeyCode.PageUp)) Add(Shortcut.SwitchNext);
            if (alt && Input.GetKeyDown(KeyCode.PageDown)) Add(Shortcut.SwitchPrev);
            if (alt && Input.GetKeyDown(KeyCode.F4)) Add(Shortcut.CloseWindow);

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

        /// <summary>キー設定画面でキー入力を待っている間 true（ゲーム側のショートカットを止める）</summary>
        public static bool Capturing;
    }
}
