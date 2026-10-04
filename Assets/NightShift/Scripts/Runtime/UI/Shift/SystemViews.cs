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

        public static List<MenuItem> Items(ShiftDesktop d)
        {
            MenuItem M(string label, Action a, string icon, string feature) =>
                new MenuItem(label, a, null, feature == null || d.Unlocked(feature)) { Icon = feature == null || d.Unlocked(feature) ? icon : "lock" };
            return new List<MenuItem>
            {
                M("1. ネットワーク接続監視", () => ConnectionMonitorView.Open(d, null), "network", Data.Features.ConnectionMonitor),
                M("2. 社員情報の検索・参照", () => EmployeeDirectoryView.Open(d), "people", Data.Features.EmployeeDirectory),
                M("3. システムログの検索・参照", () => LogViewerView.Open(d, null), "log", Data.Features.SystemLog),
                M("4. ITサポート受付管理", () => HelpDeskView.Open(d, null), "helpdesk", Data.Features.HelpDesk),
                M("5. セキュリティ情報", () => SecurityInfoView.Open(d), "shield", Data.Features.SecurityInfo),
                M("   ネットワークマップ", () => NetworkMonitorView.Open(d), "globe", Data.Features.NetworkMap),
                M("   サーバー運用管理", null, "server", Data.Features.ServerManagement),
                MenuItem.Sep(),
                new MenuItem("   メール（業務連絡・マニュアル）", () => MailboxView.Open(d)) { Icon = "mail" },
                new MenuItem("   パスワードの変更", () => PasswordChangeView.Open(d)) { Icon = "lock" },
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
                new MenuItem("デバッグ(D)", () => DebugPanelView.Open(d), "F12", Core.GameSettings.Current.debugMode),
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
            string[] features = { Data.Features.ConnectionMonitor, Data.Features.EmployeeDirectory, Data.Features.SystemLog, Data.Features.HelpDesk, Data.Features.SecurityInfo, null, null };
            KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7 };
            for (int i = 0; i < labels.Length; i++)
            {
                bool unlocked = features[i] == null || _d.Unlocked(features[i]);
                string label = unlocked ? labels[i] : labels[i] + "  <size=10>(未解放)</size>";
                var b = UIFactory.Button(g, label, 0, y, 270, 44, actions[i], unlocked ? icons[i] : "lock");
                b.Label.alignment = TextAnchor.MiddleLeft;
                b.Label.rectTransform.Stretch(44, 2, 4, 2);
                b.Icon.sprite = RetroTheme.BigIcon(unlocked ? icons[i] : "lock");
                b.Icon.rectTransform.sizeDelta = new Vector2(32, 32);
                b.Interactable = unlocked;
                if (unlocked) _w.Accelerators[keys[i]] = actions[i];
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

            UIFactory.Button(c, "メールを見る(B)", 304, 232, 136, 26, () => MailboxView.Open(_d), "mail");
            _w.Accelerators[KeyCode.B] = () => MailboxView.Open(_d);
            var lockBtn = UIFactory.Button(c, "サーバー運用管理", 446, 232, 136, 26, null, "lock");
            lockBtn.Interactable = _d.Unlocked(Data.Features.ServerManagement);

            var ng = UIFactory.Group(c, "お知らせ", 300, 266, 286, 134);
            _notice = UIFactory.Label(ng, "", 12, TextAnchor.UpperLeft);
            _notice.rectTransform.Stretch(0, 0, 0, 0);

            _w.SetStatus(0, "メールで業務連絡を確認してください。");
            _w.SetStatus(1, $"ユーザー: {_d.Game.OperatorName}");
        }

        void Update()
        {
            if (_d == null || _d.S == null) return;
            var s = _d.S;
            _w.SetStatus(2, s.Time.Now);
            if (Time.frameCount % 15 != 0) return;
            var sb = new StringBuilder();
            if (!_d.ShiftStarted) sb.AppendLine("業務開始前です。メールで業務連絡を確認し、\n[業務開始] で業務を開始してください。");
            else
            {
                sb.AppendLine($"保留中の接続要求: {s.Requests.Pending.Count} 件");
                if (_d.Unlocked(Data.Features.HelpDesk)) sb.AppendLine($"未対応の問い合わせ: {s.HelpDesk.OpenCount} 件");
                foreach (var task in s.Tasks) if (!task.Done) sb.AppendLine($"依頼: {task.Title}（未完了）");
                sb.AppendLine($"業務終了まで: {Mathf.Max(0, Mathf.CeilToInt(s.Day.endMinute - s.Time.CurrentMinute))} 分");
                if (s.HelpDesk.ActiveSession != null) sb.AppendLine($"リモート接続中: {s.HelpDesk.ActiveSession.PC.Hostname}");
                if (s.EndWarningIssued) sb.AppendLine("\n※ 業務終了が近づいています。");
            }
            _notice.text = sb.ToString();
        }
    }

    // ============================================================
    /// <summary>添付文書のビューア</summary>
    public static class DocumentView
    {
        /// <summary>マニュアル（業務ごとのページ）を開く</summary>
        public static void Open(ShiftDesktop d, Data.DocumentDefinition doc) => ManualViewer.Open(d, doc);

        static string PermissionLabel(ShiftDesktop d, string id)
        {
            foreach (var p in d.Game.Database.permissions) if (p != null && p.permissionId == id) return p.displayName;
            return id;
        }

        public static string Expand(ShiftDesktop d, string body)
        {
            body = (body ?? "").Replace("{OPERATOR}", d.S.OperatorName);
            if (body.Contains("{ACCESS_TABLE}"))
            {
                // 早見表: 接続先 → 利用システム → 必要な権限（1行1項目）
                var sb = new StringBuilder();
                foreach (var n in d.S.Network.Nodes)
                {
                    if (n == null || n.role != Data.NodeRole.Server) continue;
                    foreach (var a in n.accessTypes)
                    {
                        if (a.requiresVpnEnabled && !d.Unlocked(Data.Features.NetworkMap)) continue; // VPN は社外の話が出てから
                        string need = string.IsNullOrEmpty(a.requiredPermission) ? "不要" : PermissionLabel(d, a.requiredPermission);
                        if (a.requiresVpnEnabled) need += " ＋ VPN利用許可";
                        sb.AppendLine($"　<b>{n.nodeId}</b>　{a.accessType}　→　<color=#8A4B00><b>{need}</b></color>");
                    }
                }
                body = body.Replace("{ACCESS_TABLE}", sb.ToString().TrimEnd());
            }
            if (body.Contains("{SERVER_LIST}"))
            {
                var sb = new StringBuilder();
                foreach (var n in d.S.Network.Nodes)
                {
                    if (n == null || n.role != Data.NodeRole.Server) continue;
                    string zone = n.zone == Data.NodeZone.DMZ ? "公開(DMZ)" : "社内";
                    string tags = (n.isAdministrative ? " / <color=#B00000><b>管理系</b></color>" : "") + (n.isCritical ? " / <b>重要</b>" : "");
                    sb.AppendLine($"・<b>{n.nodeId}</b>  ({n.ipAddress})  {zone}{tags}");
                    foreach (var a in n.accessTypes)
                    {
                        string need = string.IsNullOrEmpty(a.requiredPermission) ? "不要" : PermissionLabel(d, a.requiredPermission);
                        if (a.requiresVpnEnabled) need += " ＋ VPN利用許可";
                        sb.AppendLine($"    {a.accessType} … 必要な権限: <color=#8A4B00><b>{need}</b></color>");
                    }
                }
                body = body.Replace("{SERVER_LIST}", sb.ToString().TrimEnd());
            }
            return body;
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
            if (!d.Require(Data.Features.SecurityInfo)) return;
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
            for (int i = 0; i < day.rules.Count; i++) sb.AppendLine($"<b>{i + 1}.</b> {day.rules[i]}");
            var rt = UIFactory.Label(rg, sb.ToString(), 12, TextAnchor.UpperLeft);
            rt.rectTransform.Stretch();

            var tools = UIFactory.Group(c, "ツール", 0, 156, 600, 62);
            UIFactory.Button(tools, "ネットワークマップ(N)", 0, 0, 170, 26, () => NetworkMonitorView.Open(d), "globe");
            UIFactory.Button(tools, "メール（業務連絡）(B)", 176, 0, 160, 26, () => MailboxView.Open(d), "mail");
            var locked = UIFactory.Button(tools, "サーバー運用管理", 342, 0, 160, 26, null, "lock");
            locked.Interactable = d.Unlocked(Data.Features.ServerManagement);
            w.Accelerators[KeyCode.N] = () => NetworkMonitorView.Open(d);
            w.Accelerators[KeyCode.B] = () => MailboxView.Open(d);

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
                Id = Id, Title = "システム設定", Icon = "gear", Width = 560, Height = 470, Maximizable = false,
                Menus = ShiftDesktop.StandardMenus(d), Position = new Vector2(360, 120)
            }, out bool created);
            if (!created) return;
            SettingsPanel.Build(w.Content, extra =>
            {
                UIFactory.Button(extra, "パスワードの変更(P)...", 0, 0, 200, 26, () => PasswordChangeView.Open(d), "lock");
            });
            w.Accelerators[KeyCode.P] = () => PasswordChangeView.Open(d);
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
