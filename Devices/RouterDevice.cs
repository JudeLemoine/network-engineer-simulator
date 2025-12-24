
using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RouterInterface
{
    public string name;
    public string ipAddress = "unassigned";
    public string subnetMask = "";
    public bool adminUp = false;
    public bool protocolUp = false;

    public bool isSubinterface = false;
    public string parent = "";
    public int dot1qVlan = -1;

[System.Serializable]
public class StaticRoute
{
    public string network;
    public string subnetMask;
    public string nextHop;
    public string exitInterface;
}

}

[System.Serializable]
public class DhcpLease
{
    public string mac;
    public string ip;
    public string poolName;
    public DateTime issuedAtUtc;
}

public struct DhcpOffer
{
    public string ip;
    public string mask;
    public string gateway;
    public string dns;
    public string poolName;
}
[System.Serializable]
public class DhcpPool
{
    public string name = "POOL";
    public string network = "0.0.0.0";
    public string mask = "255.255.255.0";
    public string defaultRouter = "0.0.0.0";
    public string dnsServer = "0.0.0.0";
    public int startHost = 10;
    public int endHost = 200;

    [NonSerialized] public Dictionary<string, DhcpLease> leasesByMac = new Dictionary<string, DhcpLease>();

    public void EnsureRuntime()
    {
        if (leasesByMac == null) leasesByMac = new Dictionary<string, DhcpLease>();
    }
}

public enum AclAction { Permit, Deny }
public enum AclProtocol { Ip, Icmp, Tcp, Udp }

[System.Serializable]
public class AclRule
{
    public int sequence = 10;
    public AclAction action = AclAction.Permit;
    public AclProtocol protocol = AclProtocol.Ip;
    public string src = "any";
    public string dst = "any";
    public int dstPort = -1;

    public override string ToString()
    {
        string act = action == AclAction.Permit ? "permit" : "deny";
        string proto = protocol == AclProtocol.Icmp ? "icmp" : (protocol == AclProtocol.Tcp ? "tcp" : (protocol == AclProtocol.Udp ? "udp" : "ip"));
        string portPart = (protocol == AclProtocol.Tcp || protocol == AclProtocol.Udp) && dstPort >= 0 ? $" eq {dstPort}" : "";
        return $"{sequence} {act} {proto} {src} {dst}{portPart}";
    }
}

[System.Serializable]
public class ExtendedAcl
{
    public string name;
    public List<AclRule> rules = new List<AclRule>();
    public int nextSeq = 10;

    public void AddRule(AclAction action, AclProtocol proto)
    {
        rules.Add(new AclRule
        {
            sequence = nextSeq,
            action = action,
            protocol = proto,
            src = "any",
            dst = "any"
        });

        nextSeq += 10;
        rules.Sort((a, b) => a.sequence.CompareTo(b.sequence));
    }

    public void AddRule(AclAction action, AclProtocol proto, string src, string dst, int dstPort = -1)
    {
        rules.Add(new AclRule
        {
            sequence = nextSeq,
            action = action,
            protocol = proto,
            src = string.IsNullOrWhiteSpace(src) ? "any" : src,
            dst = string.IsNullOrWhiteSpace(dst) ? "any" : dst,
            dstPort = dstPort
        });

        nextSeq += 10;
        rules.Sort((a, b) => a.sequence.CompareTo(b.sequence));
    }

    public void AddRule(AclRule rule)
    {
        if (rule == null) return;
        if (rule.sequence <= 0) rule.sequence = nextSeq;
        rules.Add(rule);
        nextSeq = System.Math.Max(nextSeq, rule.sequence + 10);
        rules.Sort((a, b) => a.sequence.CompareTo(b.sequence));
    }

}

[System.Serializable]
public class StandardAclEntry
{
    public int aclNumber = 1;
    public string network = "0.0.0.0";
    public string wildcard = "255.255.255.255";
    public bool permit = true;
}

[System.Serializable]
public class NatRule
{
    public int aclNumber = 1;
    public string outsideInterfaceName = "GigabitEthernet0/1";
    public bool overload = true;
}

[System.Serializable]
public class NatTranslation
{
    public string protocol = "icmp";
    public string insideLocal = "0.0.0.0";
    public string insideGlobal = "0.0.0.0";
    public string outsideLocal = "0.0.0.0";
    public string outsideGlobal = "0.0.0.0";
    public DateTime createdUtc;
    public DateTime lastUsedUtc;
}

public enum RouterSwitchportMode
{
    Access,
    Trunk
}

[System.Serializable]
public class RouterSwitchPortConfig
{
    public string ifName;
    public RouterSwitchportMode mode = RouterSwitchportMode.Access;

    public int accessVlan = 1;

    public string trunkAllowedVlans = "1";
    public int trunkNativeVlan = 1;
}

[System.Serializable]
public class RouterMacEntry
{
    public string mac;
    public int vlan;
    public string ifName;
    public float lastSeen;
}

[System.Serializable]
public class RouterArpEntry
{
    public string ip;
    public string mac;
    public string ifName;
    public float lastSeen;
}

public class RouterDevice : Device
{
    [Header("Router Model / Modular Bays")]
    public bool enableModuleSlots = true;
    public GameObject defaultPortPrefab;

    public List<RouterInterface> interfaces = new List<RouterInterface>()
    {
        new RouterInterface { name = "GigabitEthernet0/0" },
        new RouterInterface { name = "GigabitEthernet0/1" }
    };

    [Header("Console (line console 0)")]
    public bool consoleLoginEnabled = false;
    public string consolePassword = "cisco";

    public List<DhcpPool> dhcpPools = new List<DhcpPool>();

    public List<ExtendedAcl> acls = new List<ExtendedAcl>();


    public List<StaticRoute> staticRoutes = new List<StaticRoute>();

    [System.Serializable]
    public class InterfaceAclBinding
    {
        public string interfaceName;
        public string inboundAcl;
        public string outboundAcl;
    }
    public List<InterfaceAclBinding> interfaceAclBindings = new List<InterfaceAclBinding>();

    [System.Serializable]
    public class InterfaceNatBinding
    {
        public string interfaceName;
        public bool natInside;
        public bool natOutside;
    }
    public List<InterfaceNatBinding> interfaceNatBindings = new List<InterfaceNatBinding>();

    public List<StandardAclEntry> standardAcls = new List<StandardAclEntry>();
    public NatRule natRule = null;
    public List<NatTranslation> natTranslations = new List<NatTranslation>();

    [Header("Embedded Switch (HWIC-4ESW)")]
    public List<RouterSwitchPortConfig> switchPorts = new List<RouterSwitchPortConfig>();
    public List<RouterMacEntry> macTable = new List<RouterMacEntry>();
    public int macAgingSeconds = 30;

    [NonSerialized] private Dictionary<string, RouterArpEntry> _arp = new Dictionary<string, RouterArpEntry>(StringComparer.OrdinalIgnoreCase);

    public float linkRefreshInterval = 0.25f;
    private float _nextRefreshTime = 0f;
    private float _nextMacPurge = 0f;

    protected override void Awake()
    {
        base.Awake();

        if (_arp == null)
            _arp = new Dictionary<string, RouterArpEntry>(StringComparer.OrdinalIgnoreCase);

        if (enableModuleSlots)
            InitializeModuleSlots();

        RefreshProtocolStates();
    }

    protected override void OnPowerStateChanged(bool poweredOn)
    {
        RefreshProtocolStates();
    }

    private void Update()
    {
        if (linkRefreshInterval > 0f && Time.time >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.time + linkRefreshInterval;
            RefreshProtocolStates();
        }

        if (Time.time >= _nextMacPurge)
        {
            _nextMacPurge = Time.time + 1f;
            PurgeAgedMacs();
        }
    }

    private void SyncInterfacesFromPorts()
    {

        var ports = GetComponentsInChildren<Port>(true);
        if (ports == null || ports.Length == 0) return;

        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in ports)
        {
            if (p == null) continue;

            string raw = !string.IsNullOrWhiteSpace(p.interfaceName) ? p.interfaceName : p.portName;
            if (string.IsNullOrWhiteSpace(raw)) continue;

            string norm = NormalizeInterfaceName(raw);
            if (string.IsNullOrWhiteSpace(norm)) continue;

            if (!(norm.StartsWith("FastEthernet", StringComparison.OrdinalIgnoreCase) ||
                  norm.StartsWith("GigabitEthernet", StringComparison.OrdinalIgnoreCase) ||
                  norm.StartsWith("Serial", StringComparison.OrdinalIgnoreCase)))
                continue;

            if (TryParseSubinterface(norm, out _, out _)) continue;

            discovered.Add(norm);
        }

        if (discovered.Count == 0) return;

        bool hasFast = false, hasGig = false;
        foreach (var n in discovered)
        {
            if (n.StartsWith("FastEthernet", StringComparison.OrdinalIgnoreCase)) hasFast = true;
            if (n.StartsWith("GigabitEthernet", StringComparison.OrdinalIgnoreCase)) hasGig = true;
        }

        string hint = (deviceName ?? name ?? "").ToLowerInvariant();
        bool looksLike1841 = hint.Contains("1841");

        if (looksLike1841 || (hasFast && !hasGig))
        {
            interfaces.RemoveAll(i =>
                !i.isSubinterface &&
                (i.name.StartsWith("GigabitEthernet", StringComparison.OrdinalIgnoreCase) ||
                 i.name.StartsWith("FastEthernet", StringComparison.OrdinalIgnoreCase) ||
                 i.name.StartsWith("Serial", StringComparison.OrdinalIgnoreCase)));

            foreach (var n in discovered)
            {
                if (GetInterface(n) == null)
                    interfaces.Add(new RouterInterface { name = n });
            }
        }
        else
        {

            foreach (var n in discovered)
            {
                if (GetInterface(n) == null)
                    interfaces.Add(new RouterInterface { name = n });
            }
        }
    }

    public static string NormalizeInterfaceName(string raw)
    {
        raw = (raw ?? "").Trim();
        if (raw.Length == 0) return raw;

        string lower = raw.ToLowerInvariant();

        if (lower.StartsWith("gigabitethernet"))
            return "GigabitEthernet" + raw.Substring("gigabitethernet".Length);
        if (lower.StartsWith("gig"))
            return "GigabitEthernet" + raw.Substring(3);
        if (lower.StartsWith("gi"))
            return "GigabitEthernet" + raw.Substring(2);
        if (lower.StartsWith("g"))
            return "GigabitEthernet" + raw.Substring(1);

        if (lower.StartsWith("fastethernet"))
            return "FastEthernet" + raw.Substring("fastethernet".Length);
        if (lower.StartsWith("fast"))
            return "FastEthernet" + raw.Substring(4);
        if (lower.StartsWith("fa"))
            return "FastEthernet" + raw.Substring(2);
        if (lower.StartsWith("f"))
            return "FastEthernet" + raw.Substring(1);

        if (lower.StartsWith("serial"))
            return "Serial" + raw.Substring("serial".Length);
        if (lower.StartsWith("se"))
            return "Serial" + raw.Substring(2);
        if (lower.StartsWith("s"))
            return "Serial" + raw.Substring(1);

        return raw;
    }

    public RouterInterface GetInterface(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return null;
        string norm = NormalizeInterfaceName(ifName);

        foreach (var i in interfaces)
        {
            if (i == null) continue;
            if (string.Equals(i.name, norm, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return null;
    }

    public RouterInterface EnsureInterface(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return null;

        string norm = NormalizeInterfaceName(ifName);
        var existing = GetInterface(norm);
        if (existing != null) return existing;

        if (!TryParseSubinterface(norm, out string parentName, out int subId))
            return null;

        var parentIf = GetInterface(parentName);
        if (parentIf == null) return null;

        var sub = new RouterInterface
        {
            name = norm,
            isSubinterface = true,
            parent = parentName,
            dot1qVlan = subId
        };

        interfaces.Add(sub);
        RefreshProtocolStates();
        return sub;
    }

    public static bool TryParseSubinterface(string ifName, out string parentName, out int subId)
    {
        parentName = "";
        subId = -1;

        if (string.IsNullOrWhiteSpace(ifName)) return false;

        int dot = ifName.LastIndexOf('.');
        if (dot <= 0 || dot >= ifName.Length - 1) return false;

        parentName = ifName.Substring(0, dot);
        string suffix = ifName.Substring(dot + 1);

        if (!int.TryParse(suffix, out subId)) return false;
        if (subId < 1 || subId > 4094) return false;

        return true;
    }

    public Port GetPortForInterface(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return null;

        string lookup = NormalizeInterfaceName(ifName);
        if (TryParseSubinterface(lookup, out string parentName, out _))
            lookup = parentName;

        var ports = GetComponentsInChildren<Port>(true);
        foreach (var p in ports)
        {
            if (p == null) continue;

            string pIf = NormalizeInterfaceName(p.interfaceName);
            if (string.Equals(pIf, lookup, StringComparison.OrdinalIgnoreCase))
                return p;

            string pName = NormalizeInterfaceName(p.portName);
            if (string.Equals(pName, lookup, StringComparison.OrdinalIgnoreCase))
                return p;
        }

        return null;
    }

    public bool HasLink(string ifName)
    {
        var p = GetPortForInterface(ifName);
        if (p == null) return false;
        if (!p.IsConnected) return false;
        if (p.owner == null || p.connectedTo == null || p.connectedTo.owner == null) return false;
        if (!p.owner.IsPoweredOn) return false;
        if (!p.connectedTo.owner.IsPoweredOn) return false;
        return true;
    }

    public Port ResolvePort(string ifName)
    {
        return GetPortForInterface(ifName);
    }

    public void RefreshProtocolStates()
    {
        if (!IsPoweredOn)
        {
            foreach (var itf in interfaces)
            {
                if (itf == null) continue;
                itf.protocolUp = false;
            }
            return;
        }

        foreach (var itf in interfaces)
        {
            if (itf == null) continue;

            if (TryParseSubinterface(itf.name, out string parentName, out int subId))
            {
                itf.isSubinterface = true;
                itf.parent = parentName;
                if (itf.dot1qVlan < 1) itf.dot1qVlan = subId;
            }
            else
            {
                itf.isSubinterface = false;
                itf.parent = "";
            }
        }

        foreach (var itf in interfaces)
        {
            if (itf == null) continue;

            bool linked = HasLink(itf.isSubinterface ? itf.parent : itf.name);

            bool parentAdminUp = true;
            if (itf.isSubinterface)
            {
                var p = GetInterface(itf.parent);
                parentAdminUp = (p != null && p.adminUp);
            }

            itf.protocolUp = itf.adminUp && parentAdminUp && linked;
        }
    }

    public bool IsSwitchportCapable(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return false;
        string n = NormalizeInterfaceName(ifName);
        return GetSwitchPort(n) != null;
    }

    public RouterSwitchPortConfig GetSwitchPort(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return null;
        string n = NormalizeInterfaceName(ifName);

        foreach (var sp in switchPorts)
        {
            if (sp == null) continue;
            if (string.Equals(sp.ifName, n, StringComparison.OrdinalIgnoreCase))
                return sp;
        }
        return null;
    }

    public RouterSwitchPortConfig EnsureSwitchPort(string ifName)
    {
        string n = NormalizeInterfaceName(ifName);
        var sp = GetSwitchPort(n);
        if (sp != null) return sp;

        sp = new RouterSwitchPortConfig { ifName = n };
        switchPorts.Add(sp);
        return sp;
    }

    public static HashSet<int> ParseVlanList(string list)
    {
        var set = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(list)) return set;

        foreach (var token in list.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim();
            if (t.Contains('-'))
            {
                var parts = t.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[0], out int a)) continue;
                if (!int.TryParse(parts[1], out int b)) continue;
                if (a > b) (a, b) = (b, a);
                for (int v = a; v <= b; v++)
                    if (v >= 1 && v <= 4094) set.Add(v);
            }
            else
            {
                if (int.TryParse(t, out int v) && v >= 1 && v <= 4094)
                    set.Add(v);
            }
        }

        return set;
    }

    public int DetermineIngressVlan(string ingressIf)
    {
        var sp = GetSwitchPort(ingressIf);
        if (sp == null) return 1;

        if (sp.mode == RouterSwitchportMode.Access)
            return sp.accessVlan;

        return sp.trunkNativeVlan <= 0 ? 1 : sp.trunkNativeVlan;
    }

    public bool IsVlanAllowedOnPort(string ifName, int vlan)
    {
        var sp = GetSwitchPort(ifName);
        if (sp == null) return false;

        if (sp.mode == RouterSwitchportMode.Access)
            return sp.accessVlan == vlan;

        if (sp.trunkNativeVlan == vlan) return true;
        var allowed = ParseVlanList(sp.trunkAllowedVlans);
        return allowed.Contains(vlan);
    }

    public void LearnMac(string mac, int vlan, string ifName)
    {
        if (string.IsNullOrWhiteSpace(mac) || string.IsNullOrWhiteSpace(ifName)) return;

        mac = mac.Trim().ToUpperInvariant();
        ifName = NormalizeInterfaceName(ifName);

        foreach (var e in macTable)
        {
            if (e == null) continue;
            if (e.vlan == vlan &&
                string.Equals(e.mac, mac, StringComparison.OrdinalIgnoreCase))
            {
                e.ifName = ifName;
                e.lastSeen = Time.time;
                return;
            }
        }

        macTable.Add(new RouterMacEntry
        {
            mac = mac,
            vlan = vlan,
            ifName = ifName,
            lastSeen = Time.time
        });
    }

    public string LookupMacPort(string mac, int vlan)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;
        mac = mac.Trim().ToUpperInvariant();

        foreach (var e in macTable)
        {
            if (e == null) continue;
            if (e.vlan == vlan &&
                string.Equals(e.mac, mac, StringComparison.OrdinalIgnoreCase))
                return e.ifName;
        }
        return null;
    }

    public void PurgeAgedMacs()
    {
        if (macTable == null) return;
        float cutoff = Time.time - Mathf.Max(5, macAgingSeconds);

        for (int i = macTable.Count - 1; i >= 0; i--)
        {
            var e = macTable[i];
            if (e == null || e.lastSeen < cutoff)
                macTable.RemoveAt(i);
        }
    }

    public DhcpPool GetDhcpPool(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var p in dhcpPools)
        {
            if (p == null) continue;
            if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }

    public DhcpPool EnsureDhcpPool(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var existing = GetDhcpPool(name);
        if (existing != null)
        {
            existing.EnsureRuntime();
            return existing;
        }

        var p = new DhcpPool { name = name.Trim() };
        p.EnsureRuntime();
        dhcpPools.Add(p);
        return p;
    }

    public bool RemoveDhcpPool(string name)
    {
        var p = GetDhcpPool(name);
        if (p == null) return false;
        return dhcpPools.Remove(p);
    }

    public bool TryDhcpRequest(string clientMac, out DhcpLease lease)
    {
        lease = null;
        if (string.IsNullOrWhiteSpace(clientMac)) return false;

        clientMac = clientMac.Trim().ToUpperInvariant();

        foreach (var pool in dhcpPools)
        {
            if (pool == null) continue;
            pool.EnsureRuntime();

            if (!TryParseIPv4(pool.network, out uint net)) continue;
            if (!TryParseIPv4(pool.mask, out uint mask)) continue;

            if (pool.leasesByMac.TryGetValue(clientMac, out DhcpLease existing))
            {
                lease = existing;
                return true;
            }

            if (!TryAllocateFromPool(pool, clientMac, net, mask, out DhcpLease newLease))
                continue;

            lease = newLease;
            return true;
        }

        return false;
    }

    private bool TryAllocateFromPool(DhcpPool pool, string mac, uint net, uint mask, out DhcpLease lease)
    {
        lease = null;

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in pool.leasesByMac)
        {
            if (kv.Value != null && !string.IsNullOrWhiteSpace(kv.Value.ip))
                used.Add(kv.Value.ip.Trim());
        }

        for (int host = pool.startHost; host <= pool.endHost; host++)
        {
            uint candidate = (net & mask) | (uint)host;

            if (host == 0) continue;
            if (host == 255) continue;

            string ip = IPv4ToString(candidate);
            if (used.Contains(ip)) continue;

            var l = new DhcpLease
            {
                mac = mac,
                ip = ip,
                poolName = pool.name,
                issuedAtUtc = DateTime.UtcNow
            };

            pool.leasesByMac[mac] = l;
            lease = l;
            return true;
        }

        return false;
    }

    public ExtendedAcl GetAcl(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var a in acls)
        {
            if (a == null) continue;
            if (string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase))
                return a;
        }
        return null;
    }

    public ExtendedAcl EnsureAcl(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var existing = GetAcl(name);
        if (existing != null) return existing;

        var acl = new ExtendedAcl { name = name.Trim() };
        acls.Add(acl);
        return acl;
    }

    public InterfaceAclBinding GetInterfaceAclBinding(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return null;
        foreach (var b in interfaceAclBindings)
        {
            if (b == null) continue;
            if (string.Equals(b.interfaceName, ifName, StringComparison.OrdinalIgnoreCase))
                return b;
        }
        return null;
    }

    public InterfaceAclBinding EnsureInterfaceAclBinding(string ifName)
    {
        var b = GetInterfaceAclBinding(ifName);
        if (b != null) return b;

        b = new InterfaceAclBinding { interfaceName = ifName };
        interfaceAclBindings.Add(b);
        return b;
    }

    public void SetInterfaceAcl(string ifName, bool inbound, string aclName)
    {
        var b = EnsureInterfaceAclBinding(ifName);
        if (inbound) b.inboundAcl = aclName;
        else b.outboundAcl = aclName;
    }

    public void ClearInterfaceAcl(string ifName, bool inbound)
    {
        var b = GetInterfaceAclBinding(ifName);
        if (b == null) return;
        if (inbound) b.inboundAcl = "";
        else b.outboundAcl = "";
    }

    public void AddStandardAclPermit(int aclNumber, string network, string wildcard)
    {
        if (standardAcls == null) standardAcls = new List<StandardAclEntry>();
        standardAcls.Add(new StandardAclEntry
        {
            aclNumber = aclNumber,
            network = network,
            wildcard = wildcard,
            permit = true
        });
    }

    public InterfaceNatBinding GetInterfaceNatBinding(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return null;
        foreach (var b in interfaceNatBindings)
        {
            if (b == null) continue;
            if (string.Equals(b.interfaceName, ifName, StringComparison.OrdinalIgnoreCase))
                return b;
        }
        return null;
    }

    public InterfaceNatBinding EnsureInterfaceNatBinding(string ifName)
    {
        var b = GetInterfaceNatBinding(ifName);
        if (b != null) return b;

        b = new InterfaceNatBinding { interfaceName = ifName };
        interfaceNatBindings.Add(b);
        return b;
    }

    public void SetNatInside(string ifName, bool value)
    {
        var b = EnsureInterfaceNatBinding(ifName);
        b.natInside = value;
        if (value) b.natOutside = false;
    }

    public void SetNatOutside(string ifName, bool value)
    {
        var b = EnsureInterfaceNatBinding(ifName);
        b.natOutside = value;
        if (value) b.natInside = false;
    }

    private List<RouterModuleSlot> GetModuleSlotsSorted()
    {
        var slots = new List<RouterModuleSlot>(GetComponentsInChildren<RouterModuleSlot>(true));
        slots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
        return slots;
    }

    public void InitializeModuleSlots()
    {
        var slots = GetModuleSlotsSorted();
        foreach (var s in slots)
        {
            if (s == null) continue;
            s.ClearRuntimeLists();

            var parent = s.GetPortParent();
            var existingPorts = parent.GetComponentsInChildren<Port>(true);
            foreach (var ep in existingPorts)
            {
                if (ep != null && ep.transform.parent == parent)
                    Destroy(ep.gameObject);
            }

            if (s.installedModule != RouterModuleType.None && s.installedModule != RouterModuleType.WIC_COVER)
                InstallModule(s, s.installedModule, force: true);
        }
    }

    private static bool IsModuleCompatible(RouterModuleSlotType slotType, RouterModuleType moduleType)
    {
        if (moduleType == RouterModuleType.None) return true;

        return slotType switch
        {
            RouterModuleSlotType.WIC =>
                moduleType == RouterModuleType.WIC_COVER ||
                moduleType == RouterModuleType.WIC_1T ||
                moduleType == RouterModuleType.WIC_2T,

            RouterModuleSlotType.HWIC =>
                moduleType == RouterModuleType.HWIC_2T ||
                moduleType == RouterModuleType.HWIC_4ESW ||
                moduleType == RouterModuleType.HWIC_1GE_SFP ||
                moduleType == RouterModuleType.HWIC_WLAN_AP,

            RouterModuleSlotType.EHWIC =>
                moduleType == RouterModuleType.EHWIC_2T,

            _ => false
        };
    }

    public bool InstallModule(RouterModuleSlot slot, RouterModuleType moduleType, bool force = false)
    {
        if (slot == null) return false;

        if (!force && IsPoweredOn)
        {
            Debug.LogWarning($"{deviceName}: Cannot install modules while powered ON. Power off first.");
            return false;
        }

        if (!IsModuleCompatible(slot.slotType, moduleType))
        {
            Debug.LogWarning($"{deviceName}: Module {moduleType} is not compatible with slot type {slot.slotType} (slot {slot.slotIndex})");
            return false;
        }

        RemoveModule(slot, force: true);
        slot.installedModule = moduleType;

        if (moduleType == RouterModuleType.None || moduleType == RouterModuleType.WIC_COVER)
        {
            RefreshProtocolStates();
            return true;
        }

        if (moduleType == RouterModuleType.WIC_1T)
        {
            SpawnSerialPorts(slot, count: 1);
            RefreshProtocolStates();
            return true;
        }

        if (moduleType == RouterModuleType.WIC_2T || moduleType == RouterModuleType.HWIC_2T || moduleType == RouterModuleType.EHWIC_2T)
        {
            SpawnSerialPorts(slot, count: 2);
            RefreshProtocolStates();
            return true;
        }

        if (moduleType == RouterModuleType.HWIC_4ESW)
        {
            SpawnFastEtherSwitchPorts(slot, count: 4);
            RefreshProtocolStates();
            return true;
        }

        if (moduleType == RouterModuleType.HWIC_1GE_SFP)
        {
            SpawnGigSfpPort(slot);
            RefreshProtocolStates();
            return true;
        }

        if (moduleType == RouterModuleType.HWIC_WLAN_AP)
        {
            SpawnWifiApModule(slot);
            RefreshProtocolStates();
            return true;
        }

        RefreshProtocolStates();
        return true;
    }

    public void RemoveModule(RouterModuleSlot slot, bool force = false)
    {
        if (slot == null) return;

        if (!force && IsPoweredOn)
        {
            Debug.LogWarning($"{deviceName}: Cannot remove modules while powered ON. Power off first.");
            return;
        }

        if (slot.spawnedObjects != null)
        {
            foreach (var obj in slot.spawnedObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }
        }

        if (slot.spawnedPorts != null)
        {
            foreach (var p in slot.spawnedPorts)
            {
                if (p == null) continue;

                if (p.connectedTo != null)
                {
                    var other = p.connectedTo;
                    p.connectedTo = null;
                    other.connectedTo = null;

                    if (p.cable != null) Destroy(p.cable.gameObject);
                    else if (other.cable != null) Destroy(other.cable.gameObject);

                    p.cable = null;
                    other.cable = null;
                }

                Destroy(p.gameObject);
            }
        }

        if (slot.spawnedInterfaceNames != null)
        {
            foreach (var n in slot.spawnedInterfaceNames)
            {
                RemoveInterfaceByName(n);
                RemoveSwitchPortByName(n);
            }
        }

        slot.spawnedPorts?.Clear();
        slot.spawnedInterfaceNames?.Clear();
        slot.spawnedObjects?.Clear();
        slot.installedModule = RouterModuleType.None;

        RefreshProtocolStates();
    }

    private void RemoveInterfaceByName(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return;
        string norm = NormalizeInterfaceName(ifName);

        for (int i = interfaces.Count - 1; i >= 0; i--)
        {
            var itf = interfaces[i];
            if (itf == null) continue;
            if (string.Equals(itf.name, norm, StringComparison.OrdinalIgnoreCase))
                interfaces.RemoveAt(i);
        }
    }

    private void RemoveSwitchPortByName(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return;
        string norm = NormalizeInterfaceName(ifName);

        for (int i = switchPorts.Count - 1; i >= 0; i--)
        {
            var sp = switchPorts[i];
            if (sp == null || string.Equals(sp.ifName, norm, StringComparison.OrdinalIgnoreCase))
                switchPorts.RemoveAt(i);
        }

        for (int i = macTable.Count - 1; i >= 0; i--)
        {
            var e = macTable[i];
            if (e == null || string.Equals(e.ifName, norm, StringComparison.OrdinalIgnoreCase))
                macTable.RemoveAt(i);
        }
    }

    private bool IsInterfaceNameInUse(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return false;

        string norm = NormalizeInterfaceName(ifName);

        if (GetInterface(norm) != null) return true;

        if (GetPortForInterface(norm) != null) return true;

        return false;
    }

    private int FindNextFreeSuffix(string prefix, int startN = 0, int maxN = 127)
    {
        startN = Mathf.Max(0, startN);
        for (int n = startN; n <= maxN; n++)
        {
            string candidate = $"{prefix}{n}";
            if (!IsInterfaceNameInUse(candidate))
                return n;
        }
        return -1;
    }

    private void SpawnSerialPorts(RouterModuleSlot slot, int count)
    {
        string prefix = $"Serial0/{slot.slotIndex}/";

        for (int p = 0; p < count; p++)
        {
            int chosen = FindNextFreeSuffix(prefix, startN: p, maxN: 127);
            if (chosen < 0) chosen = p;

            string ifName = $"{prefix}{chosen}";
            string label = $"Se0/{slot.slotIndex}/{chosen}";

            if (chosen != p)
            {
                Debug.LogWarning($"{deviceName}: Serial interface name collision detected. " +
                                 $"Auto-assigned {ifName} instead of {prefix}{p}. " +
                                 $"(Fix recommended: ensure each RouterModuleSlot has a unique slotIndex.)");
            }

            if (GetInterface(ifName) == null)
                interfaces.Add(new RouterInterface { name = ifName });

            var portObj = SpawnModulePort(slot, label, ifName, p);
            if (portObj != null)
            {
                portObj.medium = PortMedium.Serial;

                slot.spawnedPorts.Add(portObj);
                slot.spawnedInterfaceNames.Add(ifName);
            }
        }
    }

    private void SpawnFastEtherSwitchPorts(RouterModuleSlot slot, int count)
    {
        string prefix = $"FastEthernet0/{slot.slotIndex}/";

        for (int p = 0; p < count; p++)
        {
            int chosen = FindNextFreeSuffix(prefix, startN: p, maxN: 127);
            if (chosen < 0) chosen = p;

            string ifName = $"{prefix}{chosen}";
            string label = $"Fa0/{slot.slotIndex}/{chosen}";

            if (chosen != p)
            {
                Debug.LogWarning($"{deviceName}: FastEthernet interface name collision detected. " +
                                 $"Auto-assigned {ifName} instead of {prefix}{p}. " +
                                 $"(Fix recommended: ensure each RouterModuleSlot has a unique slotIndex.)");
            }

            if (GetInterface(ifName) == null)
                interfaces.Add(new RouterInterface { name = ifName });

            EnsureSwitchPort(ifName);

            var portObj = SpawnModulePort(slot, label, ifName, p);
            if (portObj != null)
            {
                portObj.medium = PortMedium.Ethernet;

                slot.spawnedPorts.Add(portObj);
                slot.spawnedInterfaceNames.Add(ifName);
            }
        }
    }

    private void SpawnGigSfpPort(RouterModuleSlot slot)
    {

        string prefix = $"GigabitEthernet0/{slot.slotIndex}/";

        int chosen = FindNextFreeSuffix(prefix, startN: 0, maxN: 127);
        if (chosen < 0) chosen = 0;

        string ifName = $"{prefix}{chosen}";
        string label = $"Gi0/{slot.slotIndex}/{chosen}";

        if (chosen != 0)
        {
            Debug.LogWarning($"{deviceName}: GigabitEthernet SFP interface name collision detected. " +
                             $"Auto-assigned {ifName} instead of {prefix}0. " +
                             $"(Fix recommended: ensure each RouterModuleSlot has a unique slotIndex.)");
        }

        if (GetInterface(ifName) == null)
            interfaces.Add(new RouterInterface { name = ifName });

        var portObj = SpawnModulePort(slot, label, ifName, 0);
        if (portObj != null)
        {
            portObj.medium = PortMedium.Ethernet;

            slot.spawnedPorts.Add(portObj);
            slot.spawnedInterfaceNames.Add(ifName);
        }
    }

    private void SpawnWifiApModule(RouterModuleSlot slot)
    {
        Transform parent = slot.GetPortParent();

        GameObject apGO = new GameObject($"HWIC_WLAN_AP_{slot.slotIndex}");
        apGO.transform.SetParent(parent, false);
        apGO.transform.localPosition = new Vector3(0f, 0.03f, 0f);

        var ap = apGO.AddComponent<AccessPointDevice>();
        ap.deviceName = $"{deviceName}-AP{slot.slotIndex}";
        ap.ssid = $"{deviceName}_WIFI";
        ap.band = WifiBand.Band5GHz;
        ap.rangeMeters = 20f;
        ap.requireLineOfSight = false;

        ap.ports = new List<SwitchPortConfig>()
        {
            new SwitchPortConfig { name = "GigabitEthernet0/1", adminUp = true }
        };

        GameObject uplink = new GameObject("Gi0/1");
        uplink.transform.SetParent(apGO.transform, false);
        uplink.transform.localPosition = Vector3.zero;

        var p = uplink.AddComponent<Port>();
        p.owner = ap;
        p.medium = PortMedium.Ethernet;
        p.portName = "Gi0/1";
        p.interfaceName = "GigabitEthernet0/1";

        var link = apGO.AddComponent<RouterHostedDevicePowerLink>();
        link.hostRouter = this;
        link.hostedDevice = ap;

        ap.SetPower(IsPoweredOn);

        if (slot.spawnedObjects == null) slot.spawnedObjects = new List<GameObject>();
        slot.spawnedObjects.Add(apGO);
    }

    private Port SpawnModulePort(RouterModuleSlot slot, string portName, string interfaceName, int indexWithinModule)
    {
        if (slot == null) return null;

        Transform parent = slot.GetPortParent();
        GameObject prefab = slot.portPrefab != null ? slot.portPrefab : defaultPortPrefab;

        GameObject go;
        if (prefab != null) go = Instantiate(prefab, parent);
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
        }

        go.name = portName;
        go.transform.localPosition = new Vector3(indexWithinModule * slot.portSpacing, 0f, 0f);
        go.transform.localRotation = Quaternion.identity;

        var port = go.GetComponent<Port>();
        if (port == null) port = go.AddComponent<Port>();

        port.portName = portName;
        port.interfaceName = interfaceName;
        port.owner = this;

        return port;
    }

    private static bool IsInSubnet(uint ip, uint net, uint mask)
    {
        return (ip & mask) == (net & mask);
    }

    private string GetInterfaceIp(string ifName)
    {
        if (string.IsNullOrWhiteSpace(ifName)) return "unassigned";

        string norm = NormalizeInterfaceName(ifName);
        var intf = interfaces?.Find(i => NormalizeInterfaceName(i.name) == norm);

        if (intf == null) return "unassigned";
        if (string.IsNullOrWhiteSpace(intf.ipAddress)) return "unassigned";

        return intf.ipAddress;
    }

    public static bool TryParseIPv4(string ip, out uint value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(ip)) return false;

        var parts = ip.Trim().Split('.');
        if (parts.Length != 4) return false;

        uint result = 0;
        for (int i = 0; i < 4; i++)
        {
            if (!byte.TryParse(parts[i], out byte b)) return false;
            result = (result << 8) | b;
        }

        value = result;
        return true;
    }

    public bool TryDhcpRequestOffer(string clientMac, out DhcpOffer offer)
    {
        offer = default;

        if (!TryDhcpRequest(clientMac, out DhcpLease lease) || lease == null)
            return false;

        DhcpPool pool = null;
        if (!string.IsNullOrWhiteSpace(lease.poolName))
            pool = GetDhcpPool(lease.poolName);

        if (pool == null)
        {
            foreach (var p in dhcpPools)
            {
                if (p == null) continue;
                p.EnsureRuntime();
                if (p.leasesByMac != null && p.leasesByMac.TryGetValue(clientMac.Trim().ToUpperInvariant(), out var l) && l == lease)
                {
                    pool = p;
                    break;
                }
            }
        }

        offer.ip = lease.ip;
        offer.poolName = lease.poolName;
        offer.mask = pool != null ? pool.mask : "255.255.255.0";
        offer.gateway = pool != null ? pool.defaultRouter : "0.0.0.0";
        offer.dns = pool != null ? pool.dnsServer : "0.0.0.0";
        return true;
    }

    public static string IPv4ToString(uint value)
    {
        byte b1 = (byte)((value >> 24) & 0xFF);
        byte b2 = (byte)((value >> 16) & 0xFF);
        byte b3 = (byte)((value >> 8) & 0xFF);
        byte b4 = (byte)(value & 0xFF);
        return $"{b1}.{b2}.{b3}.{b4}";
    }

    public bool TryGetBestConnectedInterface(uint dstIp, out RouterInterface best)
    {
        best = null;

        int bestPrefix = -1;

        foreach (var itf in interfaces)
        {
            if (itf == null) continue;
            if (!itf.protocolUp) continue;

            if (string.IsNullOrWhiteSpace(itf.ipAddress) || itf.ipAddress == "unassigned") continue;
            if (string.IsNullOrWhiteSpace(itf.subnetMask)) continue;

            if (!TryParseIPv4(itf.ipAddress, out uint ip)) continue;
            if (!TryParseIPv4(itf.subnetMask, out uint mask)) continue;

            uint net = ip & mask;
            if ((dstIp & mask) != net) continue;

            int prefix = CountMaskBits(mask);
            if (prefix > bestPrefix)
            {
                bestPrefix = prefix;
                best = itf;
            }
        }

        return best != null;
    }

    
    public bool AddOrUpdateStaticRoute(string network, string mask, string nextHop, string exitIf)
    {
        network = (network ?? "").Trim();
        mask = (mask ?? "").Trim();
        nextHop = (nextHop ?? "").Trim();
        exitIf = (exitIf ?? "").Trim();

        if (!TryParseIPv4(network, out _)) return false;
        if (!TryParseIPv4(mask, out _)) return false;

        bool hasNextHop = nextHop.Length > 0;
        bool hasExit = exitIf.Length > 0;

        if (!hasNextHop && !hasExit) return false;
        if (hasNextHop && !TryParseIPv4(nextHop, out _)) return false;

        if (staticRoutes == null) staticRoutes = new List<StaticRoute>();

        string normExit = hasExit ? NormalizeInterfaceName(exitIf) : "";
        foreach (var r in staticRoutes)
        {
            if (r == null) continue;
            if (string.Equals(r.network, network, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.subnetMask, mask, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((r.exitInterface ?? "").Trim(), normExit, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((r.nextHop ?? "").Trim(), nextHop, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        staticRoutes.Add(new StaticRoute
        {
            network = network,
            subnetMask = mask,
            nextHop = hasNextHop ? nextHop : "",
            exitInterface = normExit
        });

        return true;
    }

    public bool RemoveStaticRoute(string network, string mask, string nextHop, string exitIf)
    {
        if (staticRoutes == null || staticRoutes.Count == 0) return false;

        network = (network ?? "").Trim();
        mask = (mask ?? "").Trim();
        nextHop = (nextHop ?? "").Trim();
        exitIf = (exitIf ?? "").Trim();
        string normExit = exitIf.Length > 0 ? NormalizeInterfaceName(exitIf) : "";

        for (int i = staticRoutes.Count - 1; i >= 0; i--)
        {
            var r = staticRoutes[i];
            if (r == null) continue;

            if (!string.Equals((r.network ?? "").Trim(), network, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals((r.subnetMask ?? "").Trim(), mask, StringComparison.OrdinalIgnoreCase)) continue;

            bool nextHopMatch = string.Equals((r.nextHop ?? "").Trim(), nextHop, StringComparison.OrdinalIgnoreCase);
            bool exitMatch = string.Equals((r.exitInterface ?? "").Trim(), normExit, StringComparison.OrdinalIgnoreCase);

            if (nextHopMatch && exitMatch)
            {
                staticRoutes.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public bool TryRoute(uint dstIp, out RouterInterface egress, out string nextHopIp)
    {
        egress = null;
        nextHopIp = "";

        if (TryGetBestConnectedInterface(dstIp, out RouterInterface connected))
        {
            egress = connected;
            nextHopIp = ToIPv4(dstIp);
            return true;
        }

        if (staticRoutes == null || staticRoutes.Count == 0) return false;

        int bestPrefix = -1;
        StaticRoute bestRoute = null;

        foreach (var r in staticRoutes)
        {
            if (r == null) continue;
            if (!TryParseIPv4(r.network, out uint net)) continue;
            if (!TryParseIPv4(r.subnetMask, out uint mask)) continue;

            if ((dstIp & mask) != (net & mask)) continue;

            int prefix = MaskToPrefix(mask);
            if (prefix > bestPrefix)
            {
                bestPrefix = prefix;
                bestRoute = r;
            }
        }

        if (bestRoute == null) return false;

        string exitIf = (bestRoute.exitInterface ?? "").Trim();
        string nh = (bestRoute.nextHop ?? "").Trim();

        if (exitIf.Length > 0)
        {
            egress = GetInterfaceByName(exitIf);
            if (egress == null) return false;
            if (!egress.protocolUp) return false;

            if (nh.Length > 0)
            {
                nextHopIp = nh;
                return true;
            }

            nextHopIp = ToIPv4(dstIp);
            return true;
        }

        if (nh.Length > 0)
        {
            if (!TryParseIPv4(nh, out uint nhIp)) return false;

            if (!TryGetBestConnectedInterface(nhIp, out RouterInterface outIf))
                return false;

            egress = outIf;
            nextHopIp = nh;
            return true;
        }

        return false;
    }

    public string BuildShowIpRoute()
    {
        RefreshProtocolStates();

        string outp = "Codes: C - connected, S - static, L - local";

        bool hasDefault = false;
        string defaultVia = "";

        if (staticRoutes != null)
        {
            foreach (var r in staticRoutes)
            {
                if (r == null) continue;
                if (!TryParseIPv4(r.network, out uint net)) continue;
                if (!TryParseIPv4(r.subnetMask, out uint mask)) continue;
                if (MaskToPrefix(mask) != 0) continue;

                hasDefault = true;
                defaultVia = (r.nextHop ?? "").Trim();
                break;
            }
        }

        outp += hasDefault
            ? $"Gateway of last resort is {defaultVia} to network 0.0.0.0"
            : "Gateway of last resort is not set";

        var lines = new List<string>();

        foreach (var itf in interfaces)
        {
            if (itf == null) continue;
            if (!itf.protocolUp) continue;
            if (string.IsNullOrWhiteSpace(itf.ipAddress) || itf.ipAddress == "unassigned") continue;
            if (string.IsNullOrWhiteSpace(itf.subnetMask)) continue;

            if (!TryParseIPv4(itf.ipAddress, out uint ip)) continue;
            if (!TryParseIPv4(itf.subnetMask, out uint mask)) continue;

            uint net = ip & mask;
            int prefix = MaskToPrefix(mask);
            string netStr = ToIPv4(net);

            lines.Add($"C    {netStr}/{prefix} is directly connected, {itf.name}");
            lines.Add($"L    {itf.ipAddress}/32 is directly connected, {itf.name}");
        }

        if (staticRoutes != null)
        {
            foreach (var r in staticRoutes)
            {
                if (r == null) continue;
                if (!TryParseIPv4(r.network, out uint net)) continue;
                if (!TryParseIPv4(r.subnetMask, out uint mask)) continue;
                int prefix = MaskToPrefix(mask);
                string netStr = ToIPv4(net & mask);

                string nh = (r.nextHop ?? "").Trim();
                string exitIf = (r.exitInterface ?? "").Trim();

                if (nh.Length > 0)
                {
                    lines.Add($"S    {netStr}/{prefix} [1/0] via {nh}");
                }
                else if (exitIf.Length > 0)
                {
                    lines.Add($"S    {netStr}/{prefix} is directly connected, {exitIf}");
                }
            }
        }

        lines.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (var l in lines) outp += l + "";
        return outp.TrimEnd('\n', '\r');
    }

    public RouterInterface GetInterfaceByName(string ifName)
    {
        ifName = (ifName ?? "").Trim();
        if (ifName.Length == 0) return null;

        string lookup = NormalizeInterfaceName(ifName);
        if (interfaces == null) return null;
        foreach (var i in interfaces)
        {
            if (i == null) continue;
            string n = NormalizeInterfaceName(i.name);
            if (string.Equals(n, lookup, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return null;
    }
public string GetInterfacePseudoMac(string interfaceName)
    {
        interfaceName = (interfaceName ?? "").Trim();
        if (interfaceName.Length == 0) interfaceName = "?";
        return $"R:{deviceName}:{interfaceName}";
    }

    public RouterInterface GetInterfaceByIp(string ip)
    {
        if (interfaces == null) return null;
        foreach (var i in interfaces)
        {
            if (i == null) continue;
            if (string.Equals(i.ipAddress, ip, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return null;
    }

    public void ArpAddOrUpdate(string ip, string mac, string ifName)
    {
        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(mac)) return;
        if (_arp == null) _arp = new Dictionary<string, RouterArpEntry>(StringComparer.OrdinalIgnoreCase);

        if (!_arp.TryGetValue(ip, out var e) || e == null)
        {
            e = new RouterArpEntry { ip = ip, mac = mac, ifName = ifName ?? "", lastSeen = Time.time };
            _arp[ip] = e;
        }
        else
        {
            e.mac = mac;
            e.ifName = ifName ?? e.ifName;
            e.lastSeen = Time.time;
        }
    }

    public bool ArpTryGet(string ip, out RouterArpEntry entry)
    {
        entry = null;
        if (_arp == null || string.IsNullOrWhiteSpace(ip)) return false;
        return _arp.TryGetValue(ip, out entry) && entry != null && !string.IsNullOrWhiteSpace(entry.mac);
    }

    public List<RouterArpEntry> ArpGetAll()
    {
        var list = new List<RouterArpEntry>();
        if (_arp == null) return list;
        foreach (var kv in _arp)
            if (kv.Value != null) list.Add(kv.Value);
        list.Sort((a, b) => string.Compare(a.ip, b.ip, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public void ArpClear()
    {
        if (_arp == null) _arp = new Dictionary<string, RouterArpEntry>(StringComparer.OrdinalIgnoreCase);
        _arp.Clear();
    }

    public bool TryResolveArpForPc(string ip, int requiredVlan, string outIfName, out string mac)
    {
        mac = "";
        if (string.IsNullOrWhiteSpace(ip)) return false;

        if (ArpTryGet(ip, out var e))
        {
            mac = e.mac;
            return true;
        }

        var pc = FindPcByIp(ip);
        if (pc == null) return false;

        if (requiredVlan > 0)
        {
            int pcVlan = GetPcLocalVlan(pc);
            if (pcVlan > 0 && pcVlan != requiredVlan)
                return false;
        }

        mac = string.IsNullOrWhiteSpace(pc.macAddress) ? pc.pseudoMac : pc.macAddress;
        if (string.IsNullOrWhiteSpace(mac)) mac = $"PC:{pc.deviceName}";

        ArpAddOrUpdate(ip, mac, outIfName);
        return true;
    }

    private static PcDevice FindPcByIp(string ip)
    {
        var pcs = UnityEngine.Object.FindObjectsOfType<PcDevice>(true);
        foreach (var p in pcs)
            if (p != null && string.Equals(p.ipAddress, ip, StringComparison.OrdinalIgnoreCase))
                return p;
        return null;
    }

    private static int GetPcLocalVlan(PcDevice pc)
    {
        if (pc == null) return -1;
        var nic = pc.GetNicPort();
        if (nic == null || nic.connectedTo == null || nic.connectedTo.owner == null) return -1;

        var remote = nic.connectedTo;
        if (remote.owner is SwitchDevice sw)
            return DetermineIngressVlan(sw, remote.interfaceName);

        if (remote.owner is RouterDevice r && r.IsSwitchportCapable(remote.interfaceName))
            return r.DetermineIngressVlan(remote.interfaceName);

        return -1;
    }

    private static int DetermineIngressVlan(SwitchDevice sw, string ingressIf)
    {
        var pc = sw.GetEffectivePortChannelForMember(ingressIf);
        if (pc != null)
        {
            if (pc.mode == SwitchportMode.Access) return pc.accessVlan;
            return pc.trunkNativeVlan <= 0 ? 1 : pc.trunkNativeVlan;
        }

        var portCfg = sw.GetPort(ingressIf);
        if (portCfg == null) return 1;
        if (portCfg.mode == SwitchportMode.Access) return portCfg.accessVlan;
        return portCfg.trunkNativeVlan <= 0 ? 1 : portCfg.trunkNativeVlan;
    }

    private static int CountMaskBits(uint mask)
    {
        int count = 0;
        for (int i = 31; i >= 0; i--)
        {
            if (((mask >> i) & 1u) == 1u) count++;
        }
        return count;
    }

    private bool TryGetAclByName(string aclName, out ExtendedAcl acl)
    {
        acl = null;
        if (string.IsNullOrWhiteSpace(aclName)) return false;
        if (acls == null) return false;

        foreach (var a in acls)
        {
            if (a == null) continue;
            if (string.Equals(a.name, aclName, StringComparison.OrdinalIgnoreCase))
            {
                acl = a;
                return true;
            }
        }
        return false;
    }

    private static bool AclAddressMatches(string ruleAddr, string ip)
    {
        if (string.IsNullOrWhiteSpace(ruleAddr) || string.Equals(ruleAddr, "any", StringComparison.OrdinalIgnoreCase))
            return true;

        ruleAddr = ruleAddr.Trim();

        if (ruleAddr.StartsWith("host ", StringComparison.OrdinalIgnoreCase))
        {
            string hostIp = ruleAddr.Substring(5).Trim();
            return string.Equals(hostIp, ip, StringComparison.OrdinalIgnoreCase);
        }

        if (TryParseIPv4(ruleAddr, out _))
            return string.Equals(ruleAddr, ip, StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private static bool AclProtocolMatches(AclProtocol ruleProto, AclProtocol pktProto)
    {
        if (ruleProto == AclProtocol.Ip) return true;
        return ruleProto == pktProto;
    }

    private bool EvaluateAcl(string aclName, AclProtocol pktProto, string srcIp, string dstIp, int dstPort, out string hit)
    {
        hit = null;

        if (!TryGetAclByName(aclName, out var acl) || acl == null || acl.rules == null)
        {

            return true;
        }

        foreach (var r in acl.rules)
        {
            if (r == null) continue;

            if (!AclProtocolMatches(r.protocol, pktProto)) continue;
            if (!AclAddressMatches(r.src, srcIp)) continue;
            if (!AclAddressMatches(r.dst, dstIp)) continue;

            if ((pktProto == AclProtocol.Tcp || pktProto == AclProtocol.Udp) && r.dstPort >= 0)
            {
                if (dstPort != r.dstPort) continue;
            }

            hit = r.ToString();
            return r.action == AclAction.Permit;
        }

        hit = "(implicit deny)";
        return false;
    }

    private bool TryGetInterfaceAclBinding(string ifName, out InterfaceAclBinding bind)
    {
        bind = null;
        if (string.IsNullOrWhiteSpace(ifName) || interfaceAclBindings == null) return false;

        string norm = NormalizeInterfaceName(ifName);

        foreach (var b in interfaceAclBindings)
        {
            if (b == null) continue;
            if (string.Equals(NormalizeInterfaceName(b.interfaceName), norm, StringComparison.OrdinalIgnoreCase))
            {
                bind = b;
                return true;
            }
        }
        return false;
    }

    public bool AclPermitsRoutedTraffic(AclProtocol proto, string srcIp, string dstIp, int dstPort, string ingressIf, string egressIf, out string reason)
    {
        reason = null;

        if (TryGetInterfaceAclBinding(ingressIf, out var inBind) && inBind != null && !string.IsNullOrWhiteSpace(inBind.inboundAcl))
        {
            bool ok = EvaluateAcl(inBind.inboundAcl, proto, srcIp, dstIp, dstPort, out var hit);
            if (!ok)
            {
                reason = $"Blocked by ACL {inBind.inboundAcl} (in) on {ingressIf}. Rule: {hit}";
                return false;
            }
        }

        if (TryGetInterfaceAclBinding(egressIf, out var outBind) && outBind != null && !string.IsNullOrWhiteSpace(outBind.outboundAcl))
        {
            bool ok = EvaluateAcl(outBind.outboundAcl, proto, srcIp, dstIp, dstPort, out var hit);
            if (!ok)
            {
                reason = $"Blocked by ACL {outBind.outboundAcl} (out) on {egressIf}. Rule: {hit}";
                return false;
            }
        }

        return true;
    }

    public bool NatIsInside(string ifName)
    {
        string norm = NormalizeInterfaceName(ifName);
        if (interfaceNatBindings == null) return false;
        foreach (var b in interfaceNatBindings)
        {
            if (b == null) continue;
            if (string.Equals(NormalizeInterfaceName(b.interfaceName), norm, StringComparison.OrdinalIgnoreCase))
                return b.natInside;
        }
        return false;
    }

    public bool NatIsOutside(string ifName)
    {
        string norm = NormalizeInterfaceName(ifName);
        if (interfaceNatBindings == null) return false;
        foreach (var b in interfaceNatBindings)
        {
            if (b == null) continue;
            if (string.Equals(NormalizeInterfaceName(b.interfaceName), norm, StringComparison.OrdinalIgnoreCase))
                return b.natOutside;
        }
        return false;
    }

    public bool NatAclPermitsIp(int aclNumber, string ip)
    {
        if (standardAcls == null) return false;
        if (!TryParseIPv4(ip, out uint ipU)) return false;

        foreach (var e in standardAcls)
        {
            if (e == null) continue;
            if (e.aclNumber != aclNumber) continue;
            if (!TryParseIPv4(e.network, out uint net)) continue;
            if (!TryParseIPv4(e.wildcard, out uint wc)) continue;

            uint mask = ~wc;
            bool match = (ipU & mask) == (net & mask);
            if (match) return e.permit;
        }

        return false;
    }

    public void NatClearTranslations()
    {
        if (natTranslations == null) natTranslations = new List<NatTranslation>();
        natTranslations.Clear();
    }

    private int NatAllocateEphemeralPort(string protocol, string outsideIfIp)
    {

        var used = new HashSet<int>();
        if (natTranslations != null)
        {
            foreach (var t in natTranslations)
            {
                if (t == null) continue;
                if (!string.Equals(t.protocol, protocol, StringComparison.OrdinalIgnoreCase)) continue;
                if (t.insideGlobal == null) continue;

                var parts = t.insideGlobal.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int p))
                    used.Add(p);
            }
        }

        for (int p = 10000; p <= 60000; p++)
        {
            if (!used.Contains(p)) return p;
        }
        return UnityEngine.Random.Range(10000, 60000);
    }

    public bool TryApplySourceNatForOutbound(
        string protocol,
        string ingressIf,
        string egressIf,
        string insideLocalIp,
        int insideLocalPort,
        string outsideGlobalIp,
        int outsideGlobalPort,
        out string insideGlobalOut)
    {
        insideGlobalOut = insideLocalIp;

        if (natRule == null) return false;

        string inIf = NormalizeInterfaceName(ingressIf);
        string egIf = NormalizeInterfaceName(egressIf);

        if (!NatIsInside(inIf) || !NatIsOutside(egIf)) return false;

        if (!NatAclPermitsIp(natRule.aclNumber, insideLocalIp)) return false;

        string outsideIfIp = GetInterfaceIp(natRule.outsideInterfaceName);
        if (string.IsNullOrWhiteSpace(outsideIfIp) || outsideIfIp == "unassigned")
            return false;

if (natTranslations == null) natTranslations = new List<NatTranslation>();

        string il = (insideLocalPort > 0) ? $"{insideLocalIp}:{insideLocalPort}" : insideLocalIp;
        string og = (outsideGlobalPort > 0) ? $"{outsideGlobalIp}:{outsideGlobalPort}" : outsideGlobalIp;

        foreach (var t in natTranslations)
        {
            if (t == null) continue;
            if (!string.Equals(t.protocol, protocol, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(t.insideLocal, il, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(t.outsideGlobal, og, StringComparison.OrdinalIgnoreCase)) continue;

            t.lastUsedUtc = DateTime.UtcNow;
            insideGlobalOut = t.insideGlobal;
            return true;
        }

        int igPort = (insideLocalPort > 0) ? NatAllocateEphemeralPort(protocol, outsideIfIp) : 0;
        string ig = (igPort > 0) ? $"{outsideIfIp}:{igPort}" : outsideIfIp;

        natTranslations.Add(new NatTranslation
        {
            protocol = protocol.ToLowerInvariant(),
            insideLocal = il,
            insideGlobal = ig,
            outsideGlobal = og,
            outsideLocal = og,
            createdUtc = DateTime.UtcNow,
            lastUsedUtc = DateTime.UtcNow
        });

        insideGlobalOut = ig;
        return true;
    }



public static string ToIPv4(uint value)
{
    return $"{(value >> 24) & 255}.{(value >> 16) & 255}.{(value >> 8) & 255}.{value & 255}";
}

public static int MaskToPrefix(uint mask)
{
    int p = 0;
    for (int i = 31; i >= 0; i--)
    {
        if (((mask >> i) & 1) == 1) p++;
        else break;
    }
    uint expected = p == 0 ? 0u : (uint)(0xFFFFFFFFu << (32 - p));
    if (mask != expected) return -1;
    return p;
}

}