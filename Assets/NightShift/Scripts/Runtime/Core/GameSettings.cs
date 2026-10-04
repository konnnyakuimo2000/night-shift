using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Core
{
    [Serializable]
    public class KeyBinding
    {
        public string action;
        public KeyCode key;
        public bool ctrl, alt, shift;

        public KeyBinding Clone() => new KeyBinding { action = action, key = key, ctrl = ctrl, alt = alt, shift = shift };

        public bool SameCombo(KeyBinding o) => o != null && key != KeyCode.None && key == o.key && ctrl == o.ctrl && alt == o.alt && shift == o.shift;

        public string Label
        {
            get
            {
                if (key == KeyCode.None) return "（なし）";
                string s = "";
                if (ctrl) s += "Ctrl+";
                if (alt) s += "Alt+";
                if (shift) s += "Shift+";
                return s + KeyName(key);
            }
        }

        public static string KeyName(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.Escape: return "Esc";
                case KeyCode.Return: return "Enter";
                case KeyCode.PageUp: return "PageUp";
                case KeyCode.PageDown: return "PageDown";
                case KeyCode.Backspace: return "BackSpace";
                default:
                    if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((int)(k - KeyCode.Alpha0)).ToString();
                    return k.ToString();
            }
        }
    }

    /// <summary>
    /// ゲーム設定（グラフィック・サウンド・操作・キー設定）。PlayerPrefs に JSON で保存する。
    /// タイトル画面とゲーム中のシステム設定の両方から変更できる。
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        // ---- グラフィック ----
        public int screenMode;            // 0: ウィンドウ 1: ボーダーレス全画面 2: 全画面
        public int resolutionWidth = 1280;
        public int resolutionHeight = 800;
        public int uiScalePercent = 100;
        public int frameRate = 60;        // 0 = 無制限
        public bool vsync;

        // ---- サウンド ----
        public float volume = 0.6f;
        public bool clickSound = true;
        public bool notifySound = true;

        // ---- 操作 ----
        public bool letterAccelerators = true;   // 文字キーだけでボタンを押せる（A=許可 等）
        public float notificationSeconds = 8f;

        // ---- ホラー演出（VIRUS_CATALOG.md） ----
        public bool horrorMild;            // 控えめ（不気味な画像・ささやき声を出さない。ゲーム上の効果は同じ）
        public bool reduceFlashing;        // 点滅を減らす（光過敏性への配慮）
        public bool loudSounds = true;     // 急な大きな音

        // ---- デバッグ ----
        [Tooltip("任意の日から開始・デバッグパネル（F12）を有効にする")]
        public bool debugMode = Application.isEditor;

        // ---- キー設定 ----
        public List<KeyBinding> bindings = new List<KeyBinding>();

        // アクションID
        public const string Pause = "PAUSE";
        public const string Allow = "ALLOW";
        public const string Block = "BLOCK";
        public const string Lookup = "LOOKUP";
        public const string OpenMail = "MAIL";
        public const string SwitchWindow = "SWITCH";
        public const string CloseWindow = "CLOSE";
        public const string RemoteStart = "REMOTE_START";
        public const string RemoteRun = "REMOTE_RUN";
        public const string RemoteExplorer = "REMOTE_EXPLORER";
        public const string RemoteSettings = "REMOTE_SETTINGS";
        public const string RemoteDesktop = "REMOTE_DESKTOP";
        public const string RemoteTaskManager = "REMOTE_TASKMGR";
        public const string Refresh = "REFRESH";
        public const string DebugPanel = "DEBUG";
        public const string DebugOverlay = "OVERLAY";

        public static readonly (string id, string label, string group)[] Actions =
        {
            (Pause, "一時停止", "共通"),
            (SwitchWindow, "ウィンドウの切り替え", "共通"),
            (CloseWindow, "ウィンドウを閉じる", "共通"),
            (OpenMail, "メールを開く", "共通"),
            (Allow, "接続を許可", "接続監視"),
            (Block, "接続を拒否", "接続監視"),
            (Lookup, "社員情報を参照", "接続監視"),
            (RemoteStart, "スタートメニュー", "リモートPC"),
            (RemoteRun, "ファイル名を指定して実行", "リモートPC"),
            (RemoteExplorer, "エクスプローラー", "リモートPC"),
            (RemoteSettings, "設定", "リモートPC"),
            (RemoteDesktop, "デスクトップの表示", "リモートPC"),
            (RemoteTaskManager, "タスク マネージャー", "リモートPC"),
            (Refresh, "更新", "リモートPC"),
            (DebugPanel, "デバッグパネル", "開発用"),
            (DebugOverlay, "デバッグ表示", "開発用"),
        };

        public static List<KeyBinding> DefaultBindings()
        {
            KeyBinding K(string a, KeyCode k, bool c = false, bool al = false, bool sh = false) =>
                new KeyBinding { action = a, key = k, ctrl = c, alt = al, shift = sh };
            return new List<KeyBinding>
            {
                K(Pause, KeyCode.Escape),
                K(SwitchWindow, KeyCode.Tab, c: true),
                K(CloseWindow, KeyCode.F4, c: true),
                K(OpenMail, KeyCode.M, c: true),
                K(Allow, KeyCode.A),
                K(Block, KeyCode.B),
                K(Lookup, KeyCode.R),
                K(RemoteStart, KeyCode.Home, al: true),
                K(RemoteRun, KeyCode.R, c: true, al: true),
                K(RemoteExplorer, KeyCode.E, c: true, al: true),
                K(RemoteSettings, KeyCode.I, c: true, al: true),
                K(RemoteDesktop, KeyCode.D, c: true, al: true),
                K(RemoteTaskManager, KeyCode.End, c: true, al: true),
                K(Refresh, KeyCode.F5),
                K(DebugPanel, KeyCode.F12),
                K(DebugOverlay, KeyCode.F11),
            };
        }

        // ---------------- instance ----------------
        const string PrefKey = "NightShift.Settings";
        static GameSettings _current;
        public static event Action Changed;

        public static GameSettings Current
        {
            get
            {
                if (_current == null) _current = Load();
                return _current;
            }
        }

        static GameSettings Load()
        {
            GameSettings s = null;
            try
            {
                var json = PlayerPrefs.GetString(PrefKey, "");
                if (!string.IsNullOrEmpty(json)) s = JsonUtility.FromJson<GameSettings>(json);
            }
            catch { }
            if (s == null) s = new GameSettings();
            s.EnsureBindings();
            return s;
        }

        /// <summary>新しく追加されたアクションに既定のキーを補う</summary>
        void EnsureBindings()
        {
            if (bindings == null) bindings = new List<KeyBinding>();
            foreach (var d in DefaultBindings())
                if (bindings.Find(b => b.action == d.action) == null) bindings.Add(d);
        }

        public void Save()
        {
            try
            {
                PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(this));
                PlayerPrefs.Save();
            }
            catch { }
            Changed?.Invoke();
        }

        public KeyBinding Get(string action) => bindings.Find(b => b.action == action);

        public static string KeyLabel(string action)
        {
            var b = Current.Get(action);
            return b != null ? b.Label : "（なし）";
        }

        /// <summary>修飾キーの押下状態まで一致したらこのフレームで押された</summary>
        public static bool Pressed(string action)
        {
            var b = Current.Get(action);
            if (b == null || b.key == KeyCode.None || !Input.GetKeyDown(b.key)) return false;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return ctrl == b.ctrl && alt == b.alt && shift == b.shift;
        }

        /// <summary>
        /// 業務画面のボタン操作（許可・拒否など）のキー。修飾キーなしの文字キーに割り当てていて、
        /// 設定の「文字キーだけでボタンを押す」がオフのときは Alt＋その文字キーで押す。
        /// </summary>
        public static bool PressedButton(string action)
        {
            var b = Current.Get(action);
            if (b == null || b.key == KeyCode.None || !Input.GetKeyDown(b.key)) return false;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool plainLetter = !b.ctrl && !b.alt && !b.shift &&
                ((b.key >= KeyCode.A && b.key <= KeyCode.Z) || (b.key >= KeyCode.Alpha0 && b.key <= KeyCode.Alpha9));
            if (plainLetter && !Current.letterAccelerators) return alt && !ctrl && !shift;
            return ctrl == b.ctrl && alt == b.alt && shift == b.shift;
        }

        /// <summary>割り当てる。同じ組み合わせを使っている別のアクションは、元のキーと入れ替える。</summary>
        public string Assign(string action, KeyBinding combo)
        {
            var target = Get(action);
            if (target == null) return null;
            string swapped = null;
            foreach (var b in bindings)
            {
                if (b != target && b.SameCombo(combo))
                {
                    b.key = target.key; b.ctrl = target.ctrl; b.alt = target.alt; b.shift = target.shift;
                    swapped = b.action;
                }
            }
            target.key = combo.key; target.ctrl = combo.ctrl; target.alt = combo.alt; target.shift = combo.shift;
            return swapped;
        }

        public void ResetBindings()
        {
            bindings = DefaultBindings();
        }

        public static string ActionLabel(string id)
        {
            foreach (var a in Actions) if (a.id == id) return a.label;
            return id;
        }

        // ---------------- apply ----------------
        public static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720), new Vector2Int(1280, 800), new Vector2Int(1366, 768), new Vector2Int(1440, 900),
            new Vector2Int(1600, 900), new Vector2Int(1680, 1050), new Vector2Int(1920, 1080), new Vector2Int(1920, 1200), new Vector2Int(2560, 1440)
        };

        public static readonly int[] UiScales = { 80, 90, 100, 110, 125, 150 };
        public static readonly int[] FrameRates = { 30, 60, 120, 0 };
        public static readonly float[] NotificationTimes = { 5f, 8f, 12f, 20f };

        public void ApplyAudio()
        {
            UI.AudioFx.Volume = Mathf.Clamp01(volume);
            UI.AudioFx.ClickEnabled = clickSound;
            UI.AudioFx.NotifyEnabled = notifySound;
        }

        public void ApplyGraphics()
        {
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            Application.targetFrameRate = frameRate <= 0 ? -1 : frameRate;
#if !UNITY_EDITOR
            var mode = screenMode == 2 ? FullScreenMode.ExclusiveFullScreen : screenMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (mode == FullScreenMode.FullScreenWindow)
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, mode);
            else
                Screen.SetResolution(resolutionWidth, resolutionHeight, mode);
#endif
        }

        public void ApplyAll()
        {
            ApplyAudio();
            ApplyGraphics();
            Changed?.Invoke();
        }
    }
}
