using System;
using System.Collections.Generic;
using NightShift.Controls;
using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// 設定画面（グラフィック / サウンド / 操作 / キー設定）。タイトル画面とゲーム中の両方で使う。
    /// 変更は即時反映して保存する（解像度・画面モードは [適用] で反映）。
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        RectTransform _page;
        readonly List<RetroButton> _tabs = new List<RetroButton>();
        int _tab;
        Text _status;
        Action<RectTransform> _extraGeneral;

        // キー割り当ての入力待ち
        string _capturing;
        Text _captureLabel;

        static GameSettings S => GameSettings.Current;

        /// <summary>extra: 「操作」ページの末尾に追加する項目（ゲーム中のパスワード変更など）</summary>
        public static SettingsPanel Build(RectTransform content, Action<RectTransform> extra = null)
        {
            var p = content.gameObject.AddComponent<SettingsPanel>();
            p._extraGeneral = extra;
            string[] names = { "グラフィック", "サウンド", "操作", "キー設定" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                p._tabs.Add(UIFactory.Button(content, names[i], i * 104, 0, 100, 24, () => p.ShowTab(idx)));
            }
            var frame = UIFactory.Image(content, RetroTheme.Raised, Color.white, "PageFrame");
            frame.rectTransform.Stretch(0, 26, 0, 26);
            p._page = UIFactory.Rect("Page", frame.transform).Stretch(10, 10, 10, 10);
            p._status = UIFactory.Label(content, "", 12);
            p._status.rectTransform.anchorMin = new Vector2(0, 0); p._status.rectTransform.anchorMax = new Vector2(1, 0);
            p._status.rectTransform.pivot = new Vector2(0, 0);
            p._status.rectTransform.offsetMin = new Vector2(2, 2); p._status.rectTransform.offsetMax = new Vector2(-2, 22);
            p._status.color = RetroTheme.Shadow;
            p.ShowTab(0);
            return p;
        }

        void OnDestroy() => ShortcutInput.Capturing = false;

        void ShowTab(int i)
        {
            CancelCapture();
            _tab = i;
            for (int t = 0; t < _tabs.Count; t++) _tabs[t].SetLatched(t == i);
            foreach (Transform c in _page) Destroy(c.gameObject);
            switch (i)
            {
                case 0: Graphics(); break;
                case 1: Sound(); break;
                case 2: Controls(); break;
                default: Keys(); break;
            }
        }

        void Saved(string msg = "設定を保存しました。")
        {
            S.Save();
            _status.text = msg;
        }

        // ---------------- グラフィック ----------------
        void Graphics()
        {
            float y = 0;
            UIFactory.Label(_page, "画面モード:", 0, y, 120, 20);
            var modes = new List<string> { "ウィンドウ", "ボーダーレス全画面", "全画面" };
            RetroDropdown.Create(_page, 130, y, 200, modes, S.screenMode, v => { S.screenMode = v; _status.text = "[適用] で反映されます。"; });
            y += 28;

            UIFactory.Label(_page, "解像度:", 0, y, 120, 20);
            var res = new List<string>();
            int resIndex = 0;
            for (int i = 0; i < GameSettings.Resolutions.Length; i++)
            {
                var r = GameSettings.Resolutions[i];
                res.Add($"{r.x} × {r.y}");
                if (r.x == S.resolutionWidth && r.y == S.resolutionHeight) resIndex = i;
            }
            RetroDropdown.Create(_page, 130, y, 200, res, resIndex, v =>
            {
                S.resolutionWidth = GameSettings.Resolutions[v].x;
                S.resolutionHeight = GameSettings.Resolutions[v].y;
                _status.text = "[適用] で反映されます。";
            });
            y += 28;

            UIFactory.Label(_page, "UI の拡大率:", 0, y, 120, 20);
            var scales = new List<string>();
            int scaleIndex = 2;
            for (int i = 0; i < GameSettings.UiScales.Length; i++)
            {
                scales.Add(GameSettings.UiScales[i] + " %");
                if (GameSettings.UiScales[i] == S.uiScalePercent) scaleIndex = i;
            }
            RetroDropdown.Create(_page, 130, y, 200, scales, scaleIndex, v => { S.uiScalePercent = GameSettings.UiScales[v]; Saved("UI の拡大率を変更しました。"); });
            y += 28;
            var large = UIFactory.Check(_page, "判断に使う文字を大きくする（一覧・入力欄の IP・端末名・社員番号など）", S.largeText,
                v => { S.largeText = v; Saved("次に開いた画面から反映されます。"); });
            large.GetComponent<RectTransform>().Place(0, y, 520, 20);
            y += 28;

            UIFactory.Label(_page, "フレームレート:", 0, y, 120, 20);
            var fps = new List<string>();
            int fpsIndex = 1;
            for (int i = 0; i < GameSettings.FrameRates.Length; i++)
            {
                fps.Add(GameSettings.FrameRates[i] <= 0 ? "無制限" : GameSettings.FrameRates[i] + " fps");
                if (GameSettings.FrameRates[i] == S.frameRate) fpsIndex = i;
            }
            RetroDropdown.Create(_page, 130, y, 200, fps, fpsIndex, v => { S.frameRate = GameSettings.FrameRates[v]; S.ApplyGraphics(); Saved(); });
            y += 28;

            var vs = UIFactory.Check(_page, "垂直同期 (VSync)", S.vsync, v => { S.vsync = v; S.ApplyGraphics(); Saved(); });
            vs.GetComponent<RectTransform>().Place(130, y, 200, 20);
            y += 34;

            UIFactory.Button(_page, "適用(A)", 130, y, 110, 26, () =>
            {
                S.ApplyGraphics();
                Saved(Application.isEditor ? "保存しました（エディタ実行中は画面モード・解像度は変更されません）。" : "画面設定を適用しました。");
            }, "check");
        }

        // ---------------- サウンド ----------------
        void Sound()
        {
            float y = 0;
            void VolumeRow(string label, Func<float> read, Action<float> write)
            {
                float row = y;
                UIFactory.Label(_page, label, 0, row, 120, 20);
                var bar = UIFactory.Image(_page, RetroTheme.Field, Color.white, "Bar");
                bar.rectTransform.Place(130, row + 2, 204, 16);
                var fill = UIFactory.Solid(bar.transform, RetroTheme.Navy, "Fill");
                var value = UIFactory.Label(_page, "", 380, row, 60, 20);
                void UpdateBar()
                {
                    fill.rectTransform.Place(2, 2, 200 * Mathf.Clamp01(read()), 12);
                    value.text = Mathf.RoundToInt(read() * 100) + " %";
                }
                void Adjust(float delta)
                {
                    write(Mathf.Clamp01(Mathf.Round((read() + delta) * 10) / 10f));
                    S.ApplyAudio(); UpdateBar(); Saved();
                }
                UpdateBar();
                UIFactory.Button(_page, "－", 340, row, 18, 20, () => Adjust(-0.1f));
                UIFactory.Button(_page, "＋", 360, row, 18, 20, () => Adjust(0.1f));
                y += 28;
            }
            VolumeRow("全体音量:", () => S.volume, v => S.volume = v);
            VolumeRow("BGM:", () => S.musicVolume, v => S.musicVolume = v);
            VolumeRow("環境音:", () => S.ambienceVolume, v => S.ambienceVolume = v);
            VolumeRow("効果音:", () => S.effectsVolume, v => S.effectsVolume = v);
            y += 4;
            var click = UIFactory.Check(_page, "クリック音", S.clickSound, v => { S.clickSound = v; S.ApplyAudio(); Saved(); });
            click.GetComponent<RectTransform>().Place(130, y, 200, 20);
            y += 26;
            var notify = UIFactory.Check(_page, "通知音（接続要求・問い合わせ・警告）", S.notifySound, v => { S.notifySound = v; S.ApplyAudio(); Saved(); });
            notify.GetComponent<RectTransform>().Place(130, y, 320, 20);
            y += 34;

            UIFactory.Button(_page, "通知音を試す", 130, y, 120, 24, AudioFx.Notify);
            UIFactory.Button(_page, "警告音を試す", 256, y, 120, 24, AudioFx.Alert);
            y += 36;

            UIFactory.Label(_page, "ホラー演出:", 20, y, 100, 20);
            var mild = UIFactory.Check(_page, "控えめにする（不気味な画像・ささやき声を出さない）", S.horrorMild, v => { S.horrorMild = v; Saved(); });
            mild.GetComponent<RectTransform>().Place(130, y, 380, 20);
            y += 26;
            var flash = UIFactory.Check(_page, "点滅を減らす（激しい明滅をゆっくりした暗転にする）", S.reduceFlashing, v => { S.reduceFlashing = v; Saved(); });
            flash.GetComponent<RectTransform>().Place(130, y, 380, 20);
            y += 26;
            var loud = UIFactory.Check(_page, "急な大きな音を鳴らす", S.loudSounds, v => { S.loudSounds = v; Saved(); });
            loud.GetComponent<RectTransform>().Place(130, y, 380, 20);
        }

        // ---------------- 操作 ----------------
        void Controls()
        {
            float y = 0;
            var letters = UIFactory.Check(_page, "文字キーだけでボタンを押す（例: 1〜7 でメニュー、C で閉じる）", S.letterAccelerators,
                v => { S.letterAccelerators = v; Saved(); });
            letters.GetComponent<RectTransform>().Place(0, y, 480, 20);
            y += 22;
            UIFactory.Label(_page, "オフにすると、Alt＋文字キーで操作します（入力ミスを防げます）。", 18, y, 460, 18, 12).color = RetroTheme.HintText;
            y += 30;

            UIFactory.Label(_page, "通知の表示時間:", 0, y, 120, 20);
            var times = new List<string>();
            int ti = 1;
            for (int i = 0; i < GameSettings.NotificationTimes.Length; i++)
            {
                times.Add(GameSettings.NotificationTimes[i] + " 秒");
                if (Mathf.Approximately(GameSettings.NotificationTimes[i], S.notificationSeconds)) ti = i;
            }
            RetroDropdown.Create(_page, 130, y, 140, times, ti, v => { S.notificationSeconds = GameSettings.NotificationTimes[v]; Saved(); });
            y += 36;

            UIFactory.Label(_page, "Windows キー・Alt+Tab・Ctrl+Shift+Esc などは手元のPCが処理するため、\nリモートPCの操作には [キー設定] で割り当てたキーを使います。", 0, y, 480, 34, 12).color = RetroTheme.HintText;
            y += 44;

            var debug = UIFactory.Check(_page, "デバッグモード（タイトルから任意の日を開始・デバッグパネル）", S.debugMode, v => { S.debugMode = v; Saved(); });
            debug.GetComponent<RectTransform>().Place(0, y, 480, 20);
            y += 30;

            _extraGeneral?.Invoke(UIFactory.Rect("Extra", _page).Place(0, y, 480, 40));
        }

        // ---------------- キー設定 ----------------
        void Keys()
        {
            var sr = UIFactory.Scroll(_page, out var content, true);
            ((RectTransform)sr.transform).Stretch(0, 0, 0, 34);
            float y = 4;
            string group = null;
            foreach (var a in GameSettings.Actions)
            {
                if (a.group != group)
                {
                    group = a.group;
                    var g = UIFactory.Label(content, "■ " + group, 8, y, 300, 18, 12);
                    g.fontStyle = FontStyle.Bold;
                    g.color = RetroTheme.TextBlue;
                    y += 20;
                }
                string id = a.id;
                UIFactory.Label(content, a.label, 20, y, 200, 20);
                var label = UIFactory.ValueBox(content, 220, y, 150, 20, GameSettings.KeyLabel(id));
                UIFactory.Button(content, "変更", 376, y, 60, 20, () => BeginCapture(id, label));
                y += 24;
            }
            content.sizeDelta = new Vector2(0, y + 8);

            var reset = UIFactory.Button(_page, "初期設定に戻す", 0, 0, 140, 26, () =>
            {
                S.ResetBindings();
                Saved("キー設定を初期設定に戻しました。");
                ShowTab(3);
            });
            reset.Rect.anchorMin = reset.Rect.anchorMax = new Vector2(0, 0);
            reset.Rect.pivot = new Vector2(0, 0);
            reset.Rect.anchoredPosition = new Vector2(0, 2);
            _status.text = "[変更] を押してから、割り当てるキー（Ctrl / Alt / Shift との組み合わせ可）を押してください。";
        }

        void BeginCapture(string id, Text label)
        {
            CancelCapture();
            _capturing = id;
            _captureLabel = label;
            label.text = "キーを押してください…";
            label.color = new Color(0.7f, 0, 0);
            ShortcutInput.Capturing = true;
            _status.text = $"「{GameSettings.ActionLabel(id)}」に割り当てるキーを押してください（右クリックでキャンセル）。";
        }

        void CancelCapture()
        {
            if (_capturing != null && _captureLabel != null)
            {
                _captureLabel.text = GameSettings.KeyLabel(_capturing);
                _captureLabel.color = RetroTheme.Text;
            }
            _capturing = null;
            _captureLabel = null;
            ShortcutInput.Capturing = false;
        }

        static bool IsModifier(KeyCode k) =>
            k == KeyCode.LeftControl || k == KeyCode.RightControl || k == KeyCode.LeftAlt || k == KeyCode.RightAlt || k == KeyCode.AltGr ||
            k == KeyCode.LeftShift || k == KeyCode.RightShift || k == KeyCode.LeftWindows || k == KeyCode.RightWindows ||
            k == KeyCode.LeftCommand || k == KeyCode.RightCommand;

        void Update()
        {
            if (_capturing == null) return;
            if (Input.GetMouseButtonDown(1)) { CancelCapture(); _status.text = "キャンセルしました。"; return; }
            if (!Input.anyKeyDown) return;
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (k == KeyCode.None || IsModifier(k) || k >= KeyCode.Mouse0) continue;
                if (!Input.GetKeyDown(k)) continue;
                var combo = new KeyBinding
                {
                    key = k,
                    ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                    alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr),
                    shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                };
                string id = _capturing;
                string swapped = S.Assign(id, combo);
                _capturing = null;
                ShortcutInput.Capturing = false;
                Saved(swapped != null
                    ? $"「{GameSettings.ActionLabel(id)}」を {combo.Label} にしました（「{GameSettings.ActionLabel(swapped)}」と入れ替えました）。"
                    : $"「{GameSettings.ActionLabel(id)}」を {combo.Label} にしました。");
                ShowTab(3);
                return;
            }
        }
    }
}
