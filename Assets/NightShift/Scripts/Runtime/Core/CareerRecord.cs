using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Core
{
    [Serializable]
    public class IdCount
    {
        public string id;
        public int count;
    }

    /// <summary>1夜分の記録（エンディング・ゲームオーバーの判定に使う）</summary>
    [Serializable]
    public class NightRecord
    {
        public int day;
        public string rank;
        public int score;
        public int maliciousAllowed;
        public int fines;
        public bool overtime;
        public bool damaged;        // 脅威による被害が出た夜
        public int aiHandled;       // サイバー防御AIが処理した件数
        public int totalRequests;
        public int correctReports;
        public int trustDelta;
    }

    /// <summary>
    /// 30日間の記録。新しいゲームで作り直し、毎夜の終わりに更新する。
    /// （所持金は OperatorWallet、パスワードは OperatorAccount が持つ）
    /// </summary>
    [Serializable]
    public class CareerRecord
    {
        public int version;
        public int seed;                                   // このプレイの乱数の種（ランダム解禁日など）
        public List<IdCount> helpDeskUsed = new List<IdCount>();
        public List<NightRecord> nights = new List<NightRecord>();
        public int trust = 70;                             // 会社の信用（0〜100）
        public int accountCompromised;                     // 自分のアカウントの被害回数
        public List<string> clues = new List<string>();    // 前任者の手がかり（保存済み）
        public List<string> flags = new List<string>();    // 汎用フラグ（間違えた問い合わせの後日の影響など）
        public List<string> endingsSeen = new List<string>();
        public List<string> gameOversSeen = new List<string>();
        public List<string> infections = new List<string>(); // 夜をまたいで残る感染（"V3:SALES-PC05" など）
        public List<IdCount> items = new List<IdCount>();   // 所持品（機材・ツールは 1、消耗品は個数）
        public List<string> disabledTools = new List<string>(); // 持っているが使わない設定にしたツール
        public int vaccineDefinitionDay;                    // ウイルス定義ファイルを最後に更新した日
        public int extraTokensTonight;                      // 今夜の追加トークン
        public List<string> autoReviewConditions = new List<string>(); // 自動審査ツールの条件
        public bool autoReviewEnabled;                                  // 自動審査ツールの自動許可（初めは停止）

        const int CurrentVersion = 1;
        const string Key = "NightShift.Career";
        const string SeenKey = "NightShift.EndingsSeen";

        public static CareerRecord Current { get; private set; } = Load();

        /// <summary>テスト用: 保存データ（PlayerPrefs）に触れずに、記録を差し替える</summary>
        public static void SetCurrentForTests(CareerRecord r) => Current = r;
        /// <summary>テスト中は保存しない</summary>
        public static bool SaveDisabled;

        public static CareerRecord Load()
        {
            CareerRecord r = null;
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json)) r = JsonUtility.FromJson<CareerRecord>(json);
            }
            catch { }
            if (r == null || r.version != CurrentVersion) r = NewCareer();
            r.LoadSeen();
            return r;
        }

        static CareerRecord NewCareer() => new CareerRecord { version = CurrentVersion, seed = Environment.TickCount & 0x7fffffff };

        /// <summary>新しいゲーム（エンディング一覧は残す）</summary>
        public static void StartNew()
        {
            var seen = Current?.endingsSeen ?? new List<string>();
            var go = Current?.gameOversSeen ?? new List<string>();
            Current = NewCareer();
            Current.endingsSeen = seen;
            Current.gameOversSeen = go;
            Current.Save();
        }

        /// <summary>夜の開始時点の記録に戻す（その夜をやり直すとき）</summary>
        public static void Restore(string json)
        {
            try
            {
                var r = JsonUtility.FromJson<CareerRecord>(json);
                if (r != null) { Current = r; Current.Save(); }
            }
            catch { }
        }

        public string Snapshot() => JsonUtility.ToJson(this);

        public void Save()
        {
            if (SaveDisabled) return;
            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
                PlayerPrefs.SetString(SeenKey, string.Join(",", endingsSeen) + "|" + string.Join(",", gameOversSeen));
                PlayerPrefs.Save();
            }
            catch { }
        }

        void LoadSeen()
        {
            try
            {
                var s = PlayerPrefs.GetString(SeenKey, "");
                if (string.IsNullOrEmpty(s)) return;
                var parts = s.Split('|');
                foreach (var id in parts[0].Split(',')) if (id.Length > 0 && !endingsSeen.Contains(id)) endingsSeen.Add(id);
                if (parts.Length > 1) foreach (var id in parts[1].Split(',')) if (id.Length > 0 && !gameOversSeen.Contains(id)) gameOversSeen.Add(id);
            }
            catch { }
        }

        // ---------------- 問い合わせ ----------------
        public int HelpDeskCount(string id)
        {
            foreach (var c in helpDeskUsed) if (c.id == id) return c.count;
            return 0;
        }

        public void CountHelpDesk(string id)
        {
            foreach (var c in helpDeskUsed) if (c.id == id) { c.count++; return; }
            helpDeskUsed.Add(new IdCount { id = id, count = 1 });
        }

        /// <summary>このプレイでのランダム解禁日（ID と種から決まる。やり直しても変わらない）</summary>
        public int RandomUnlockDay(string id, int min, int max)
        {
            unchecked
            {
                int h = seed;
                foreach (char ch in id) h = h * 31 + ch;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                int span = Math.Max(1, max - min + 1);
                return min + (int)((uint)h % (uint)span);
            }
        }

        // ---------------- 所持品 ----------------
        public int ItemCount(string id)
        {
            foreach (var c in items) if (c.id == id) return c.count;
            return 0;
        }

        public bool Owns(string id) => ItemCount(id) > 0;

        public void AddItem(string id, int n = 1)
        {
            foreach (var c in items) if (c.id == id) { c.count += n; return; }
            items.Add(new IdCount { id = id, count = n });
        }

        public bool UseItem(string id)
        {
            foreach (var c in items) if (c.id == id && c.count > 0) { c.count--; return true; }
            return false;
        }

        /// <summary>ツールが使える状態か（持っていて、無効にしていない）</summary>
        public bool ToolActive(string id) => Owns(id) && !disabledTools.Contains(id);

        // ---------------- フラグ ----------------
        public bool HasFlag(string f) => flags.Contains(f);
        public void SetFlag(string f) { if (!flags.Contains(f)) flags.Add(f); }
        public void ClearFlag(string f) => flags.Remove(f);

        public NightRecord Night(int day) => nights.Find(n => n.day == day);

        public void RecordNight(NightRecord n)
        {
            nights.RemoveAll(x => x.day == n.day);
            nights.Add(n);
            nights.Sort((a, b) => a.day.CompareTo(b.day));
        }

        /// <summary>DAY8 以降の平均ランク（S=5 … D=1）</summary>
        public float AverageRankValue(int fromDay = 8)
        {
            int n = 0; float sum = 0;
            foreach (var r in nights)
            {
                if (r.day < fromDay) continue;
                sum += RankValue(r.rank); n++;
            }
            return n == 0 ? 0 : sum / n;
        }

        public static float RankValue(string rank)
        {
            switch (rank)
            {
                case "S": return 5; case "A": return 4; case "B": return 3; case "C": return 2; default: return 1;
            }
        }

        public int ConsecutiveRank(string rank, int uptoDay)
        {
            int c = 0;
            for (int d = uptoDay; d >= 1; d--)
            {
                var n = Night(d);
                if (n == null || n.rank != rank) break;
                c++;
            }
            return c;
        }

        public int ConsecutiveOvertime(int uptoDay)
        {
            int c = 0;
            for (int d = uptoDay; d >= 1; d--)
            {
                var n = Night(d);
                if (n == null || !n.overtime) break;
                c++;
            }
            return c;
        }

        public void AddTrust(int delta) => trust = Mathf.Clamp(trust + delta, 0, 100);
    }
}
