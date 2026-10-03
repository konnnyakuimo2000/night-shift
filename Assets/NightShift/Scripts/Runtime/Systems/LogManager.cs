using System;
using System.Collections.Generic;

namespace NightShift.Systems
{
    public class LogEntry
    {
        public int Sequence;
        public float Time;
        public string Source;
        public string Destination;
        public string User;
        public string EventType;
        public string Result;
        public string Detail;
    }

    /// <summary>システムログ。自動相関などは持たない（プレイヤー自身が気付く設計）。</summary>
    public class LogManager
    {
        readonly List<LogEntry> _entries = new List<LogEntry>();
        public IReadOnlyList<LogEntry> Entries => _entries;
        public event Action<LogEntry> Added;
        public const int MaxEntries = 3000;
        int _seq;

        public LogEntry Add(float time, string source, string destination, string user, string eventType, string result, string detail = "")
        {
            var e = new LogEntry
            {
                Sequence = ++_seq, Time = time, Source = source ?? "-", Destination = destination ?? "-",
                User = string.IsNullOrEmpty(user) ? "-" : user, EventType = eventType ?? "-", Result = result ?? "-", Detail = detail ?? ""
            };
            _entries.Add(e);
            if (_entries.Count > MaxEntries) _entries.RemoveAt(0);
            Added?.Invoke(e);
            return e;
        }

        public IEnumerable<string> EventTypes()
        {
            var set = new SortedSet<string>();
            foreach (var e in _entries) set.Add(e.EventType);
            return set;
        }
    }
}
