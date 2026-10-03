using System;
using System.Collections.Generic;
using NightShift.Data;

namespace NightShift.Systems
{
    public enum TrafficResult { Delivered, Pending, Denied }

    public class TrafficEvent
    {
        public List<string> Path = new List<string>(); // ノードIDの経路
        public TrafficResult Result;
        public string Label;
    }

    /// <summary>
    /// ネットワーク構成と通信の可視化イベント。DAY1ではサーバーは「通信先として存在する」だけ。
    /// DAY2で ServerManager / ServerState をここに追加する想定。
    /// </summary>
    public class NetworkManager
    {
        public const string InternetId = "INTERNET";
        public const string FirewallId = "FIREWALL";
        public const string ClientsId = "EMPLOYEE PCs";

        readonly List<NetworkNodeDefinition> _nodes;
        public IReadOnlyList<NetworkNodeDefinition> Nodes => _nodes;
        public event Action<TrafficEvent> Traffic;

        public NetworkManager(List<NetworkNodeDefinition> nodes)
        {
            _nodes = nodes;
        }

        public NetworkNodeDefinition Get(string id) => _nodes.Find(n => n != null && n.nodeId == id);

        public NetworkNodeDefinition NodeForService(string serviceId) =>
            _nodes.Find(n => n != null && n.FindByService(serviceId) != null);

        public List<string> BuildPath(bool external, NetworkNodeDefinition destination)
        {
            var path = new List<string>();
            string dest = destination != null ? destination.nodeId : FirewallId;
            if (external)
            {
                path.Add(InternetId);
                path.Add(FirewallId);
                if (dest != FirewallId && dest != InternetId) path.Add(dest);
            }
            else
            {
                path.Add(ClientsId);
                if (dest == InternetId) { path.Add(FirewallId); path.Add(InternetId); }
                else if (dest != ClientsId) path.Add(dest);
            }
            return path;
        }

        public void Emit(bool external, NetworkNodeDefinition destination, TrafficResult result, string label = null)
        {
            var t = new TrafficEvent { Path = BuildPath(external, destination), Result = result, Label = label };
            Traffic?.Invoke(t);
        }
    }
}
