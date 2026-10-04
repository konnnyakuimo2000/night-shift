using System.Collections.Generic;
using NightShift.Core;
using NightShift.Data;
using NightShift.Events;
using NightShift.Systems;

namespace NightShift.Tests
{
    /// <summary>テスト共通: 初期データ（DefaultDataFactory）から作ったデータベースとシフト。</summary>
    public static class TestData
    {
        static GameDatabase _db;

        /// <summary>初期データのデータベース（アセットではなくメモリ上に作る。プレイヤーのセーブには触れない）</summary>
        public static GameDatabase Db => _db != null ? _db : (_db = DefaultDataFactory.Build().database);

        public static DayDefinition Day(int n) => Db.GetDay(n);

        public static ShiftSession Session(int day, int seed = 1) => new ShiftSession(Db, Day(day), seed, "tester");

        public static EventDefinition FindEvent(DayDefinition day, string id)
        {
            foreach (var d in day.violationPool) if (d != null && d.eventId == id) return d;
            foreach (var d in day.routinePool) if (d != null && d.eventId == id) return d;
            foreach (var d in day.fixedEvents) if (d != null && d.eventId == id) return d;
            return null;
        }

        public static ThreatScenarioDefinition FindThreat(DayDefinition day, string id)
        {
            foreach (var t in day.threats) if (t != null && t.scenarioId == id) return t;
            return null;
        }

        public static HelpDeskDefinition FindHelpDesk(string id)
        {
            foreach (var h in Day(30).helpDeskPool) if (h != null && h.definitionId == id) return h;
            return null;
        }

        /// <summary>定義から接続要求を1件作る（社員の条件に合わない乱数のときは null なので、種を変えて数回試す）</summary>
        public static EventInstance Build(ShiftSession s, EventDefinition def)
        {
            for (int i = 0; i < 20; i++)
            {
                var e = s.Factory.Build(def);
                if (e != null) return e;
            }
            return null;
        }

        public static CareerRecord Career(params string[] ranksFromDay8)
        {
            var c = new CareerRecord { seed = 42, trust = 70 };
            for (int i = 0; i < ranksFromDay8.Length; i++)
                c.nights.Add(new NightRecord { day = 8 + i, rank = ranksFromDay8[i] });
            return c;
        }

        public static List<string> AllConditions() => new List<string>(UI.Shift.AutoReviewTool.Conditions);
    }
}
