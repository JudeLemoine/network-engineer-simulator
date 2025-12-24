using System;
using System.Collections.Generic;
using UnityEngine;

public class StpManager : MonoBehaviour
{
    [Header("Recompute")]
    [Tooltip("How often STP recomputes (seconds). 0.5–1.0 is fine.")]
    public float recomputeInterval = 0.75f;

    private float _nextTime;

    private void Update()
    {
        if (Time.time < _nextTime) return;
        _nextTime = Time.time + Mathf.Max(0.1f, recomputeInterval);

        Recompute();
    }

    private void Recompute()
    {
        var switches = FindObjectsOfType<SwitchDevice>();
        if (switches == null || switches.Length == 0) return;

        foreach (var sw in switches)
        {
            if (sw == null) continue;
            sw.RefreshEtherChannels();
        }

        SwitchDevice root = switches[0];
        foreach (var sw in switches)
        {
            if (sw == null) continue;
            if (CompareBridgeId(sw.BridgeId, root.BridgeId) < 0)
                root = sw;
        }

        var edges = new List<Edge>();
        var seen = new HashSet<string>();

        foreach (var sw in switches)
        {
            if (sw == null) continue;

            foreach (var cfg in sw.ports)
            {
                if (cfg == null) continue;
                if (!cfg.adminUp) continue;
                if (!sw.HasLink(cfg.name)) continue;

                var unityPort = sw.GetUnityPortForInterface(cfg.name);
                if (unityPort == null || unityPort.connectedTo == null) continue;
                if (unityPort.connectedTo.owner is not SwitchDevice other) continue;

                var remoteUnity = unityPort.connectedTo;
                string remotePhys = ResolveIfName(other, remoteUnity);
                if (string.IsNullOrWhiteSpace(remotePhys)) continue;

                string aIf = ResolveLogicalIfNameForStp(sw, cfg.name);
                if (string.IsNullOrWhiteSpace(aIf)) continue;

                string bIf = ResolveLogicalIfNameForStpAllowAnyMember(other, remotePhys);
                if (string.IsNullOrWhiteSpace(bIf)) continue;

                var e = new Edge
                {
                    a = sw,
                    aIf = aIf,
                    b = other,
                    bIf = bIf
                };

                if (seen.Add(e.Key))
                    edges.Add(e);
            }
        }

        foreach (var sw in switches)
        {
            if (sw == null) continue;

            sw.CurrentRoot = root;

            foreach (var cfg in sw.ports)
            {
                if (cfg == null) continue;

                bool up = cfg.adminUp && sw.HasLink(cfg.name);
                if (!up)
                {
                    cfg.stpState = StpPortState.Disabled;
                    cfg.stpRole = StpPortRole.Disabled;
                }
                else
                {
                    cfg.stpState = StpPortState.Forwarding;
                    cfg.stpRole = StpPortRole.Designated;
                }
            }

            var groupIds = GetAllChannelGroupIds(sw);
            foreach (var gid in groupIds)
            {
                if (!sw.IsChannelFormed(gid)) continue;

                var pc = sw.GetOrCreatePortChannel(gid);
                if (!pc.protocolUp)
                {
                    pc.stpState = StpPortState.Disabled;
                    pc.stpRole = StpPortRole.Disabled;
                }
                else
                {
                    pc.stpState = StpPortState.Forwarding;
                    pc.stpRole = StpPortRole.Designated;
                }
            }
        }

        var uf = new UnionFind();
        foreach (var sw in switches)
            if (sw != null) uf.MakeSet(sw.GetInstanceID());

        var treeEdges = new HashSet<string>();
        foreach (var e in edges)
        {
            if (e.a == null || e.b == null) continue;

            int aId = e.a.GetInstanceID();
            int bId = e.b.GetInstanceID();

            if (uf.Union(aId, bId))
                treeEdges.Add(e.Key);
        }

        foreach (var e in edges)
        {
            if (e.a == null || e.b == null) continue;

            bool inTree = treeEdges.Contains(e.Key);

            if (inTree)
            {
                SetPortForwarding(e.a, e.aIf, StpPortRole.Designated);
                SetPortForwarding(e.b, e.bIf, StpPortRole.Designated);
            }
            else
            {

                if (CompareBridgeId(e.a.BridgeId, e.b.BridgeId) <= 0)
                {
                    SetPortForwarding(e.a, e.aIf, StpPortRole.Designated);
                    SetPortBlocking(e.b, e.bIf, StpPortRole.Alternate);
                }
                else
                {
                    SetPortForwarding(e.b, e.bIf, StpPortRole.Designated);
                    SetPortBlocking(e.a, e.aIf, StpPortRole.Alternate);
                }
            }
        }

        foreach (var sw in switches)
        {
            if (sw == null) continue;
            sw.IsRootBridge = (sw == root);
        }

        foreach (var sw in switches)
        {
            if (sw == null) continue;
            if (sw == root) continue;

            string bestLogical = null;
            (int priority, string mac) bestNeighbor = default;
            bool found = false;

            foreach (var cfg in sw.ports)
            {
                if (cfg == null) continue;

                string logical = ResolveLogicalIfNameForStp(sw, cfg.name);
                if (string.IsNullOrWhiteSpace(logical)) continue;

                if (sw.IsPortChannelName(logical, out int poId))
                {
                    if (!sw.IsChannelFormed(poId)) continue;

                    var pc = sw.GetOrCreatePortChannel(poId);
                    if (!pc.protocolUp) continue;
                    if (pc.stpState != StpPortState.Forwarding) continue;

                    string rep = sw.GetChannelRepresentativePhysical(poId);
                    if (string.IsNullOrWhiteSpace(rep)) continue;

                    var p = sw.GetUnityPortForInterface(rep);
                    if (p == null || p.connectedTo == null) continue;
                    if (p.connectedTo.owner is not SwitchDevice nb) continue;

                    var nbId = nb.BridgeId;
                    if (!found || CompareBridgeId(nbId, bestNeighbor) < 0)
                    {
                        found = true;
                        bestLogical = logical;
                        bestNeighbor = nbId;
                    }

                    continue;
                }

                if (cfg.stpState != StpPortState.Forwarding) continue;
                if (!sw.HasLink(cfg.name)) continue;

                var phys = sw.GetUnityPortForInterface(cfg.name);
                if (phys == null || phys.connectedTo == null) continue;
                if (phys.connectedTo.owner is not SwitchDevice nb2) continue;

                var nbId2 = nb2.BridgeId;
                if (!found || CompareBridgeId(nbId2, bestNeighbor) < 0)
                {
                    found = true;
                    bestLogical = logical;
                    bestNeighbor = nbId2;
                }
            }

            if (!string.IsNullOrWhiteSpace(bestLogical))
                SetPortRoleOnly(sw, bestLogical, StpPortRole.Root);
        }

        foreach (var sw in switches)
        {
            if (sw == null) continue;
            sw.ApplyPortChannelStpToMembers();
        }
    }

    private static HashSet<int> GetAllChannelGroupIds(SwitchDevice sw)
    {
        var ids = new HashSet<int>();
        if (sw == null) return ids;

        foreach (var p in sw.ports)
        {
            if (p == null) continue;
            if (p.channelGroup > 0 && p.channelMode != EtherChannelMode.None)
                ids.Add(p.channelGroup);
        }

        return ids;
    }

    private string ResolveLogicalIfNameForStp(SwitchDevice sw, string physicalIfName)
    {
        if (sw == null || string.IsNullOrWhiteSpace(physicalIfName)) return null;

        return sw.GetLogicalInterfaceForStp(physicalIfName);
    }

    private string ResolveLogicalIfNameForStpAllowAnyMember(SwitchDevice sw, string physicalIfName)
    {
        if (sw == null || string.IsNullOrWhiteSpace(physicalIfName)) return null;

        if (sw.IsChannelMember(physicalIfName, out int gid) && sw.IsChannelFormed(gid))
            return $"Port-channel{gid}";

        return sw.GetLogicalInterfaceForStp(physicalIfName) ?? physicalIfName;
    }

    private int CompareBridgeId((int priority, string mac) a, (int priority, string mac) b)
    {
        if (a.priority != b.priority) return a.priority.CompareTo(b.priority);
        return string.Compare(a.mac, b.mac, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveIfName(SwitchDevice sw, Port port)
    {
        if (sw == null || port == null) return null;

        if (!string.IsNullOrWhiteSpace(port.interfaceName))
        {
            var cfg = sw.GetPort(port.interfaceName);
            if (cfg != null) return cfg.name;
        }

        if (!string.IsNullOrWhiteSpace(port.portName))
        {
            var cfg = sw.GetPort(port.portName);
            if (cfg != null) return cfg.name;
        }

        if (!string.IsNullOrWhiteSpace(port.interfaceName))
            return port.interfaceName;

        if (!string.IsNullOrWhiteSpace(port.portName))
            return port.portName;

        return null;
    }

    private void SetPortForwarding(SwitchDevice sw, string ifName, StpPortRole role)
    {
        if (sw == null || string.IsNullOrWhiteSpace(ifName)) return;

        if (sw.IsPortChannelName(ifName, out int poId))
        {
            if (!sw.IsChannelFormed(poId))
            {
                var pcDown = sw.GetOrCreatePortChannel(poId);
                pcDown.stpState = StpPortState.Disabled;
                pcDown.stpRole = StpPortRole.Disabled;
                return;
            }

            var pc = sw.GetOrCreatePortChannel(poId);
            if (!pc.protocolUp)
            {
                pc.stpState = StpPortState.Disabled;
                pc.stpRole = StpPortRole.Disabled;
                return;
            }

            pc.stpState = StpPortState.Forwarding;
            pc.stpRole = role;
            return;
        }

        var cfg = sw.GetPort(ifName);
        if (cfg == null) return;

        if (!cfg.adminUp || !sw.HasLink(cfg.name))
        {
            cfg.stpState = StpPortState.Disabled;
            cfg.stpRole = StpPortRole.Disabled;
            return;
        }

        cfg.stpState = StpPortState.Forwarding;
        cfg.stpRole = role;
    }

    private void SetPortBlocking(SwitchDevice sw, string ifName, StpPortRole role)
    {
        if (sw == null || string.IsNullOrWhiteSpace(ifName)) return;

        if (sw.IsPortChannelName(ifName, out int poId))
        {
            if (!sw.IsChannelFormed(poId))
            {
                var pcDown = sw.GetOrCreatePortChannel(poId);
                pcDown.stpState = StpPortState.Disabled;
                pcDown.stpRole = StpPortRole.Disabled;
                return;
            }

            var pc = sw.GetOrCreatePortChannel(poId);
            if (!pc.protocolUp)
            {
                pc.stpState = StpPortState.Disabled;
                pc.stpRole = StpPortRole.Disabled;
                return;
            }

            pc.stpState = StpPortState.Blocking;
            pc.stpRole = role;
            return;
        }

        var cfg = sw.GetPort(ifName);
        if (cfg == null) return;

        if (!cfg.adminUp || !sw.HasLink(cfg.name))
        {
            cfg.stpState = StpPortState.Disabled;
            cfg.stpRole = StpPortRole.Disabled;
            return;
        }

        cfg.stpState = StpPortState.Blocking;
        cfg.stpRole = role;
    }

    private void SetPortRoleOnly(SwitchDevice sw, string ifName, StpPortRole role)
    {
        if (sw == null || string.IsNullOrWhiteSpace(ifName)) return;

        if (sw.IsPortChannelName(ifName, out int poId))
        {
            var pc = sw.GetOrCreatePortChannel(poId);
            pc.stpRole = (pc.stpState == StpPortState.Disabled) ? StpPortRole.Disabled : role;
            return;
        }

        var cfg = sw.GetPort(ifName);
        if (cfg == null) return;

        cfg.stpRole = (cfg.stpState == StpPortState.Disabled) ? StpPortRole.Disabled : role;
    }

    private struct Edge
    {
        public SwitchDevice a;
        public string aIf;
        public SwitchDevice b;
        public string bIf;

        public string Key
        {
            get
            {
                int ida = a != null ? a.GetInstanceID() : 0;
                int idb = b != null ? b.GetInstanceID() : 0;

                string left = $"{ida}:{aIf}";
                string right = $"{idb}:{bIf}";

                if (string.Compare(left, right, StringComparison.Ordinal) <= 0)
                    return $"{left}|{right}";
                return $"{right}|{left}";
            }
        }
    }

    private class UnionFind
    {
        private readonly Dictionary<int, int> _parent = new();

        public void MakeSet(int x)
        {
            if (!_parent.ContainsKey(x))
                _parent[x] = x;
        }

        public int Find(int x)
        {
            if (_parent[x] != x)
                _parent[x] = Find(_parent[x]);
            return _parent[x];
        }

        public bool Union(int a, int b)
        {
            if (!_parent.ContainsKey(a) || !_parent.ContainsKey(b)) return false;
            int ra = Find(a);
            int rb = Find(b);
            if (ra == rb) return false;
            _parent[rb] = ra;
            return true;
        }
    }
}
