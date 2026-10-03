using System.Collections.Generic;
using NightShift.Data;
using NightShift.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI.Shift
{
    /// <summary>
    /// 簡易 Network Monitor。Source → Destination の通信を可視化する。
    /// DAY1 ではサーバーは「Nodeとして存在する」だけで、クリックしても管理できない。
    /// </summary>
    public class NetworkMonitorView : MonoBehaviour
    {
        public const string Id = "netmap";
        ShiftDesktop _d;
        RetroWindow _w;
        RectTransform _map;
        readonly Dictionary<string, Vector2> _pos = new Dictionary<string, Vector2>();
        readonly Dictionary<string, Text> _counters = new Dictionary<string, Text>();
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        readonly List<Packet> _packets = new List<Packet>();
        int _total;

        class Packet
        {
            public List<Vector2> Points;
            public float T;          // 経過セグメント数
            public float Stop;       // 停止位置（セグメント数）
            public TrafficResult Result;
            public Image Img;
            public Text Mark;
            public float Hold;
        }

        public static void Open(ShiftDesktop d)
        {
            if (!d.Require(Data.Features.NetworkMap)) return;
            var w = d.OpenWindow(new WindowSpec
            {
                Id = Id, Title = "ネットワークマップ", Icon = "globe", Width = 700, Height = 500, Maximizable = false,
                Menus = ShiftDesktop.StandardMenus(d), StatusCells = 2, StatusWidths = new float[] { 160 }
            }, out bool created);
            if (!created) return;
            var v = w.gameObject.AddComponent<NetworkMonitorView>();
            v._d = d; v._w = w;
            v.Build();
        }

        void Build()
        {
            var bg = UIFactory.Image(_w.Content, RetroTheme.Field, new Color(0.06f, 0.1f, 0.14f), "Map");
            bg.rectTransform.Stretch(0, 0, 0, 0);
            _map = UIFactory.Rect("Inner", bg.transform).Stretch(8, 8, 8, 8);
            Canvas.ForceUpdateCanvases();
            Layout();
            _d.S.Network.Traffic += OnTraffic;
            _w.SetStatus(0, "サーバーは監視のみ（管理操作は DAY 2 より）");
        }

        void OnDestroy()
        {
            if (_d != null && _d.S != null) _d.S.Network.Traffic -= OnTraffic;
        }

        void Layout()
        {
            var size = new Vector2(670, 400);
            foreach (var n in _d.S.Network.Nodes)
            {
                if (n == null) continue;
                _pos[n.nodeId] = new Vector2(n.mapPosition.x * size.x, n.mapPosition.y * size.y);
            }
            // 回線
            void L(string a, string b)
            {
                if (_pos.ContainsKey(a) && _pos.ContainsKey(b)) Line(_pos[a], _pos[b], new Color(0.3f, 0.45f, 0.55f), 1.5f);
            }
            L(NetworkManager.InternetId, NetworkManager.FirewallId);
            L(NetworkManager.ClientsId, NetworkManager.FirewallId);
            foreach (var n in _d.S.Network.Nodes)
            {
                if (n == null || n.role != NodeRole.Server) continue;
                L(NetworkManager.FirewallId, n.nodeId);
                if (n.zone == NodeZone.Internal) L(NetworkManager.ClientsId, n.nodeId);
            }
            foreach (var n in _d.S.Network.Nodes)
            {
                if (n == null) continue;
                NodeView(n);
            }
        }

        void NodeView(NetworkNodeDefinition n)
        {
            var p = _pos[n.nodeId];
            var root = UIFactory.Rect("Node:" + n.nodeId, _map);
            root.anchorMin = root.anchorMax = new Vector2(0, 1);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(p.x, -p.y);
            root.sizeDelta = new Vector2(110, 54);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            string icon = n.role == NodeRole.Internet ? "globe" : n.role == NodeRole.Firewall ? "firewall" : n.role == NodeRole.ClientGroup ? "pc" : "server";
            var ic = UIFactory.Image(root, RetroTheme.BigIcon(icon), Color.white, "Icon");
            ic.raycastTarget = false;
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 1);
            ic.rectTransform.pivot = new Vector2(0.5f, 1);
            ic.rectTransform.anchoredPosition = new Vector2(0, 0);
            ic.rectTransform.sizeDelta = new Vector2(28, 28);
            var t = UIFactory.Label(root, n.nodeId, 11, TextAnchor.UpperCenter, new Color(0.85f, 0.92f, 1f));
            t.rectTransform.Stretch(-20, 28, -20, 0);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var cnt = UIFactory.Label(root, "", 10, TextAnchor.UpperCenter, new Color(0.5f, 0.6f, 0.7f));
            cnt.rectTransform.Stretch(-20, 41, -20, -12);
            cnt.horizontalOverflow = HorizontalWrapMode.Overflow;
            _counters[n.nodeId] = cnt;
            _counts[n.nodeId] = 0;
            UpdateCounter(n.nodeId);
            var ch = root.gameObject.AddComponent<ClickHandler>();
            var node = n;
            ch.OnClick = () => OnNodeClicked(node);
            ch.OnRightClick = _ => OnNodeClicked(node);
        }

        void OnNodeClicked(NetworkNodeDefinition n)
        {
            AudioFx.Click();
            if (n.role == NodeRole.Server && _d.S.Day.dayNumber < n.manageableFromDay)
                _w.SetStatus(0, $"{n.nodeId} ({n.ipAddress}) — このサーバーを管理する権限がありません。");
            else
                _w.SetStatus(0, $"{n.nodeId} ({n.ipAddress})  区分: {ZoneLabel(n.zone)}");
        }

        static string ZoneLabel(NodeZone z)
        {
            switch (z)
            {
                case NodeZone.External: return "社外";
                case NodeZone.Perimeter: return "境界";
                case NodeZone.DMZ: return "DMZ（公開）";
                case NodeZone.Internal: return "社内";
                default: return "社内端末";
            }
        }

        void UpdateCounter(string id)
        {
            if (_counters.TryGetValue(id, out var t)) t.text = _counts[id] > 0 ? $"{_counts[id]} 件" : "";
        }

        Image Line(Vector2 a, Vector2 b, Color c, float thickness)
        {
            var img = UIFactory.Solid(_map, c, "Line");
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            var A = new Vector2(a.x, -a.y);
            var B = new Vector2(b.x, -b.y);
            rt.anchoredPosition = A;
            var d = B - A;
            rt.sizeDelta = new Vector2(d.magnitude, thickness);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            return img;
        }

        void OnTraffic(TrafficEvent t)
        {
            var pts = new List<Vector2>();
            foreach (var id in t.Path) if (_pos.TryGetValue(id, out var p)) pts.Add(p);
            if (pts.Count < 2) return;
            string dest = t.Path[t.Path.Count - 1];
            if (_counts.ContainsKey(dest) && t.Result != TrafficResult.Pending) { _counts[dest]++; UpdateCounter(dest); }
            _total++;
            _w.SetStatus(1, $"通信 {_total} 件");
            if (_packets.Count > 60) return;
            var img = UIFactory.Solid(_map, t.Result == TrafficResult.Pending ? new Color(1f, 0.85f, 0.3f) : new Color(0.75f, 0.95f, 1f), "Packet");
            img.raycastTarget = false;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0, 1);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = new Vector2(6, 6);
            var pk = new Packet { Points = pts, Result = t.Result, Img = img };
            int segs = pts.Count - 1;
            pk.Stop = t.Result == TrafficResult.Delivered ? segs : t.Result == TrafficResult.Denied ? (segs >= 2 ? 1f : 0.5f) : (segs >= 2 ? 1f : 0.5f);
            _packets.Add(pk);
            Place(pk);
        }

        static Vector2 Eval(List<Vector2> pts, float t)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(t), 0, pts.Count - 2);
            float f = Mathf.Clamp01(t - i);
            return Vector2.Lerp(pts[i], pts[i + 1], f);
        }

        void Place(Packet p)
        {
            var v = Eval(p.Points, p.T);
            p.Img.rectTransform.anchoredPosition = new Vector2(v.x, -v.y);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime * (_d.S.Time.Paused ? 0 : 1);
            for (int i = _packets.Count - 1; i >= 0; i--)
            {
                var p = _packets[i];
                if (p.T < p.Stop)
                {
                    p.T = Mathf.Min(p.Stop, p.T + dt * 1.6f);
                    Place(p);
                    continue;
                }
                if (p.Hold == 0 && p.Result == TrafficResult.Denied)
                {
                    p.Mark = UIFactory.Label(_map, "×", 14, TextAnchor.MiddleCenter, new Color(1f, 0.45f, 0.4f));
                    p.Mark.rectTransform.anchorMin = p.Mark.rectTransform.anchorMax = new Vector2(0, 1);
                    p.Mark.rectTransform.sizeDelta = new Vector2(16, 16);
                    p.Mark.rectTransform.anchoredPosition = p.Img.rectTransform.anchoredPosition;
                }
                p.Hold += dt;
                float life = p.Result == TrafficResult.Delivered ? 0.15f : 1.6f;
                if (p.Result == TrafficResult.Pending) p.Img.enabled = Mathf.Repeat(p.Hold, 0.4f) < 0.25f;
                if (p.Hold >= life)
                {
                    Destroy(p.Img.gameObject);
                    if (p.Mark != null) Destroy(p.Mark.gameObject);
                    _packets.RemoveAt(i);
                }
            }
        }
    }
}
