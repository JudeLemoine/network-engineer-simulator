using System;
using System.Text;

public static class SwitchConfigSerializer
{
    public static string Generate(SwitchDevice s)
    {
        if (s == null) return "";

        var sb = new StringBuilder();

        sb.AppendLine("enable");
        sb.AppendLine("configure terminal");

        string host = string.IsNullOrWhiteSpace(s.deviceName) ? s.gameObject.name : s.deviceName;
        sb.AppendLine($"hostname {host}");

        if (s.stpPriority != 32768)
            sb.AppendLine($"spanning-tree vlan 1 priority {s.stpPriority}");

        AppendVlans(sb, s);
        AppendPorts(sb, s);

        sb.AppendLine("end");
        sb.AppendLine("write memory");

        return sb.ToString();
    }

    static void AppendVlans(StringBuilder sb, SwitchDevice s)
    {
        if (s.vlans == null) return;

        for (int i = 0; i < s.vlans.Count; i++)
        {
            var v = s.vlans[i];
            if (v == null) continue;
            if (v.id <= 0) continue;
            if (v.id == 1 && (string.IsNullOrWhiteSpace(v.name) || v.name.Equals("default", StringComparison.OrdinalIgnoreCase)))
                continue;

            sb.AppendLine($"vlan {v.id}");
            if (!string.IsNullOrWhiteSpace(v.name))
                sb.AppendLine($" name {v.name}");
            sb.AppendLine(" exit");
        }
    }

    static void AppendPorts(StringBuilder sb, SwitchDevice s)
    {
        if (s.ports == null) return;

        for (int i = 0; i < s.ports.Count; i++)
        {
            var p = s.ports[i];
            if (p == null) continue;
            if (string.IsNullOrWhiteSpace(p.name)) continue;

            sb.AppendLine($"interface {p.name}");

            if (p.mode == SwitchportMode.Access)
            {
                sb.AppendLine(" switchport mode access");
                if (p.accessVlan > 0) sb.AppendLine($" switchport access vlan {p.accessVlan}");
            }
            else
            {
                sb.AppendLine(" switchport mode trunk");
                if (!string.IsNullOrWhiteSpace(p.trunkAllowedVlans))
                    sb.AppendLine($" switchport trunk allowed vlan {p.trunkAllowedVlans}");
                if (p.trunkNativeVlan > 0)
                    sb.AppendLine($" switchport trunk native vlan {p.trunkNativeVlan}");
            }

            if (p.channelGroup > 0 && p.channelMode != EtherChannelMode.None)
            {
                string mode = p.channelMode switch
                {
                    EtherChannelMode.On => "on",
                    EtherChannelMode.Active => "active",
                    EtherChannelMode.Passive => "passive",
                    _ => ""
                };

                if (!string.IsNullOrWhiteSpace(mode))
                    sb.AppendLine($" channel-group {p.channelGroup} mode {mode}");
            }

            if (p.adminUp) sb.AppendLine(" no shutdown");
            else sb.AppendLine(" shutdown");

            sb.AppendLine(" exit");
        }
    }
}
