using System;
using System.Collections.Generic;
using UnityEngine;

public class PcDevice : Device
{
    [Header("PC Network Config")]
    public string ipAddress = "0.0.0.0";
    public string subnetMask = "255.255.255.0";
    public string defaultGateway = "0.0.0.0";

    [Header("Optional")]
    public string dnsServer = "0.0.0.0";

    [Header("DHCP")]
    public bool dhcpEnabled = true;
    public string lastDhcpLease = "";

    [Header("Layer 2")]
    [Tooltip("Used by PcSession and switching logic. Must be unique per PC.")]
    public string macAddress = "PC-00";

    [Tooltip("Compatibility alias. If set, macAddress will mirror this when empty.")]
    public string pseudoMac = "";

    [System.Serializable]
    public class ArpEntry
    {
        public string ip;
        public string mac;
        public float lastSeen;
    }

    [NonSerialized] private Dictionary<string, ArpEntry> _arp = new Dictionary<string, ArpEntry>(StringComparer.OrdinalIgnoreCase);

    protected override void Awake()
    {
        base.Awake();
        if (string.IsNullOrWhiteSpace(macAddress) && !string.IsNullOrWhiteSpace(pseudoMac))
            macAddress = pseudoMac;

        if (string.IsNullOrWhiteSpace(pseudoMac) && !string.IsNullOrWhiteSpace(macAddress))
            pseudoMac = macAddress;

        if (_arp == null)
            _arp = new Dictionary<string, ArpEntry>(StringComparer.OrdinalIgnoreCase);
    }

    public void ArpAddOrUpdate(string ip, string mac)
    {
        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(mac)) return;
        if (_arp == null) _arp = new Dictionary<string, ArpEntry>(StringComparer.OrdinalIgnoreCase);

        if (!_arp.TryGetValue(ip, out var e) || e == null)
        {
            e = new ArpEntry { ip = ip, mac = mac, lastSeen = Time.time };
            _arp[ip] = e;
        }
        else
        {
            e.mac = mac;
            e.lastSeen = Time.time;
        }
    }

    public bool ArpTryGet(string ip, out string mac)
    {
        mac = "";
        if (_arp == null || string.IsNullOrWhiteSpace(ip)) return false;
        if (_arp.TryGetValue(ip, out var e) && e != null && !string.IsNullOrWhiteSpace(e.mac))
        {
            mac = e.mac;
            return true;
        }
        return false;
    }

    public List<ArpEntry> ArpGetAll()
    {
        var list = new List<ArpEntry>();
        if (_arp == null) return list;

        foreach (var kv in _arp)
            if (kv.Value != null) list.Add(kv.Value);

        list.Sort((a, b) => string.Compare(a.ip, b.ip, System.StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public void ArpClear()
    {
        if (_arp == null) _arp = new Dictionary<string, ArpEntry>(StringComparer.OrdinalIgnoreCase);
        _arp.Clear();
    }

    public Port GetNicPort()
    {
        var ports = GetComponentsInChildren<Port>(true);

        Port anyEthernet = null;
        Port connectedWireless = null;

        foreach (var p in ports)
        {
            if (p == null) continue;

            if (p.medium == PortMedium.Ethernet)
            {
                if (p.IsConnected) return p;      // best case
                if (anyEthernet == null) anyEthernet = p;
            }
            else if (p.medium == PortMedium.Wireless)
            {
                if (p.IsConnected) connectedWireless = p;
            }
        }

        if (connectedWireless != null) return connectedWireless;

        if (anyEthernet != null) return anyEthernet;

        foreach (var p in ports)
        {
            if (p == null) continue;
            string ifn = (p.interfaceName ?? "").ToLowerInvariant();
            string pn = (p.portName ?? "").ToLowerInvariant();
            if (ifn.Contains("ethernet") || pn.Contains("ethernet"))
                return p;
        }

        return null;
    }

    public Port GetRs232Port()
    {
        var ports = GetComponentsInChildren<Port>(true);
        foreach (var p in ports)
        {
            if (p == null) continue;

            if (p.medium == PortMedium.RS232)
                return p;

            string ifn = (p.interfaceName ?? "").ToLowerInvariant();
            string pn = (p.portName ?? "").ToLowerInvariant();
            if (ifn.Contains("rs232") || pn.Contains("rs232") || pn.Contains("com"))
                return p;
        }
        return null;
    }

    public Port GetConsolePort()
    {
        return GetRs232Port();
    }

    public bool HasLink()
    {
        var p = GetNicPort();
        return p != null && p.IsConnected;
    }
}
