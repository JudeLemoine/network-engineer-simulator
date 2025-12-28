using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class PcSession : ITerminalSession
{
    private readonly PcDevice _pc;

    private bool _inConsole = false;
    private ITerminalSession _consoleSession = null;

    public PcSession(PcDevice pc) { _pc = pc; }

    public string Prompt
    {
        get
        {
            if (_inConsole && _consoleSession != null)
                return _consoleSession.Prompt;

            return _pc != null ? $"{_pc.deviceName}> " : "PC> ";
        }
    }

    public string Execute(string input)
    {
        if (_pc == null) return "";

        input = (input ?? "").Trim();

        if (_inConsole && _consoleSession != null)
        {
            string result = _consoleSession.Execute(input);
            if (result == "logout")
            {
                _inConsole = false;
                _consoleSession = null;
                return "Connection closed.";
            }
            return result;
        }

        if (string.IsNullOrWhiteSpace(input)) return "";

        if (input.Equals("terminal", StringComparison.OrdinalIgnoreCase))
            return TryOpenConsole();

        if (input.Equals("wifi", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("wifi help", StringComparison.OrdinalIgnoreCase))
            return BuildWifiHelp();

        if (input.Equals("wifi status", StringComparison.OrdinalIgnoreCase))
            return WifiStatus();

        if (input.Equals("wifi scan", StringComparison.OrdinalIgnoreCase))
            return WifiScan();

        if (input.StartsWith("wifi connect ", StringComparison.OrdinalIgnoreCase))
        {

            string args = input.Substring("wifi connect ".Length).Trim();
            if (string.IsNullOrWhiteSpace(args))
                return "Usage: wifi connect <ssid> [password]";

            string ssid;
            string pass = "";

            var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            ssid = parts[0].Trim();
            if (parts.Length == 2) pass = parts[1].Trim();

            return WifiConnect(ssid, pass);
        }

        if (input.StartsWith("wifi setkey ", StringComparison.OrdinalIgnoreCase))
        {
            string key = input.Substring("wifi setkey ".Length).Trim();
            return WifiSetKey(key);
        }

        if (input.Equals("wifi forget", StringComparison.OrdinalIgnoreCase))
            return WifiForget();

        if (input.Equals("wifi disconnect", StringComparison.OrdinalIgnoreCase))
            return WifiDisconnect();

        if (input.Equals("set ip dhcp", StringComparison.OrdinalIgnoreCase))
        {
            return DhcpRenew();
        }
if (input.StartsWith("set ip ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return "Usage: set ip <ip> <mask> [gateway]";
            _pc.ipAddress = parts[2];
            _pc.subnetMask = parts[3];
            if (parts.Length >= 5) _pc.defaultGateway = parts[4];
            return "OK";
        }

        if (input.Equals("ipconfig /renew", StringComparison.OrdinalIgnoreCase))
            return DhcpRenew();

        if (input.Equals("ipconfig /release", StringComparison.OrdinalIgnoreCase))
            return DhcpRelease();

if (input.Equals("ipconfig", StringComparison.OrdinalIgnoreCase))
        {
            string linkEth = "Disconnected";
            string linkWifi = "Disconnected";

            var nic = _pc.GetNicPort();
            string active = "None";
            if (nic != null)
            {
                if (nic.medium == PortMedium.Ethernet) active = "Ethernet";
                else if (nic.medium == PortMedium.Wireless) active = "Wireless";
                else active = nic.medium.ToString();
            }

            var ports = _pc.GetComponentsInChildren<Port>(true);
            foreach (var p in ports)
            {
                if (p == null) continue;
                if (p.medium == PortMedium.Ethernet)
                    linkEth = (p.connectedTo != null) ? "Connected" : "Disconnected";
                else if (p.medium == PortMedium.Wireless)
                    linkWifi = (p.connectedTo != null) ? "Connected" : "Disconnected";
            }

            string con = (_pc.GetConsolePort() != null && _pc.GetConsolePort().connectedTo != null) ? "Connected" : "Disconnected";

            return
                $"   IP Address. . . . . . . . . . . . : {_pc.ipAddress}\n" +
                $"   Subnet Mask . . . . . . . . . . . : {_pc.subnetMask}\n" +
                $"   Default Gateway . . . . . . . . . : {_pc.defaultGateway}\n" +
                $"   DNS Server . . . . . . . . . . . .: {_pc.dnsServer}\n" +
                $"   Link (Ethernet). . . . . . . . . . : {linkEth}\n" +
                $"   Link (Wireless). . . . . . . . . . : {linkWifi}\n" +
                $"   Link (Console) . . . . . . . . . . : {con}\n" +
                $"   Active NIC . . . . . . . . . . . . : {active}";
        }

        if (input.Equals("arp -a", StringComparison.OrdinalIgnoreCase))
        {
            var entries = _pc.ArpGetAll();
            if (entries.Count == 0)
                return "No ARP Entries Found.";

            var sb = new StringBuilder();
            sb.AppendLine($"Interface: {_pc.ipAddress} --- 0x1");
            sb.AppendLine("  Internet Address      Physical Address      Type");

            foreach (var e in entries)
            {
                string mac = (e.mac ?? "").Replace(':', '-');
                sb.AppendLine($"  {e.ip,-20} {mac,-20} dynamic");
            }

            return sb.ToString().TrimEnd();
        }

        if (input.Equals("arp -d", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("arp -d *", StringComparison.OrdinalIgnoreCase))
        {
            _pc.ArpClear();
            return "ARP cache cleared.";
        }

        if (input.StartsWith("ping ", StringComparison.OrdinalIgnoreCase))
        {
            string target = input.Substring("ping ".Length).Trim();
            return DoPing(target);
        }

        if (input.StartsWith("telnet ", StringComparison.OrdinalIgnoreCase))
        {
            var p = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 3) return "Usage: telnet <ip> <port>";
            string ip = p[1];
            if (!int.TryParse(p[2], out int port) || port <= 0 || port > 65535) return "Invalid port.";
            return DoTelnet(ip, port);
        }

        if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Commands:\n" +
                "  ipconfig\n" +
                "  set ip <ip> <mask> [gw]\n" +
                "  ping <ip>\n" +
                "  arp -a\n" +
                "  arp -d *\n" +
                "  wifi help\n" +
                "  terminal   (open console cable session)\n";
        }

        return "Unknown command. Type 'help'.";
    }

    private string TryOpenConsole()
    {
        var con = _pc.GetConsolePort();
        if (con == null) return "No console port on this PC.";

        if (!con.IsConnected || con.connectedTo == null || con.connectedTo.owner == null)
            return "No console cable connected.";

        var remotePort = con.connectedTo;

        if (remotePort.owner is RouterDevice r)
        {
            _consoleSession = new ConsoleRouterSession(r);
            _inConsole = true;
            return "Connected to router console. (Type 'exit' at router prompt to close)";
        }

        if (remotePort.owner is SwitchDevice)
            return "Switch console not implemented yet (router console is).";

        return "Connected device has no supported console.";
    }

    private WirelessAdapter GetWifiAdapter() => _pc.GetComponentInChildren<WirelessAdapter>(true);

    private string BuildWifiHelp()
    {
        return
            "WiFi Commands:\n" +
            "  wifi scan\n" +
            "  wifi status\n" +
            "  wifi connect <ssid> [password]\n" +
            "  wifi disconnect\n" +
            "  wifi setkey <password>\n" +
            "  wifi forget\n";
    }

    private string WifiSetKey(string key)
    {
        var wa = GetWifiAdapter();
        if (wa == null)
            return "No WirelessAdapter found on this PC.\n(Add the WirelessAdapter component to the PC GameObject.)";

        if (string.IsNullOrWhiteSpace(key))
            return "Usage: wifi setkey <password>";

        wa.SetStoredKey(key);
        return "WiFi key saved.";
    }

    private string WifiForget()
    {
        var wa = GetWifiAdapter();
        if (wa == null)
            return "No WirelessAdapter found on this PC.\n(Add the WirelessAdapter component to the PC GameObject.)";

        wa.ClearStoredKey();
        return "WiFi key cleared.";
    }

    private string WifiStatus()
    {
        var wa = GetWifiAdapter();
        if (wa == null)
            return "No WirelessAdapter found on this PC.\n(Add the WirelessAdapter component to the PC GameObject.)";

        var sb = new StringBuilder();

        sb.AppendLine("Wireless Adapter Status:");
        sb.AppendLine($"  Supports 2.4 GHz : {(wa.supports2_4GHz ? "Yes" : "No")}");
        sb.AppendLine($"  Supports 5 GHz   : {(wa.supports5GHz ? "Yes" : "No")}");
        sb.AppendLine($"  Auto-Join        : {(wa.autoJoin ? "On" : "Off")}");
        sb.AppendLine($"  Desired SSID     : {(string.IsNullOrWhiteSpace(wa.desiredSsid) ? "(any)" : wa.desiredSsid)}");
        sb.AppendLine($"  Stored Key Set   : {(!string.IsNullOrWhiteSpace(wa.storedKey) ? "Yes" : "No")}");

        if (!wa.isAssociated || wa.currentAp == null)
        {
            sb.AppendLine();
            sb.AppendLine("  Connected        : No");
            return sb.ToString().TrimEnd();
        }

        float dist = Vector3.Distance(wa.transform.position, wa.currentAp.transform.position);
        float range = Mathf.Max(0.001f, wa.currentAp.rangeMeters);
        float signalPct = Mathf.Clamp01(1f - (dist / range)) * 100f;

        string band = (wa.currentAp.band == WifiBand.Band2_4GHz) ? "2.4 GHz" : "5 GHz";
        string sec = (wa.currentAp.securityMode == WifiSecurityMode.Open) ? "OPEN" : "WPA2-PSK";

        sb.AppendLine();
        sb.AppendLine("  Connected        : Yes");
        sb.AppendLine($"  SSID             : {wa.currentAp.ssid}");
        sb.AppendLine($"  Band             : {band}");
        sb.AppendLine($"  Security         : {sec}");
        sb.AppendLine($"  AP Device        : {wa.currentAp.deviceName}");
        sb.AppendLine($"  Distance         : {dist:0.0} m");
        sb.AppendLine($"  Signal           : {signalPct:0}%");
        sb.AppendLine($"  In Range         : {(wa.currentAp.CanSeeAdapter(wa) ? "Yes" : "No")}");

        var ports = _pc.GetComponentsInChildren<Port>(true);
        foreach (var p in ports)
        {
            if (p != null && p.medium == PortMedium.Wireless)
            {
                sb.AppendLine($"  Link (Wireless0) : {(p.connectedTo != null ? "Connected" : "Disconnected")}");
                break;
            }
        }

        return sb.ToString().TrimEnd();
    }

    private string WifiScan()
    {
        var wa = GetWifiAdapter();
        if (wa == null)
            return "No WirelessAdapter found on this PC.\n(Add the WirelessAdapter component to the PC GameObject.)";

        var aps = UnityEngine.Object.FindObjectsOfType<AccessPointDevice>(true);
        var list = new List<(AccessPointDevice ap, float dist, bool visible, bool supported)>();

        foreach (var ap in aps)
        {
            if (ap == null) continue;

            float dist = Vector3.Distance(wa.transform.position, ap.transform.position);
            bool visible = ap.IsPoweredOn && ap.CanSeeAdapter(wa);

            bool supported =
                (ap.band == WifiBand.Band2_4GHz && wa.supports2_4GHz) ||
                (ap.band == WifiBand.Band5GHz && wa.supports5GHz);

            list.Add((ap, dist, visible, supported));
        }

        list.Sort((a, b) => a.dist.CompareTo(b.dist));

        var sb = new StringBuilder();
        sb.AppendLine("Scanning for WiFi networks...");

        int shown = 0;
        foreach (var item in list)
        {
            string band = (item.ap.band == WifiBand.Band2_4GHz) ? "2.4 GHz" : "5 GHz";
            string sec = (item.ap.securityMode == WifiSecurityMode.Open) ? "OPEN" : "WPA2";

            string state;
            if (!item.ap.IsPoweredOn) state = "OFF";
            else if (!item.visible) state = "OUT OF RANGE";
            else state = "IN RANGE";

            string support = item.supported ? "OK" : "UNSUPPORTED";

            float range = Mathf.Max(0.001f, item.ap.rangeMeters);
            float signalPct = Mathf.Clamp01(1f - (item.dist / range)) * 100f;

            sb.AppendLine($"  SSID: {item.ap.ssid,-18} Sec: {sec,-4} Band: {band,-6} Dist: {item.dist,5:0.0}m  Sig: {signalPct,3:0}%  {state,-12} {support}");
            shown++;
            if (shown >= 25) break;
        }

        if (shown == 0)
            sb.AppendLine("  (No access points found in scene)");

        sb.AppendLine();
        sb.AppendLine("Tip: wifi connect <ssid> [password]   |   wifi setkey <password>");

        return sb.ToString().TrimEnd();
    }

    private string WifiConnect(string ssid, string password)
    {
        var wa = GetWifiAdapter();
        if (wa == null)
            return "No WirelessAdapter found on this PC.\n(Add the WirelessAdapter component to the PC GameObject.)";

        if (string.IsNullOrWhiteSpace(ssid))
            return "Usage: wifi connect <ssid> [password]";

        AccessPointDevice best = null;
        float bestDist = float.MaxValue;

        var aps = UnityEngine.Object.FindObjectsOfType<AccessPointDevice>(true);
        foreach (var ap in aps)
        {
            if (ap == null) continue;
            if (!ap.IsPoweredOn) continue;
            if (!string.Equals(ap.ssid, ssid, StringComparison.OrdinalIgnoreCase)) continue;

            bool supported =
                (ap.band == WifiBand.Band2_4GHz && wa.supports2_4GHz) ||
                (ap.band == WifiBand.Band5GHz && wa.supports5GHz);

            if (!supported) continue;
            if (!ap.CanSeeAdapter(wa)) continue;

            float d = Vector3.Distance(wa.transform.position, ap.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = ap;
            }
        }

        if (best == null)
            return $"No reachable AP found for SSID '{ssid}'.\nRun: wifi scan";

        string keyUsed = (password ?? "").Trim();
        if (best.RequiresKey && string.IsNullOrWhiteSpace(keyUsed))
            keyUsed = (wa.storedKey ?? "").Trim();

        if (best.RequiresKey && string.IsNullOrWhiteSpace(keyUsed))
            return $"Network '{ssid}' requires a password.\nUse: wifi connect {ssid} <password>  OR  wifi setkey <password>";

        bool ok = wa.Connect(best, keyUsed);
        if (!ok)
        {
            if (best.RequiresKey) return $"Failed to connect to '{ssid}': Wrong password (WPA2-PSK).";
            return $"Failed to connect to '{ssid}'. (Band unsupported or out of range)";
        }

        string band = (best.band == WifiBand.Band2_4GHz) ? "2.4 GHz" : "5 GHz";
        string sec = (best.securityMode == WifiSecurityMode.Open) ? "OPEN" : "WPA2-PSK";
        return $"Connected to '{best.ssid}' ({band}, {sec}) via {best.deviceName}.";
    }

    private string WifiDisconnect()
    {
        var wa = GetWifiAdapter();
        if (wa == null)
            return "No WirelessAdapter found on this PC.\n(Add the WirelessAdapter component to the PC GameObject.)";

        if (!wa.isAssociated || wa.currentAp == null)
            return "Not connected.";

        string ssid = wa.currentAp.ssid;
        wa.Disconnect();
        return $"Disconnected from '{ssid}'.";
    }

    private string DoTelnet(string targetIp, int port)
    {
        if (_pc == null) return $"Telnet to {targetIp}:{port} failed.";

        var myPort = _pc.GetNicPort();
        if (myPort == null || myPort.connectedTo == null) return $"Telnet to {targetIp}:{port} failed.";

        if (!TryParseIPv4(_pc.ipAddress, out uint myIp)) return $"Telnet to {targetIp}:{port} failed.";
        if (!TryParseIPv4(_pc.subnetMask, out uint myMask)) return $"Telnet to {targetIp}:{port} failed.";
        if (!TryParseIPv4(targetIp, out uint dstIp)) return $"Telnet to {targetIp}:{port} failed.";

        PcDevice dstPc = FindPcByIp(targetIp);
        if (dstPc == null) return $"Telnet to {targetIp}:{port} failed (host unreachable).";

        if (IsSameSubnet(myIp, dstIp, myMask))
        {
            if (!CanReachDeviceAtL2(myPort, dstPc))
                return $"Telnet to {targetIp}:{port} failed (host unreachable).";

            var sh = dstPc.GetComponent<ServiceHost>();
            if (sh == null || !sh.IsOpen(ServiceHost.ServiceProtocol.Tcp, port, out var svc))
                return $"Telnet to {targetIp}:{port} failed (connection refused).";

            string banner = (svc != null && !string.IsNullOrWhiteSpace(svc.banner)) ? $"\n{svc.banner}" : "";
            return $"Connected to {targetIp} on port {port}.{banner}";
        }

        if (!TryParseIPv4(_pc.defaultGateway, out uint gwIp))
            return $"Telnet to {targetIp}:{port} failed (no default gateway).";

        if (!IsSameSubnet(myIp, gwIp, myMask))
            return $"Telnet to {targetIp}:{port} failed (default gateway unreachable).";

        RouterDevice gwRouter = FindRouterByInterfaceIp(_pc.defaultGateway, myMask, myIp);
        if (gwRouter == null) return $"Telnet to {targetIp}:{port} failed (no gateway router).";

        if (!CanReachDeviceAtL2(myPort, gwRouter))
            return $"Telnet to {targetIp}:{port} failed (gateway unreachable).";

        if (!gwRouter.TryGetBestConnectedInterface(dstIp, out RouterInterface egress))
            return $"Telnet to {targetIp}:{port} failed (no route).";

        if (!TryParseIPv4(egress.ipAddress, out uint egIp) || !TryParseIPv4(egress.subnetMask, out uint egMask))
            return $"Telnet to {targetIp}:{port} failed (no route).";

        if (!IsSameSubnet(dstIp, egIp, egMask))
            return $"Telnet to {targetIp}:{port} failed (no route).";

        var gwInIf = gwRouter.GetInterfaceByIp(_pc.defaultGateway);
        string gwInIfName = (gwInIf != null) ? gwInIf.name : "unknown";
        gwRouter.TryApplySourceNatForOutbound(
            protocol: "tcp",
            ingressIf: gwInIfName,
            egressIf: egress.name,
            insideLocalIp: _pc.ipAddress,
            insideLocalPort: UnityEngine.Random.Range(1024, 65535),
            outsideGlobalIp: targetIp,
            outsideGlobalPort: port,
            insideGlobalOut: out _);

        var sh2 = dstPc.GetComponent<ServiceHost>();
        if (sh2 == null || !sh2.IsOpen(ServiceHost.ServiceProtocol.Tcp, port, out var svc2))
            return $"Telnet to {targetIp}:{port} failed (connection refused).";

        string banner2 = (svc2 != null && !string.IsNullOrWhiteSpace(svc2.banner)) ? $"\n{svc2.banner}" : "";
        return $"Connected to {targetIp} on port {port}.{banner2}";
    }
    private string DoPing(string targetIp)
    {
        if (_pc == null) return BuildPingFail(targetIp);

        var myPort = _pc.GetNicPort();
        if (myPort == null || myPort.connectedTo == null) return BuildPingFail(targetIp);

        if (!TryParseIPv4(_pc.ipAddress, out uint myIp)) return BuildPingFail(targetIp);
        if (!TryParseIPv4(_pc.subnetMask, out uint myMask)) return BuildPingFail(targetIp);
        if (!TryParseIPv4(targetIp, out uint dstIp)) return BuildPingFail(targetIp);

        PcDevice dstPc = FindPcByIp(targetIp);
        if (dstPc == null) return BuildPingFail(targetIp);

        if (IsSameSubnet(myIp, dstIp, myMask))
        {
            if (myPort.connectedTo.owner is PcDevice directPc)
            {
                if (directPc != dstPc) return BuildPingFail(targetIp);

                string directMac = GetPseudoMac(dstPc);
                _pc.ArpAddOrUpdate(targetIp, directMac);
                return BuildPingSuccess(targetIp);
            }

            string srcMac = _pc.macAddress;

            int localVlan = -1;
            if (myPort.connectedTo.owner is SwitchDevice sw0)
                localVlan = DetermineIngressVlan(sw0, myPort.connectedTo.interfaceName);
            else if (myPort.connectedTo.owner is RouterDevice r0 && r0.IsSwitchportCapable(myPort.connectedTo.interfaceName))
                localVlan = r0.DetermineIngressVlan(myPort.connectedTo.interfaceName);

            if (!TryResolveLocalArp(targetIp, dstPc, localVlan, out string dstMac))
                return BuildPingFail(targetIp);

            if (myPort.connectedTo.owner is SwitchDevice firstSwitch)
            {
                string ingressIf = myPort.connectedTo.interfaceName;
                int vlan = DetermineIngressVlan(firstSwitch, ingressIf);

                var visited = new HashSet<string>();
                bool okSwitch = ForwardAtSwitch(firstSwitch, ingressIf, vlan, srcMac, dstMac, dstPc, visited);
                return okSwitch ? BuildPingSuccess(targetIp) : BuildPingFail(targetIp);
            }

            if (myPort.connectedTo.owner is RouterDevice router)
            {

                string ingressIf = myPort.connectedTo.interfaceName;
                if (!router.IsSwitchportCapable(ingressIf))
                    return BuildPingFail(targetIp);

                int vlan = router.DetermineIngressVlan(ingressIf);

                var visited = new HashSet<string>();
                bool okRouter = ForwardAtRouterSwitch(router, ingressIf, vlan, srcMac, dstMac, dstPc, visited);
                return okRouter ? BuildPingSuccess(targetIp) : BuildPingFail(targetIp);
            }

            return BuildPingFail(targetIp);
        }

        if (!TryParseIPv4(_pc.defaultGateway, out uint gwIp))
            return BuildPingFail(targetIp);

        if (!IsSameSubnet(myIp, gwIp, myMask))
            return BuildPingFail(targetIp);

        RouterDevice gwRouter = FindRouterByInterfaceIp(_pc.defaultGateway, myMask, myIp);
        if (gwRouter == null) return BuildPingFail(targetIp);

        var gwIf = gwRouter.GetInterfaceByIp(_pc.defaultGateway);
        string gwIfName = (gwIf != null) ? gwIf.name : "?";
        string gwMac = gwRouter.GetInterfacePseudoMac(gwIfName);
        _pc.ArpAddOrUpdate(_pc.defaultGateway, gwMac);

        gwRouter.ArpAddOrUpdate(_pc.ipAddress, _pc.macAddress, gwIfName);

        if (!CanReachDeviceAtL2(myPort, gwRouter))
            return BuildPingFail(targetIp);

        var visitedRouters = new HashSet<int>();
        bool ok = RouteFromRouter(gwRouter, dstIp, dstPc, 16, visitedRouters);
        return ok ? BuildPingSuccess(targetIp) : BuildPingFail(targetIp);
    }

    private static PcDevice FindPcByIp(string ip)
    {
        var pcs = UnityEngine.Object.FindObjectsOfType<PcDevice>(true);
        foreach (var p in pcs)
            if (p != null && string.Equals(p.ipAddress, ip, StringComparison.OrdinalIgnoreCase))
                return p;
        return null;
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

    private bool TryResolveLocalArp(string targetIp, PcDevice dstPc, int localVlan, out string dstMac)
    {
        dstMac = "";
        if (_pc == null || dstPc == null || string.IsNullOrWhiteSpace(targetIp)) return false;

        if (_pc.ArpTryGet(targetIp, out dstMac) && !string.IsNullOrWhiteSpace(dstMac))
            return true;

        if (localVlan > 0)
        {
            int dstVlan = GetPcLocalVlan(dstPc);
            if (dstVlan > 0 && dstVlan != localVlan)
                return false;
        }

        dstMac = GetPseudoMac(dstPc);
        if (string.IsNullOrWhiteSpace(dstMac)) return false;
        _pc.ArpAddOrUpdate(targetIp, dstMac);
        return true;
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

    private bool ForwardAtSwitch(
        SwitchDevice sw,
        string ingressIf,
        int vlan,
        string srcMac,
        string dstMac,
        Device destinationDevice,
        HashSet<string> visited)
    {
        string key = $"{sw.GetInstanceID()}|{vlan}|{ingressIf}";
        if (visited.Contains(key)) return false;
        visited.Add(key);

        sw.PurgeAgedMacs();
        sw.LearnMac(srcMac, vlan, ingressIf);

        string knownOutIf = sw.LookupMacPort(dstMac, vlan);
        if (!string.IsNullOrWhiteSpace(knownOutIf))
            return TrySendOutPort(sw, knownOutIf, vlan, srcMac, dstMac, destinationDevice, visited);

        var floodedLogical = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in sw.ports)
        {
            if (p == null) continue;
            if (string.Equals(p.name, ingressIf, StringComparison.OrdinalIgnoreCase)) continue;
            if (!p.adminUp) continue;
            if (!sw.HasLink(p.name)) continue;
            if (p.stpState == StpPortState.Blocking) continue;
            if (!IsVlanAllowedOnInterface(sw, p.name, vlan)) continue;

            string logicalKey = p.name;
            if (sw.IsChannelMember(p.name, out int gid) && sw.IsChannelFormed(gid))
                logicalKey = $"Port-channel{gid}";

            if (!floodedLogical.Add(logicalKey))
                continue;

            if (TrySendOutPort(sw, logicalKey, vlan, srcMac, dstMac, destinationDevice, visited))
                return true;
        }

        return false;
    }

    private bool TrySendOutPort(
        SwitchDevice sw,
        string egressIf,
        int vlan,
        string srcMac,
        string dstMac,
        Device destinationDevice,
        HashSet<string> visited)
    {
        string physEgress = sw.ResolveEgressPhysicalForLogical(egressIf, vlan, srcMac, dstMac);
        if (string.IsNullOrWhiteSpace(physEgress)) return false;

        var cfg = sw.GetPort(physEgress);
        if (cfg == null) return false;
        if (!cfg.adminUp) return false;
        if (!sw.HasLink(cfg.name)) return false;
        if (cfg.stpState == StpPortState.Blocking) return false;

        var unityPort = sw.GetUnityPortForInterface(physEgress);
        if (unityPort == null || unityPort.connectedTo == null) return false;

        if (!IsVlanAllowedOnInterface(sw, physEgress, vlan)) return false;

        var remote = unityPort.connectedTo;
        if (remote.owner == null) return false;

        if (remote.owner == destinationDevice)
            return true;

        if (remote.owner is SwitchDevice nextSwitch)
        {
            string nextIngressIf = remote.interfaceName;

            var nextCfg = nextSwitch.GetPort(nextIngressIf);
            if (nextCfg == null) return false;
            if (!nextCfg.adminUp) return false;
            if (!nextSwitch.HasLink(nextCfg.name)) return false;
            if (nextCfg.stpState == StpPortState.Blocking) return false;
            if (!IsVlanAllowedOnInterface(nextSwitch, nextIngressIf, vlan)) return false;

            return ForwardAtSwitch(nextSwitch, nextIngressIf, vlan, srcMac, dstMac, destinationDevice, visited);
        }

        if (remote.owner is RouterDevice router && router.IsSwitchportCapable(remote.interfaceName))
        {
            return ForwardAtRouterSwitch(router, remote.interfaceName, vlan, srcMac, dstMac, destinationDevice, visited);
        }

        return false;
    }

    private static bool IsVlanAllowedOnInterface(SwitchDevice sw, string ifName, int vlan)
    {
        if (sw == null) return false;

        var pc = sw.GetEffectivePortChannelForMember(ifName);
        if (pc != null)
        {
            if (pc.mode == SwitchportMode.Access)
                return pc.accessVlan == vlan;

            if (pc.trunkNativeVlan == vlan) return true;
            var allowed = SwitchDevice.ParseVlanList(pc.trunkAllowedVlans);
            return allowed.Contains(vlan);
        }

        var p = sw.GetPort(ifName);
        if (p == null) return false;

        if (p.mode == SwitchportMode.Access)
            return p.accessVlan == vlan;

        if (p.trunkNativeVlan == vlan) return true;
        var allowed2 = SwitchDevice.ParseVlanList(p.trunkAllowedVlans);
        return allowed2.Contains(vlan);
    }

    private bool ForwardAtRouterSwitch(
        RouterDevice r,
        string ingressIf,
        int vlan,
        string srcMac,
        string dstMac,
        Device destinationDevice,
        HashSet<string> visited)
    {
        string key = $"R{r.GetInstanceID()}|{vlan}|{ingressIf}";
        if (visited.Contains(key)) return false;
        visited.Add(key);

        r.PurgeAgedMacs();
        r.LearnMac(srcMac, vlan, ingressIf);

        string knownOutIf = r.LookupMacPort(dstMac, vlan);
        if (!string.IsNullOrWhiteSpace(knownOutIf))
            return TrySendOutRouterSwitchPort(r, knownOutIf, vlan, srcMac, dstMac, destinationDevice, visited);

        foreach (var sp in r.switchPorts)
        {
            if (sp == null) continue;
            if (string.Equals(sp.ifName, ingressIf, StringComparison.OrdinalIgnoreCase)) continue;

            var iface = r.GetInterface(sp.ifName);
            if (iface == null || !iface.adminUp) continue;
            if (!r.HasLink(sp.ifName)) continue;
            if (!r.IsVlanAllowedOnPort(sp.ifName, vlan)) continue;

            if (TrySendOutRouterSwitchPort(r, sp.ifName, vlan, srcMac, dstMac, destinationDevice, visited))
                return true;
        }

        return false;
    }

    private bool TrySendOutRouterSwitchPort(
        RouterDevice r,
        string egressIf,
        int vlan,
        string srcMac,
        string dstMac,
        Device destinationDevice,
        HashSet<string> visited)
    {
        if (!r.IsVlanAllowedOnPort(egressIf, vlan)) return false;

        var iface = r.GetInterface(egressIf);
        if (iface == null || !iface.adminUp) return false;
        if (!r.HasLink(egressIf)) return false;

        var unityPort = r.GetPortForInterface(egressIf);
        if (unityPort == null || unityPort.connectedTo == null) return false;

        var remote = unityPort.connectedTo;
        if (remote.owner == null) return false;

        if (remote.owner == destinationDevice)
            return true;

        if (remote.owner is SwitchDevice nextSwitch)
        {
            if (!IsVlanAllowedOnInterface(nextSwitch, remote.interfaceName, vlan)) return false;
            return ForwardAtSwitch(nextSwitch, remote.interfaceName, vlan, srcMac, dstMac, destinationDevice, visited);
        }

        if (remote.owner is RouterDevice nextRouter && nextRouter.IsSwitchportCapable(remote.interfaceName))
        {
            return ForwardAtRouterSwitch(nextRouter, remote.interfaceName, vlan, srcMac, dstMac, destinationDevice, visited);
        }

        return false;
    }

    private static RouterDevice FindRouterByInterfaceIp(string gwIpString, uint pcMask, uint pcIp)
    {
        var routers = UnityEngine.Object.FindObjectsOfType<RouterDevice>(true);
        foreach (var r in routers)
        {
            if (r == null) continue;
            if (!r.IsPoweredOn) continue;

            foreach (var itf in r.interfaces)
            {
                if (itf == null) continue;
                if (!itf.protocolUp) continue;
                if (string.IsNullOrWhiteSpace(itf.ipAddress)) continue;

                if (!string.Equals(itf.ipAddress.Trim(), gwIpString.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (TryParseIPv4(itf.ipAddress, out uint gw) && IsSameSubnet(pcIp, gw, pcMask))
                    return r;
            }
        }
        return null;
    }

    private bool CanReachDeviceAtL2(Port myNicPort, Device destination)
    {
        if (myNicPort == null || myNicPort.connectedTo == null || destination == null) return false;

        if (myNicPort.connectedTo.owner == destination)
            return true;

        string srcMac = _pc.macAddress;

        string dstMac = "__GW__";

        if (myNicPort.connectedTo.owner is SwitchDevice sw)
        {
            string ingressIf = myNicPort.connectedTo.interfaceName;
            int vlan = DetermineIngressVlan(sw, ingressIf);

            var visited = new HashSet<string>();
            return ForwardAtSwitch(sw, ingressIf, vlan, srcMac, dstMac, destination, visited);
        }

        if (myNicPort.connectedTo.owner is RouterDevice r && r.IsSwitchportCapable(myNicPort.connectedTo.interfaceName))
        {
            string ingressIf = myNicPort.connectedTo.interfaceName;
            int vlan = r.DetermineIngressVlan(ingressIf);

            var visited = new HashSet<string>();
            return ForwardAtRouterSwitch(r, ingressIf, vlan, srcMac, dstMac, destination, visited);
        }

        return false;
    }


    bool RouteFromRouter(RouterDevice current, uint dstIp, PcDevice dstPc, int ttl, HashSet<int> visitedRouters)
    {
        if (current == null || dstPc == null) return false;
        if (!current.IsPoweredOn) return false;
        if (ttl <= 0) return false;

        int rid = current.GetInstanceID();
        if (visitedRouters.Contains(rid)) return false;
        visitedRouters.Add(rid);

        if (!current.TryRoute(dstIp, out RouterInterface egress, out string nextHopIp))
            return false;

        if (egress == null) return false;

        string outPhys = egress.isSubinterface ? egress.parent : egress.name;
        if (string.IsNullOrWhiteSpace(outPhys)) return false;
        if (!current.HasLink(outPhys)) return false;

        int outVlan = egress.isSubinterface ? egress.dot1qVlan : -1;

        var p = current.ResolvePort(outPhys);
        if (p == null || p.connectedTo == null) return false;

        var remote = p.connectedTo;

        string srcMacR = current.GetInterfacePseudoMac(egress.name);

        if (string.Equals(nextHopIp, dstPc.ipAddress, StringComparison.OrdinalIgnoreCase))
        {
            string dstMacPc = dstPc.macAddress;

            var visited2 = new HashSet<string>();
            bool ok = CanReachAtL2FromRemote(remote, outVlan, srcMacR, dstMacPc, dstPc, visited2);
            return ok;
        }

        RouterDevice nhRouter = FindRouterByExactInterfaceIp(nextHopIp);
        if (nhRouter == null) return false;
        if (!nhRouter.IsPoweredOn) return false;

        var nhIf = nhRouter.GetInterfaceByIp(nextHopIp);
        if (nhIf == null || !nhIf.protocolUp) return false;

        var visitedHop = new HashSet<string>();
        bool canReachNh = CanReachAtL2FromRemote(remote, outVlan, srcMacR, "__NH__", nhRouter, visitedHop);
        if (!canReachNh) return false;

        current.ArpAddOrUpdate(nextHopIp, nhRouter.GetInterfacePseudoMac(nhIf.name), egress.name);

        return RouteFromRouter(nhRouter, dstIp, dstPc, ttl - 1, visitedRouters);
    }

    bool CanReachAtL2FromRemote(Port remote, int outVlan, string srcMac, string dstMac, Device destinationDevice, HashSet<string> visited)
    {
        if (remote == null || destinationDevice == null) return false;

        if (remote.owner == destinationDevice)
            return true;

        if (remote.owner is SwitchDevice sw)
        {
            string ingressIf = remote.interfaceName;
            int vlan = outVlan > 0 ? outVlan : DetermineIngressVlan(sw, ingressIf);

            if (!IsVlanAllowedOnInterface(sw, ingressIf, vlan))
                return false;

            return ForwardAtSwitch(sw, ingressIf, vlan, srcMac, dstMac, destinationDevice, visited);
        }

        if (remote.owner is RouterDevice r && r.IsSwitchportCapable(remote.interfaceName))
        {
            string ingressIf = remote.interfaceName;
            int vlan = outVlan > 0 ? outVlan : r.DetermineIngressVlan(ingressIf);

            return ForwardAtRouterSwitch(r, ingressIf, vlan, srcMac, dstMac, destinationDevice, visited);
        }

        return false;
    }

    RouterDevice FindRouterByExactInterfaceIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;

        var routers = UnityEngine.Object.FindObjectsOfType<RouterDevice>(true);
        foreach (var r in routers)
        {
            if (r == null) continue;
            if (!r.IsPoweredOn) continue;

            foreach (var itf in r.interfaces)
            {
                if (itf == null) continue;
                if (!itf.protocolUp) continue;
                if (string.IsNullOrWhiteSpace(itf.ipAddress)) continue;

                if (string.Equals(itf.ipAddress.Trim(), ip.Trim(), StringComparison.OrdinalIgnoreCase))
                    return r;
            }
        }
        return null;
    }


private static string BuildPingSuccess(string ip)
    {
        return
            $"Pinging {ip} with 32 bytes of data:\n" +
            $"Reply from {ip}: bytes=32 time<1ms TTL=128\n\n" +
            $"Ping statistics for {ip}:\n" +
            $"    Packets: Sent = 1, Received = 1, Lost = 0 (0% loss)";
    }

    private static string BuildPingFail(string ip)
    {
        return
            $"Pinging {ip} with 32 bytes of data:\n" +
            $"Request timed out.\n\n" +
            $"Ping statistics for {ip}:\n" +
            $"    Packets: Sent = 1, Received = 0, Lost = 1 (100% loss)";
    }

    private static bool TryParseIPv4(string ip, out uint value) => RouterDevice.TryParseIPv4(ip, out value);
    private static bool IsSameSubnet(uint a, uint b, uint mask) => (a & mask) == (b & mask);
    private static string GetPseudoMac(PcDevice pc) => pc != null ? pc.macAddress : "00:00:00:00:00:00";

    private string DhcpRenew()
    {
        var nic = _pc.GetNicPort();
        if (nic == null || nic.connectedTo == null)
            return "DHCP failed: no active NIC link.";

        _pc.dhcpEnabled = true;

        var routers = GameObject.FindObjectsOfType<RouterDevice>();
        foreach (var r in routers)
        {
            if (r == null) continue;

            if (!CanReachDeviceAtL2(nic, r))
                continue;

            if (r.TryDhcpRequestOffer(_pc.macAddress, out DhcpOffer offer))
            {
                _pc.ipAddress = offer.ip;
                _pc.subnetMask = string.IsNullOrWhiteSpace(offer.mask) ? "255.255.255.0" : offer.mask;
                _pc.defaultGateway = string.IsNullOrWhiteSpace(offer.gateway) ? "0.0.0.0" : offer.gateway;
                _pc.dnsServer = string.IsNullOrWhiteSpace(offer.dns) ? "0.0.0.0" : offer.dns;

                return $"DHCP configured:\n  IP Address . . . . . . . . . . . : {_pc.ipAddress}\n  Subnet Mask  . . . . . . . . . . . : {_pc.subnetMask}\n  Default Gateway . . . . . . . . . : {_pc.defaultGateway}\n  DNS Server . . . . . . . . . . .  : {_pc.dnsServer}";
            }
        }

        return "DHCP failed: no DHCP server reachable on the local network.";
    }

    private string DhcpRelease()
    {
        _pc.dhcpEnabled = false;
        _pc.ipAddress = "0.0.0.0";
        _pc.subnetMask = "0.0.0.0";
        _pc.defaultGateway = "0.0.0.0";
        _pc.dnsServer = "0.0.0.0";
        return "DHCP released. IP configuration cleared.";
    }
}
