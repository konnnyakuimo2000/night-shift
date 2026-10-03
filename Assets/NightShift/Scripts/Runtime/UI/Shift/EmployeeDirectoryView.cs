using System.Collections.Generic;
using System.Linq;
using NightShift.Data;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>社員情報の検索・参照（DAY1の中心的な調査ツール）</summary>
    public class EmployeeDirectoryView : MonoBehaviour
    {
        public const string Id = "directory";
        ShiftDesktop _d;
        RetroWindow _w;
        InputField _id, _name, _device;
        RetroDropdown _dept;
        TableView _table;
        Text _count;
        List<EmployeeData> _results = new List<EmployeeData>();

        public static EmployeeDirectoryView Open(ShiftDesktop d)
        {
            if (!d.Require(Data.Features.EmployeeDirectory)) return null;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "社員情報の検索・参照", Icon = "people", Width = 740, Height = 470,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 1
            }, out bool created);
            if (!created) return w.GetComponent<EmployeeDirectoryView>();
            var v = w.gameObject.AddComponent<EmployeeDirectoryView>();
            v._d = d; v._w = w;
            v.Build();
            return v;
        }

        /// <summary>他画面から検索条件を指定して開く（社員番号・端末番号のみ。結果の判断は表示しない）</summary>
        public static void OpenWith(ShiftDesktop d, string employeeId, string device)
        {
            var v = Open(d);
            if (v == null) return;
            v.Clear(false);
            if (!string.IsNullOrEmpty(employeeId) && employeeId != "-") v._id.text = employeeId;
            else if (!string.IsNullOrEmpty(device) && d.Unlocked(Features.DeviceCheck)) v._device.text = device;
            v.Search();
        }

        void Build()
        {
            var c = _w.Content;
            var g = UIFactory.Group(c, "検索条件", 0, 0, 720, 96);
            UIFactory.Label(g, "社員番号(I):", 0, 0, 84, 20);
            _id = UIFactory.Input(g, 86, 0, 130);
            UIFactory.Label(g, "氏名(N):", 230, 0, 60, 20);
            _name = UIFactory.Input(g, 292, 0, 180);
            UIFactory.Label(g, "部署(D):", 0, 26, 84, 20);
            var depts = new List<string> { "(すべて)" };
            depts.AddRange(_d.S.Employees.Departments);
            _dept = RetroDropdown.Create(g, 86, 26, 130, depts);
            bool deviceCheck = _d.Unlocked(Features.DeviceCheck);
            var deviceLabel = UIFactory.Label(g, "端末番号(T):", 230, 26, 70, 20);
            _device = UIFactory.Input(g, 292, 26, 180);
            // 端末の確認は DAY2 から（それまでは端末で判断させない）
            deviceLabel.gameObject.SetActive(deviceCheck);
            _device.gameObject.SetActive(deviceCheck);

            UIFactory.Button(g, "検索開始(F)", 520, -2, 178, 24, Search, "search");
            UIFactory.Button(g, "条件クリア(R)", 520, 26, 178, 24, () => Clear(true), "doc");

            var rg = UIFactory.Group(c, "検索結果", 0, 102, 720, 280);
            _table = deviceCheck
                ? TableView.CreateStretch(rg, 0, 0, 0, 0,
                    new TableColumn("社員番号", 76), new TableColumn("氏名", 110), new TableColumn("所属", 150),
                    new TableColumn("役職", 130), new TableColumn("登録端末", 110), new TableColumn("勤務状態", 70))
                : TableView.CreateStretch(rg, 0, 0, 0, 0,
                    new TableColumn("社員番号", 86), new TableColumn("氏名", 140), new TableColumn("所属", 170),
                    new TableColumn("役職", 160), new TableColumn("勤務状態", 80));
            _table.DoubleClicked += o => { if (_d.Unlocked(Features.EmployeeDetail)) EmployeeDetailView.Open(_d, (EmployeeData)o, _results); };

            _count = UIFactory.Label(c, "", 0, 390, 160, 24);
            var detailBtn = UIFactory.Button(c, "詳細表示(D)", 400, 388, 150, 26, ShowDetail, "people");
            detailBtn.Interactable = _d.Unlocked(Features.EmployeeDetail);
            UIFactory.Button(c, "閉じる(C)", 570, 388, 150, 26, _w.RequestClose, "exit");

            _w.Accelerators[KeyCode.F] = Search;
            _w.Accelerators[KeyCode.Return] = Search;
            _w.Accelerators[KeyCode.KeypadEnter] = Search;
            _w.Accelerators[KeyCode.R] = () => Clear(true);
            if (_d.Unlocked(Features.EmployeeDetail)) _w.Accelerators[KeyCode.D] = ShowDetail;
            _w.Accelerators[KeyCode.C] = _w.RequestClose;
            _w.Accelerators[KeyCode.UpArrow] = () => _table.MoveSelection(-1);
            _w.Accelerators[KeyCode.DownArrow] = () => _table.MoveSelection(1);
            Search();
        }

        void Clear(bool search)
        {
            _id.text = ""; _name.text = ""; _device.text = "";
            _dept.Set(0);
            if (search) Search();
        }

        static bool Contains(string hay, string needle) =>
            string.IsNullOrEmpty(needle) || (hay ?? "").ToUpperInvariant().Contains(needle.Trim().ToUpperInvariant());

        static string Normalize(string s) => (s ?? "").Replace(" ", "").Replace("　", "");

        public void Search()
        {
            string dept = _dept.Index > 0 ? _dept.Value : null;
            _results = _d.S.Employees.All.Where(e =>
                Contains(e.employeeId, _id.text) &&
                (string.IsNullOrEmpty(_name.text) || Normalize(e.fullName).Contains(Normalize(_name.text)) || Normalize(e.kana).Contains(Normalize(_name.text))) &&
                (dept == null || e.department == dept) &&
                (string.IsNullOrEmpty(_device.text) || Contains(e.deviceId, _device.text) || (!string.IsNullOrEmpty(e.laptopDeviceId) && Contains(e.laptopDeviceId, _device.text)))
            ).ToList();
            if (_d.Unlocked(Features.DeviceCheck))
                _table.SetRows(_results, e => new[] { e.employeeId, e.fullName, e.department, e.position, e.deviceId, e.onShift ? "勤務中" : "退勤" });
            else
                _table.SetRows(_results, e => new[] { e.employeeId, e.fullName, e.department, e.position, e.onShift ? "勤務中" : "退勤" });
            _count.text = $"件数:  {_results.Count} 件";
            _w.SetStatus(0, _results.Count == 0 ? "該当する社員は見つかりませんでした。"
                : _d.Unlocked(Features.EmployeeDetail) ? "ダブルクリックまたは [詳細表示] で社員情報を表示します。" : "検索結果の一覧を表示しています。");
            if (_results.Count > 0) _table.Select(0);
        }

        void ShowDetail()
        {
            if (_table.Selected is EmployeeData e) EmployeeDetailView.Open(_d, e, _results);
            else AudioFx.Error();
        }
    }

    // ============================================================
    /// <summary>社員情報の参照（権限・管理者権限・普段使うシステムを表示）</summary>
    public class EmployeeDetailView : MonoBehaviour
    {
        ShiftDesktop _d;
        RetroWindow _w;
        List<EmployeeData> _list;
        int _index;
        RectTransform _body;

        public static void Open(ShiftDesktop d, EmployeeData e, List<EmployeeData> list)
        {
            if (!d.Require(Features.EmployeeDetail)) return;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = "employee", Title = "社員情報の参照", Icon = "people", Width = 640, Height = 560, Maximizable = false,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 2, StatusWidths = new float[] { 160 }
            }, out bool created);
            var v = created ? w.gameObject.AddComponent<EmployeeDetailView>() : w.GetComponent<EmployeeDetailView>();
            v._d = d; v._w = w;
            v._list = list != null && list.Contains(e) ? new List<EmployeeData>(list) : new List<EmployeeData>(d.S.Employees.All);
            v._index = Mathf.Max(0, v._list.IndexOf(e));
            if (created) v.BuildFrame();
            v.Show();
        }

        void BuildFrame()
        {
            var c = _w.Content;
            _body = UIFactory.Rect("Body", c).Stretch(0, 0, 0, 34);
            float bx = 0;
            var buttons = new[]
            {
                UIFactory.Button(c, "|◀", () => Nav(-999)),
                UIFactory.Button(c, "◀", () => Nav(-1)),
                UIFactory.Button(c, "▶", () => Nav(1)),
                UIFactory.Button(c, "▶|", () => Nav(999)),
            };
            foreach (var b in buttons)
            {
                b.Rect.anchorMin = b.Rect.anchorMax = new Vector2(0, 0);
                b.Rect.pivot = new Vector2(0, 0);
                b.Rect.sizeDelta = new Vector2(30, 24);
                b.Rect.anchoredPosition = new Vector2(bx, 2);
                bx += 32;
            }
            var close = UIFactory.Button(c, "閉じる(C)", _w.RequestClose, "exit");
            close.Rect.anchorMin = close.Rect.anchorMax = new Vector2(1, 0);
            close.Rect.pivot = new Vector2(1, 0);
            close.Rect.sizeDelta = new Vector2(110, 26);
            close.Rect.anchoredPosition = new Vector2(0, 2);
            _w.Accelerators[KeyCode.C] = _w.RequestClose;
            _w.Accelerators[KeyCode.LeftArrow] = () => Nav(-1);
            _w.Accelerators[KeyCode.RightArrow] = () => Nav(1);
            _w.Accelerators[KeyCode.PageUp] = () => Nav(-1);
            _w.Accelerators[KeyCode.PageDown] = () => Nav(1);
        }

        void Nav(int delta)
        {
            if (_list.Count == 0) return;
            if (delta == -999) _index = 0;
            else if (delta == 999) _index = _list.Count - 1;
            else _index = Mathf.Clamp(_index + delta, 0, _list.Count - 1);
            Show();
        }

        void Show()
        {
            foreach (Transform t in _body) Destroy(t.gameObject);
            var e = _list[_index];
            _w.SetTitle($"社員情報の参照 - {e.employeeId} {e.fullName}");
            _w.SetStatus(0, $"レコード: {_index + 1} / {_list.Count}");
            _w.SetStatus(1, "参照のみ（編集権限なし）");

            float y = 0;
            void Row(string label, string value, float x = 0, float lw = 92, float vw = 200)
            {
                UIFactory.Label(_body, label, x, y, lw, 20);
                UIFactory.ValueBox(_body, x + lw, y, vw, 20, value);
            }
            Row("社員番号:", e.employeeId, 0, 92, 110);
            Row("勤務状態:", e.onShift ? "勤務中" : "退勤", 250, 70, 110);
            y += 24;
            Row("氏名:", e.fullName, 0, 92, 200);
            Row("年齢:", e.age.ToString(), 310, 50, 60);
            y += 24;
            Row("氏名(カナ):", e.kana, 0, 92, 200); y += 24;
            Row("所属:", e.department, 0, 92, 200);
            Row("役職:", e.position, 310, 50, 140); y += 24;
            Row("登録端末:", e.deviceId, 0, 92, 140);
            Row("端末IP:", _d.S.Employees.GetIp(e), 250, 70, 130); y += 24;
            Row("VPN利用:", e.vpnEnabled ? "許可" : "不可", 0, 92, 140);
            Row("VPN端末:", string.IsNullOrEmpty(e.laptopDeviceId) ? "-" : e.laptopDeviceId, 250, 70, 130); y += 24;
            Row("勤務時間:", e.workHours, 0, 92, 200); y += 30;

            // 顔写真
            var frame = UIFactory.Image(_body, RetroTheme.Field, Color.white, "PhotoFrame");
            frame.rectTransform.Place(514, 0, 102, 126);
            var photo = UIFactory.Image(frame.transform, PortraitPainter.Get(e), Color.white, "Photo");
            photo.type = Image.Type.Simple;
            photo.rectTransform.Stretch(3, 3, 3, 3);
            UIFactory.Label(_body, "顔写真", 514, 128, 102, 16, 11, TextAnchor.MiddleCenter).color = RetroTheme.Shadow;

            if (!_d.Unlocked(Features.EmployeePermissions))
            {
                var lg = UIFactory.Group(_body, "【権限情報】", 0, y, 490, 70);
                UIFactory.Label(lg, "権限情報の閲覧は、現在の担当範囲では許可されていません。", 12, TextAnchor.MiddleLeft).rectTransform.Stretch();
                return;
            }

            var perms = new List<PermissionDefinition>(_d.Game.Database.permissions);
            perms.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
            float gy = y;
            float h1 = PermissionBox("【システム利用権限】", PermissionCategory.SystemUse, perms, e, 0, gy);
            // 管理者権限は DAY4 から（それまでは利用システムの権限だけを見せる）
            float h2 = _d.Unlocked(Features.AdminPermissions) ? PermissionBox("【管理者権限】", PermissionCategory.Administrator, perms, e, 246, gy) : 0;
            y = gy + Mathf.Max(h1, h2) + 6;

            var ng = UIFactory.Group(_body, "【普段使うシステム】（参考）", 0, y, 490, 48);
            var names = new List<string>();
            foreach (var t in e.normalAccessTargets) names.Add(ServiceLabel(t));
            UIFactory.Label(ng, names.Count == 0 ? "-" : string.Join("　/　", names), 12, TextAnchor.MiddleLeft)
                .rectTransform.Stretch();
        }

        static string ServiceLabel(string serviceId)
        {
            switch (serviceId)
            {
                case "WEB": return "Web閲覧";
                case "MAIL": return "メール";
                case "FILE": return "共有ファイル";
                case "DEV": return "開発環境";
                case "HR": return "人事システム";
                case "ACCOUNTING": return "経理システム";
                case "VPN": return "VPN接続";
                case "ADMIN": return "サーバー管理";
                default: return serviceId;
            }
        }

        float PermissionBox(string title, PermissionCategory cat, List<PermissionDefinition> perms, EmployeeData e, float x, float y)
        {
            var list = perms.FindAll(p => p.category == cat);
            float h = 28 + list.Count * 19;
            var g = UIFactory.Group(_body, title, x, y, 244, h);
            float ry = 0;
            foreach (var p in list)
            {
                UIFactory.Label(g, p.displayName, 4, ry, 150, 18);
                bool has = e.HasPermission(p.permissionId);
                UIFactory.ValueBox(g, 160, ry, 40, 18, has ? "  ○" : "  ×");
                ry += 19;
            }
            return h;
        }
    }
}
