using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Data
{
    public enum NodeZone { External, Perimeter, DMZ, Internal, Client }
    public enum NodeRole { Internet, Firewall, ClientGroup, Server }

    /// <summary>ノードが受け付ける接続種別。必要な権限をデータで持つ。</summary>
    [Serializable]
    public class AccessTypeEntry
    {
        [Tooltip("表示名（利用システム）。例: 共有ファイル（閲覧） / 人事システム / VPN接続")]
        public string accessType;
        [Tooltip("この接続が対応する業務サービス (WEB/MAIL/FILE...)。通常行動との照合用")]
        public string serviceId;
        [Tooltip("必要な権限ID（空なら不要）")]
        public string requiredPermission;
        [Tooltip("VPN利用許可が必要か")]
        public bool requiresVpnEnabled;
    }

    [CreateAssetMenu(menuName = "NightShift/Network Node", fileName = "Node")]
    public class NetworkNodeDefinition : ScriptableObject
    {
        public string nodeId;
        public string displayName;
        public NodeRole role;
        public NodeZone zone;
        public string ipAddress;
        [Tooltip("内部重要システム（未登録端末からのアクセス禁止対象）")]
        public bool isCritical;
        [Tooltip("管理系システム")]
        public bool isAdministrative;
        [Tooltip("Network Monitor 上の配置 (0-1)")]
        public Vector2 mapPosition;
        [Tooltip("このノードを管理画面から操作できるようになる日 (DAY1では操作不可)")]
        public int manageableFromDay = 2;
        public List<AccessTypeEntry> accessTypes = new List<AccessTypeEntry>();

        public AccessTypeEntry FindAccess(string accessType)
        {
            foreach (var a in accessTypes) if (a.accessType == accessType) return a;
            return null;
        }

        public AccessTypeEntry FindByService(string serviceId)
        {
            foreach (var a in accessTypes) if (a.serviceId == serviceId) return a;
            return null;
        }
    }
}
