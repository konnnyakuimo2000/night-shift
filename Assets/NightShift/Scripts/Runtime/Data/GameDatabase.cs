using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Data
{
    /// <summary>
    /// 全データの入口。Resources/NightShiftData/GameDatabase.asset から読み込む。
    /// アセットが無い場合は DefaultDataFactory でメモリ上に同じ初期データを構築する。
    /// </summary>
    [CreateAssetMenu(menuName = "NightShift/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public const string ResourcePath = "NightShiftData/GameDatabase";

        [Tooltip("初期データの版。DefaultDataFactory.DataVersion より古い場合、エディタで再生成を促す")]
        public int dataVersion;

        public string companyName = "NEXUS CORPORATION";
        [Tooltip("日本語の社名（タイムカード・メールに表示）")]
        public string companyNameJa = "ネクサスコーポレーション";
        public string systemName = "社内情報システム";
        public string systemVersion = "Ver.4.21";

        [Tooltip("給与の計算式")]
        public PayrollSettings payroll = new PayrollSettings();

        public List<PermissionDefinition> permissions = new List<PermissionDefinition>();
        public List<EmployeeData> employees = new List<EmployeeData>();
        public List<NetworkNodeDefinition> nodes = new List<NetworkNodeDefinition>();
        public List<DayDefinition> days = new List<DayDefinition>();

        /// <summary>指定日の定義（無ければ null）</summary>
        public DayDefinition FindDay(int day)
        {
            foreach (var d in days) if (d != null && d.dayNumber == day) return d;
            return null;
        }

        public DayDefinition GetDay(int day)
        {
            foreach (var d in days) if (d != null && d.dayNumber == day) return d;
            return days.Count > 0 ? days[0] : null;
        }

        public NetworkNodeDefinition GetNode(string id)
        {
            foreach (var n in nodes) if (n != null && n.nodeId == id) return n;
            return null;
        }

        public NetworkNodeDefinition GetNodeForService(string serviceId)
        {
            foreach (var n in nodes)
                if (n != null && n.FindByService(serviceId) != null) return n;
            return null;
        }

        /// <summary>参照切れがないか（壊れたアセットでの起動を防ぐ）</summary>
        public bool IsValid()
        {
            if (days.Count == 0 || employees.Count == 0 || nodes.Count == 0 || permissions.Count == 0) return false;
            foreach (var d in days) if (d == null) return false;
            foreach (var e in employees) if (e == null) return false;
            foreach (var n in nodes) if (n == null) return false;
            foreach (var p in permissions) if (p == null) return false;
            return true;
        }

        static GameDatabase _cached;

        public static GameDatabase Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<GameDatabase>(ResourcePath);
            if (_cached != null && !_cached.IsValid())
            {
                Debug.LogWarning("[NightShift] GameDatabase asset has missing references. Using built-in default data (NightShift > Create DAY1 Data Assets で再生成してください)");
                _cached = null;
            }
            if (_cached == null)
            {
                Debug.Log("[NightShift] GameDatabase asset not found. Using built-in default data (menu: NightShift > Create DAY1 Data Assets で編集用アセットを生成できます)");
                _cached = DefaultDataFactory.Build().database;
            }
            return _cached;
        }
    }
}
