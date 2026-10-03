using System.Text;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>開発用デバッグパネル（F12）</summary>
    public class DebugPanelView : MonoBehaviour
    {
        public const string Id = "debug";
        ShiftDesktop _d;
        Text _info;
        int _remoteCursor, _binaryCursor;

        public static void Open(ShiftDesktop d)
        {
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "DEBUG", Icon = "debug", Width = 470, Height = 560, Maximizable = false, StatusCells = 1,
                Position = new Vector2(780, 40)
            }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<DebugPanelView>();
            v._d = d;
            v.Build(w);
        }

        void Build(RetroWindow w)
        {
            var c = w.Content;
            var s = _d.S;

            var tg = UIFactory.Group(c, "TimeScale", 0, 0, 450, 54);
            float[] scales = { 0.5f, 1, 2, 5, 10, 30 };
            for (int i = 0; i < scales.Length; i++)
            {
                float sc = scales[i];
                UIFactory.Button(tg, "x" + sc, i * 72, 0, 68, 22, () => s.Time.DebugTimeScale = sc);
            }

            var jg = UIFactory.Group(c, "時刻移動", 0, 58, 450, 54);
            var input = UIFactory.Input(jg, 0, 0, 80, 22, "03:45");
            UIFactory.Button(jg, "移動", 86, 0, 70, 22, () =>
            {
                var parts = input.text.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
                    s.Time.JumpTo(h * 60 + m);
            });
            UIFactory.Label(jg, "※ 経過したイベントはまとめて発生します", 164, 0, 270, 22, 11);

            var eg = UIFactory.Group(c, "イベント発生", 0, 116, 450, 112);
            UIFactory.Button(eg, "Normal Event x5", 0, 0, 140, 24, () => { for (int i = 0; i < 5; i++) s.Director.Normal.SpawnEmployeeTraffic(); });
            UIFactory.Button(eg, "Connection Request", 146, 0, 140, 24, () =>
            {
                var def = s.Director.FindDefinition(x => x.kind == EventKind.ConnectionRequest && x.destinationMode == DestinationMode.FromEmployeeNormalTargets);
                if (def != null) s.Director.Spawn(def);
            });
            UIFactory.Button(eg, "Security Alert", 292, 0, 140, 24, () =>
            {
                var def = s.Director.FindDefinition(x => x.notifyAsSecurityAlert);
                if (def != null) s.Director.Spawn(def);
            });
            UIFactory.Button(eg, "二択 Help Desk", 0, 30, 140, 24, () => SpawnHelpDesk(HelpDeskType.Binary));
            UIFactory.Button(eg, "Remote Help Desk", 146, 30, 140, 24, () => SpawnHelpDesk(HelpDeskType.Remote));
            UIFactory.Button(eg, "Threat Scenario", 292, 30, 140, 24, () =>
            {
                if (s.Day.threats.Count == 0) return;
                var def = s.Day.threats[s.Day.threats.Count - 1];
                s.Director.Threats.Schedule(s.Director.Scheduler, def, s.Time.CurrentMinute);
            });
            UIFactory.Button(eg, "Shift終了", 0, 60, 140, 24, () => { if (s.Phase == ShiftPhase.Running) s.EndShift(); else s.Complete(); });
            UIFactory.Button(eg, $"DAY{s.Day.dayNumber} Restart", 146, 60, 140, 24, () => _d.Game.RestartDay());
            UIFactory.Button(eg, "Overlay (F11)", 292, 60, 140, 24, () => _d.ToggleOverlay());

            var dg = UIFactory.Group(c, "DAY移動（最初から開始）", 0, 232, 450, 54);
            for (int i = 1; i <= 3; i++)
            {
                int day = i;
                var b = UIFactory.Button(dg, $"DAY{day}", (i - 1) * 100, 0, 94, 22, () => _d.Game.StartDay(day));
                b.Interactable = _d.Game.Database.FindDay(day) != null;
            }

            var ig = UIFactory.Group(c, "保留中の要求（開発用: 期待動作）", 0, 290, 450, 190);
            _info = UIFactory.Label(ig, "", 11, TextAnchor.UpperLeft);
            _info.rectTransform.Stretch();
            _info.font = RetroTheme.MonoFont;
            w.SetStatus(0, "開発用機能です。");
        }

        void SpawnHelpDesk(HelpDeskType type)
        {
            var list = _d.S.Day.helpDesk.FindAll(h => h != null && h.type == type);
            if (list.Count == 0) return;
            int idx = type == HelpDeskType.Remote ? _remoteCursor++ : _binaryCursor++;
            _d.S.Director.HelpDeskGen.Spawn(list[idx % list.Count]);
        }

        void Update()
        {
            if (_d == null || Time.frameCount % 15 != 0) return;
            var sb = new StringBuilder();
            foreach (var e in _d.S.Requests.Pending)
                sb.AppendLine($"{e.RequestId} {e.DisplayUser,-8} → {e.DestinationLabel,-11} {e.Expected,-5} {(e.IsMalicious ? "MAL" : "")} {e.ExpectedReason}");
            var next = _d.S.Director.Scheduler.NextPending();
            sb.AppendLine();
            sb.AppendLine(next != null ? $"次のイベント: {TimeManager.Format(next.Time)} {next.Label}" : "次のイベント: なし");
            _info.text = sb.ToString();
        }
    }

    /// <summary>デバッグ用オーバーレイ（F11）</summary>
    public class DebugOverlay : MonoBehaviour
    {
        ShiftDesktop _d;
        Text _text;
        bool _visible;

        public static DebugOverlay Build(RectTransform parent, ShiftDesktop d)
        {
            var bg = UIFactory.Solid(parent, new Color(0, 0, 0, 0.7f), "DebugOverlay");
            bg.raycastTarget = false;
            bg.rectTransform.Place(4, 4, 330, 120);
            var o = bg.gameObject.AddComponent<DebugOverlay>();
            o._d = d;
            o._text = UIFactory.Label(bg.transform, "", 11, TextAnchor.UpperLeft, new Color(0.6f, 1f, 0.6f), true);
            o._text.rectTransform.Stretch(6, 4, 6, 4);
            bg.gameObject.SetActive(false);
            return o;
        }

        public void Toggle()
        {
            _visible = !_visible;
            gameObject.SetActive(_visible);
        }

        public void Tick()
        {
            if (!_visible || _d == null) return;
            var s = _d.S;
            var session = s.HelpDesk.ActiveSession;
            _text.text =
                $"Current Time    : {TimeManager.FormatWithSeconds(s.Time.CurrentMinute)}  x{s.Time.DebugTimeScale}{(s.Time.Paused ? " PAUSED" : "")}\n" +
                $"Phase           : {s.Phase}\n" +
                $"Active Events   : {s.Director.Scheduler.PendingCount} scheduled\n" +
                $"Pending Requests: {s.Requests.Pending.Count}\n" +
                $"Active Tickets  : {s.HelpDesk.OpenCount}\n" +
                $"Remote Session  : {(session != null ? session.PC.Hostname + " (" + session.Ticket.TicketId + ")" : "-")}\n" +
                $"Seed            : {s.Seed}";
        }
    }
}
