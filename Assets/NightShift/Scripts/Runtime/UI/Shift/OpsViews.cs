using System.Collections.Generic;
using NightShift.Data;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>サーバー運用管理（DAY15〜）。サービスの状態・プロセス・バックアップからの復元。</summary>
    public class ServerManagementView : MonoBehaviour
    {
        public const string Id = "servers";
        ShiftDesktop _d;
        RetroWindow _w;
        TableView _servers, _services, _procs;
        Text _info;
        bool _dirty = true;

        public static void Open(ShiftDesktop d)
        {
            if (!d.Require(Features.ServerManagement)) return;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "サーバー運用管理", Icon = "server", Width = 820, Height = 520,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 1
            }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<ServerManagementView>();
            v._d = d; v._w = w;
            v.Build();
        }

        void Build()
        {
            var c = _w.Content;
            var sg = UIFactory.Group(c, "サーバー", 0, 0, 250, 430);
            _servers = TableView.CreateStretch(sg, 0, 0, 0, 0, new TableColumn("サーバー", 120), new TableColumn("状態", 90));
            _servers.SelectionChanged += _ => RefreshDetail();

            var vg = UIFactory.Group(c, "サービス", 258, 0, 542, 150);
            _services = TableView.CreateStretch(vg, 0, 0, 130, 0, new TableColumn("サービス", 220), new TableColumn("状態", 80));
            UIFactory.Button(vg, "再起動(R)", 410, 0, 122, 24, Restart, "run");

            var pg = UIFactory.Group(c, "プロセス", 258, 156, 542, 220);
            _procs = TableView.CreateStretch(pg, 0, 0, 130, 0, new TableColumn("プロセス", 150), new TableColumn("発行元", 120), new TableColumn("メモリ", 70));
            UIFactory.Button(pg, "プロセスを終了(K)", 410, 0, 122, 24, Kill, "exit");

            var bg = UIFactory.Group(c, "バックアップ", 258, 382, 542, 48);
            _info = UIFactory.Label(bg, "", 12, TextAnchor.MiddleLeft);
            _info.rectTransform.Stretch(0, 0, 160, 0);
            UIFactory.Button(bg, "バックアップから復元", 390, 2, 142, 24, Restore, "doc");

            _w.Accelerators[KeyCode.R] = Restart;
            _w.Accelerators[KeyCode.K] = Kill;
            _w.SetStatus(0, "サーバーを選ぶと、サービスとプロセスを表示します。止める前に、本当に必要か確かめること。");
            _d.S.Servers.Changed += MarkDirty;
            Refresh();
            if (_servers.Count > 0) _servers.Select(0);
        }

        void OnDestroy() { if (_d != null && _d.S != null) _d.S.Servers.Changed -= MarkDirty; }
        void MarkDirty() => _dirty = true;

        ServerState Selected => _servers.Selected as ServerState;

        void Refresh()
        {
            var sel = Selected;
            _servers.SetRows(new List<ServerState>(_d.S.Servers.Servers), s => new[] { s.Id, s.StatusLabel });
            if (sel != null) _servers.SelectData(sel);
            RefreshDetail();
            _dirty = false;
        }

        void RefreshDetail()
        {
            var st = Selected;
            if (st == null) { _services.SetRows(new List<ServiceState>(), x => new string[0]); _procs.SetRows(new List<ServerProcess>(), x => new string[0]); _info.text = ""; return; }
            _services.SetRows(st.Services, x => new[] { x.Name, x.Running ? "実行中" : "停止" });
            _procs.SetRows(st.Processes, p => new[] { p.Name, p.Publisher, $"{p.MemoryKb:N0} KB" });
            _info.text = st.Restoring
                ? $"復元中… 完了予定 {Core.TimeManager.Format(st.RestoreDoneAt)}"
                : st.Defaced ? "公開ページの内容が、登録された内容と一致しません。" : "最新のバックアップ: 前日 23:00";
        }

        void Kill()
        {
            var st = Selected;
            if (st == null || !(_procs.Selected is ServerProcess p)) { AudioFx.Error(); return; }
            if (st.Restoring) { _w.SetStatus(0, "復元中は操作できません。"); return; }
            _w.SetStatus(0, _d.S.Servers.KillProcess(st, p));
            Refresh();
        }

        void Restart()
        {
            var st = Selected;
            if (st == null || !(_services.Selected is ServiceState svc)) { AudioFx.Error(); return; }
            _w.SetStatus(0, _d.S.Servers.RestartService(st, svc));
            Refresh();
        }

        void Restore()
        {
            var st = Selected;
            if (st == null) { AudioFx.Error(); return; }
            MessageBox.Confirm(_d, "バックアップから復元", $"{st.Id} をバックアップから復元します。\n復元が終わるまで（約{ServerOps.RestoreMinutes:0}分）サービスは止まります。よろしいですか？", () =>
            {
                _w.SetStatus(0, _d.S.Servers.StartRestore(st));
                Refresh();
            });
        }

        void Update()
        {
            if (_dirty || Time.frameCount % 30 == 0) Refresh();
        }
    }

    // ============================================================
    /// <summary>ユーザー管理（DAY19〜）。アカウントの一時停止。</summary>
    public class UserManagementView : MonoBehaviour
    {
        public const string Id = "users";
        ShiftDesktop _d;
        RetroWindow _w;
        TableView _table;
        bool _dirty = true;

        public static void Open(ShiftDesktop d)
        {
            if (!d.Require(Features.UserManagement)) return;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "ユーザー管理", Icon = "people", Width = 700, Height = 460,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 1
            }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<UserManagementView>();
            v._d = d; v._w = w;
            v.Build();
        }

        void Build()
        {
            var c = _w.Content;
            var g = UIFactory.Group(c, "アカウント", 0, 0, 680, 340);
            _table = TableView.CreateStretch(g, 0, 0, 0, 0,
                new TableColumn("社員番号", 80), new TableColumn("氏名", 120), new TableColumn("所属", 130),
                new TableColumn("在籍状態", 70), new TableColumn("勤務状態", 70), new TableColumn("アカウント", 80), new TableColumn("メモ", 110));
            UIFactory.Button(c, "アカウントを一時停止(S)", 0, 348, 180, 28, Suspend, "lock");
            UIFactory.Button(c, "閉じる(C)", 560, 348, 120, 28, _w.RequestClose, "exit");
            _w.Accelerators[KeyCode.S] = Suspend;
            _w.Accelerators[KeyCode.C] = _w.RequestClose;
            _w.SetStatus(0, "アカウントを一時停止すると、そのIDからの接続はすべて拒否されます。");
            _d.S.Security.Changed += MarkDirty;
            Refresh();
        }

        void OnDestroy() { if (_d != null && _d.S != null) _d.S.Security.Changed -= MarkDirty; }
        void MarkDirty() => _dirty = true;

        static string Memo(EmployeeData e)
        {
            if (e.employeeId == "E-0004") return "体調不良により長期欠勤";
            if (e.employment == EmploymentStatus.Retired) return "退職（無効化 未処理）";
            if (e.employment == EmploymentStatus.Leave) return "休職中";
            return "";
        }

        void Refresh()
        {
            var sel = _table.Selected;
            var list = new List<EmployeeData>(_d.S.Employees.All);
            _table.SetRows(list, e => new[]
            {
                e.employeeId, e.DisplayName, e.department, EmployeeData.EmploymentLabel(e.employment),
                e.onShift ? "勤務中" : "退勤", _d.S.Security.IsSuspended(e.employeeId) ? "停止中" : "有効", Memo(e)
            });
            if (sel != null) _table.SelectData(sel);
            _dirty = false;
        }

        void Suspend()
        {
            if (!(_table.Selected is EmployeeData e)) { AudioFx.Error(); return; }
            _w.SetStatus(0, _d.S.Security.Suspend(e));
            Refresh();
        }

        void Update() { if (_dirty) Refresh(); }
    }
}
