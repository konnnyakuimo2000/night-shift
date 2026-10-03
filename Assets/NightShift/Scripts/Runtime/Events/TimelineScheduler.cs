using System;
using System.Collections.Generic;

namespace NightShift.Events
{
    public class ScheduledItem
    {
        public float Time;
        public float Latest;             // preferWhileRemoteActive の場合の最終時刻
        public bool PreferWhileRemote;
        public bool Gated;               // チュートリアル: 前のタスクが片付くまで待つ
        public float GateDeadline;       // 待機の上限
        public string Label;
        public Action Fire;
        public bool Fired;
    }

    /// <summary>ゲーム内時刻ベースのスケジューラー。</summary>
    public class TimelineScheduler
    {
        readonly List<ScheduledItem> _items = new List<ScheduledItem>();
        public IReadOnlyList<ScheduledItem> Items => _items;
        public bool AcceptNew { get; set; } = true;

        public ScheduledItem Add(float time, string label, Action fire)
        {
            var it = new ScheduledItem { Time = time, Latest = time, Label = label, Fire = fire };
            _items.Add(it);
            return it;
        }

        public ScheduledItem AddPreferRemote(float earliest, float latest, string label, Action fire)
        {
            var it = new ScheduledItem { Time = earliest, Latest = latest, PreferWhileRemote = true, Label = label, Fire = fire };
            _items.Add(it);
            return it;
        }

        public ScheduledItem AddGated(float time, float maxDelay, string label, Action fire)
        {
            var it = Add(time, label, fire);
            it.Gated = true;
            it.GateDeadline = time + maxDelay;
            return it;
        }

        /// <summary>gateOpen: プレイヤーの未処理タスクがなく、次のイベントを出してよい状態</summary>
        public void Tick(float now, bool remoteActive, bool gateOpen = true)
        {
            if (!AcceptNew) return;
            ScheduledItem nextGated = null;
            // 列挙中に追加される可能性があるためインデックスで回す
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (it.Fired) continue;
                bool due = it.PreferWhileRemote
                    ? (now >= it.Time && remoteActive) || now >= it.Latest
                    : now >= it.Time;
                if (!due) continue;
                if (it.Gated && now < it.GateDeadline)
                {
                    // 待機中のチュートリアルイベントは、予定時刻の早いものから1件ずつ
                    if (nextGated == null || it.Time < nextGated.Time) nextGated = it;
                    continue;
                }
                Fire(it);
            }
            if (nextGated != null && gateOpen) Fire(nextGated);
        }

        static void Fire(ScheduledItem it)
        {
            it.Fired = true;
            try { it.Fire?.Invoke(); }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
        }

        public int PendingCount
        {
            get { int n = 0; foreach (var i in _items) if (!i.Fired) n++; return n; }
        }

        public ScheduledItem NextPending()
        {
            ScheduledItem best = null;
            foreach (var i in _items)
                if (!i.Fired && (best == null || i.Time < best.Time)) best = i;
            return best;
        }
    }
}
