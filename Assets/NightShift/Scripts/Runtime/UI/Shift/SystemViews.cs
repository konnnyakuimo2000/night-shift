using System;
using System.Collections.Generic;
using System.Text;
using NightShift.Controls;
using NightShift.Core;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>「社内情報システム」メインメニュー</summary>
    public class MainMenuView : MonoBehaviour
    {
        public const string Id = "mainmenu";
        ShiftDesktop _d;
        RetroWindow _w;
        Text _notice;
        Text _clock;

        public const string ServerFeature = "SERVER_MANAGEMENT";

        public static List<MenuItem> Items(ShiftDesktop d)
        {
            bool serverUnlocked = d.S.Day.IsUnlocked(ServerFeature);
            return new List<MenuItem>
            {
                new MenuItem("1. ネットワーク接続監視", () => ConnectionMonitorView.Open(d, null)) { Icon = "network" },
                new MenuItem("2. 社員情報の検索・参照", () => EmployeeDirectoryView.Open(d)) { Icon = "people" },
                new MenuItem("3. システムログの検索・参照", () => LogViewerView.Open(d, null)) { Icon = "log" },
                new MenuItem("4. ITサポート受付管理", () => HelpDeskView.Open(d, null)) { Icon = "helpdesk" },
                new MenuItem("5. セキュリティ情報", () => SecurityInfoView.Open(d)) { Icon = "shield" },
                new MenuItem("   ネットワークマップ", () => NetworkMonitorView.Open(d)) { Icon = "globe" },
                new MenuItem("   サーバー運用管理 (権限なし)", null, null, serverUnlocked) { Icon = "server" },
                MenuItem.Sep(),
                new MenuItem("6. システム設定", () => SettingsView.Open(d)) { Icon = "gear" },
                new MenuItem("7. 業務終了", () => TryEndShift(d)) { Icon = "exit" },
            };
        }

        static void TryEndShift(ShiftDesktop d)
        {
            if (d.S.Phase == ShiftPhase.Running && d.S.Time.CurrentMinute < d.S.Day.endMinute)
            {
                AudioFx.Error();
                MessageBox.Show(d, "業務終了", "業務時間中は終了できません。\n業務終了時刻は 04:00 です。", "warning");
                return;
            }
            if (d.S.Phase == ShiftPhase.Grace) d.S.Complete();
        }

        public static void Open(ShiftDesktop d)
        {
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = $"{d.Game.Database.systemName} - メニュー", Icon = "network", Width = 600, Height = 470,
                Closable = false, Maximizable = false, Position = new Vector2(16, 12),
                Menus = MenuBar(d), StatusCells = 3, StatusWidths = new float[] { 130, 60 }
            }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<MainMenuView>();
            v._d = d; v._w = w;
            v.Build();
        }

        static List<KeyValuePair<string, List<MenuItem>>> MenuBar(ShiftDesktop d)
        {
            var menus = ShiftDesktop.StandardMenus(d);
            menus.Insert(3, new KeyValuePair<string, List<MenuItem>>("ツール(T)", new List<MenuItem>
            {
                new MenuItem("システム設定(S)", () => SettingsView.Open(d)),
                new MenuItem("デバッグ(D)", () => DebugPanelView.Open(d), "F12"),
            }));
            return menus;
        }

        void Build()
        {
            var c = _w.Content;
            var g = UIFactory.Group(c, "メインメニュー", 0, 0, 290, 400);
            float y = 0;
            string[] labels = { "1.  ネットワーク接続監視", "2.  社員情報の検索・参照", "3.  システムログの検索・参照", "4.  ITサポート受付管理", "5.  セキュリティ情報", "6.  システム設定", "7.  業務終了" };
            string[] icons = { "network", "people", "log", "helpdesk", "shield", "gear", "exit" };
            Action[] actions =
            {
                () => ConnectionMonitorView.Open(_d, null),
                () => EmployeeDirectoryView.Open(_d),
                () => LogViewerView.Open(_d, null),
                () => HelpDeskView.Open(_d, null),
                () => SecurityInfoView.Open(_d),
                () => SettingsView.Open(_d),
                () => TryEndShift(_d)
            };
            KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7 };
            for (int i = 0; i < labels.Length; i++)
            {
                var b = UIFactory.Button(g, labels[i], 0, y, 270, 44, actions[i], icons[i]);
                b.Label.alignment = TextAnchor.MiddleLeft;
                b.Label.rectTransform.Stretch(44, 2, 4, 2);
                b.Icon.sprite = RetroTheme.BigIcon(icons[i]);
                b.Icon.rectTransform.sizeDelta = new Vector2(32, 32);
                _w.Accelerators[keys[i]] = actions[i];
                y += 50;
            }

            // 右側: ロゴ・イラスト・お知らせ
            var title = UIFactory.Label(c, _d.Game.Database.systemName, 300, 6, 285, 40, 26, TextAnchor.MiddleCenter);
            title.color = RetroTheme.Navy;
            title.fontStyle = FontStyle.Bold;
            UIFactory.Solid(c, RetroTheme.Navy, "Line1").rectTransform.Place(304, 4, 278, 2);
            UIFactory.Solid(c, RetroTheme.Navy, "Line2").rectTransform.Place(304, 48, 278, 2);
            UIFactory.Label(c, $"{_d.Game.Database.companyName}  {_d.Game.Database.systemVersion}", 300, 52, 285, 16, 11, TextAnchor.MiddleCenter).color = RetroTheme.Shadow;

            var art = UIFactory.Image(c, RetroTheme.Field, new Color(0.18f, 0.48f, 0.5f), "Art");
            art.rectTransform.Place(304, 74, 278, 150);
            var big = UIFactory.Image(art.transform, RetroTheme.BigIcon("pc"), Color.white, "PC");
            big.rectTransform.Place(40, 30, 96, 96);
            var srv = UIFactory.Image(art.transform, RetroTheme.BigIcon("server"), Color.white, "Server");
            srv.rectTransform.Place(150, 46, 64, 64);
            var net = UIFactory.Image(art.transform, RetroTheme.BigIcon("network"), Color.white, "Net");
            net.rectTransform.Place(214, 90, 48, 48);

            var lockBtn = UIFactory.Button(c, "サーバー運用管理", 304, 232, 278, 26, () =>
            {
                AudioFx.Error();
                MessageBox.Show(_d, "サーバー運用管理", "このメニューを利用する権限がありません。\n（担当範囲: DAY 2 より）", "lock");
            }, "server");
            lockBtn.Label.color = RetroTheme.TextDisabled;

            var ng = UIFactory.Group(c, "お知らせ", 300, 266, 286, 134);
            _notice = UIFactory.Label(ng, "", 12, TextAnchor.UpperLeft);
            _notice.rectTransform.Stretch(0, 0, 0, 0);

            _w.SetStatus(0, "業務連絡を確認してください。");
            _w.SetStatus(1, $"ユーザー: {_d.Game.OperatorName}");
        }

        void Update()
        {
            if (_d == null || _d.S == null) return;
            var s = _d.S;
            _w.SetStatus(2, s.Time.Now);
            if (Time.frameCount % 15 != 0) return;
            var sb = new StringBuilder();
            if (!_d.ShiftStarted) sb.AppendLine("業務開始前です。業務連絡を確認し、\n[START SHIFT] で業務を開始してください。");
            else
            {
                sb.AppendLine($"保留中の接続要求: {s.Requests.Pending.Count} 件");
                sb.AppendLine($"未対応の問い合わせ: {s.HelpDesk.OpenCount} 件");
                sb.AppendLine($"業務終了まで: {Mathf.Max(0, Mathf.CeilToInt(s.Day.endMinute - s.Time.CurrentMinute))} 分");
                if (s.HelpDesk.ActiveSession != null) sb.AppendLine($"リモート接続中: {s.HelpDesk.ActiveSession.PC.Hostname}");
                if (s.EndWarningIssued) sb.AppendLine("\n※ 業務終了が近づいています。");
            }
            _notice.text = sb.ToString();
        }
    }

    // ============================================================
    /// <summary>業務連絡（Morning Briefing）。長文チュートリアルではなく、簡潔なルール提示のみ。</summary>
    public class BriefingView : MonoBehaviour
    {
        public const string Id = "briefing";

        public static void Open(ShiftDesktop d, Action onStart)
        {
            bool preShift = !d.ShiftStarted && onStart != null;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = $"業務連絡 - DAY {d.S.Day.dayNumber}", Icon = "mail", Width = 600, Height = 540,
                Closable = !preShift, Minimizable = !preShift, Maximizable = false, Position = new Vector2(330, 60)
            }, out bool created);
            if (!created) return;
            var day = d.S.Day;
            var c = w.Content;

            void Row(string label, string value, float y)
            {
                UIFactory.Label(c, label, 0, y, 60, 20);
                UIFactory.ValueBox(c, 62, y, 520, 20, value, false);
            }
            Row("差出人:", day.briefingFrom, 0);
            Row("宛先:", "夜間IT運用担当 各位", 24);
            Row("日付:", day.dateLabel, 48);
            Row("件名:", $"【業務連絡】DAY {day.dayNumber} 夜間業務について", 72);

            var sr = UIFactory.Scroll(c, out var content, true);
            ((RectTransform)sr.transform).Stretch(0, 100, 0, preShift ? 44 : 34);
            var sb = new StringBuilder();
            sb.AppendLine(day.briefingBody);
            for (int i = 0; i < day.rules.Count; i++) sb.AppendLine($"  {i + 1}. {day.rules[i]}");
            sb.AppendLine();
            sb.AppendLine(day.briefingFooter);
            var t = UIFactory.Label(content, sb.ToString(), 12, TextAnchor.UpperLeft);
            t.rectTransform.Stretch(8, 6, 8, 0);
            t.verticalOverflow = VerticalWrapMode.Overflow;
            content.sizeDelta = new Vector2(0, t.preferredHeight + 20);
            Screens.Delay.Run(w.gameObject, 0.05f, () => { if (t) content.sizeDelta = new Vector2(0, t.preferredHeight + 20); });

            if (preShift)
            {
                var start = UIFactory.Button(c, "START SHIFT(S)", 0, 0, 180, 32, () =>
                {
                    w.Closable = true;
                    w.Close();
                    onStart();
                }, "clock");
                start.Rect.anchorMin = start.Rect.anchorMax = new Vector2(1, 0);
                start.Rect.pivot = new Vector2(1, 0);
                start.Rect.anchoredPosition = new Vector2(0, 2);
                start.Label.fontStyle = FontStyle.Bold;
                w.Accelerators[KeyCode.S] = start.Click;
                var note = UIFactory.Label(c, "※ 業務開始後は時間が進みます（Esc で一時停止）", 12);
                note.rectTransform.anchorMin = new Vector2(0, 0); note.rectTransform.anchorMax = new Vector2(1, 0);
                note.rectTransform.pivot = new Vector2(0, 0);
                note.rectTransform.offsetMin = new Vector2(0, 6); note.rectTransform.offsetMax = new Vector2(-190, 30);
                note.color = RetroTheme.Shadow;
            }
            else
            {
                var close = UIFactory.Button(c, "閉じる(C)", 0, 0, 100, 26, w.RequestClose);
                close.Rect.anchorMin = close.Rect.anchorMax = new Vector2(1, 0);
                close.Rect.pivot = new Vector2(1, 0);
                close.Rect.anchoredPosition = new Vector2(0, 2);
                w.Accelerators[KeyCode.C] = w.RequestClose;
            }
        }
    }

    // ============================================================
    public class SecurityInfoView : MonoBehaviour
    {
        public const string Id = "security";
        ShiftDesktop _d;
        TableView _alerts;
        int _lastCount = -1;

        public static void Open(ShiftDesktop d)
        {
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "セキュリティ情報", Icon = "shield", Width = 620, Height = 480,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 1
            }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<SecurityInfoView>();
            v._d = d;
            var c = w.Content;
            var day = d.S.Day;

            var rg = UIFactory.Group(c, "接続審査の基本ルール", 0, 0, 600, 150);
            var sb = new StringBuilder();
            for (int i = 0; i < day.rules.Count; i++) sb.AppendLine($"{i + 1}. {day.rules[i]}");
            var rt = UIFactory.Label(rg, sb.ToString(), 12, TextAnchor.UpperLeft);
            rt.rectTransform.Stretch();

            var tools = UIFactory.Group(c, "ツール", 0, 156, 600, 62);
            UIFactory.Button(tools, "ネットワークマップ(N)", 0, 0, 170, 26, () => NetworkMonitorView.Open(d), "globe");
            UIFactory.Button(tools, "業務連絡を表示(B)", 176, 0, 160, 26, () => BriefingView.Open(d, null), "mail");
            var locked = UIFactory.Button(tools, "サーバー運用管理", 342, 0, 160, 26, () =>
            {
                AudioFx.Error();
                MessageBox.Show(d, "サーバー運用管理", "このメニューを利用する権限がありません。\n（担当範囲: DAY 2 より）", "lock");
            }, "server");
            locked.Label.color = RetroTheme.TextDisabled;
            w.Accelerators[KeyCode.N] = () => NetworkMonitorView.Open(d);
            w.Accelerators[KeyCode.B] = () => BriefingView.Open(d, null);

            var ag = UIFactory.Group(c, "セキュリティ警告の履歴", 0, 224, 600, 180);
            v._alerts = TableView.CreateStretch(ag, 0, 0, 0, 0, new TableColumn("時刻", 60), new TableColumn("件名", 140), new TableColumn("内容", 360));
            w.SetStatus(0, "セキュリティ警告は自動判定ではありません。内容は各自で確認してください。");
        }

        void Update()
        {
            if (_d == null) return;
            var list = new List<Notification>();
            foreach (var n in _d.S.Notifications.History) if (n.Type == NotificationType.SecurityAlert) list.Add(n);
            if (list.Count == _lastCount) return;
            _lastCount = list.Count;
            _alerts.SetRows(list, n => new[] { TimeManager.Format(n.GameTime), n.Title, n.Body });
        }
    }

    // ============================================================
    public class SettingsView : MonoBehaviour
    {
        public const string Id = "settings";

        public static void Open(ShiftDesktop d)
        {
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "システム設定", Icon = "gear", Width = 400, Height = 300, Maximizable = false,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 1
            }, out bool created);
            if (!created) return;
            var c = w.Content;
            var g = UIFactory.Group(c, "サウンド", 0, 0, 380, 80);
            UIFactory.Label(g, "音量:", 0, 0, 50, 20);
            var vol = UIFactory.ValueBox(g, 52, 0, 60, 20, Mathf.RoundToInt(AudioFx.Volume * 100) + " %");
            UIFactory.Button(g, "－", 118, 0, 26, 20, () => { AudioFx.Volume = Mathf.Clamp01(AudioFx.Volume - 0.1f); vol.text = Mathf.RoundToInt(AudioFx.Volume * 100) + " %"; AudioFx.Notify(); });
            UIFactory.Button(g, "＋", 148, 0, 26, 20, () => { AudioFx.Volume = Mathf.Clamp01(AudioFx.Volume + 0.1f); vol.text = Mathf.RoundToInt(AudioFx.Volume * 100) + " %"; AudioFx.Notify(); });
            var click = UIFactory.Check(g, "クリック音", AudioFx.ClickEnabled, v => AudioFx.ClickEnabled = v);
            click.GetComponent<RectTransform>().Place(190, 0, 150, 20);

            var kg = UIFactory.Group(c, "キーボード", 0, 86, 380, 130);
            var cap = UIFactory.Check(kg, "OSショートカットの取り込み（未対応）", false, null);
            cap.GetComponent<RectTransform>().Place(0, 0, 360, 20);
            if (!KeyboardCapture.Supported) cap.Interactable = false;
            var info = UIFactory.Label(kg, KeyboardCapture.Supported
                ? "Win+R / Alt+Tab / Ctrl+Shift+Esc などをゲーム内で受け付けます。"
                : "エディタ実行中はOSのショートカットを取り込めません。代替キー:\n Alt+Home=スタート  Ctrl+Alt+R/E/I/D=Win+R/E/I/D\n Ctrl+Alt+End=タスクマネージャー  Ctrl+Tab=切替  Ctrl+F4=閉じる", 11, TextAnchor.UpperLeft);
            info.rectTransform.Place(0, 24, 360, 70);
            w.SetStatus(0, "設定は即時に反映されます。");
        }
    }

    // ============================================================
    public static class MessageBox
    {
        public static void Show(ShiftDesktop d, string title, string message, string icon = "info")
        {
            var w = d.OpenWindow(new WindowSpec
            {
                Id = "msg:" + title, Title = title, Icon = icon, Width = 340, Height = 150, Minimizable = false, Maximizable = false,
                Position = new Vector2(470, 280), ShowInTaskbar = false
            }, out bool created);
            if (!created) return;
            var c = w.Content;
            var ic = UIFactory.Image(c, RetroTheme.BigIcon(icon), Color.white, "Icon");
            ic.rectTransform.Place(8, 10, 32, 32);
            UIFactory.Label(c, message, 52, 6, 270, 60, 12, TextAnchor.MiddleLeft);
            var ok = UIFactory.Button(c, "OK", 120, 80, 90, 24, w.Close);
            w.Accelerators[KeyCode.Return] = w.Close;
            w.Accelerators[KeyCode.O] = w.Close;
        }
    }
}
