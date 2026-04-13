using System;
using System.Text;

public static class RouterConfigSerializer
{
    public static string Generate(RouterDevice r)
    {
        if (r == null) return "";

        var sb = new StringBuilder();

        sb.AppendLine("enable");
        sb.AppendLine("configure terminal");

        string host = !string.IsNullOrWhiteSpace(r.deviceName) ? r.deviceName : r.gameObject.name;
        sb.AppendLine($"hostname {host}");

        if (!string.IsNullOrWhiteSpace(r.bannerMotd))
            sb.AppendLine($"banner motd #{r.bannerMotd}#");

        if (r.servicePasswordEncryption)
            sb.AppendLine("service password-encryption");

        if (!r.domainLookupEnabled)
            sb.AppendLine("no ip domain-lookup");

        if (!string.IsNullOrWhiteSpace(r.nameServer))
            sb.AppendLine($"ip name-server {r.nameServer}");

        if (!string.IsNullOrWhiteSpace(r.enableSecret))
        {
            string pw = r.servicePasswordEncryption ? $"7 {Encrypt(r.enableSecret)}" : r.enableSecret;
            sb.AppendLine($"enable secret {pw}");
        }
        else if (!string.IsNullOrWhiteSpace(r.enablePassword))
        {
            string pw = r.servicePasswordEncryption ? $"7 {Encrypt(r.enablePassword)}" : r.enablePassword;
            sb.AppendLine($"enable password {pw}");
        }

        AppendInterfaces(sb, r);
        AppendInterfaceNat(sb, r);
        AppendInterfaceAcls(sb, r);

        AppendDhcp(sb, r);
        AppendStandardAcls(sb, r);
        AppendExtendedAcls(sb, r);
        AppendNatRule(sb, r);
        AppendOspf(sb, r);
        AppendConsole(sb, r);

        sb.AppendLine("end");
        sb.AppendLine("write memory");

        return sb.ToString();
    }

    static void AppendInterfaces(StringBuilder sb, RouterDevice r)
    {
        if (r.interfaces == null) return;

        for (int i = 0; i < r.interfaces.Count; i++)
        {
            var itf = r.interfaces[i];
            if (itf == null) continue;
            if (string.IsNullOrWhiteSpace(itf.name)) continue;

            sb.AppendLine($"interface {itf.name}");

            if (!string.IsNullOrWhiteSpace(itf.description))
                sb.AppendLine($" description {itf.description}");

            if (!string.IsNullOrWhiteSpace(itf.ipAddress) &&
                !itf.ipAddress.Equals(NetworkUtils.UnassignedIp, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(itf.subnetMask))
            {
                sb.AppendLine($" ip address {itf.ipAddress} {itf.subnetMask}");
            }

            if (itf.isSubinterface && itf.dot1qVlan > 0)
                sb.AppendLine($" encapsulation dot1Q {itf.dot1qVlan}");

            if (itf.adminUp) sb.AppendLine(" no shutdown");
            else sb.AppendLine(" shutdown");

            sb.AppendLine(" exit");
        }
    }

    static void AppendInterfaceNat(StringBuilder sb, RouterDevice r)
    {
        if (r.interfaceNatBindings == null) return;

        for (int i = 0; i < r.interfaceNatBindings.Count; i++)
        {
            var b = r.interfaceNatBindings[i];
            if (b == null) continue;
            if (string.IsNullOrWhiteSpace(b.interfaceName)) continue;

            if (!b.natInside && !b.natOutside) continue;

            sb.AppendLine($"interface {b.interfaceName}");

            if (b.natInside) sb.AppendLine(" ip nat inside");
            if (b.natOutside) sb.AppendLine(" ip nat outside");

            sb.AppendLine(" exit");
        }
    }

    static void AppendInterfaceAcls(StringBuilder sb, RouterDevice r)
    {
        if (r.interfaceAclBindings == null) return;

        for (int i = 0; i < r.interfaceAclBindings.Count; i++)
        {
            var b = r.interfaceAclBindings[i];
            if (b == null) continue;
            if (string.IsNullOrWhiteSpace(b.interfaceName)) continue;

            bool any = !string.IsNullOrWhiteSpace(b.inboundAcl) || !string.IsNullOrWhiteSpace(b.outboundAcl);
            if (!any) continue;

            sb.AppendLine($"interface {b.interfaceName}");

            if (!string.IsNullOrWhiteSpace(b.inboundAcl))
                sb.AppendLine($" ip access-group {b.inboundAcl} in");

            if (!string.IsNullOrWhiteSpace(b.outboundAcl))
                sb.AppendLine($" ip access-group {b.outboundAcl} out");

            sb.AppendLine(" exit");
        }
    }

    static void AppendDhcp(StringBuilder sb, RouterDevice r)
    {
        if (r.dhcpExcludedAddresses != null)
        {
            foreach (var ex in r.dhcpExcludedAddresses)
            {
                if (ex == null || string.IsNullOrWhiteSpace(ex.low)) continue;
                string h = string.IsNullOrWhiteSpace(ex.high) ? ex.low : ex.high;
                if (string.Equals(ex.low, h, StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine($"ip dhcp excluded-address {ex.low}");
                else
                    sb.AppendLine($"ip dhcp excluded-address {ex.low} {h}");
            }
        }

        if (r.dhcpPools == null || r.dhcpPools.Count == 0) return;

        for (int i = 0; i < r.dhcpPools.Count; i++)
        {
            var p = r.dhcpPools[i];
            if (p == null) continue;
            if (string.IsNullOrWhiteSpace(p.name)) continue;

            sb.AppendLine($"ip dhcp pool {p.name}");

            if (!string.IsNullOrWhiteSpace(p.network) && !string.IsNullOrWhiteSpace(p.mask))
                sb.AppendLine($" network {p.network} {p.mask}");

            if (!string.IsNullOrWhiteSpace(p.defaultRouter) && !p.defaultRouter.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($" default-router {p.defaultRouter}");

            if (!string.IsNullOrWhiteSpace(p.dnsServer) && !p.dnsServer.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($" dns-server {p.dnsServer}");

            if (p.startHost > 0 && p.endHost > 0)
                sb.AppendLine($" address range {p.startHost} {p.endHost}");

            sb.AppendLine(" exit");
        }
    }

    static void AppendStandardAcls(StringBuilder sb, RouterDevice r)
    {
        if (r.standardAcls == null || r.standardAcls.Count == 0) return;

        for (int i = 0; i < r.standardAcls.Count; i++)
        {
            var e = r.standardAcls[i];
            if (e == null) continue;
            if (e.aclNumber <= 0) continue;

            string act = e.permit ? "permit" : "deny";
            string nw = string.IsNullOrWhiteSpace(e.network) ? "0.0.0.0" : e.network;
            string wc = string.IsNullOrWhiteSpace(e.wildcard) ? "255.255.255.255" : e.wildcard;

            sb.AppendLine($"access-list {e.aclNumber} {act} {nw} {wc}");
        }
    }

    static void AppendExtendedAcls(StringBuilder sb, RouterDevice r)
    {
        if (r.acls == null || r.acls.Count == 0) return;

        for (int i = 0; i < r.acls.Count; i++)
        {
            var acl = r.acls[i];
            if (acl == null) continue;
            if (string.IsNullOrWhiteSpace(acl.name)) continue;

            sb.AppendLine($"ip access-list extended {acl.name}");

            if (acl.rules != null)
            {
                acl.rules.Sort((a, b) => a.sequence.CompareTo(b.sequence));
                for (int j = 0; j < acl.rules.Count; j++)
                {
                    var rule = acl.rules[j];
                    if (rule == null) continue;
                    sb.AppendLine(" " + rule.ToString());
                }
            }

            sb.AppendLine(" exit");
        }
    }

    static void AppendNatRule(StringBuilder sb, RouterDevice r)
    {
        if (r.natRule == null) return;
        if (r.natRule.aclNumber <= 0) return;
        if (string.IsNullOrWhiteSpace(r.natRule.outsideInterfaceName)) return;

        if (r.natRule.overload)
            sb.AppendLine($"ip nat inside source list {r.natRule.aclNumber} interface {r.natRule.outsideInterfaceName} overload");
        else
            sb.AppendLine($"ip nat inside source list {r.natRule.aclNumber} interface {r.natRule.outsideInterfaceName}");
    }

    static void AppendOspf(StringBuilder sb, RouterDevice r)
    {
        if (r.ospfProcesses == null || r.ospfProcesses.Count == 0) return;

        for (int i = 0; i < r.ospfProcesses.Count; i++)
        {
            var p = r.ospfProcesses[i];
            if (p == null) continue;
            if (p.pid <= 0) continue;

            sb.AppendLine($"router ospf {p.pid}");

            if (p.networks != null)
            {
                for (int n = 0; n < p.networks.Count; n++)
                {
                    var net = p.networks[n];
                    if (net == null) continue;
                    if (string.IsNullOrWhiteSpace(net.network)) continue;
                    if (string.IsNullOrWhiteSpace(net.wildcard)) continue;
                    sb.AppendLine($" network {net.network} {net.wildcard} area {net.area}");
                }
            }

            if (p.passiveInterfaces != null)
            {
                for (int k = 0; k < p.passiveInterfaces.Count; k++)
                {
                    string pi = p.passiveInterfaces[k];
                    if (string.IsNullOrWhiteSpace(pi)) continue;
                    sb.AppendLine($" passive-interface {pi}");
                }
            }

            sb.AppendLine(" exit");
        }
    }

    static void AppendConsole(StringBuilder sb, RouterDevice r)
    {
        sb.AppendLine("line console 0");

        if (r.consoleLoginEnabled)
        {
            string pw = r.servicePasswordEncryption
                ? $"7 {Encrypt(r.consolePassword)}"
                : r.consolePassword;
            if (!string.IsNullOrWhiteSpace(pw))
                sb.AppendLine($" password {pw}");
            sb.AppendLine(" login");
        }
        else
        {
            sb.AppendLine(" no login");
        }

        sb.AppendLine(" exit");

        if (r.vtyLoginEnabled || !string.IsNullOrWhiteSpace(r.vtyPassword))
        {
            sb.AppendLine("line vty 0 4");
            if (!string.IsNullOrWhiteSpace(r.vtyPassword))
            {
                string pw = r.servicePasswordEncryption
                    ? $"7 {Encrypt(r.vtyPassword)}"
                    : r.vtyPassword;
                sb.AppendLine($" password {pw}");
            }
            if (r.vtyLoginEnabled) sb.AppendLine(" login");
            else sb.AppendLine(" no login");
            sb.AppendLine(" exit");
        }
    }

    // Trivial Cisco type-7 simulation (just reversal for display; not real Cisco encryption)
    static string Encrypt(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "00";
        var bytes = System.Text.Encoding.ASCII.GetBytes(plain);
        var sb2 = new StringBuilder();
        foreach (var b in bytes) sb2.Append((b ^ 0x15).ToString("X2"));
        return sb2.ToString();
    }
}
