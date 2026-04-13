using System;
using System.Collections.Generic;
using UnityEngine;

public enum IosMode
{
    UserExec,
    PrivExec,
    GlobalConfig,
    InterfaceConfig,
    DhcpPoolConfig,
    AclConfig,
    RouterOspfConfig,
    LineConsoleConfig,
    SviConfig
}

public class IosSession : ITerminalSession
{
    private readonly RouterDevice _router;
    private RouterInterface _currentIf;

    private DhcpPool _currentPool;
    private ExtendedAcl _currentAcl;
    private int _currentOspfPid = -1;

    public string Hostname { get; private set; } = "Router";
    public IosMode Mode { get; private set; } = IosMode.UserExec;

    private bool _awaitingConsolePassword = false;
    private bool _awaitingEnablePassword = false;
    // true when LineConsoleConfig is actually configuring VTY lines
    private bool _lineIsVty = false;

    public IosSession(RouterDevice router)
    {
        _router = router;
        if (_router != null) Hostname = _router.deviceName;

        if (_router != null && _router.consoleLoginEnabled)
        {
            _awaitingConsolePassword = true;
            Mode = IosMode.UserExec;
        }
    }

    public string Prompt
    {
        get
        {
            if (_awaitingConsolePassword || _awaitingEnablePassword)
                return "Password:";

            return Mode switch
            {
                IosMode.UserExec => $"{Hostname}>",
                IosMode.PrivExec => $"{Hostname}#",
                IosMode.GlobalConfig => $"{Hostname}(config)#",
                IosMode.InterfaceConfig => $"{Hostname}(config-if)#",
                IosMode.DhcpPoolConfig => $"{Hostname}(dhcp-config)#",
                IosMode.AclConfig => $"{Hostname}(config-ext-nacl)#",
                IosMode.RouterOspfConfig => $"{Hostname}(config-router)#",
                IosMode.LineConsoleConfig => $"{Hostname}(config-line)#",
                _ => $"{Hostname}>"
            };
        }
    }

    public string Execute(string input)
    {

        if (_router != null && !_router.IsPoweredOn)
            return "% System is powered off. Flip the power switch to use the console.";

        input = (input ?? "").Trim();

        if (_awaitingConsolePassword)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            string pw = _router != null ? (_router.consolePassword ?? "") : "";
            if (input == pw)
            {
                _awaitingConsolePassword = false;
                Mode = IosMode.UserExec;
                return "";
            }

            return "% Bad passwords";
        }

        if (input.Length == 0) return "";

        if (input.Equals("en", StringComparison.OrdinalIgnoreCase)) input = "enable";
        if (input.Equals("conf t", StringComparison.OrdinalIgnoreCase)) input = "configure terminal";
        if (input.Equals("sh ip int br", StringComparison.OrdinalIgnoreCase)) input = "show ip interface brief";
        if (input.Equals("sh access-lists", StringComparison.OrdinalIgnoreCase)) input = "show access-lists";
        if (input.Equals("sh ip nat tr", StringComparison.OrdinalIgnoreCase)) input = "show ip nat translations";
        if (input.Equals("sh ip nat st", StringComparison.OrdinalIgnoreCase)) input = "show ip nat statistics";
        if (input.Equals("sh ip ro", StringComparison.OrdinalIgnoreCase)) input = "show ip route";

        if (_awaitingEnablePassword)
        {
            if (string.IsNullOrEmpty(input)) return "";

            string required = (!string.IsNullOrWhiteSpace(_router?.enableSecret))
                ? _router.enableSecret
                : (_router?.enablePassword ?? "");

            if (input == required)
            {
                _awaitingEnablePassword = false;
                Mode = IosMode.PrivExec;
                return "";
            }
            _awaitingEnablePassword = false;
            return "% Bad passwords";
        }

        if (input.Equals("enable", StringComparison.OrdinalIgnoreCase))
        {
            if (_router != null)
            {
                string required = !string.IsNullOrWhiteSpace(_router.enableSecret)
                    ? _router.enableSecret
                    : _router.enablePassword;

                if (!string.IsNullOrWhiteSpace(required))
                {
                    _awaitingEnablePassword = true;
                    return "Password:";
                }
            }
            Mode = IosMode.PrivExec;
            return "";
        }

        if (input.Equals("disable", StringComparison.OrdinalIgnoreCase))
        {
            Mode = IosMode.UserExec;
            return "";
        }

        if (input.Equals("configure terminal", StringComparison.OrdinalIgnoreCase))
        {
            if (Mode != IosMode.PrivExec)
                return "% Invalid input detected at '^' marker.";

            Mode = IosMode.GlobalConfig;
            return "Enter configuration commands, one per line. End with CNTL/Z.";
        }

        if (input.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            Mode = IosMode.PrivExec;
            _currentIf = null;
            _currentPool = null;
            _currentAcl = null;
            return "";
        }

        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            if (Mode == IosMode.InterfaceConfig)
            {
                Mode = IosMode.GlobalConfig;
                _currentIf = null;
                return "";
            }

            if (Mode == IosMode.DhcpPoolConfig)
            {
                Mode = IosMode.GlobalConfig;
                _currentPool = null;
                return "";
            }

            if (Mode == IosMode.AclConfig)
            {
                Mode = IosMode.GlobalConfig;
                _currentAcl = null;
                return "";
            }

            if (Mode == IosMode.LineConsoleConfig)
            {
                Mode = IosMode.GlobalConfig;
                _lineIsVty = false;
                return "";
            }

            if (Mode == IosMode.GlobalConfig)
            {
                Mode = IosMode.PrivExec;
                return "";
            }

            return "logout";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("hostname ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                Hostname = parts[1];
                if (_router != null) _router.deviceName = Hostname;
            }
            return "";
        }

        if (Mode == IosMode.GlobalConfig && input.Equals("line console 0", StringComparison.OrdinalIgnoreCase))
        {
            _lineIsVty = false;
            Mode = IosMode.LineConsoleConfig;
            return "";
        }

        if (Mode == IosMode.GlobalConfig && (
            input.StartsWith("line vty ", StringComparison.OrdinalIgnoreCase)))
        {
            _lineIsVty = true;
            Mode = IosMode.LineConsoleConfig;
            return "";
        }

        if (Mode == IosMode.GlobalConfig && (
            input.StartsWith("enable secret ", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";
            _router.enableSecret = input.Substring("enable secret ".Length).Trim();
            return "";
        }

        if (Mode == IosMode.GlobalConfig && (
            input.StartsWith("enable password ", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";
            _router.enablePassword = input.Substring("enable password ".Length).Trim();
            return "";
        }

        if (Mode == IosMode.GlobalConfig && (
            input.Equals("service password-encryption", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";
            _router.servicePasswordEncryption = true;
            return "";
        }

        if (Mode == IosMode.GlobalConfig && (
            input.Equals("no service password-encryption", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";
            _router.servicePasswordEncryption = false;
            return "";
        }

        if (Mode == IosMode.GlobalConfig && (
            input.Equals("no ip domain-lookup", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("no ip domain lookup", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";
            _router.domainLookupEnabled = false;
            return "";
        }

        if (Mode == IosMode.GlobalConfig && (
            input.Equals("ip domain-lookup", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("ip domain lookup", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";
            _router.domainLookupEnabled = true;
            return "";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("ip name-server ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            string ip = input.Substring("ip name-server ".Length).Trim();
            if (!TryParseIPv4(ip, out _)) return "% Invalid IP address.";
            _router.nameServer = ip;
            return "";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("banner motd ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            // banner motd #<message># — grab everything after the delimiter
            string rest = input.Substring("banner motd ".Length);
            if (rest.Length < 2) return "% Incomplete command.";
            char delim = rest[0];
            string inner = rest.Substring(1);
            int end = inner.IndexOf(delim);
            _router.bannerMotd = end >= 0 ? inner.Substring(0, end) : inner;
            return "";
        }

        if (Mode == IosMode.LineConsoleConfig)
        {
            if (_router == null) return "% Device not ready.";

            if (input.StartsWith("password ", StringComparison.OrdinalIgnoreCase))
            {
                string pw = input.Substring("password ".Length);
                if (_lineIsVty) _router.vtyPassword = pw;
                else _router.consolePassword = pw;
                return "";
            }

            if (input.Equals("login", StringComparison.OrdinalIgnoreCase))
            {
                if (_lineIsVty) _router.vtyLoginEnabled = true;
                else _router.consoleLoginEnabled = true;
                return "";
            }

            if (input.Equals("no login", StringComparison.OrdinalIgnoreCase))
            {
                if (_lineIsVty) _router.vtyLoginEnabled = false;
                else _router.consoleLoginEnabled = false;
                return "";
            }

            return "% Invalid input detected at '^' marker.";
        }


        if (Mode == IosMode.GlobalConfig && input.StartsWith("ip route ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            string rest = input.Substring("ip route ".Length).Trim();
            if (string.IsNullOrWhiteSpace(rest)) return "% Incomplete command.";

            var parts = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return "% Incomplete command.";

            string net = parts[0];
            string mask = parts[1];
            string p3 = parts[2];

            string exitIf = "";
            string nextHop = "";

            if (parts.Length == 3)
            {
                if (TryParseIPv4(p3, out _))
                    nextHop = p3;
                else
                    exitIf = p3;
            }
            else
            {
                exitIf = p3;
                nextHop = parts[3];
            }

            bool ok = _router.AddOrUpdateStaticRoute(net, mask, nextHop, exitIf);
            return ok ? "" : "% Invalid input detected at '^' marker.";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("no ip route ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            string rest = input.Substring("no ip route ".Length).Trim();
            if (string.IsNullOrWhiteSpace(rest)) return "% Incomplete command.";

            var parts = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return "% Incomplete command.";

            string net = parts[0];
            string mask = parts[1];
            string p3 = parts[2];

            string exitIf = "";
            string nextHop = "";

            if (parts.Length == 3)
            {
                if (TryParseIPv4(p3, out _))
                    nextHop = p3;
                else
                    exitIf = p3;
            }
            else
            {
                exitIf = p3;
                nextHop = parts[3];
            }

            bool removed = _router.RemoveStaticRoute(net, mask, nextHop, exitIf);
            return removed ? "" : "% Route not found.";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("ip dhcp excluded-address ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            string rest = input.Substring("ip dhcp excluded-address ".Length).Trim();
            if (string.IsNullOrWhiteSpace(rest)) return "% Incomplete command.";

            var parts = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (!TryParseIPv4(parts[0], out _)) return "% Invalid IP address.";
            string low = parts[0];
            string high = (parts.Length >= 2 && TryParseIPv4(parts[1], out _)) ? parts[1] : low;

            _router.AddDhcpExcludedRange(low, high);
            return "";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("no ip dhcp excluded-address ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            string rest = input.Substring("no ip dhcp excluded-address ".Length).Trim();
            if (string.IsNullOrWhiteSpace(rest)) return "% Incomplete command.";

            var parts = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (!TryParseIPv4(parts[0], out _)) return "% Invalid IP address.";
            string low = parts[0];
            string high = (parts.Length >= 2 && TryParseIPv4(parts[1], out _)) ? parts[1] : low;

            bool removed = _router.RemoveDhcpExcludedRange(low, high);
            return removed ? "" : "% Excluded range not found.";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("ip dhcp pool ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            string name = input.Substring("ip dhcp pool ".Length).Trim();
            if (string.IsNullOrWhiteSpace(name)) return "% Incomplete command.";

            _currentPool = _router.EnsureDhcpPool(name);
            Mode = IosMode.DhcpPoolConfig;
            return "";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("router ospf ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return "% Incomplete command.";
            if (!int.TryParse(parts[2], out int pid) || pid <= 0) return "% Invalid input detected at '^' marker.";
            _router.OspfEnsureProcess(pid);
            _currentOspfPid = pid;
            Mode = IosMode.RouterOspfConfig;
            return "";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("no router ospf ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return "% Incomplete command.";
            if (!int.TryParse(parts[3], out int pid) || pid <= 0) return "% Invalid input detected at '^' marker.";
            _router.OspfRemoveProcess(pid);
            if (_currentOspfPid == pid) _currentOspfPid = -1;
            return "";
        }


        if (Mode == IosMode.GlobalConfig && input.StartsWith("no ip dhcp pool ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            string name = input.Substring("no ip dhcp pool ".Length).Trim();
            if (string.IsNullOrWhiteSpace(name)) return "% Incomplete command.";

            bool removed = _router.RemoveDhcpPool(name);
            return removed ? "" : "% Pool not found.";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("ip access-list extended ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            string name = input.Substring("ip access-list extended ".Length).Trim();
            if (string.IsNullOrWhiteSpace(name)) return "% Incomplete command.";

            _currentAcl = _router.EnsureAcl(name);
            Mode = IosMode.AclConfig;
            return "";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("access-list ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5) return "% Invalid input detected at '^' marker.";

            if (!int.TryParse(parts[1], out int aclNum)) return "% Invalid input detected at '^' marker.";
            if (!parts[2].Equals("permit", StringComparison.OrdinalIgnoreCase)) return "% Invalid input detected at '^' marker.";

            string network = parts[3];
            string wildcard = parts[4];

            _router.AddStandardAclPermit(aclNum, network, wildcard);
            return "";
        }

        if (Mode == IosMode.GlobalConfig && input.StartsWith("ip nat inside source list ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 9) return "% Invalid input detected at '^' marker.";
            if (!parts[4].Equals("list", StringComparison.OrdinalIgnoreCase)) return "% Invalid input detected at '^' marker.";
            if (!int.TryParse(parts[5], out int aclNum)) return "% Invalid input detected at '^' marker.";
            if (!parts[6].Equals("interface", StringComparison.OrdinalIgnoreCase)) return "% Invalid input detected at '^' marker.";

            string ifName = NormalizeInterfaceName(parts[7]);
            bool overload = parts[8].Equals("overload", StringComparison.OrdinalIgnoreCase);
            if (!overload) return "% Invalid input detected at '^' marker.";

            _router.natRule = new NatRule
            {
                aclNumber = aclNum,
                outsideInterfaceName = ifName,
                overload = true
            };
            return "";
        }

        if ((Mode == IosMode.GlobalConfig || Mode == IosMode.InterfaceConfig) &&
            (input.StartsWith("interface ", StringComparison.OrdinalIgnoreCase) ||
             input.StartsWith("int ", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null)
                return "% Device not ready.";

            string ifName = input.StartsWith("interface ", StringComparison.OrdinalIgnoreCase)
                ? input.Substring("interface ".Length).Trim()
                : input.Substring("int ".Length).Trim();

            ifName = NormalizeInterfaceName(ifName);

            _currentIf = _router.GetInterface(ifName);
            if (_currentIf == null)
                _currentIf = _router.EnsureInterface(ifName);

            if (_currentIf == null)
                return "% Invalid interface.";

            Mode = IosMode.InterfaceConfig;
            return "";
        }

        if (Mode == IosMode.DhcpPoolConfig)
        {
            if (_router == null) return "% Device not ready.";
            if (_currentPool == null) return "% No DHCP pool selected.";

            if (input.StartsWith("network ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3) return "% Incomplete command.";
                if (!TryParseIPv4(parts[1], out _)) return "% Invalid network address.";
                if (!TryParseIPv4(parts[2], out _)) return "% Invalid subnet mask.";
                _currentPool.network = parts[1];
                _currentPool.mask = parts[2];
                return "";
            }

            if (input.StartsWith("default-router ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) return "% Incomplete command.";
                if (!TryParseIPv4(parts[1], out _)) return "% Invalid IP address.";
                _currentPool.defaultRouter = parts[1];
                return "";
            }

            if (input.StartsWith("dns-server ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) return "% Incomplete command.";
                if (!TryParseIPv4(parts[1], out _)) return "% Invalid IP address.";
                _currentPool.dnsServer = parts[1];
                return "";
            }

            if (input.StartsWith("address range ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 4) return "% Incomplete command.";

                if (!int.TryParse(parts[2], out int s) || !int.TryParse(parts[3], out int e))
                    return "% Invalid input detected at '^' marker.";

                if (s < 2 || s > 254 || e < 2 || e > 254 || s > e)
                    return "% Invalid input detected at '^' marker.";

                _currentPool.startHost = s;
                _currentPool.endHost = e;
                return "";
            }

            return "% Invalid input detected at '^' marker.";
        }

        if (Mode == IosMode.AclConfig)
        {
            if (_router == null) return "% Device not ready.";
            if (_currentAcl == null) return "% No ACL selected.";

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                string action = parts[0].ToLower();
                string proto = parts[1].ToLower();

                if (action != "permit" && action != "deny")
                    return "% Invalid input detected at '^' marker.";

                AclAction act = action == "permit" ? AclAction.Permit : AclAction.Deny;

                AclProtocol pr;
                if (proto == "ip") pr = AclProtocol.Ip;
                else if (proto == "icmp") pr = AclProtocol.Icmp;
                else if (proto == "tcp") pr = AclProtocol.Tcp;
                else if (proto == "udp") pr = AclProtocol.Udp;
                else return "% Invalid input detected at '^' marker.";

                int idx = 2;
                string ParseAddr()
                {
                    if (idx >= parts.Length) return null;
                    if (parts[idx].Equals("any", StringComparison.OrdinalIgnoreCase))
                    {
                        idx += 1;
                        return "any";
                    }
                    if (parts[idx].Equals("host", StringComparison.OrdinalIgnoreCase))
                    {
                        if (idx + 1 >= parts.Length) return null;
                        string ip = parts[idx + 1];
                        idx += 2;
                        return "host " + ip;
                    }

                    string ip2 = parts[idx];
                    idx += 1;
                    return "host " + ip2;
                }

                string src = ParseAddr();
                string dst = ParseAddr();
                if (src == null || dst == null)
                    return "% Incomplete command.";

                int dstPort = -1;
                if (idx < parts.Length)
                {

                    if (parts[idx].Equals("eq", StringComparison.OrdinalIgnoreCase))
                    {
                        if (idx + 1 >= parts.Length) return "% Incomplete command.";
                        if (!int.TryParse(parts[idx + 1], out dstPort)) return "% Invalid input detected at '^' marker.";
                        idx += 2;
                    }
                }

                if (dstPort >= 0 && pr != AclProtocol.Tcp && pr != AclProtocol.Udp)
                    return "% Invalid input detected at '^' marker.";

                _currentAcl.AddRule(act, pr, src, dst, dstPort);
                return "";
            }}


        if (Mode == IosMode.RouterOspfConfig)
        {
            if (_router == null) return "% Device not ready.";
            if (_currentOspfPid <= 0) return "% Invalid input detected at '^' marker.";

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Mode = IosMode.GlobalConfig;
                return "";
            }

            if (input.StartsWith("network ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) return "% Incomplete command.";
                                string net = parts[1];
                string wc = parts[2];

                int areaIdx = -1;
                for (int i = 0; i < parts.Length; i++)
                    if (parts[i].Equals("area", StringComparison.OrdinalIgnoreCase))
                        areaIdx = i;

                if (areaIdx < 0 || areaIdx + 1 >= parts.Length) return "% Incomplete command.";
                if (!int.TryParse(parts[areaIdx + 1], out int area)) return "% Invalid input detected at '^' marker.";
                if (area != 0) return "% Only area 0 is supported.";

                bool ok = _router.OspfAddNetwork(_currentOspfPid, net, wc, area);
                return ok ? "" : "% Invalid input detected at '^' marker.";
            }

            if (input.StartsWith("passive-interface ", StringComparison.OrdinalIgnoreCase))
            {
                string ifName = input.Substring("passive-interface ".Length).Trim();
                if (ifName.Length == 0) return "% Incomplete command.";
                _router.OspfSetPassive(_currentOspfPid, ifName, true);
                return "";
            }

            if (input.StartsWith("no passive-interface ", StringComparison.OrdinalIgnoreCase))
            {
                string ifName = input.Substring("no passive-interface ".Length).Trim();
                if (ifName.Length == 0) return "% Incomplete command.";
                _router.OspfSetPassive(_currentOspfPid, ifName, false);
                return "";
            }

            return "% Invalid input detected at '^' marker.";
        }

if (Mode == IosMode.InterfaceConfig)
        {
            if (_currentIf == null)
                return "% No interface selected.";

            if (input.StartsWith("encapsulation ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3) return "% Incomplete command.";

                if (!parts[1].Equals("dot1Q", StringComparison.OrdinalIgnoreCase))
                    return "% Invalid encapsulation type.";

                if (!int.TryParse(parts[2], out int vlan) || vlan < 1 || vlan > 4094)
                    return "% Invalid VLAN ID.";

                if (!_currentIf.isSubinterface)
                    return "% Encapsulation is only supported on subinterfaces (e.g., Gi0/0.10).";

                _currentIf.dot1qVlan = vlan;
                _router.RefreshProtocolStates();
                return "";
            }

            if (input.StartsWith("ip address ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 4)
                    return "% Incomplete command.";

                if (!TryParseIPv4(parts[2], out _))
                    return "% Invalid IP address.";
                if (!TryParseIPv4(parts[3], out _))
                    return "% Invalid subnet mask.";

                _currentIf.ipAddress = parts[2];
                _currentIf.subnetMask = parts[3];
                _router.RefreshProtocolStates();
                return "";
            }

            if (input.Equals("no shutdown", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("no shut", StringComparison.OrdinalIgnoreCase))
            {
                _currentIf.adminUp = true;
                _router.RefreshProtocolStates();
                return "";
            }

            if (input.Equals("shutdown", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("shut", StringComparison.OrdinalIgnoreCase))
            {
                _currentIf.adminUp = false;
                _router.RefreshProtocolStates();
                return "";
            }

            if (input.StartsWith("ip access-group ", StringComparison.OrdinalIgnoreCase))
            {
                if (_router == null) return "% Device not ready.";

                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 4) return "% Incomplete command.";

                string aclName = parts[2];
                string dir = parts[3].ToLower();

                if (dir != "in" && dir != "out")
                    return "% Invalid input detected at '^' marker.";

                if (_router.GetAcl(aclName) == null)
                    return "% Access-list not found.";

                _router.SetInterfaceAcl(_currentIf.name, inbound: (dir == "in"), aclName: aclName);
                return "";
            }

            if (input.StartsWith("no ip access-group ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 4) return "% Incomplete command.";

                string dir = parts[3].ToLower();
                if (dir != "in" && dir != "out")
                    return "% Invalid input detected at '^' marker.";

                _router.ClearInterfaceAcl(_currentIf.name, inbound: (dir == "in"));
                return "";
            }

            if (input.Equals("ip nat inside", StringComparison.OrdinalIgnoreCase))
            {
                _router.SetNatInside(_currentIf.name, true);
                return "";
            }

            if (input.Equals("ip nat outside", StringComparison.OrdinalIgnoreCase))
            {
                _router.SetNatOutside(_currentIf.name, true);
                return "";
            }

            if (input.Equals("no ip nat inside", StringComparison.OrdinalIgnoreCase))
            {
                _router.SetNatInside(_currentIf.name, false);
                return "";
            }

            if (input.Equals("no ip nat outside", StringComparison.OrdinalIgnoreCase))
            {
                _router.SetNatOutside(_currentIf.name, false);
                return "";
            }

            if (input.StartsWith("description ", StringComparison.OrdinalIgnoreCase))
            {
                _currentIf.description = input.Substring("description ".Length);
                return "";
            }

            if (input.Equals("no description", StringComparison.OrdinalIgnoreCase))
            {
                _currentIf.description = "";
                return "";
            }
        }

        
        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            (input.Equals("show running-config", StringComparison.OrdinalIgnoreCase) ||
             input.Equals("show run", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";
            var store = _router.GetComponent<CliConfigStorage>();
            if (store == null) return "% No config storage attached to this device.";
            return store.GetRunningConfigText();
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            (input.Equals("write memory", StringComparison.OrdinalIgnoreCase) ||
             input.Equals("wr mem", StringComparison.OrdinalIgnoreCase) ||
             input.Equals("copy running-config startup-config", StringComparison.OrdinalIgnoreCase) ||
             input.Equals("copy run start", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";
            var store = _router.GetComponent<CliConfigStorage>();
            if (store == null) return "% No config storage attached to this device.";

            string path = store.SaveRunningConfigAsNewFile();
            if (string.IsNullOrWhiteSpace(path)) return "% Failed to save configuration.";

            return "Building configuration...\n[OK]\nSaved: " + path;
        }

if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            input.Equals("show ip interface brief", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null)
            {
                return
                    "Interface              IP-Address      OK? Method Status                Protocol\n" +
                    "GigabitEthernet0/0      unassigned      YES unset  administratively down down\n" +
                    "GigabitEthernet0/1      unassigned      YES unset  administratively down down";
            }

            _router.RefreshProtocolStates();

            string header =
                "Interface              IP-Address      OK? Method Status                Protocol\n";

            string body = "";
            foreach (var i in _router.interfaces)
            {
                if (i == null) continue;

                string ip = string.IsNullOrWhiteSpace(i.ipAddress) ? NetworkUtils.UnassignedIp : i.ipAddress;
                string status = i.adminUp ? "up" : "administratively down";
                string proto = i.protocolUp ? "up" : "down";
                body += $"{i.name,-22} {ip,-15} YES unset  {status,-20} {proto}\n";
            }

            return header + body.TrimEnd('\n');
        }


        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            input.Equals("show ip route", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            return _router.BuildShowIpRoute();
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            input.Equals("show ip ospf neighbor", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            return _router.BuildShowIpOspfNeighbor();
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            input.Equals("show ip protocols", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            return _router.BuildShowIpProtocols();
        }


        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            input.Equals("show access-lists", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            if (_router.acls == null || _router.acls.Count == 0) return "No access lists configured.";

            string outp = "";
            foreach (var acl in _router.acls)
            {
                if (acl == null) continue;
                outp += $"Extended IP access list {acl.name}\n";
                if (acl.rules == null || acl.rules.Count == 0)
                {
                    outp += "  (empty)\n\n";
                    continue;
                }

                foreach (var r in acl.rules)
                    outp += $"  {r}\n";

                outp += "\n";
            }
            return outp.TrimEnd('\n');
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            (input.Equals("show ip arp", StringComparison.OrdinalIgnoreCase) ||
             input.Equals("show arp", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";

            var entries = _router.ArpGetAll();
            if (entries.Count == 0)
                return "No ARP entries.";

            string outp = "Protocol  Address          Age (min)  Hardware Addr   Type   Interface\n";
            foreach (var e in entries)
            {
                int ageMin = Mathf.Max(0, Mathf.FloorToInt((Time.time - e.lastSeen) / 60f));
                outp += $"Internet  {e.ip,-15} {ageMin,-9}  {e.mac,-14}  ARPA   {e.ifName}\n";
            }

            return outp.TrimEnd('\n');
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            (input.Equals("show mac address-table", StringComparison.OrdinalIgnoreCase) ||
             input.Equals("show mac-address-table", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";

            _router.PurgeAgedMacs();
            if (_router.macTable == null || _router.macTable.Count == 0)
                return "Mac Address Table\n-----------------------------------------\nVlan    Mac Address       Type        Ports\n----    -----------       --------    -----\n(empty)";

            string outp = "Mac Address Table\n-----------------------------------------\n";
            outp += "Vlan    Mac Address       Type        Ports\n";
            outp += "----    -----------       --------    -----\n";
            foreach (var e in _router.macTable)
            {
                if (e == null) continue;
                string shortIf = e.ifName.Length > 2 ? e.ifName : e.ifName;
                // shorten interface name the Cisco way
                string portShort = RouterDevice.NormalizeInterfaceName(e.ifName);
                outp += $"{e.vlan.ToString().PadRight(8)}{e.mac.PadRight(18)}DYNAMIC     {portShort}\n";
            }
            return outp.TrimEnd('\n');
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            input.Equals("show ip nat translations", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";

            if (_router.natTranslations == null || _router.natTranslations.Count == 0)
                return "No NAT translations.";

            string outp = "Pro  Inside global      Inside local       Outside global     Outside local\n";
            outp += "---  ----------------  ----------------  ----------------  ----------------\n";

            foreach (var t in _router.natTranslations)
            {
                if (t == null) continue;
                outp += $"{t.protocol,-3}  {t.insideGlobal,-16}  {t.insideLocal,-16}  {t.outsideGlobal,-16}  {t.outsideLocal,-16}\n";
            }

            return outp.TrimEnd('\n');
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            input.Equals("show ip nat statistics", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";

            int count = (_router.natTranslations != null) ? _router.natTranslations.Count : 0;
            string rule = (_router.natRule == null)
                ? "NAT is not configured."
                : $"Inside source list {_router.natRule.aclNumber} interface {_router.natRule.outsideInterfaceName} overload";

            return
                "NAT statistics:\n" +
                $"  Total active translations: {count}\n" +
                $"  Rule: {rule}";
        }

        if (Mode == IosMode.PrivExec &&
            (input.Equals("clear ip nat translations", StringComparison.OrdinalIgnoreCase) ||
             input.Equals("clear ip nat translation *", StringComparison.OrdinalIgnoreCase) ||
             input.Equals("clear ip nat trans", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";

            _router.NatClearTranslations();
            return "NAT translations cleared.";
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            input.Equals("show ip dhcp binding", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";

            var leases = CollectAllDhcpLeases(_router);
            if (leases.Count == 0)
                return "Bindings from all pools are empty.";

            string outp = "IP address      Client-ID/MAC        Pool        Issued(UTC)\n";
            outp += "--------------- -------------------- ----------- ------------------------\n";
            foreach (var l in leases)
            {
                string mac = l.mac ?? "";
                string pool = l.poolName ?? "";
                string issued = l.issuedAtUtc.ToString("yyyy-MM-dd HH:mm:ss");
                outp += $"{l.ip,-15} {mac,-20} {pool,-11} {issued}\n";
            }
            return outp.TrimEnd('\n');
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            input.Equals("show ip dhcp pool", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";

            if (_router.dhcpPools == null || _router.dhcpPools.Count == 0)
                return "No DHCP pools configured.";

            string outp = "";
            foreach (var p in _router.dhcpPools)
            {
                if (p == null) continue;
                int used = (p.leasesByMac != null) ? p.leasesByMac.Count : 0;

                outp += $"Pool {p.name} :\n";
                outp += $"  Network: {p.network} {p.mask}\n";
                outp += $"  Default-router: {p.defaultRouter}\n";
                outp += $"  DNS-server: {p.dnsServer}\n";
                outp += $"  Range: host {p.startHost} - {p.endHost}\n";
                outp += $"  Active leases: {used}\n\n";
            }

            return outp.TrimEnd('\n');
        }

        if (Mode == IosMode.PrivExec &&
            input.StartsWith("ping ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            string target = input.Substring("ping ".Length).Trim();
            return DoRouterPing(target);
        }

        if (Mode == IosMode.PrivExec &&
            (input.StartsWith("traceroute ", StringComparison.OrdinalIgnoreCase) ||
             input.StartsWith("tracert ", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";
            string target = input.StartsWith("traceroute ", StringComparison.OrdinalIgnoreCase)
                ? input.Substring("traceroute ".Length).Trim()
                : input.Substring("tracert ".Length).Trim();
            return DoRouterTraceroute(target);
        }

        if (Mode == IosMode.PrivExec &&
            input.StartsWith("clock set ", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            string rest = input.Substring("clock set ".Length).Trim();
            _router.clockOffset = rest;
            return "";
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            input.Equals("show version", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            return BuildShowVersion();
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            (input.Equals("show interfaces", StringComparison.OrdinalIgnoreCase) ||
             input.Equals("show int", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";
            return BuildShowInterfaces();
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            (input.Equals("show cdp neighbors", StringComparison.OrdinalIgnoreCase) ||
             input.Equals("show cdp nei", StringComparison.OrdinalIgnoreCase)))
        {
            if (_router == null) return "% Device not ready.";
            return BuildShowCdpNeighbors();
        }

        if ((Mode == IosMode.PrivExec || Mode == IosMode.UserExec) &&
            input.Equals("show clock", StringComparison.OrdinalIgnoreCase))
        {
            if (_router == null) return "% Device not ready.";
            string clk = !string.IsNullOrWhiteSpace(_router.clockOffset)
                ? _router.clockOffset
                : DateTime.UtcNow.ToString("HH:mm:ss.ff") + " UTC";
            return clk;
        }

        return "% Invalid input detected at '^' marker.";
    }

    private string DoRouterPing(string targetIp)
    {
        if (_router == null) return $"% Ping error: device not ready.";
        if (!TryParseIPv4(targetIp, out uint dstIp))
            return $"% Invalid IP address: {targetIp}";

        // Check direct connectivity first
        bool directlyConnected = false;
        RouterInterface egressIf = null;

        if (_router.TryGetBestConnectedInterface(dstIp, out egressIf))
            directlyConnected = true;

        // If not directly connected, check routing table (static routes + OSPF)
        if (!directlyConnected)
        {
            var bestRoute = _router.FindBestRoute(dstIp);
            if (bestRoute != null)
            {
                egressIf = !string.IsNullOrWhiteSpace(bestRoute.exitInterface)
                    ? _router.GetInterface(bestRoute.exitInterface)
                    : null;
                directlyConnected = false; // routed
            }
        }

        if (egressIf == null && !directlyConnected)
        {
            // No route found
            return
                $"Type escape sequence to abort.\n" +
                $"Sending 5, 100-byte ICMP Echos to {targetIp}, timeout is 2 seconds:\n" +
                $".....\n" +
                $"Success rate is 0 percent (0/5)";
        }

        // Find destination device in scene
        var allPcs = UnityEngine.Object.FindObjectsOfType<PcDevice>(true);
        bool found = false;
        foreach (var pc in allPcs)
        {
            if (pc != null && string.Equals(pc.ipAddress, targetIp, StringComparison.OrdinalIgnoreCase))
            { found = true; break; }
        }
        if (!found)
        {
            // Also check router interfaces
            var allRouters = UnityEngine.Object.FindObjectsOfType<RouterDevice>(true);
            foreach (var r in allRouters)
            {
                if (r == null) continue;
                foreach (var itf in r.interfaces)
                {
                    if (itf != null && string.Equals(itf.ipAddress, targetIp, StringComparison.OrdinalIgnoreCase))
                    { found = true; break; }
                }
                if (found) break;
            }
        }

        if (!found)
        {
            return
                $"Type escape sequence to abort.\n" +
                $"Sending 5, 100-byte ICMP Echos to {targetIp}, timeout is 2 seconds:\n" +
                $".....\n" +
                $"Success rate is 0 percent (0/5)";
        }

        return
            $"Type escape sequence to abort.\n" +
            $"Sending 5, 100-byte ICMP Echos to {targetIp}, timeout is 2 seconds:\n" +
            $"!!!!!\n" +
            $"Success rate is 100 percent (5/5), round-trip min/avg/max = 1/2/4 ms";
    }

    private string BuildShowVersion()
    {
        string model = (Hostname.ToLower().Contains("1841") || _router.gameObject.name.ToLower().Contains("1841"))
            ? "Cisco 1841 (revision 1.0)"
            : "Cisco ISR 2911 (revision 1.0)";
        string ios = "Version 15.1(4)M12a, RELEASE SOFTWARE (fc1)";
        string uptime = $"{Mathf.FloorToInt(Time.time / 3600)}h {Mathf.FloorToInt((Time.time % 3600) / 60)}m";

        string clk = !string.IsNullOrWhiteSpace(_router.clockOffset)
            ? _router.clockOffset
            : DateTime.UtcNow.ToString("HH:mm:ss") + " UTC";

        string output = "";
        output += $"Cisco IOS Software, {ios}\n";
        output += $"Technical Support: http://www.cisco.com/techsupport\n\n";
        output += $"{model}\n";
        output += $"{Hostname} uptime is {uptime}\n";
        output += $"System returned to ROM by reload at {clk}\n\n";
        output += $"ROM: System Bootstrap, Version 12.4(13r)T, RELEASE SOFTWARE\n\n";
        output += $"cisco 2911 (revision 1.0) with 483328K/40960K bytes of memory.\n";
        output += $"Processor board ID FTX152400MY\n\n";

        int physCount = 0;
        foreach (var itf in _router.interfaces)
            if (itf != null && !itf.isSubinterface) physCount++;
        output += $"{physCount} Gigabit Ethernet interfaces\n";
        output += $"1 terminal line\n";
        output += $"DRAM configuration is 64 bits wide with parity disabled.\n";
        output += $"255K bytes of non-volatile configuration memory.\n";
        output += $"249856K bytes of ATA System CompactFlash (Read/Write)\n\n";
        output += $"Configuration register is 0x2102";
        return output;
    }

    private string BuildShowInterfaces()
    {
        _router.RefreshProtocolStates();
        string output = "";

        foreach (var itf in _router.interfaces)
        {
            if (itf == null) continue;
            string status = itf.adminUp ? "is up" : "is administratively down";
            string proto = itf.protocolUp ? "is up" : "is down";
            string ip = (string.IsNullOrWhiteSpace(itf.ipAddress) || itf.ipAddress == NetworkUtils.UnassignedIp)
                ? "unassigned" : $"{itf.ipAddress}/{MaskToCidr(itf.subnetMask)}";
            string desc = !string.IsNullOrWhiteSpace(itf.description) ? $"\n  Description: {itf.description}" : "";

            output += $"{itf.name} {status}, line protocol {proto}{desc}\n";
            output += $"  Internet address is {ip}\n";
            output += $"  MTU 1500 bytes, BW 1000000 Kbit/sec, DLY 10 usec,\n";
            output += $"     reliability 255/255, txload 1/255, rxload 1/255\n";
            output += $"  Encapsulation ARPA, loopback not set\n";
            output += $"  Keepalive set (10 sec)\n";
            output += $"  Full-duplex, 1000Mb/s, media type is RJ45\n";
            output += $"  Input queue: 0/75/0/0 (size/max/drops/flushes); Total output drops: 0\n";
            output += $"  5 minute input rate 0 bits/sec, 0 packets/sec\n";
            output += $"  5 minute output rate 0 bits/sec, 0 packets/sec\n";
            output += $"     0 packets input, 0 bytes, 0 no buffer\n";
            output += $"     0 packets output, 0 bytes, 0 underruns\n\n";
        }

        return output.TrimEnd('\n');
    }

    private static int MaskToCidr(string mask)
    {
        if (!RouterDevice.TryParseIPv4(mask, out uint m)) return 0;
        int bits = 0;
        for (int i = 31; i >= 0; i--)
            if (((m >> i) & 1u) == 1u) bits++;
        return bits;
    }

    private string BuildShowCdpNeighbors()
    {
        string output = "Capability Codes: R - Router, T - Trans Bridge, B - Source Route Bridge\n";
        output += "                  S - Switch, H - Host, I - IGMP, r - Repeater\n\n";
        output += $"Device ID        Local Intrfce     Holdtme    Capability  Platform  Port ID\n";

        var ports = _router.GetComponentsInChildren<Port>(true);
        bool any = false;

        foreach (var p in ports)
        {
            if (p == null || !p.IsConnected || p.connectedTo == null) continue;
            if (p.medium != PortMedium.Ethernet) continue;

            var remote = p.connectedTo;
            if (remote.owner == null) continue;

            string deviceId = remote.owner.deviceName ?? remote.owner.name;
            string localIf = RouterDevice.NormalizeInterfaceName(p.interfaceName ?? p.portName ?? "?");
            string remoteIf = RouterDevice.NormalizeInterfaceName(remote.interfaceName ?? remote.portName ?? "?");
            string capability = (remote.owner is RouterDevice) ? "R" : (remote.owner is SwitchDevice) ? "S" : "H";
            string platform = (remote.owner is RouterDevice) ? "cisco ISR" : (remote.owner is SwitchDevice) ? "cisco WS-C" : "cisco";

            output += $"{deviceId,-17}{localIf,-18}{"120",-11}{capability,-12}{platform,-10}{remoteIf}\n";
            any = true;
        }

        if (!any) output += "(No CDP neighbors found)\n";
        return output.TrimEnd('\n');
    }

    private string DoRouterTraceroute(string targetIp)
    {
        if (!TryParseIPv4(targetIp, out uint dstIp))
            return $"% Invalid IP address: {targetIp}";

        string header = $"Type escape sequence to abort.\n" +
                        $"Tracing the route to {targetIp}\n" +
                        $"VRF info: (vrf in name/id, vrf out name/id)\n";

        // Build hop list by following the routing table
        var hops = new List<string>();
        var visited = new HashSet<int>();

        RouterDevice current = _router;
        int maxHops = 15;

        for (int hop = 1; hop <= maxHops; hop++)
        {
            if (current == null || !current.TryRoute(dstIp, out RouterInterface egress, out string nextHopIp))
                break;

            // Direct delivery
            if (string.Equals(nextHopIp, targetIp, StringComparison.OrdinalIgnoreCase) ||
                (TryParseIPv4(nextHopIp, out uint nhIp) && current.TryGetBestConnectedInterface(dstIp, out _)))
            {
                // Destination is directly reachable from current router's egress
                hops.Add($"  {hop}  {targetIp}  1 msec  1 msec  2 msec");
                break;
            }

            hops.Add($"  {hop}  {nextHopIp}  1 msec  1 msec  2 msec");

            // Advance to next-hop router
            int rid = current.GetInstanceID();
            if (visited.Contains(rid)) break;
            visited.Add(rid);

            var allRouters = UnityEngine.Object.FindObjectsOfType<RouterDevice>(true);
            RouterDevice nhRouter = null;
            foreach (var r in allRouters)
            {
                if (r == null || !r.IsPoweredOn) continue;
                foreach (var itf in r.interfaces)
                {
                    if (itf != null && string.Equals(itf.ipAddress, nextHopIp, StringComparison.OrdinalIgnoreCase))
                    { nhRouter = r; break; }
                }
                if (nhRouter != null) break;
            }

            if (nhRouter == null) break;
            current = nhRouter;
        }

        if (hops.Count == 0)
            return header + "  1  * * * (no route to host)";

        return header + string.Join("\n", hops);
    }

    private static List<DhcpLease> CollectAllDhcpLeases(RouterDevice router)
    {
        var list = new List<DhcpLease>();
        if (router == null || router.dhcpPools == null) return list;

        foreach (var p in router.dhcpPools)
        {
            if (p == null) continue;
            p.EnsureRuntime();
            if (p.leasesByMac == null) continue;

            foreach (var kv in p.leasesByMac)
            {
                if (kv.Value != null)
                    list.Add(kv.Value);
            }
        }

        list.Sort((a, b) => string.Compare(a.ip, b.ip, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private string NormalizeInterfaceName(string raw)
    {
        return RouterDevice.NormalizeInterfaceName(raw);
    }

private static bool TryParseIPv4(string ip, out uint value) => RouterDevice.TryParseIPv4(ip, out value);

}
