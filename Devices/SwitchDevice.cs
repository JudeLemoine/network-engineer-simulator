using System;
using System.Collections.Generic;
using UnityEngine;

public enum SwitchportMode
{
    Access,
    Trunk
}

public enum StpPortState
{
    Forwarding,
    Blocking,
    Disabled
}

public enum StpPortRole
{
    Root,
    Designated,
    Alternate,
    Disabled
}

public enum EtherChannelMode
{
    None,
    On,
    Active,
    Passive
}

[System.Serializable]
public class SwitchPortConfig
{
    public string name = "GigabitEthernet0/1";
    public bool adminUp = true;
    public bool protocolUp = false;

    public SwitchportMode mode = SwitchportMode.Access;

    public int accessVlan = 1;

    public string trunkAllowedVlans = "1";
    public int trunkNativeVlan = 1;

    [HideInInspector] public StpPortState stpState = StpPortState.Forwarding;
    [HideInInspector] public StpPortRole stpRole = StpPortRole.Designated;

    [Header("EtherChannel")]
    public int channelGroup = 0;
    public EtherChannelMode channelMode = EtherChannelMode.None;
    [HideInInspector] public bool channelSuspended = false;
}

[System.Serializable]
public class SwitchPortChannelConfig
{
    public int id = 1;
    public string name => $"Port-channel{id}";

    public SwitchportMode mode = SwitchportMode.Trunk;

    public int accessVlan = 1;

    public string trunkAllowedVlans = "1";
    public int trunkNativeVlan = 1;

    [HideInInspector] public StpPortState stpState = StpPortState.Forwarding;
    [HideInInspector] public StpPortRole stpRole = StpPortRole.Designated;

    [HideInInspector] public bool protocolUp = false;
}

[System.Serializable]
public class SwitchVlan
{
    public int id = 1;
    public string name = "default";
    public bool active = true;
}

[System.Serializable]
public class SwitchMacEntry
{
    public string mac;
    public int vlan;
    public string portIfName;
    public float lastSeen;
}

public class SwitchDevice : Device
{
    [Header("Switch Identity (STP)")]
    [Tooltip("Cisco default is 32768. Lower wins root election.")]
    public int stpPriority = 32768;

    [Header("Switch Ports")]
    public List<SwitchPortConfig> ports = new List<SwitchPortConfig>()
    {
        new SwitchPortConfig { name = "GigabitEthernet0/1" },
        new SwitchPortConfig { name = "GigabitEthernet0/2" },
        new SwitchPortConfig { name = "GigabitEthernet0/3" },
        new SwitchPortConfig { name = "GigabitEthernet0/4" }
    };

    [Header("VLAN Database")]
    public List<SwitchVlan> vlans = new List<SwitchVlan>()
    {
        new SwitchVlan { id = 1, name = "default", active = true }
    };

    [Header("MAC Address Table")]
    public List<SwitchMacEntry> macTable = new List<SwitchMacEntry>();

    [Header("MAC Aging")]
    public int macAgingSeconds = 30;

    [HideInInspector] public bool IsRootBridge = false;
    [HideInInspector] public SwitchDevice CurrentRoot;

    [Header("EtherChannel (Port-Channels)")]
    public List<SwitchPortChannelConfig> portChannels = new List<SwitchPortChannelConfig>();

    private float _nextPurgeTime;

    public (int priority, string mac) BridgeId
    {
        get { return (stpPriority, GetBridgeMac()); }
    }

    private void Update()
    {

        foreach (var p in ports)
        {
            if (p == null) continue;

            bool linked = HasLink(p.name);
            p.protocolUp = p.adminUp && linked;

            if (!p.protocolUp)
            {
                p.stpState = StpPortState.Disabled;
                p.stpRole = StpPortRole.Disabled;
            }
        }

        RefreshEtherChannels();

        if (Time.time >= _nextPurgeTime)
        {
            PurgeAgedMacs();
            _nextPurgeTime = Time.time + 1f;
        }
    }

    public SwitchPortChannelConfig GetOrCreatePortChannel(int id)
    {
        if (id < 1) id = 1;

        foreach (var pc in portChannels)
            if (pc != null && pc.id == id) return pc;

        var created = new SwitchPortChannelConfig { id = id };
        portChannels.Add(created);
        return created;
    }

    public SwitchPortChannelConfig GetPortChannel(int id)
    {
        foreach (var pc in portChannels)
            if (pc != null && pc.id == id) return pc;
        return null;
    }

    public bool IsPortChannelName(string ifName, out int id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(ifName)) return false;
        string s = ifName.Trim();
        if (s.StartsWith("Port-channel", StringComparison.OrdinalIgnoreCase))
        {
            string rest = s.Substring("Port-channel".Length).Trim();
            return int.TryParse(rest, out id);
        }
        if (s.StartsWith("Po", StringComparison.OrdinalIgnoreCase))
        {
            string rest = s.Substring(2).Trim();
            return int.TryParse(rest, out id);
        }
        return false;
    }

    public bool IsChannelMember(string ifName, out int groupId)
    {
        groupId = 0;
        var p = GetPort(ifName);
        if (p == null) return false;
        groupId = p.channelGroup;
        return groupId > 0 && p.channelMode != EtherChannelMode.None;
    }

    public List<SwitchPortConfig> GetChannelMembers(int groupId)
    {
        var list = new List<SwitchPortConfig>();
        foreach (var p in ports)
        {
            if (p == null) continue;
            if (p.channelGroup == groupId && p.channelMode != EtherChannelMode.None)
                list.Add(p);
        }

        list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public string GetChannelRepresentativePhysical(int groupId)
    {
        var members = GetChannelMembers(groupId);
        return members.Count > 0 ? members[0].name : null;
    }

    public bool IsChannelFormed(int groupId)
    {

        var members = GetChannelMembers(groupId);
        if (members.Count == 0) return false;

        foreach (var m in members)
        {
            if (!m.adminUp || !HasLink(m.name)) continue;

            var unity = GetUnityPortForInterface(m.name);
            if (unity == null || unity.connectedTo == null) continue;
            var remotePort = unity.connectedTo;
            if (remotePort.owner is not SwitchDevice remoteSw) continue;

            var remoteCfg = remoteSw.GetPort(remotePort.interfaceName);
            if (remoteCfg == null) continue;

            if (remoteCfg.channelGroup != groupId) continue;

            if (!remoteCfg.adminUp || !remoteSw.HasLink(remoteCfg.name)) continue;

            if (IsEtherChannelCompatible(m.channelMode, remoteCfg.channelMode))
                return true;
        }

        return false;
    }

    private static bool IsEtherChannelCompatible(EtherChannelMode a, EtherChannelMode b)
    {

        if (a == EtherChannelMode.On || b == EtherChannelMode.On)
            return a == EtherChannelMode.On && b == EtherChannelMode.On;

        bool aLacp = a == EtherChannelMode.Active || a == EtherChannelMode.Passive;
        bool bLacp = b == EtherChannelMode.Active || b == EtherChannelMode.Passive;
        if (!aLacp || !bLacp) return false;

        return !(a == EtherChannelMode.Passive && b == EtherChannelMode.Passive);
    }

    public bool IsChannelRepresentative(string ifName)
    {
        if (!IsChannelMember(ifName, out int gid)) return false;
        if (!IsChannelFormed(gid)) return false;

        var members = GetChannelMembers(gid);
        if (members.Count == 0) return false;
        return string.Equals(members[0].name, NormalizeInterfaceName(ifName), StringComparison.OrdinalIgnoreCase);
    }

    public string GetLogicalInterfaceForStp(string physicalIfName)
    {

        if (IsChannelMember(physicalIfName, out int gid) && IsChannelFormed(gid))
            return IsChannelRepresentative(physicalIfName) ? $"Port-channel{gid}" : null;

        return NormalizeInterfaceName(physicalIfName);
    }

    public SwitchPortChannelConfig GetEffectivePortChannelForMember(string physicalIfName)
    {
        if (!IsChannelMember(physicalIfName, out int gid)) return null;
        if (!IsChannelFormed(gid)) return null;
        return GetOrCreatePortChannel(gid);
    }

    public void RefreshEtherChannels()
    {

        var groupIds = new HashSet<int>();
        foreach (var p in ports)
        {
            if (p == null) continue;
            if (p.channelGroup > 0 && p.channelMode != EtherChannelMode.None)
                groupIds.Add(p.channelGroup);
        }

        foreach (var gid in groupIds)
        {
            bool formed = IsChannelFormed(gid);

            foreach (var m in GetChannelMembers(gid))
            {
                if (m == null) continue;
                m.channelSuspended = !formed;
            }

            var pc = GetOrCreatePortChannel(gid);

            bool up = false;
            if (formed)
            {
                foreach (var m in GetChannelMembers(gid))
                {
                    if (m == null) continue;
                    if (!m.adminUp) continue;
                    if (!HasLink(m.name)) continue;
                    up = true;
                    break;
                }
            }
            pc.protocolUp = up;
        }
    }

    public void ApplyPortChannelStpToMembers()
    {

        var groupIds = new HashSet<int>();
        foreach (var p in ports)
        {
            if (p == null) continue;
            if (p.channelGroup > 0 && p.channelMode != EtherChannelMode.None)
                groupIds.Add(p.channelGroup);
        }

        foreach (var gid in groupIds)
        {
            if (!IsChannelFormed(gid)) continue;

            var pc = GetOrCreatePortChannel(gid);
            foreach (var m in GetChannelMembers(gid))
            {
                if (m == null) continue;

                if (!m.adminUp || !HasLink(m.name))
                {
                    m.stpState = StpPortState.Disabled;
                    m.stpRole = StpPortRole.Disabled;
                    continue;
                }

                m.stpState = pc.stpState;
                m.stpRole = pc.stpRole;
            }
        }
    }

    public string ResolveEgressPhysicalForLogical(
        string requestedIf,
        int vlan,
        string srcMac,
        string dstMac)
    {

        if (IsPortChannelName(requestedIf, out int poId))
        {
            return ChooseActiveMember(poId, vlan, srcMac, dstMac);
        }

        string phys = NormalizeInterfaceName(requestedIf);

        if (IsChannelMember(phys, out int gid) && IsChannelFormed(gid))
        {

            return ChooseActiveMember(gid, vlan, srcMac, dstMac);
        }

        return phys;
    }

    private string ChooseActiveMember(int groupId, int vlan, string srcMac, string dstMac)
    {
        var members = GetChannelMembers(groupId);
        if (members.Count == 0) return null;

        var upMembers = new List<SwitchPortConfig>();
        foreach (var m in members)
        {
            if (m == null) continue;
            if (!m.adminUp) continue;
            if (!HasLink(m.name)) continue;
            if (m.channelSuspended) continue;

            if (m.stpState == StpPortState.Blocking) continue;

            upMembers.Add(m);
        }

        if (upMembers.Count == 0) return null;
        if (upMembers.Count == 1) return upMembers[0].name;

        int h = 0;
        if (!string.IsNullOrWhiteSpace(srcMac)) h ^= srcMac.GetHashCode();
        if (!string.IsNullOrWhiteSpace(dstMac)) h ^= (dstMac.GetHashCode() << 1);

        int idx = Mathf.Abs(h) % upMembers.Count;
        return upMembers[idx].name;
    }

    public SwitchPortConfig GetPort(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return null;

        string norm = NormalizeInterfaceName(ifName);
        foreach (var p in ports)
        {
            if (p == null) continue;
            if (string.Equals(p.name, norm, StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }

    public Port GetUnityPortForInterface(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return null;

        string norm = NormalizeInterfaceName(ifName);
        string shortNorm = ShortIfName(norm);

        var unityPorts = GetComponentsInChildren<Port>(true);
        foreach (var up in unityPorts)
        {
            if (up == null) continue;

            string upIf = NormalizeInterfaceName(up.interfaceName);
            string upIfShort = ShortIfName(upIf);

            if (string.Equals(upIf, norm, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(upIfShort, shortNorm, StringComparison.OrdinalIgnoreCase))
                return up;

            if (!string.IsNullOrWhiteSpace(up.portName))
            {
                string pn = up.portName.Trim();
                if (string.Equals(pn, shortNorm, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeInterfaceName(pn), norm, StringComparison.OrdinalIgnoreCase))
                    return up;
            }
        }

        return null;
    }

    public bool HasLink(string ifName)
    {
        var p = GetUnityPortForInterface(ifName);
        return p != null && p.IsConnected;
    }

    public SwitchVlan GetVlan(int id)
    {
        foreach (var v in vlans)
            if (v != null && v.id == id) return v;
        return null;
    }

    public SwitchVlan EnsureVlan(int id)
    {
        var v = GetVlan(id);
        if (v != null) return v;

        v = new SwitchVlan { id = id, name = $"VLAN{id}", active = true };
        vlans.Add(v);
        return v;
    }

    public void LearnMac(string mac, int vlan, string ingressIfName)
    {
        if (string.IsNullOrWhiteSpace(mac)) return;
        if (string.IsNullOrWhiteSpace(ingressIfName)) return;

        PurgeAgedMacs();
        mac = mac.Trim().ToUpperInvariant();

        var existing = macTable.Find(e => e != null && e.mac == mac && e.vlan == vlan);
        if (existing != null)
        {
            existing.portIfName = NormalizeInterfaceName(ingressIfName);
            existing.lastSeen = Time.time;
            return;
        }

        macTable.Add(new SwitchMacEntry
        {
            mac = mac,
            vlan = vlan,
            portIfName = NormalizeInterfaceName(ingressIfName),
            lastSeen = Time.time
        });
    }

    public string LookupMacPort(string mac, int vlan)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;

        PurgeAgedMacs();
        mac = mac.Trim().ToUpperInvariant();

        var e = macTable.Find(x => x != null && x.mac == mac && x.vlan == vlan);
        return e != null ? e.portIfName : null;
    }

    public void ClearDynamicMacs() => macTable.Clear();

    public void PurgeAgedMacs()
    {
        if (macAgingSeconds <= 0) return;
        float cutoff = Time.time - macAgingSeconds;

        for (int i = macTable.Count - 1; i >= 0; i--)
        {
            var e = macTable[i];
            if (e == null || e.lastSeen < cutoff)
                macTable.RemoveAt(i);
        }
    }

    public static HashSet<int> ParseVlanList(string vlanList)
    {
        var set = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(vlanList)) return set;

        if (vlanList.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            for (int v = 1; v <= 4094; v++) set.Add(v);
            return set;
        }

        var parts = vlanList.Split(',');
        foreach (var raw in parts)
        {
            var token = raw.Trim();
            if (string.IsNullOrWhiteSpace(token)) continue;

            if (token.Contains("-"))
            {
                var rr = token.Split('-');
                if (rr.Length != 2) continue;

                if (int.TryParse(rr[0].Trim(), out int a) && int.TryParse(rr[1].Trim(), out int b))
                {
                    if (a > b) { int t = a; a = b; b = t; }
                    a = Mathf.Clamp(a, 1, 4094);
                    b = Mathf.Clamp(b, 1, 4094);
                    for (int v = a; v <= b; v++) set.Add(v);
                }
            }
            else
            {
                if (int.TryParse(token, out int v))
                {
                    v = Mathf.Clamp(v, 1, 4094);
                    set.Add(v);
                }
            }
        }

        return set;
    }

    public static string ToCiscoVlanList(HashSet<int> vlans)
    {
        if (vlans == null || vlans.Count == 0) return "";

        var list = new List<int>(vlans);
        list.Sort();

        var ranges = new List<string>();
        int start = list[0];
        int prev = list[0];

        for (int i = 1; i < list.Count; i++)
        {
            int v = list[i];
            if (v == prev + 1)
            {
                prev = v;
                continue;
            }

            ranges.Add(start == prev ? start.ToString() : $"{start}-{prev}");
            start = prev = v;
        }

        ranges.Add(start == prev ? start.ToString() : $"{start}-{prev}");
        return string.Join(",", ranges);
    }

    public static bool IsValidVlanId(int vlan) => vlan >= 1 && vlan <= 4094;

    public static string NormalizeInterfaceName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        raw = raw.Trim();
        string lower = raw.ToLowerInvariant();

        if (lower.StartsWith("port-channel"))
        {
            string rest = raw.Substring("port-channel".Length).Trim();
            return "Port-channel" + rest;
        }
        if (lower.StartsWith("po"))
        {
            string rest = raw.Substring(2).Trim();
            return "Port-channel" + rest;
        }

        if (lower.StartsWith("gigabitethernet"))
        {
            string rest = raw.Substring("gigabitethernet".Length).Trim();
            return "GigabitEthernet" + rest;
        }
        if (lower.StartsWith("gig"))
        {
            string rest = raw.Substring(3).Trim();
            return "GigabitEthernet" + rest;
        }
        if (lower.StartsWith("gi"))
        {
            string rest = raw.Substring(2).Trim();
            if (rest.StartsWith("gabitEthernet", StringComparison.OrdinalIgnoreCase))
                return raw;
            return "GigabitEthernet" + rest;
        }
        if (lower.StartsWith("g"))
        {
            string rest = raw.Substring(1).Trim();
            return "GigabitEthernet" + rest;
        }

        if (lower.StartsWith("fastethernet"))
        {
            string rest = raw.Substring("fastethernet".Length).Trim();
            return "FastEthernet" + rest;
        }
        if (lower.StartsWith("fast"))
        {
            string rest = raw.Substring(4).Trim();
            return "FastEthernet" + rest;
        }
        if (lower.StartsWith("fa"))
        {
            string rest = raw.Substring(2).Trim();
            return "FastEthernet" + rest;
        }
        if (lower.StartsWith("f"))
        {
            string rest = raw.Substring(1).Trim();
            return "FastEthernet" + rest;
        }

        return raw;
    }

    public static string ShortIfName(string full)
    {
        if (string.IsNullOrWhiteSpace(full)) return full;

        if (full.StartsWith("Port-channel", StringComparison.OrdinalIgnoreCase))
            return "Po" + full.Substring("Port-channel".Length);

        if (full.StartsWith("GigabitEthernet", StringComparison.OrdinalIgnoreCase))
            return "Gi" + full.Substring("GigabitEthernet".Length);

        if (full.StartsWith("FastEthernet", StringComparison.OrdinalIgnoreCase))
            return "Fa" + full.Substring("FastEthernet".Length);

        return full;
    }

    private string GetBridgeMac()
    {

        string n = deviceName ?? name ?? "Switch";
        uint h = (uint)n.GetHashCode();

        ushort a = (ushort)((h >> 16) & 0xFFFF);
        ushort b = (ushort)(h & 0xFFFF);
        ushort c = (ushort)((n.Length * 313) & 0xFFFF);

        return $"{a:X4}.{b:X4}.{c:X4}".ToLowerInvariant();
    }
}
