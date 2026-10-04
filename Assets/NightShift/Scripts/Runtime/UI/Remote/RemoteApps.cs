using System;
using System.Collections.Generic;
using System.Text;
using NightShift.RemotePC;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Remote
{
    /// <summary>リモートPC上のアプリ。すべて同じ VirtualPCState を参照する（GUI と CLI のどちらからでも調査・解決できる）。</summary>
    public static class RemoteApps
    {
        // ============================================================ Settings
        public static void Settings(RemoteDesktop d, int page)
        {
            var w = d.Open(new WindowSpec { Id = "settings", Title = "設定", Icon = "gear", Width = 520, Height = 380, Maximizable = false }, out bool created);
            var app = created ? w.gameObject.AddComponent<SettingsApp>() : w.GetComponent<SettingsApp>();
            if (created) app.Build(d, w);
            app.ShowPage(page);
        }

        public class SettingsApp : MonoBehaviour, IRemoteApp
        {
            RemoteDesktop _d;
            RectTransform _page;
            int _current;
            readonly List<RetroButton> _nav = new List<RetroButton>();

            public void Build(RemoteDesktop d, RetroWindow w)
            {
                _d = d;
                var c = w.Content;
                var left = UIFactory.Image(c, RetroTheme.Field, Color.white, "Nav");
                left.rectTransform.Place(0, 0, 150, 330);
                string[] labels = { "ネットワーク", "プリンター", "システム", "修正プログラム", "画面", "バックアップ", "イベントログ" };
                string[] icons = { "network", "printer", "pc", "check", "pc", "drive", "log" };
                for (int i = 0; i < labels.Length; i++)
                {
                    int idx = i;
                    var b = UIFactory.FlatButton(left.transform, labels[i], () => ShowPage(idx), icons[i]);
                    b.Label.alignment = TextAnchor.MiddleLeft;
                    b.Rect.Place(4, 4 + i * 28, 142, 26);
                    _nav.Add(b);
                }
                _page = UIFactory.Rect("Page", c).Place(160, 0, 340, 330);
                d.PC.Changed += Refresh;
                w.Closed += () => d.PC.Changed -= Refresh;
            }

            public void ShowPage(int page)
            {
                _current = page;
                for (int i = 0; i < _nav.Count; i++) _nav[i].Label.fontStyle = i == page ? FontStyle.Bold : FontStyle.Normal;
                Refresh();
            }

            public void Refresh()
            {
                if (_page == null) return;
                foreach (Transform t in _page) Destroy(t.gameObject);
                var pc = _d.PC;
                switch (_current)
                {
                    case 0:
                    {
                        UIFactory.Label(_page, "ネットワークとインターネット", 0, 0, 340, 22, 14).fontStyle = FontStyle.Bold;
                        var g = UIFactory.Group(_page, "Wi-Fi", 0, 28, 340, 130);
                        UIFactory.Label(g, "Wi-Fi:", 0, 0, 80, 20);
                        var toggle = UIFactory.Button(g, pc.WiFiEnabled ? "オン" : "オフ", 84, 0, 70, 20, () => pc.SetWiFi(!pc.WiFiEnabled));
                        toggle.SetLatched(pc.WiFiEnabled);
                        if (!pc.NetworkAdapterEnabled) toggle.Interactable = false;
                        UIFactory.Label(g, "状態:", 0, 26, 80, 20);
                        string status = !pc.NetworkAdapterEnabled ? "アダプターが無効です" : !pc.WiFiEnabled ? "切断" : $"接続済み ({pc.Ssid})";
                        UIFactory.ValueBox(g, 84, 26, 220, 20, status);
                        UIFactory.Label(g, "IP アドレス:", 0, 52, 80, 20);
                        UIFactory.ValueBox(g, 84, 52, 220, 20, string.IsNullOrEmpty(pc.IPAddress) ? "-" : pc.IPAddress);
                        UIFactory.Label(g, "インターネット:", 0, 78, 84, 20);
                        UIFactory.ValueBox(g, 84, 78, 220, 20, pc.InternetConnectivity ? "接続済み" : "接続なし");
                        var ipg = UIFactory.Group(_page, "IP アドレスの設定", 0, 166, 340, 80);
                        var dhcp = UIFactory.Check(ipg, "IP アドレスを自動的に取得する", pc.DHCPEnabled, v => pc.SetDhcp(v), true);
                        dhcp.GetComponent<RectTransform>().Place(0, 0, 320, 20);
                        if (!pc.DHCPEnabled) UIFactory.Label(ipg, $"現在の設定: {pc.StaticIp}（手動）", 20, 20, 300, 16, 11).color = RetroTheme.Shadow;
                        var dns = UIFactory.Check(ipg, "DNS サーバーのアドレスを自動的に取得する", pc.DnsAuto, v => pc.SetDnsAuto(v), true);
                        dns.GetComponent<RectTransform>().Place(0, 38, 320, 20);
                        UIFactory.Button(_page, "アダプターのオプションを変更する...", 0, 256, 240, 24, () => NetworkConnections(_d), "network");
                        break;
                    }
                    case 3:
                    {
                        UIFactory.Label(_page, "修正プログラムの更新", 0, 0, 340, 22, 14).fontStyle = FontStyle.Bold;
                        var g = UIFactory.Group(_page, "状態", 0, 28, 340, 110);
                        UIFactory.Label(g, pc.PendingPatches > 0 ? $"適用されていない修正プログラムが {pc.PendingPatches} 件あります。" : "コンピューターは最新の状態です。", 0, 0, 330, 20);
                        var apply = UIFactory.Button(g, "すべて適用", 0, 30, 120, 26, pc.ApplyPatches, "check");
                        apply.Interactable = pc.PendingPatches > 0;
                        UIFactory.Button(g, "後で", 126, 30, 80, 26, () => { });
                        break;
                    }
                    case 4:
                    {
                        UIFactory.Label(_page, "画面のプロパティ", 0, 0, 340, 22, 14).fontStyle = FontStyle.Bold;
                        var g = UIFactory.Group(_page, "スクリーン セーバー", 0, 28, 340, 90);
                        UIFactory.Label(g, "スクリーン セーバー: 3D 迷路　　待ち時間: 10 分", 0, 0, 330, 20);
                        var lck = UIFactory.Check(g, "パスワードで保護する", pc.ScreenLockEnabled, v => pc.SetScreenLock(v));
                        lck.GetComponent<RectTransform>().Place(0, 28, 300, 20);
                        break;
                    }
                    case 5:
                    {
                        UIFactory.Label(_page, "バックアップ", 0, 0, 340, 22, 14).fontStyle = FontStyle.Bold;
                        var g = UIFactory.Group(_page, "マイ ドキュメント", 0, 28, 340, 100);
                        UIFactory.Label(g, pc.BackupDone ? "バックアップ済み（MO は取り外されています）" : "最後のバックアップ: なし", 0, 0, 330, 20);
                        var bk = UIFactory.Button(g, "MO にバックアップして取り外す", 0, 30, 220, 26, pc.Backup, "drive");
                        bk.Interactable = !pc.BackupDone;
                        break;
                    }
                    case 6:
                    {
                        UIFactory.Label(_page, "イベント ログ", 0, 0, 340, 22, 14).fontStyle = FontStyle.Bold;
                        var g = UIFactory.Group(_page, "システム / アプリケーション / セキュリティ", 0, 28, 340, 110);
                        UIFactory.Label(g, pc.LogsCleared ? "ログはありません。" : $"記録: 1,284 件{(pc.LogsSaved ? "（フロッピーに保存済み）" : "")}", 0, 0, 330, 20);
                        var save = UIFactory.Button(g, "ログをフロッピーに保存", 0, 30, 170, 26, pc.SaveLogs, "drive");
                        save.Interactable = !pc.LogsCleared && !pc.LogsSaved;
                        var clr = UIFactory.Button(g, "ログを消去", 176, 30, 110, 26, pc.ClearLogs, "exit");
                        clr.Interactable = !pc.LogsCleared;
                        break;
                    }
                    case 1:
                    {
                        UIFactory.Label(_page, "プリンター", 0, 0, 340, 22, 14).fontStyle = FontStyle.Bold;
                        var g = UIFactory.Group(_page, "インストールされているプリンター", 0, 28, 340, 120);
                        UIFactory.IconImage(g, "printer", 0, 2, 32);
                        UIFactory.Label(g, $"{pc.PrinterName} (既定)", 40, 0, 280, 18).fontStyle = FontStyle.Bold;
                        string st = !pc.PrinterOnline ? "オフライン" : pc.PrintQueuePaused ? "一時停止" : "準備完了";
                        UIFactory.Label(g, $"状態: {st}", 40, 18, 280, 18);
                        UIFactory.Label(g, $"ドキュメント: {pc.PrintQueue.Count}", 40, 36, 280, 18);
                        UIFactory.Button(g, "キューを開く(Q)", 40, 62, 140, 24, () => PrintQueue(_d), "doc");
                        break;
                    }
                    default:
                    {
                        UIFactory.Label(_page, "システム", 0, 0, 340, 22, 14).fontStyle = FontStyle.Bold;
                        float y = 30;
                        void Row(string k, string v) { UIFactory.Label(_page, k, 0, y, 110, 20); UIFactory.ValueBox(_page, 112, y, 220, 20, v); y += 26; }
                        Row("コンピューター名:", pc.Hostname);
                        Row("ユーザー:", pc.UserName);
                        Row("OS:", pc.OsName);
                        Row("ドメイン:", pc.Domain);
                        Row("ディスク使用率:", pc.DiskUsage + " %");
                        break;
                    }
                }
            }
        }

        // ============================================================ Network Connections (ncpa.cpl)
        public static void NetworkConnections(RemoteDesktop d)
        {
            var w = d.Open(new WindowSpec { Id = "ncpa", Title = "ネットワーク接続", Icon = "network", Width = 460, Height = 260, Maximizable = false }, out bool created);
            if (!created) return;
            var app = w.gameObject.AddComponent<NetworkConnectionsApp>();
            app.Build(d, w);
        }

        public class NetworkConnectionsApp : MonoBehaviour, IRemoteApp
        {
            RemoteDesktop _d;
            TableView _table;
            RetroButton _enable, _disable;
            class Adapter { public string Name, Device, Status; public bool Wifi; }
            readonly List<Adapter> _rows = new List<Adapter>();

            public void Build(RemoteDesktop d, RetroWindow w)
            {
                _d = d;
                var c = w.Content;
                _table = TableView.CreateStretch(c, 0, 0, 0, 34, new TableColumn("名前", 150), new TableColumn("状態", 110), new TableColumn("デバイス名", 160));
                _table.SelectionChanged += _ => UpdateButtons();
                _enable = UIFactory.Button(c, "有効にする(E)", Enable);
                _disable = UIFactory.Button(c, "無効にする(D)", Disable);
                foreach (var (b, x) in new[] { (_enable, 0f), (_disable, 120f) })
                {
                    b.Rect.anchorMin = b.Rect.anchorMax = new Vector2(0, 0);
                    b.Rect.pivot = new Vector2(0, 0);
                    b.Rect.sizeDelta = new Vector2(114, 24);
                    b.Rect.anchoredPosition = new Vector2(x, 2);
                }
                w.Accelerators[KeyCode.E] = Enable;
                w.Accelerators[KeyCode.D] = Disable;
                d.PC.Changed += Refresh;
                w.Closed += () => d.PC.Changed -= Refresh;
                Refresh();
            }

            public void Refresh()
            {
                var pc = _d.PC;
                var sel = _table.SelectedIndex;
                _rows.Clear();
                _rows.Add(new Adapter { Name = "ローカル エリア接続", Device = pc.EthernetAdapterName, Status = "ケーブルが接続されていません" });
                _rows.Add(new Adapter
                {
                    Name = "ワイヤレス ネットワーク接続", Device = pc.WifiAdapterName, Wifi = true,
                    Status = !pc.NetworkAdapterEnabled ? "無効" : !pc.WiFiEnabled ? "切断 (Wi-Fi オフ)" : $"接続 ({pc.Ssid})"
                });
                _table.SetRows(_rows, a => new[] { a.Name, a.Status, a.Device });
                if (sel >= 0) _table.Select(sel);
                UpdateButtons();
            }

            void UpdateButtons()
            {
                var a = _table.Selected as Adapter;
                _enable.Interactable = a != null && a.Wifi && !_d.PC.NetworkAdapterEnabled;
                _disable.Interactable = a != null && a.Wifi && _d.PC.NetworkAdapterEnabled;
            }

            void Enable() { if (_table.Selected is Adapter a && a.Wifi) _d.PC.SetAdapter(true); }
            void Disable() { if (_table.Selected is Adapter a && a.Wifi) _d.PC.SetAdapter(false); }
        }

        // ============================================================ Print Queue
        public static void PrintQueue(RemoteDesktop d)
        {
            var w = d.Open(new WindowSpec { Id = "printq", Title = d.PC.PrinterName, Icon = "printer", Width = 520, Height = 260 }, out bool created);
            if (!created) return;
            var app = w.gameObject.AddComponent<PrintQueueApp>();
            app.Build(d, w);
        }

        public class PrintQueueApp : MonoBehaviour, IRemoteApp
        {
            RemoteDesktop _d;
            RetroWindow _w;
            TableView _table;

            public void Build(RemoteDesktop d, RetroWindow w)
            {
                _d = d; _w = w;
                // メニューは開くたびに状態を反映させたいので、ボタンで自前のポップアップを出す
                var c = w.Content;
                var printerMenu = UIFactory.FlatButton(c, "プリンター(P)", null);
                printerMenu.Rect.Place(0, 0, 90, 20);
                printerMenu.Button.onClick.AddListener(() => ShowPrinterMenu(printerMenu));
                var docMenu = UIFactory.FlatButton(c, "ドキュメント(D)", null);
                docMenu.Rect.Place(92, 0, 100, 20);
                docMenu.Button.onClick.AddListener(() => ShowDocMenu(docMenu));
                _table = TableView.CreateStretch(c, 0, 22, 0, 0,
                    new TableColumn("ドキュメント名", 170), new TableColumn("状態", 80), new TableColumn("所有者", 80), new TableColumn("ページ", 50), new TableColumn("受付時刻", 70));
                w.Accelerators[KeyCode.P] = () => ShowPrinterMenu(printerMenu);
                w.Accelerators[KeyCode.D] = () => ShowDocMenu(docMenu);
                w.Accelerators[KeyCode.Delete] = CancelSelected;
                d.PC.Changed += Refresh;
                w.Closed += () => d.PC.Changed -= Refresh;
                Refresh();
            }

            void ShowPrinterMenu(RetroButton anchor)
            {
                var pc = _d.PC;
                var items = new List<MenuItem>
                {
                    new MenuItem("印刷の一時停止(A)", () => pc.SetQueuePaused(!pc.PrintQueuePaused)) { Checked = pc.PrintQueuePaused },
                    new MenuItem("すべてのドキュメントの取り消し(L)", () => pc.CancelAllJobs()),
                    MenuItem.Sep(),
                    new MenuItem("プリンターをオフラインで使用する(U)", () => pc.SetPrinterOnline(!pc.PrinterOnline)) { Checked = !pc.PrinterOnline },
                    MenuItem.Sep(),
                    new MenuItem("閉じる(C)", _w.Close),
                };
                ShowAt(anchor, items);
            }

            void ShowDocMenu(RetroButton anchor)
            {
                var items = new List<MenuItem>
                {
                    new MenuItem("キャンセル(C)", CancelSelected, "Delete", _table.Selected != null),
                };
                ShowAt(anchor, items);
            }

            static void ShowAt(RetroButton anchor, List<MenuItem> items)
            {
                var corners = new Vector3[4];
                anchor.Rect.GetWorldCorners(corners);
                PopupMenu.Show(items, RectTransformUtility.WorldToScreenPoint(null, corners[0]), null, 220);
            }

            void CancelSelected()
            {
                if (_table.Selected is PrintJob j) _d.PC.CancelJob(j);
            }

            public void Refresh()
            {
                var pc = _d.PC;
                string st = !pc.PrinterOnline ? " - オフラインで使用" : pc.PrintQueuePaused ? " - 一時停止" : "";
                _w.SetTitle(pc.PrinterName + st);
                _table.SetRows(pc.PrintQueue, j => new[]
                {
                    j.Document, j.Printing && pc.CanPrint ? "印刷中" : pc.PrintQueuePaused ? "一時停止" : !pc.PrinterOnline ? "オフライン" : "待機中",
                    j.Owner, j.Pages.ToString(), j.Submitted
                });
            }

            void Update()
            {
                if (Time.frameCount % 30 == 0) Refresh();
            }
        }

        // ============================================================ Terminal
        public static void Terminal(RemoteDesktop d)
        {
            var w = d.Open(new WindowSpec { Id = "cmd", Title = "コマンド プロンプト", Icon = "terminal", Width = 560, Height = 340 }, out bool created, "cmd.exe");
            if (!created) return;
            var app = w.gameObject.AddComponent<TerminalApp>();
            app.Build(d, w);
        }

        public class TerminalApp : MonoBehaviour, IRemoteApp
        {
            RemoteDesktop _d;
            RetroWindow _w;
            Text _out;
            RectTransform _content;
            ScrollRect _scroll;
            InputField _input;
            readonly StringBuilder _buffer = new StringBuilder();
            readonly List<string> _history = new List<string>();
            int _historyIndex;

            string Prompt => $"C:\\Documents and Settings\\{_d.PC.UserName.Replace("nexus\\", "")}>";

            public void Build(RemoteDesktop d, RetroWindow w)
            {
                _d = d; _w = w;
                var c = w.Content;
                var bg = UIFactory.Solid(c, RetroTheme.Terminal, "Console");
                bg.rectTransform.Stretch();
                _scroll = UIFactory.Scroll(bg.transform, out _content, false);
                ((RectTransform)_scroll.transform).Stretch(0, 24, 0, 22);
                // よく使う確認（コマンドを打たずにボタンで実行）
                var tools = UIFactory.Solid(bg.transform, RetroTheme.Face, "QuickTools");
                tools.rectTransform.StretchH(0, 24);
                UIFactory.Label(tools.transform, " よく使う確認:", 0, 2, 90, 20, 11);
                UIFactory.Button(tools.transform, "ipconfig", 92, 2, 80, 20, () => Exec("ipconfig"));
                UIFactory.Button(tools.transform, "ping intranet", 176, 2, 100, 20, () => Exec("ping intranet"));
                UIFactory.Button(tools.transform, "ping file-srv01", 280, 2, 110, 20, () => Exec("ping file-srv01"));
                _scroll.GetComponent<Image>().color = RetroTheme.Terminal;
                _out = UIFactory.Label(_content, "", 12, TextAnchor.UpperLeft, RetroTheme.TerminalText, true);
                _out.supportRichText = false;
                _out.rectTransform.Stretch(4, 2, 4, 0);
                _out.verticalOverflow = VerticalWrapMode.Overflow;

                _input = UIFactory.Input(bg.transform, "", false, true);
                var irt = (RectTransform)_input.transform;
                irt.anchorMin = new Vector2(0, 0); irt.anchorMax = new Vector2(1, 0); irt.pivot = new Vector2(0, 0);
                irt.offsetMin = new Vector2(0, 0); irt.offsetMax = new Vector2(0, 22);
                _input.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f);
                _input.textComponent.color = RetroTheme.TerminalText;
                _input.caretColor = RetroTheme.TerminalText;
                _input.onEndEdit.AddListener(OnSubmit);

                _buffer.AppendLine($"{d.PC.OsName} [Version 5.0.2195]");
                _buffer.AppendLine("(C) NEXUS Systems 1985-1998.");
                _buffer.AppendLine();
                _buffer.Append(Prompt);
                Render();
                w.Focused += () => _input.ActivateInputField();
                Screens.Delay.Run(w.gameObject, 0.05f, () => { if (_input) _input.ActivateInputField(); });
            }

            void OnSubmit(string line)
            {
                if (!(Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))) return;
                Exec(line);
            }

            /// <summary>1行を実行（入力欄・ボタン共通）</summary>
            void Exec(string line)
            {
                _buffer.AppendLine(line);
                if (!string.IsNullOrWhiteSpace(line)) { _history.Add(line); _historyIndex = _history.Count; }
                bool exit = false;
                string result = TerminalCommands.Execute(line, _d.PC, () => _buffer.Clear(), () => exit = true, _d.Rng);
                if (exit) { _w.Close(); return; }
                if (!string.IsNullOrEmpty(result)) _buffer.AppendLine(result.TrimEnd('\n'));
                if (_buffer.Length > 0) _buffer.AppendLine();
                _buffer.Append(Prompt);
                // 古い出力を切り詰める
                if (_buffer.Length > 8000) _buffer.Remove(0, _buffer.Length - 8000);
                _input.text = "";
                Render();
                _input.ActivateInputField();
            }

            void Render()
            {
                _out.text = _buffer.ToString();
                _content.sizeDelta = new Vector2(0, _out.preferredHeight + 10);
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = 0;
            }

            void Update()
            {
                if (_input == null || !_input.isFocused || _history.Count == 0) return;
                if (Input.GetKeyDown(KeyCode.UpArrow)) { _historyIndex = Mathf.Max(0, _historyIndex - 1); SetInput(_history[_historyIndex]); }
                if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    _historyIndex = Mathf.Min(_history.Count, _historyIndex + 1);
                    SetInput(_historyIndex < _history.Count ? _history[_historyIndex] : "");
                }
            }

            void SetInput(string s)
            {
                _input.text = s;
                _input.caretPosition = s.Length;
            }

            public void Refresh() { }
        }

        // ============================================================ Run
        public static void Run(RemoteDesktop d)
        {
            var w = d.Open(new WindowSpec
            {
                Id = "run", Title = "ファイル名を指定して実行", Icon = "run", Width = 380, Height = 170,
                Minimizable = false, Maximizable = false, Position = new Vector2(8, 330)
            }, out bool created);
            if (!created) return;
            var c = w.Content;
            UIFactory.IconImage(c, "run", 6, 6, 32);
            UIFactory.Label(c, "実行するプログラム名、または開くフォルダーやドキュメント名を入力してください。", 46, 2, 310, 36, 12, TextAnchor.UpperLeft);
            UIFactory.Label(c, "名前(O):", 6, 48, 60, 20);
            var input = UIFactory.Input(c, 66, 48, 290);
            void Go()
            {
                string cmd = input.text.Trim();
                if (cmd.Length == 0) return;
                if (Launch(d, cmd)) w.Close();
                else d.Message(cmd, $"'{cmd}' が見つかりません。名前を正しく入力したかどうかを確認してから、やり直してください。", "warning");
            }
            input.onEndEdit.AddListener(_ => { if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)) Go(); });
            UIFactory.Button(c, "OK", 120, 82, 74, 22, Go);
            UIFactory.Button(c, "キャンセル", 200, 82, 74, 22, w.Close);
            var browse = UIFactory.Button(c, "参照(B)...", 280, 82, 76, 22, null);
            browse.Interactable = false;
            w.Focused += () => input.ActivateInputField();
            Screens.Delay.Run(w.gameObject, 0.05f, () => { if (input) input.ActivateInputField(); });
        }

        /// <summary>Run ダイアログから起動できる名前。拡張時はここに追加する。</summary>
        public static bool Launch(RemoteDesktop d, string cmd)
        {
            string c = cmd.Trim().ToLowerInvariant();
            if (c.EndsWith(".exe")) c = c.Substring(0, c.Length - 4);
            switch (c)
            {
                case "cmd": case "command": Terminal(d); return true;
                case "taskmgr": TaskManager(d); return true;
                case "explorer": Explorer(d, "マイ コンピュータ"); return true;
                case "ncpa.cpl": case "control netconnections": NetworkConnections(d); return true;
                case "control printers": PrintQueue(d); return true;
                case "control": case "ms-settings:": Settings(d, 0); return true;
                case "ms-settings:network": case "ms-settings:network-wifi": Settings(d, 0); return true;
                case "ms-settings:printers": Settings(d, 1); return true;
                case "msedge": case "iexplore": case "browser": case "nxbrowser": Browser(d); return true;
            }
            if (c.StartsWith("http://") || c.StartsWith("https://")) { Browser(d, cmd.Trim()); return true; }
            if (c.StartsWith("\\\\")) { Explorer(d, "ネットワーク"); return true; }
            return false;
        }

        // ============================================================ 偽の警告画面（R4・サポート詐欺）
        /// <summary>
        /// ブラウザーが出している偽の警告。閉じるボタンは効かない。タスク マネージャーでブラウザー（netvoyager.exe）を終了すると消える。
        /// </summary>
        public static void FakeAlert(RemoteDesktop d)
        {
            var w = d.Open(new WindowSpec { Id = "fakealert", Title = "!! セキュリティ警告 !!", Icon = "warning", Width = 460, Height = 300, Closable = false, Minimizable = false, Maximizable = false }, out bool created);
            if (!created) return;
            var c = w.Content;
            var bg = UIFactory.Solid(c, new Color(0.75f, 0.05f, 0.05f), "Alert");
            bg.rectTransform.Stretch();
            UIFactory.Label(bg.transform, "警告: このコンピューターはウイルスに感染しています！", 10, 10, 430, 24, 15, TextAnchor.MiddleCenter, Color.white).fontStyle = FontStyle.Bold;
            UIFactory.Label(bg.transform, "VACCINE 99 セキュリティ センター", 10, 40, 430, 20, 12, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Label(bg.transform, "個人情報が盗まれています。今すぐ下のツールをインストールするか、\nサポート窓口（0120-XXX-XXX）に電話してください。", 10, 70, 430, 50, 12, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Button(bg.transform, "駆除ツールをインストール", 130, 140, 200, 30, () => { d.PC.InstallUnknownTool(); AudioFx.Error(); });
            UIFactory.Label(bg.transform, "※ このウィンドウを閉じないでください", 10, 190, 430, 18, 11, TextAnchor.MiddleCenter, new Color(1, 0.85f, 0.85f));
            System.Action watch = null;
            watch = () => { if (!d.PC.FakeAlertOpen && w != null) { d.PC.Changed -= watch; w.Close(); } };
            d.PC.Changed += watch;
            w.Closed += () => d.PC.Changed -= watch;
        }

        // ============================================================ Task Manager
        public static void TaskManager(RemoteDesktop d)
        {
            var w = d.Open(new WindowSpec { Id = "taskmgr", Title = "タスク マネージャー", Icon = "taskmgr", Width = 440, Height = 400, Maximizable = false }, out bool created);
            if (!created) return;
            w.gameObject.AddComponent<TaskManagerApp>().Build(d, w);
        }

        public class TaskManagerApp : MonoBehaviour, IRemoteApp
        {
            RemoteDesktop _d;
            RetroWindow _w;
            int _tab = 1;
            RectTransform _page;
            TableView _table;
            readonly List<RetroButton> _tabs = new List<RetroButton>();
            float _timer;

            public void Build(RemoteDesktop d, RetroWindow w)
            {
                _d = d; _w = w;
                var c = w.Content;
                string[] names = { "アプリケーション", "プロセス", "ネットワーク" };
                for (int i = 0; i < names.Length; i++)
                {
                    int idx = i;
                    var b = UIFactory.Button(c, names[i], 2 + i * 104, 0, 102, 22, () => SetTab(idx));
                    _tabs.Add(b);
                }
                _page = UIFactory.Rect("Page", c).Stretch(0, 24, 0, 0);
                w.Accelerators[KeyCode.Delete] = EndSelected;
                d.PC.Changed += Rebuild;
                d.Windows.Changed += Rebuild;
                w.Closed += () => { d.PC.Changed -= Rebuild; d.Windows.Changed -= Rebuild; };
                SetTab(1);
            }

            void SetTab(int i)
            {
                _tab = i;
                for (int t = 0; t < _tabs.Count; t++) _tabs[t].SetLatched(t == i);
                Rebuild();
            }

            void Rebuild()
            {
                if (_page == null) return;
                foreach (Transform t in _page) Destroy(t.gameObject);
                _table = null;
                var pc = _d.PC;
                if (_tab == 0)
                {
                    var apps = new List<RetroWindow>();
                    foreach (var w in _d.Windows.Windows) if (w != null && w != _w && w.ShowInTaskbar) apps.Add(w);
                    _table = TableView.CreateStretch(_page, 0, 0, 0, 32, new TableColumn("タスク", 260), new TableColumn("状態", 100));
                    _table.SetRows(apps, w => new[] { w.Title, "実行中" });
                    Bottom("タスクの終了(E)", EndSelected, 0);
                    Bottom("切り替え(S)", () => { if (_table.Selected is RetroWindow w) _d.Windows.Focus(w); }, 120);
                }
                else if (_tab == 1)
                {
                    _table = TableView.CreateStretch(_page, 0, 0, 0, 32,
                        new TableColumn("イメージ名", 130), new TableColumn("PID", 50), new TableColumn("ユーザー名", 90), new TableColumn("メモリ使用量", 100));
                    var list = new List<VirtualProcess>(pc.RunningProcesses);
                    _table.SetRows(list, p => new[] { p.Name, p.Pid.ToString(), p.User, $"{p.MemoryKb:N0} K" });
                    Bottom("プロセスの終了(E)", EndSelected, 0);
                }
                else
                {
                    var g = UIFactory.Group(_page, "ワイヤレス ネットワーク接続", 0, 0, 410, 120);
                    var graph = UIFactory.Solid(g, Color.black, "Graph");
                    graph.rectTransform.Place(0, 0, 250, 70);
                    if (pc.InternetConnectivity)
                        for (int i = 0; i < 25; i++)
                        {
                            float h = 4 + Mathf.PerlinNoise(i * 0.4f, Time.time * 0.3f) * 30;
                            var bar = UIFactory.Solid(graph.transform, new Color(0.2f, 0.9f, 0.3f), "B");
                            bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(0, 0);
                            bar.rectTransform.pivot = new Vector2(0, 0);
                            bar.rectTransform.anchoredPosition = new Vector2(i * 10, 2);
                            bar.rectTransform.sizeDelta = new Vector2(8, h);
                        }
                    UIFactory.Label(g, $"リンク速度: {(pc.InternetConnectivity ? "54 Mbps" : "-")}\n状態: {(pc.InternetConnectivity ? "接続" : "切断")}\n使用率: {(pc.InternetConnectivity ? "0.4 %" : "0 %")}", 258, 0, 150, 70, 12, TextAnchor.UpperLeft);
                }
                _w.SetTitle($"タスク マネージャー");
            }

            void Bottom(string label, Action a, float x)
            {
                var b = UIFactory.Button(_page, label, a);
                b.Rect.anchorMin = b.Rect.anchorMax = new Vector2(0, 0);
                b.Rect.pivot = new Vector2(0, 0);
                b.Rect.sizeDelta = new Vector2(114, 24);
                b.Rect.anchoredPosition = new Vector2(x, 2);
            }

            void EndSelected()
            {
                if (_table == null || _table.Selected == null) return;
                if (_table.Selected is RetroWindow w) { w.Close(); return; }
                if (_table.Selected is VirtualProcess p)
                {
                    if (!_d.PC.KillProcess(p)) _d.Message("プロセスの終了", "このプロセスを終了できません。\nアクセスが拒否されました。", "warning");
                    else
                    {
                        // 対応するアプリWindowも閉じる
                        string id = p.Name == "cmd.exe" ? "cmd" : p.Name == "nxbrowser.exe" ? "browser" : null;
                        var win = id != null ? _d.Windows.Find(id) : null;
                        if (win != null) win.Close();
                    }
                }
            }

            public void Refresh() => Rebuild();

            void Update()
            {
                if (_tab != 2) return;
                _timer += Time.unscaledDeltaTime;
                if (_timer > 1f) { _timer = 0; Rebuild(); }
            }
        }

        // ============================================================ Explorer
        public static void Explorer(RemoteDesktop d, string folder)
        {
            var w = d.Open(new WindowSpec { Id = "explorer", Title = folder, Icon = "folder", Width = 520, Height = 340 }, out bool created);
            var app = created ? w.gameObject.AddComponent<ExplorerApp>() : w.GetComponent<ExplorerApp>();
            if (created) app.Build(d, w);
            app.Navigate(folder);
        }

        public class ExplorerApp : MonoBehaviour, IRemoteApp
        {
            RemoteDesktop _d;
            RetroWindow _w;
            TableView _table;
            Text _address;
            string _folder;
            class Item { public string Name, Size, Type; public string Target; }

            public void Build(RemoteDesktop d, RetroWindow w)
            {
                _d = d; _w = w;
                var c = w.Content;
                UIFactory.Label(c, "アドレス(D):", 0, 0, 70, 20);
                _address = UIFactory.ValueBox(c, 72, 0, 420, 20, "");
                var nav = UIFactory.Image(c, RetroTheme.Field, Color.white, "Tree");
                nav.rectTransform.Place(0, 24, 130, 270);
                string[] places = { "デスクトップ", "マイ ドキュメント", "マイ コンピュータ", "ネットワーク" };
                for (int i = 0; i < places.Length; i++)
                {
                    string p = places[i];
                    var b = UIFactory.FlatButton(nav.transform, p, () => Navigate(p), i == 3 ? "network" : i == 2 ? "pc" : "folder");
                    b.Label.alignment = TextAnchor.MiddleLeft;
                    b.Rect.Place(2, 2 + i * 24, 126, 22);
                }
                _table = TableView.Create(c, 134, 24, 360, 270, new TableColumn("名前", 190), new TableColumn("サイズ", 60), new TableColumn("種類", 90));
                _table.DoubleClicked += o => { if (o is Item it && it.Target != null) Navigate(it.Target); };
                w.Accelerators[KeyCode.Backspace] = () => Navigate("マイ コンピュータ");
            }

            public void Navigate(string folder)
            {
                _folder = folder;
                Refresh();
            }

            public void Refresh()
            {
                var pc = _d.PC;
                var items = new List<Item>();
                string address = _folder;
                switch (_folder)
                {
                    case "マイ コンピュータ":
                        items.Add(new Item { Name = "ローカル ディスク (C:)", Size = "", Type = "ローカル ディスク" });
                        items.Add(new Item { Name = "共有 (\\\\FILE-SRV01) (S:)", Type = "ネットワーク ドライブ", Target = "\\\\FILE-SRV01\\share" });
                        items.Add(new Item { Name = "マイ ドキュメント", Type = "フォルダー", Target = "マイ ドキュメント" });
                        break;
                    case "ネットワーク":
                        if (pc.InternetConnectivity)
                        {
                            items.Add(new Item { Name = "FILE-SRV01", Type = "コンピューター", Target = "\\\\FILE-SRV01\\share" });
                            items.Add(new Item { Name = "NX-LaserPrinter 3F", Type = "プリンター" });
                        }
                        break;
                    case "\\\\FILE-SRV01\\share":
                        if (!pc.InternetConnectivity)
                        {
                            _d.Message("\\\\FILE-SRV01\\share", "\\\\FILE-SRV01\\share にアクセスできません。\nネットワーク パスが見つかりません。", "warning");
                            _folder = "マイ コンピュータ";
                            Refresh();
                            return;
                        }
                        items.Add(new Item { Name = "営業部", Type = "フォルダー" });
                        items.Add(new Item { Name = "開発部", Type = "フォルダー" });
                        items.Add(new Item { Name = "全社共有", Type = "フォルダー" });
                        break;
                    default:
                        foreach (var f in pc.Files)
                            if (f.folder == _folder) items.Add(new Item { Name = f.name, Size = $"{f.sizeKb} KB", Type = TypeOf(f.name) });
                        break;
                }
                _address.text = address;
                _w.SetTitle(_folder);
                _table.SetRows(items, i => new[] { i.Name, i.Size ?? "", i.Type });
            }

            static string TypeOf(string name)
            {
                if (name.EndsWith(".doc")) return "文書";
                if (name.EndsWith(".xls")) return "ワークシート";
                if (name.EndsWith(".txt")) return "テキスト文書";
                if (name.EndsWith(".lnk")) return "ショートカット";
                return "ファイル";
            }
        }

        // ============================================================ Browser
        public static void Browser(RemoteDesktop d, string url = null)
        {
            var w = d.Open(new WindowSpec { Id = "browser", Title = "ブラウザー", Icon = "globe", Width = 560, Height = 380 }, out bool created, "nxbrowser.exe");
            var app = created ? w.gameObject.AddComponent<BrowserApp>() : w.GetComponent<BrowserApp>();
            if (created) app.Build(d, w);
            app.Go(url ?? "http://intranet.nexus.local/");
        }

        public class BrowserApp : MonoBehaviour, IRemoteApp
        {
            RemoteDesktop _d;
            RetroWindow _w;
            InputField _url;
            Text _page;
            string _current;

            public void Build(RemoteDesktop d, RetroWindow w)
            {
                _d = d; _w = w;
                var c = w.Content;
                UIFactory.Button(c, "更新", 0, 0, 50, 22, Refresh);
                UIFactory.Label(c, "アドレス:", 56, 0, 54, 22);
                _url = UIFactory.Input(c, 110, 1, 370, 20);
                UIFactory.Button(c, "移動", 486, 0, 50, 22, () => Go(_url.text));
                _url.onEndEdit.AddListener(s => { if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)) Go(s); });
                var bg = UIFactory.Image(c, RetroTheme.Field, Color.white, "Page");
                bg.rectTransform.Stretch(0, 26, 0, 0);
                _page = UIFactory.Label(bg.transform, "", 12, TextAnchor.UpperLeft);
                _page.rectTransform.Stretch(14, 12, 14, 8);
            }

            public void Go(string url)
            {
                url = (url ?? "").Trim();
                if (url.Length == 0) return;
                if (!url.Contains("://")) url = "http://" + url;
                _current = url;
                _url.text = url;
                Refresh();
            }

            public void Refresh()
            {
                if (_current == null) return;
                var pc = _d.PC;
                if (!pc.InternetConnectivity)
                {
                    _w.SetTitle("ページを表示できません - ブラウザー");
                    _page.text = "<size=16><b>ページを表示できません</b></size>\n\n" +
                                 "検索中のページは現在、利用できません。\n\n" +
                                 "・ネットワークへの接続を確認してください。\n" +
                                 "・[更新] ボタンをクリックするか、後でやり直してください。";
                    return;
                }
                if (_current.Contains("intranet"))
                {
                    _w.SetTitle("NEXUS イントラネット - ブラウザー");
                    _page.text = "<size=16><b>NEXUS CORPORATION イントラネット</b></size>\n\n" +
                                 "■ お知らせ\n・5/22(金) 定期メンテナンスのため、夜間に社内システムが停止します。\n・不審なメールの添付ファイルは開かず、情報システム部へ連絡してください。\n\n" +
                                 "■ リンク\n・社内電話帳　・勤怠システム　・経費精算　・共有フォルダー (\\\\FILE-SRV01)";
                }
                else
                {
                    _w.SetTitle(_current + " - ブラウザー");
                    _page.text = $"<size=14><b>{_current}</b></size>\n\n社外のWebサイトです。\n（ページは正常に表示されました）";
                }
            }
        }
    }
}
