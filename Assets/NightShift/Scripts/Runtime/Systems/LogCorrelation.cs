using System.Collections.Generic;

namespace NightShift.Systems
{
    /// <summary>
    /// ログ解析アプリの「相関」タブの集計（DAY22〜の追加機能）。関連があるかの判断はプレイヤー。
    /// 社外の接続元を範囲ごとにまとめるほか、同じ利用者が複数の範囲から来ている記録も並べる。
    /// 利用者が「-」など不明な記録は、一人の利用者として結び付けない。
    /// </summary>
    public static class LogCorrelation
    {
        public static string Range(string ip)
        {
            int dot = ip.LastIndexOf('.');
            return dot > 0 ? ip.Substring(0, dot) + ".*" : ip;
        }

        static bool IsExternalIp(string ip) =>
            !string.IsNullOrEmpty(ip) && char.IsDigit(ip[0]) && !ip.StartsWith("10.") && !ip.StartsWith("172.16.") && !ip.StartsWith("127.");

        /// <summary>利用者として結び付けてよい名前か（不明・システムの記録は除く）</summary>
        public static bool IsKnownUser(string user)
        {
            if (string.IsNullOrEmpty(user)) return false;
            string u = user.Trim();
            return u != "-" && u != "UNKNOWN" && u != "SYSTEM" && u != "(不明)" && u != "（不明）";
        }

        /// <summary>社外の接続元を範囲ごとに（2件以上の範囲だけ）。範囲 → 時刻順の記録</summary>
        public static List<KeyValuePair<string, List<LogEntry>>> ByRange(IReadOnlyList<LogEntry> logs, int min = 2)
        {
            var groups = new Dictionary<string, List<LogEntry>>();
            var order = new List<string>();
            foreach (var l in logs)
            {
                if (!IsExternalIp(l.Source)) continue;
                string r = Range(l.Source);
                if (!groups.TryGetValue(r, out var list)) { groups[r] = list = new List<LogEntry>(); order.Add(r); }
                list.Add(l);
            }
            var result = new List<KeyValuePair<string, List<LogEntry>>>();
            foreach (var r in order) if (groups[r].Count >= min) result.Add(new KeyValuePair<string, List<LogEntry>>(r, groups[r]));
            return result;
        }

        /// <summary>同じ利用者が、2つ以上の範囲（社内・社外を問わない）から記録を残している。利用者 → 時刻順の記録</summary>
        public static List<KeyValuePair<string, List<LogEntry>>> ByUserAcrossRanges(IReadOnlyList<LogEntry> logs)
        {
            var groups = new Dictionary<string, List<LogEntry>>();
            var ranges = new Dictionary<string, HashSet<string>>();
            var order = new List<string>();
            foreach (var l in logs)
            {
                if (!IsKnownUser(l.User) || string.IsNullOrEmpty(l.Source) || !char.IsDigit(l.Source[0])) continue;
                if (!groups.TryGetValue(l.User, out var list))
                {
                    groups[l.User] = list = new List<LogEntry>();
                    ranges[l.User] = new HashSet<string>();
                    order.Add(l.User);
                }
                list.Add(l);
                ranges[l.User].Add(Range(l.Source));
            }
            var result = new List<KeyValuePair<string, List<LogEntry>>>();
            foreach (var u in order) if (ranges[u].Count >= 2) result.Add(new KeyValuePair<string, List<LogEntry>>(u, groups[u]));
            return result;
        }
    }
}
