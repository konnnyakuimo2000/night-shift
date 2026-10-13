using System;
using System.Collections.Generic;

namespace NightShift.Systems
{
    /// <summary>感染記録（図鑑）の発見段階。上の段階ほど多くの情報を公開する</summary>
    public enum DiscoveryStage { None = 0, Observed = 1, Detected = 2, Removed = 3, Recovered = 4 }

    /// <summary>図鑑の1種類分の記録（キャリアに保存。旧セーブでは空の一覧から始まる）</summary>
    [Serializable]
    public class VirusDexEntry
    {
        public string id;
        public int stage;           // DiscoveryStage（過去に到達した最も上の段階。再感染でも下がらない）
        public int observedDay;     // 初めて症状を観測した夜
        public int detectedDay;
        public int removedDay;
        public int recoveredDay;
        public int lastHandledDay;  // 最後に駆除・復旧した夜（「過去に対応済み」）
        public List<string> history = new List<string>(); // 夜ごとの対応履歴（"DAY16 駆除" など。同じ夜・同じ段階は1回）
    }

    /// <summary>感染記録の更新。観測・検出・駆除・復旧を何度通知しても、記録と履歴は重複しない（報酬もない）</summary>
    public static class VirusDex
    {
        public static VirusDexEntry Find(Core.CareerRecord c, string id) => c.virusDex.Find(x => x.id == id);

        public static DiscoveryStage StageOf(Core.CareerRecord c, string id) => (DiscoveryStage)(Find(c, id)?.stage ?? 0);

        /// <summary>段階を記録する。新しく進んだら true</summary>
        public static bool Record(Core.CareerRecord c, string id, DiscoveryStage stage, int day)
        {
            var v = VirusCatalog.Get(id);
            if (v == null || stage == DiscoveryStage.None) return false;
            // 残る被害がない種類は、駆除が終われば復旧も終わっている
            if (stage == DiscoveryStage.Removed && !HasResidualDamage(id)) stage = DiscoveryStage.Recovered;
            var e = Find(c, id);
            if (e == null) { e = new VirusDexEntry { id = id }; c.virusDex.Add(e); }
            if (stage >= DiscoveryStage.Observed && e.observedDay == 0) e.observedDay = day;
            if (stage >= DiscoveryStage.Detected && e.detectedDay == 0) e.detectedDay = day;
            if (stage >= DiscoveryStage.Removed && e.removedDay == 0) e.removedDay = day;
            if (stage >= DiscoveryStage.Recovered && e.recoveredDay == 0) e.recoveredDay = day;
            if (stage >= DiscoveryStage.Removed) e.lastHandledDay = day;
            string h = $"DAY{day} {Label(stage)}";
            if (stage >= DiscoveryStage.Removed && !e.history.Contains(h)) e.history.Add(h);
            if ((int)stage <= e.stage) return false;
            e.stage = (int)stage;
            return true;
        }

        /// <summary>駆除のあとにも被害が残る（復元が必要な）種類</summary>
        public static bool HasResidualDamage(string id) => id == "V2" || id == "V6";

        public static string Label(DiscoveryStage s)
        {
            switch (s)
            {
                case DiscoveryStage.Observed: return "症状を観測";
                case DiscoveryStage.Detected: return "検出";
                case DiscoveryStage.Removed: return "駆除";
                case DiscoveryStage.Recovered: return "対応済み";
                default: return "未確認";
            }
        }
    }
}
