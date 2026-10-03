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

        public string companyName = "NEXUS CORPORATION";
        public string systemName = "社内情報システム";
        public string systemVersion = "Ver.4.21";

        public List<PermissionDefinition> permissions = new List<PermissionDefinition>();
        public List<EmployeeData> employees = new List<EmployeeData>();
        public List<NetworkNodeDefinition> nodes = new List<NetworkNodeDefinition>();
        public List<DayDefinition> days = new List<DayDefinition>();

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

        static GameDatabase _cached;

        public static GameDatabase Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<GameDatabase>(ResourcePath);
            if (_cached == null)
            {
                Debug.Log("[NightShift] GameDatabase asset not found. Using built-in default data (menu: NightShift > Create DAY1 Data Assets で編集用アセットを生成できます)");
                _cached = DefaultDataFactory.Build().database;
            }
            return _cached;
        }
    }
}
