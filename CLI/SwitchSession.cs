using System;
using System.Collections.Generic;
using UnityEngine;

public class SwitchSession : ITerminalSession
{
    private readonly SwitchDevice _sw;

    private SwitchPortConfig _currentPort;
    private SwitchPortChannelConfig _currentPortChannel;
    private List<SwitchPortConfig> _rangePorts;

    private IosMode _mode = IosMode.UserExec;

    public SwitchSession(SwitchDevice sw) { _sw = sw; }

    public string Prompt
    {
        get
        {
            string host = _sw != null ? _sw.deviceName : "Switch";
            return _mode switch
            {
                IosMode.UserExec => $"{host}>",
                IosMode.PrivExec => $"{host}#",
                IosMode.GlobalConfig => $"{host}(config)#",
                IosMode.InterfaceConfig => $"{host}(config-if)#",
                _ => $"{host}>"
            };
        }
    }

    public string Execute(string input)
    {
        input = (input ?? "").Trim();
        if (string.IsNullOrWhiteSpace(input)) return "";

        if (input.Equals("en", StringComparison.OrdinalIgnoreCase)) input = "enable";
        if (input.Equals("conf t", StringComparison.OrdinalIgnoreCase)) input = "configure terminal";
        if (input.Equals("wr", StringComparison.OrdinalIgnoreCase)) input = "write memory";

        if (_mode == IosMode.UserExec)
        {
            if (input.Equals("enable", StringComparison.OrdinalIgnoreCase))
            {
                _mode = IosMode.PrivExec;
                return "";
            }


            if (input.Equals("show running-config", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("show run", StringComparison.OrdinalIgnoreCase))
            {
                if (_sw == null) return "% Device not ready.";
                var store = _sw.GetComponent<CliConfigStorage>();
                if (store == null) return "% No config storage attached to this device.";
                return store.GetRunningConfigText();
            }

            if (input.Equals("write memory", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("wr mem", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("copy running-config startup-config", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("copy run start", StringComparison.OrdinalIgnoreCase))
            {
                if (_sw == null) return "% Device not ready.";
                var store = _sw.GetComponent<CliConfigStorage>();
                if (store == null) return "% No config storage attached to this device.";

                string path = store.SaveRunningConfigAsNewFile();
                if (string.IsNullOrWhiteSpace(path)) return "% Failed to save configuration.";

                return "Building configuration...\n[OK]\nSaved: " + path;
            }

            if (input.StartsWith("show ", StringComparison.OrdinalIgnoreCase))
                return HandleShow(input);

            return "% Invalid input detected at '^' marker.";
        }

        if (_mode == IosMode.PrivExec)
        {
            if (input.Equals("disable", StringComparison.OrdinalIgnoreCase))
            {
                _mode = IosMode.UserExec;
                return "";
            }

            if (input.Equals("configure terminal", StringComparison.OrdinalIgnoreCase))
            {
                _mode = IosMode.GlobalConfig;
                return "Enter configuration commands, one per line. End with CNTL/Z.";
            }


            if (input.Equals("show running-config", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("show run", StringComparison.OrdinalIgnoreCase))
            {
                if (_sw == null) return "% Device not ready.";
                var store = _sw.GetComponent<CliConfigStorage>();
                if (store == null) return "% No config storage attached to this device.";
                return store.GetRunningConfigText();
            }

            if (input.Equals("write memory", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("wr mem", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("copy running-config startup-config", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("copy run start", StringComparison.OrdinalIgnoreCase))
            {
                if (_sw == null) return "% Device not ready.";
                var store = _sw.GetComponent<CliConfigStorage>();
                if (store == null) return "% No config storage attached to this device.";

                string path = store.SaveRunningConfigAsNewFile();
                if (string.IsNullOrWhiteSpace(path)) return "% Failed to save configuration.";

                return "Building configuration...\n[OK]\nSaved: " + path;
            }

            if (input.StartsWith("show ", StringComparison.OrdinalIgnoreCase))
                return HandleShow(input);

            if (input.Equals("write memory", StringComparison.OrdinalIgnoreCase))
                return "Building configuration...\n[OK]";

            return "% Invalid input detected at '^' marker.";
        }

        if (_mode == IosMode.GlobalConfig)
        {
            if (input.Equals("end", StringComparison.OrdinalIgnoreCase) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                _mode = IosMode.PrivExec;
                ClearInterfaceContext();
                return "";
            }

            if (_sw == null) return "% Device not ready.";

            if (input.StartsWith("spanning-tree priority ", StringComparison.OrdinalIgnoreCase))
            {
                string s = input.Substring("spanning-tree priority ".Length).Trim();
                if (!int.TryParse(s, out int prio) || prio < 0) return "% Invalid priority.";
                _sw.stpPriority = prio;
                return "";
            }

            if (input.StartsWith("vlan ", StringComparison.OrdinalIgnoreCase))
            {
                string idStr = input.Substring("vlan ".Length).Trim();
                if (!int.TryParse(idStr, out int vid) || !SwitchDevice.IsValidVlanId(vid))
                    return "% Invalid VLAN id.";

                _sw.EnsureVlan(vid);
                return "";
            }

            if (input.StartsWith("interface range ", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("int range ", StringComparison.OrdinalIgnoreCase))
            {
                string rest = input.StartsWith("interface range ", StringComparison.OrdinalIgnoreCase)
                    ? input.Substring("interface range ".Length).Trim()
                    : input.Substring("int range ".Length).Trim();

                var range = ParseInterfaceRange(rest);
                if (range == null || range.Count == 0) return "% Invalid interface range.";

                _rangePorts = range;
                _currentPort = null;
                _currentPortChannel = null;
                _mode = IosMode.InterfaceConfig;
                return "";
            }

            if (input.StartsWith("interface port-channel ", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("int port-channel ", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("interface po", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("int po", StringComparison.OrdinalIgnoreCase))
            {
                string idStr = ExtractPortChannelId(input);
                if (!int.TryParse(idStr, out int id) || id < 1) return "% Invalid interface.";

                _currentPortChannel = _sw.GetOrCreatePortChannel(id);
                _currentPort = null;
                _rangePorts = null;
                _mode = IosMode.InterfaceConfig;
                return "";
            }

            if (input.StartsWith("interface ", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("int ", StringComparison.OrdinalIgnoreCase))
            {
                string ifName = input.StartsWith("interface ", StringComparison.OrdinalIgnoreCase)
                    ? input.Substring("interface ".Length).Trim()
                    : input.Substring("int ".Length).Trim();

                ifName = SwitchDevice.NormalizeInterfaceName(ifName);

                _currentPort = _sw.GetPort(ifName);
                if (_currentPort == null) return "% Invalid interface.";

                _currentPortChannel = null;
                _rangePorts = null;
                _mode = IosMode.InterfaceConfig;
                return "";
            }

            return "% Invalid input detected at '^' marker.";
        }

        if (_mode == IosMode.InterfaceConfig)
        {
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                _mode = IosMode.GlobalConfig;
                ClearInterfaceContext();
                return "";
            }

            if (input.StartsWith("interface ", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("int ", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("int fa", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("int gi", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("int te", StringComparison.OrdinalIgnoreCase))
            {
                string ifName = input.StartsWith("interface ", StringComparison.OrdinalIgnoreCase)
                    ? input.Substring("interface ".Length).Trim()
                    : input.Substring("int ".Length).Trim();

                ifName = SwitchDevice.NormalizeInterfaceName(ifName);

                _currentPort = _sw.GetPort(ifName);
                if (_currentPort == null) return "% Invalid interface.";

                _currentPortChannel = null;
                _rangePorts = null;
                _mode = IosMode.InterfaceConfig;
                return "";
            }

            if (input.StartsWith("interface port-channel", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("int port-channel", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("interface po", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("int po", StringComparison.OrdinalIgnoreCase))
            {
                string idStr = ExtractPortChannelId(input);
                if (!int.TryParse(idStr, out int id) || id < 1) return "% Invalid interface.";

                _currentPortChannel = _sw.GetOrCreatePortChannel(id);
                _currentPort = null;
                _rangePorts = null;
                _mode = IosMode.InterfaceConfig;
                return "";
            }

            if (_sw == null) return "% Device not ready.";

            if (input.StartsWith("channel-group ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) return "% Invalid input detected at '^' marker.";

                if (!int.TryParse(parts[1], out int gid) || gid < 1) return "% Invalid input detected at '^' marker.";
                if (!parts[2].Equals("mode", StringComparison.OrdinalIgnoreCase)) return "% Invalid input detected at '^' marker.";

                EtherChannelMode mode = parts[3].ToLowerInvariant() switch
                {
                    "on" => EtherChannelMode.On,
                    "active" => EtherChannelMode.Active,
                    "passive" => EtherChannelMode.Passive,
                    _ => EtherChannelMode.None
                };
                if (mode == EtherChannelMode.None) return "% Invalid input detected at '^' marker.";

                foreach (var p in GetSelectedPhysicalPorts())
                {
                    p.channelGroup = gid;
                    p.channelMode = mode;
                    p.channelSuspended = false;
                }

                _sw.GetOrCreatePortChannel(gid);
                _sw.RefreshEtherChannels();
                return "";
            }

            if (input.Equals("no channel-group", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var p in GetSelectedPhysicalPorts())
                {
                    p.channelGroup = 0;
                    p.channelMode = EtherChannelMode.None;
                    p.channelSuspended = false;
                }
                _sw.RefreshEtherChannels();
                return "";
            }

            if (input.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var p in GetSelectedPhysicalPorts())
                {
                    p.adminUp = false;
                    p.protocolUp = false;
                }
                _sw.RefreshEtherChannels();
                return "";
            }

            if (input.Equals("no shutdown", StringComparison.OrdinalIgnoreCase) || input.Equals("no shut", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var p in GetSelectedPhysicalPorts())
                {
                    p.adminUp = true;
                    p.protocolUp = _sw.HasLink(p.name);
                }
                _sw.RefreshEtherChannels();
                return "";
            }

            if (input.StartsWith("switchport mode ", StringComparison.OrdinalIgnoreCase))
            {
                string modeStr = input.Substring("switchport mode ".Length).Trim().ToLowerInvariant();
                if (modeStr != "access" && modeStr != "trunk") return "% Invalid switchport mode.";

                if (IsPortChannelContext())
                {
                    _currentPortChannel.mode = (modeStr == "access") ? SwitchportMode.Access : SwitchportMode.Trunk;
                    return "";
                }

                foreach (var p in GetSelectedPhysicalPorts())
                    p.mode = (modeStr == "access") ? SwitchportMode.Access : SwitchportMode.Trunk;

                return "";
            }

            if (input.StartsWith("switchport access vlan ", StringComparison.OrdinalIgnoreCase))
            {
                string idStr = input.Substring("switchport access vlan ".Length).Trim();
                if (!int.TryParse(idStr, out int vid) || !SwitchDevice.IsValidVlanId(vid))
                    return "% Invalid VLAN id.";

                _sw.EnsureVlan(vid);

                if (IsPortChannelContext())
                {
                    _currentPortChannel.accessVlan = vid;
                    return "";
                }

                foreach (var p in GetSelectedPhysicalPorts())
                    p.accessVlan = vid;

                return "";
            }

            if (input.StartsWith("switchport trunk native vlan ", StringComparison.OrdinalIgnoreCase))
            {
                string idStr = input.Substring("switchport trunk native vlan ".Length).Trim();
                if (!int.TryParse(idStr, out int vid) || !SwitchDevice.IsValidVlanId(vid))
                    return "% Invalid VLAN id.";

                _sw.EnsureVlan(vid);

                if (IsPortChannelContext())
                {
                    _currentPortChannel.trunkNativeVlan = vid;
                    return "";
                }

                foreach (var p in GetSelectedPhysicalPorts())
                    p.trunkNativeVlan = vid;

                return "";
            }

            if (input.StartsWith("switchport trunk allowed vlan ", StringComparison.OrdinalIgnoreCase))
            {
                string rest = input.Substring("switchport trunk allowed vlan ".Length).Trim();

                if (IsPortChannelContext())
                {
                    ApplyAllowedVlanChangeToPortChannel(_currentPortChannel, rest);
                    return "";
                }

                foreach (var p in GetSelectedPhysicalPorts())
                    ApplyAllowedVlanChangeToPhysical(p, rest);

                return "";
            }

            return "% Invalid input detected at '^' marker.";
        }

        return "% Invalid input detected at '^' marker.";
    }

    private void ClearInterfaceContext()
    {
        _currentPort = null;
        _currentPortChannel = null;
        _rangePorts = null;
    }

    private bool IsPortChannelContext() => _currentPortChannel != null;

    private IEnumerable<SwitchPortConfig> GetSelectedPhysicalPorts()
    {
        if (_rangePorts != null && _rangePorts.Count > 0) return _rangePorts;
        if (_currentPort != null) return new List<SwitchPortConfig> { _currentPort };
        return new List<SwitchPortConfig>();
    }

    private static void ApplyAllowedVlanChangeToPhysical(SwitchPortConfig p, string rest)
    {
        if (rest.StartsWith("add ", StringComparison.OrdinalIgnoreCase))
        {
            var add = SwitchDevice.ParseVlanList(rest.Substring(4).Trim());
            var cur = SwitchDevice.ParseVlanList(p.trunkAllowedVlans);
            foreach (var v in add) cur.Add(v);
            p.trunkAllowedVlans = SwitchDevice.ToCiscoVlanList(cur);
            return;
        }

        if (rest.StartsWith("remove ", StringComparison.OrdinalIgnoreCase))
        {
            var rem = SwitchDevice.ParseVlanList(rest.Substring(7).Trim());
            var cur = SwitchDevice.ParseVlanList(p.trunkAllowedVlans);
            foreach (var v in rem) cur.Remove(v);
            p.trunkAllowedVlans = SwitchDevice.ToCiscoVlanList(cur);
            return;
        }

        var set = SwitchDevice.ParseVlanList(rest);
        p.trunkAllowedVlans = SwitchDevice.ToCiscoVlanList(set);
    }

    private void ApplyAllowedVlanChangeToPortChannel(SwitchPortChannelConfig pc, string rest)
    {
        if (rest.StartsWith("add ", StringComparison.OrdinalIgnoreCase))
        {
            var add = SwitchDevice.ParseVlanList(rest.Substring(4).Trim());
            var cur = SwitchDevice.ParseVlanList(pc.trunkAllowedVlans);
            foreach (var v in add) { cur.Add(v); _sw.EnsureVlan(v); }
            pc.trunkAllowedVlans = SwitchDevice.ToCiscoVlanList(cur);
            return;
        }

        if (rest.StartsWith("remove ", StringComparison.OrdinalIgnoreCase))
        {
            var rem = SwitchDevice.ParseVlanList(rest.Substring(7).Trim());
            var cur = SwitchDevice.ParseVlanList(pc.trunkAllowedVlans);
            foreach (var v in rem) cur.Remove(v);
            pc.trunkAllowedVlans = SwitchDevice.ToCiscoVlanList(cur);
            return;
        }

        var set = SwitchDevice.ParseVlanList(rest);
        foreach (var v in set) _sw.EnsureVlan(v);
        pc.trunkAllowedVlans = SwitchDevice.ToCiscoVlanList(set);
    }

    private List<SwitchPortConfig> ParseInterfaceRange(string expr)
    {
        if (_sw == null) return null;
        if (string.IsNullOrWhiteSpace(expr)) return null;

        var result = new List<SwitchPortConfig>();

        if (expr.Contains(","))
        {
            var parts = expr.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var p = _sw.GetPort(SwitchDevice.NormalizeInterfaceName(part.Trim()));
                if (p != null && !result.Contains(p)) result.Add(p);
            }
            return result;
        }

        if (expr.Contains("-"))
        {
            var rr = expr.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (rr.Length != 2) return null;

            string left = rr[0].Trim();
            string right = rr[1].Trim();

            left = SwitchDevice.NormalizeInterfaceName(left);

            if (!right.Contains("/") && int.TryParse(right, out int rightNum))
            {
                int slash = left.LastIndexOf('/');
                if (slash < 0) return null;
                string prefix = left.Substring(0, slash + 1);
                right = prefix + rightNum;
            }
            right = SwitchDevice.NormalizeInterfaceName(right);

            int lslash = left.LastIndexOf('/');
            int rslash = right.LastIndexOf('/');
            if (lslash < 0 || rslash < 0) return null;

            string lprefix = left.Substring(0, lslash + 1);
            string rprefix = right.Substring(0, rslash + 1);
            if (!lprefix.Equals(rprefix, StringComparison.OrdinalIgnoreCase)) return null;

            if (!int.TryParse(left.Substring(lslash + 1), out int a)) return null;
            if (!int.TryParse(right.Substring(rslash + 1), out int b)) return null;

            if (a > b) { int t = a; a = b; b = t; }

            for (int i = a; i <= b; i++)
            {
                string ifn = $"{lprefix}{i}";
                var p = _sw.GetPort(ifn);
                if (p != null && !result.Contains(p)) result.Add(p);
            }

            return result;
        }

        var single = _sw.GetPort(SwitchDevice.NormalizeInterfaceName(expr.Trim()));
        if (single != null) result.Add(single);
        return result;
    }

    private static string ExtractPortChannelId(string cmd)
    {
        var lower = cmd.ToLowerInvariant();
        if (lower.Contains("port-channel"))
        {
            int idx = lower.IndexOf("port-channel", StringComparison.Ordinal);
            return cmd.Substring(idx + "port-channel".Length).Trim();
        }
        if (lower.Contains("po"))
        {
            int idx = lower.IndexOf("po", StringComparison.Ordinal);
            return cmd.Substring(idx + 2).Trim();
        }
        return "";
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

    private (string flag, string reason) GetMemberOperationalState(int groupId, SwitchPortConfig localMember)
    {
        if (localMember == null) return ("D", "No member");
        if (!localMember.adminUp) return ("D", "Administratively down");
        if (!_sw.HasLink(localMember.name)) return ("D", "Link down");

        var unity = _sw.GetUnityPortForInterface(localMember.name);
        if (unity == null || unity.connectedTo == null) return ("s", "No partner connected");

        if (unity.connectedTo.owner is not SwitchDevice remoteSw)
            return ("s", "Partner is not a switch");

        string remoteIf = unity.connectedTo.interfaceName;
        var remoteCfg = remoteSw.GetPort(remoteIf);
        if (remoteCfg == null)
            return ("s", "Partner port not found");

        if (remoteCfg.channelGroup != groupId || remoteCfg.channelMode == EtherChannelMode.None)
            return ("s", "Partner not in channel-group");

        if (!remoteCfg.adminUp) return ("s", "Partner admin down");
        if (!remoteSw.HasLink(remoteCfg.name)) return ("s", "Partner link down");

        if (!IsEtherChannelCompatible(localMember.channelMode, remoteCfg.channelMode))
        {
            if ((localMember.channelMode == EtherChannelMode.Passive) && (remoteCfg.channelMode == EtherChannelMode.Passive))
                return ("s", "LACP passive/passive");
            return ("s", "Mode mismatch");
        }

        return ("P", null);
    }

    private string GetProtocolNameForGroup(int groupId)
    {
        var members = _sw.GetChannelMembers(groupId);
        bool anyOn = false;
        foreach (var m in members)
        {
            if (m == null) continue;
            if (m.channelMode == EtherChannelMode.On) { anyOn = true; break; }
        }
        return anyOn ? "NONE(static)" : "LACP";
    }

    private string GetLikelySuspendReasonForGroup(int groupId)
    {
        var members = _sw.GetChannelMembers(groupId);
        foreach (var m in members)
        {
            if (m == null) continue;
            var st = GetMemberOperationalState(groupId, m);
            if (st.flag == "s" && !string.IsNullOrWhiteSpace(st.reason))
                return st.reason;
        }
        return "No compatible partner";
    }

    private void TryGetPartnerInfo(SwitchPortConfig localMember, out string partnerSystemId, out string partnerPort, out string partnerMode)
    {
        partnerSystemId = "-";
        partnerPort = "-";
        partnerMode = "-";

        var unity = _sw.GetUnityPortForInterface(localMember.name);
        if (unity == null || unity.connectedTo == null) return;
        if (unity.connectedTo.owner is not SwitchDevice remoteSw) return;

        partnerSystemId = remoteSw.BridgeId.mac;
        partnerPort = SwitchDevice.ShortIfName(unity.connectedTo.interfaceName);

        var rCfg = remoteSw.GetPort(unity.connectedTo.interfaceName);
        if (rCfg == null) return;

        partnerMode = rCfg.channelMode switch
        {
            EtherChannelMode.Active => "active",
            EtherChannelMode.Passive => "passive",
            EtherChannelMode.On => "on",
            _ => "none"
        };
    }

    private string HandleShow(string input)
    {
        if (_sw == null) return "% Device not ready.";

        if (input.Equals("show lacp neighbor", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("show lacp neighbors", StringComparison.OrdinalIgnoreCase))
            return BuildShowLacpNeighbor();

        if (input.StartsWith("show etherchannel port-channel", StringComparison.OrdinalIgnoreCase))
            return BuildShowEtherChannelPortChannel(input);

        if (input.Equals("show interfaces port-channel", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("show interfaces po", StringComparison.OrdinalIgnoreCase))
            return "% Incomplete command.";

        if (input.StartsWith("show interfaces port-channel ", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("show interfaces po", StringComparison.OrdinalIgnoreCase))
            return BuildShowPortChannelInterface(input);

        if (input.Equals("show etherchannel summary", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("show etherchannel", StringComparison.OrdinalIgnoreCase))
            return BuildShowEtherChannelSummary();

        if (input.Equals("show spanning-tree", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("show stp", StringComparison.OrdinalIgnoreCase))
            return BuildShowSpanningTree();

        if (input.Equals("show interfaces trunk", StringComparison.OrdinalIgnoreCase))
            return BuildShowTrunks();

        if (input.Equals("show interfaces", StringComparison.OrdinalIgnoreCase))
            return BuildShowInterfaces();

        if (input.Equals("show mac address-table", StringComparison.OrdinalIgnoreCase))
            return BuildShowMacTable();

        if (input.Equals("show vlan brief", StringComparison.OrdinalIgnoreCase))
            return BuildShowVlanBrief();

        return "% Invalid input detected at '^' marker.";
    }

    private string BuildShowEtherChannelPortChannel(string input)
    {

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        int groupId = -1;
        if (parts.Length >= 4)
        {
            string last = parts[3].Trim().ToLowerInvariant();
            if (last.StartsWith("po")) last = last.Substring(2);
            if (last.StartsWith("port-channel")) last = last.Substring("port-channel".Length);
            int.TryParse(last, out groupId);
        }

        _sw.RefreshEtherChannels();

        if (groupId > 0)
            return BuildShowEtherChannelPortChannelDetail(groupId);

        string o = "";
        o += "Port-channel Information\n";
        o += "Group  Port-channel  Protocol     Status   Members\n";
        o += "-----  -----------   ----------   ------   -----------------------------\n";

        foreach (var pc in _sw.portChannels)
        {
            if (pc == null) continue;

            bool formed = _sw.IsChannelFormed(pc.id);
            string status = formed && pc.protocolUp ? "Up" : "Down";
            string proto = GetProtocolNameForGroup(pc.id);

            string members = "";
            foreach (var m in _sw.GetChannelMembers(pc.id))
            {
                if (m == null) continue;
                var st = GetMemberOperationalState(pc.id, m);
                members += $"{st.flag}{SwitchDevice.ShortIfName(m.name)} ";
            }

            o += $"{pc.id.ToString().PadRight(5)}  {($"Po{pc.id}").PadRight(11)}  {proto.PadRight(10)}   {status.PadRight(6)}   {members.Trim()}\n";
        }

        return o.TrimEnd('\n');
    }

    private string BuildShowEtherChannelPortChannelDetail(int groupId)
    {
        var pc = _sw.GetPortChannel(groupId);
        if (pc == null) pc = _sw.GetOrCreatePortChannel(groupId);

        bool formed = _sw.IsChannelFormed(groupId);
        string proto = GetProtocolNameForGroup(groupId);

        string o = "";
        o += $"Port-channel{groupId} (Po{groupId})\n";
        o += $"  Protocol: {proto}\n";
        o += $"  Status: {(formed && pc.protocolUp ? "Up" : "Down")}\n";
        if (!formed) o += $"  Suspend Reason: {GetLikelySuspendReasonForGroup(groupId)}\n";
        o += "\n";

        o += "  Members:\n";
        o += "  Port        LocalMode  Flags  PartnerSystemID      PartnerPort  PartnerMode  Reason\n";
        o += "  ----------  ---------  -----  -------------------  ----------   -----------  -------------------------\n";

        foreach (var m in _sw.GetChannelMembers(groupId))
        {
            if (m == null) continue;

            var st = GetMemberOperationalState(groupId, m);
            string flags = st.flag == "P" ? "P" : (st.flag == "D" ? "D" : "s");
            string localMode = m.channelMode.ToString().ToLowerInvariant();

            TryGetPartnerInfo(m, out string partnerSys, out string partnerPort, out string partnerMode);

            string reason = st.reason ?? "";
            o += $"  {SwitchDevice.ShortIfName(m.name).PadRight(10)}  {localMode.PadRight(9)}  {flags.PadRight(5)}  {partnerSys.PadRight(19)}  {partnerPort.PadRight(10)}   {partnerMode.PadRight(11)}  {reason}\n";
        }

        return o.TrimEnd('\n');
    }

    private string BuildShowLacpNeighbor()
    {
        _sw.RefreshEtherChannels();

        string o = "";
        o += "LACP Neighbors\n";
        o += "Port        Group  Actor  PartnerSystemID      PartnerPort  Partner  State\n";
        o += "----------  -----  -----  -------------------  ----------   -------  ----------------\n";

        bool any = false;

        foreach (var p in _sw.ports)
        {
            if (p == null) continue;

            bool isLacp = p.channelMode == EtherChannelMode.Active || p.channelMode == EtherChannelMode.Passive;
            if (!isLacp) continue;

            any = true;

            string actor = p.channelMode == EtherChannelMode.Active ? "active" : "passive";
            string grp = (p.channelGroup > 0) ? p.channelGroup.ToString() : "-";

            string partnerSys = "-";
            string partnerPort = "-";
            string partnerMode = "-";
            string state = "";

            if (!p.adminUp) state = "AdminDown";
            else if (!_sw.HasLink(p.name)) state = "LinkDown";
            else
            {
                TryGetPartnerInfo(p, out partnerSys, out partnerPort, out partnerMode);

                if (partnerSys == "-") state = "NoPartner";
                else if (p.channelGroup <= 0) state = "NoGroup";
                else
                {
                    var st = GetMemberOperationalState(p.channelGroup, p);
                    if (st.flag == "P") state = "Up (Bundled)";
                    else if (st.flag == "s") state = $"Suspended ({st.reason})";
                    else state = $"Down ({st.reason})";
                }
            }

            o += $"{SwitchDevice.ShortIfName(p.name).PadRight(10)}  {grp.PadRight(5)}  {actor.PadRight(5)}  {partnerSys.PadRight(19)}  {partnerPort.PadRight(10)}   {partnerMode.PadRight(7)}  {state}\n";
        }

        if (!any) return "No LACP neighbors found.";
        return o.TrimEnd('\n');
    }

    private string BuildShowEtherChannelSummary()
    {
        _sw.RefreshEtherChannels();

        string o = "";
        o += "Flags:  D - down        P - bundled in port-channel\n";
        o += "        s - suspended   I - stand-alone\n\n";
        o += "Group  Port-channel  Protocol    Ports\n";
        o += "-----  -----------   --------    -----------------------------\n";

        foreach (var pc in _sw.portChannels)
        {
            if (pc == null) continue;

            bool formed = _sw.IsChannelFormed(pc.id);
            string proto = GetProtocolNameForGroup(pc.id);

            string poName = $"Po{pc.id}";
            string status = formed && pc.protocolUp ? "(SU)" : "(SD)";

            o += $"{pc.id.ToString().PadRight(5)} {poName.PadRight(11)} {proto.PadRight(11)} ";

            var members = _sw.GetChannelMembers(pc.id);
            foreach (var m in members)
            {
                if (m == null) continue;
                var st = GetMemberOperationalState(pc.id, m);
                o += $"{st.flag}{SwitchDevice.ShortIfName(m.name)} ";
            }

            o += $"{status}\n";
        }

        return o.TrimEnd('\n');
    }

    private string BuildShowPortChannelInterface(string input)
    {
        string idStr = ExtractPortChannelId(input);
        if (!int.TryParse(idStr, out int id)) return "% Invalid input detected at '^' marker.";

        var pc = _sw.GetPortChannel(id);
        if (pc == null) pc = _sw.GetOrCreatePortChannel(id);

        _sw.RefreshEtherChannels();

        bool formed = _sw.IsChannelFormed(id);
        string protoName = GetProtocolNameForGroup(id);

        string o = "";
        string full = $"Port-channel{id}";

        string lineProto = pc.protocolUp ? "up" : "down";
        o += $"{full} is {(pc.protocolUp ? "up" : "down")}, line protocol is {lineProto}\n";

        string bundle = (formed && pc.protocolUp) ? "(SU)" : "(SD)";
        o += $"  Protocol: {protoName}\n";
        o += $"  Bundle State: {bundle}\n";
        if (!formed)
            o += $"  Suspend Reason: {GetLikelySuspendReasonForGroup(id)}\n";

        o += $"  switchport mode: {pc.mode}\n";
        if (pc.mode == SwitchportMode.Access)
            o += $"  access vlan: {pc.accessVlan}\n";
        else
            o += $"  native vlan: {pc.trunkNativeVlan}\n  allowed vlans: {pc.trunkAllowedVlans}\n";

        o += "  Members:\n";
        o += "    Port        LocalMode  PartnerMode  Flags  Reason\n";
        o += "    ----------  ---------  ----------   -----  -------------------------\n";

        var members = _sw.GetChannelMembers(id);
        foreach (var m in members)
        {
            if (m == null) continue;

            var st = GetMemberOperationalState(id, m);

            TryGetPartnerInfo(m, out _, out _, out string partnerMode);
            string localMode = m.channelMode.ToString().ToLowerInvariant();
            string flags = st.flag == "P" ? "P" : (st.flag == "D" ? "D" : "s");
            string reason = st.reason ?? "";

            o += $"    {SwitchDevice.ShortIfName(m.name).PadRight(10)}  {localMode.PadRight(9)}  {partnerMode.PadRight(10)}  {flags.PadRight(5)}  {reason}\n";
        }

        o += $"  STP: {pc.stpRole} / {pc.stpState}\n";
        return o.TrimEnd('\n');
    }

    private string BuildShowSpanningTree()
    {
        _sw.RefreshEtherChannels();

        var (prio, mac) = _sw.BridgeId;
        bool isRoot = _sw.IsRootBridge;

        string rootMac = isRoot ? mac : (_sw.CurrentRoot != null ? _sw.CurrentRoot.BridgeId.mac : mac);
        int rootPrio = isRoot ? prio : (_sw.CurrentRoot != null ? _sw.CurrentRoot.BridgeId.priority : prio);

        string o = "";
        o += "VLAN0001\n";
        o += "  Spanning tree enabled protocol ieee\n";
        o += $"  Root ID    Priority    {rootPrio}\n";
        o += $"             Address     {rootMac}\n";
        o += $"             Cost        {(isRoot ? 0 : 4)}\n";
        o += $"             {(isRoot ? "This bridge is the root" : "Root port is selected")}\n\n";

        o += $"  Bridge ID  Priority    {prio}  (priority {prio})\n";
        o += $"             Address     {mac}\n\n";

        o += "Interface        Role Sts\n";
        o += "---------------- ---- ---\n";

        foreach (var p in _sw.ports)
        {
            if (p == null) continue;

            if (_sw.IsChannelMember(p.name, out int gid) && _sw.IsChannelFormed(gid))
                continue;

            string role = p.stpRole switch
            {
                StpPortRole.Root => "Root",
                StpPortRole.Designated => "Desg",
                StpPortRole.Alternate => "Altn",
                _ => "----"
            };

            string sts = p.stpState switch
            {
                StpPortState.Forwarding => "FWD",
                StpPortState.Blocking => "BLK",
                _ => "DSB"
            };

            o += $"{SwitchDevice.ShortIfName(p.name).PadRight(16)} {role.PadRight(4)} {sts}\n";
        }

        foreach (var pc in _sw.portChannels)
        {
            if (pc == null) continue;
            if (!_sw.IsChannelFormed(pc.id)) continue;

            string role = pc.stpRole switch
            {
                StpPortRole.Root => "Root",
                StpPortRole.Designated => "Desg",
                StpPortRole.Alternate => "Altn",
                _ => "----"
            };

            string sts = pc.stpState switch
            {
                StpPortState.Forwarding => "FWD",
                StpPortState.Blocking => "BLK",
                _ => "DSB"
            };

            o += $"{($"Po{pc.id}").PadRight(16)} {role.PadRight(4)} {sts}\n";
        }

        return o.TrimEnd('\n');
    }

    private string BuildShowTrunks()
    {
        _sw.RefreshEtherChannels();

        string output = "Port        Mode         Encapsulation  Status        Native vlan\n";
        output += "----------  -----------  -------------  ------------  ----------\n";

        foreach (var p in _sw.ports)
        {
            if (p == null) continue;
            if (p.mode != SwitchportMode.Trunk) continue;

            if (_sw.IsChannelMember(p.name, out int gid) && _sw.IsChannelFormed(gid))
                continue;

            string status = (p.adminUp && p.protocolUp && p.stpState == StpPortState.Forwarding) ? "trunking" : "not-trunking";
            output += $"{SwitchDevice.ShortIfName(p.name).PadRight(10)}  {"on".PadRight(11)}  {"802.1q".PadRight(13)}  {status.PadRight(12)}  {p.trunkNativeVlan}\n";
        }

        foreach (var pc in _sw.portChannels)
        {
            if (pc == null) continue;
            if (!_sw.IsChannelFormed(pc.id)) continue;
            if (pc.mode != SwitchportMode.Trunk) continue;

            string status = (pc.protocolUp && pc.stpState == StpPortState.Forwarding) ? "trunking" : "not-trunking";
            output += $"{($"Po{pc.id}").PadRight(10)}  {"on".PadRight(11)}  {"802.1q".PadRight(13)}  {status.PadRight(12)}  {pc.trunkNativeVlan}\n";
        }

        output += "\nPort        Vlans allowed on trunk\n";
        output += "----------  ---------------------\n";

        foreach (var p in _sw.ports)
        {
            if (p == null) continue;
            if (p.mode != SwitchportMode.Trunk) continue;

            if (_sw.IsChannelMember(p.name, out int gid) && _sw.IsChannelFormed(gid))
                continue;

            string list = string.IsNullOrWhiteSpace(p.trunkAllowedVlans) ? "none" : p.trunkAllowedVlans;
            output += $"{SwitchDevice.ShortIfName(p.name).PadRight(10)}  {list}\n";
        }

        foreach (var pc in _sw.portChannels)
        {
            if (pc == null) continue;
            if (!_sw.IsChannelFormed(pc.id)) continue;
            if (pc.mode != SwitchportMode.Trunk) continue;

            string list = string.IsNullOrWhiteSpace(pc.trunkAllowedVlans) ? "none" : pc.trunkAllowedVlans;
            output += $"{($"Po{pc.id}").PadRight(10)}  {list}\n";
        }

        return output.TrimEnd('\n');
    }

    private string BuildShowInterfaces()
    {
        _sw.RefreshEtherChannels();

        string output = "";

        foreach (var p in _sw.ports)
        {
            if (p == null) continue;

            string adminState = p.adminUp ? "up" : "administratively down";
            string protoState = (p.adminUp && p.protocolUp) ? "up" : "down";

            output += $"{p.name} is {adminState}, line protocol is {protoState}\n";

            if (_sw.IsChannelMember(p.name, out int gid) && _sw.IsChannelFormed(gid))
            {
                string memberFlag = (!p.adminUp || !_sw.HasLink(p.name)) ? "down" : (p.channelSuspended ? "suspended" : "bundled");
                output += $"  Member of Port-channel{gid} ({memberFlag})\n";
                output += $"  STP: {p.stpRole} / {p.stpState}\n\n";
                continue;
            }

            output += $"  switchport mode: {p.mode}\n";
            if (p.mode == SwitchportMode.Access)
                output += $"  access vlan: {p.accessVlan}\n";
            else
                output += $"  native vlan: {p.trunkNativeVlan}\n  allowed vlans: {p.trunkAllowedVlans}\n";

            if (p.channelGroup > 0 && p.channelMode != EtherChannelMode.None)
                output += $"  etherchannel: group {p.channelGroup}, mode {p.channelMode}, {(p.channelSuspended ? "suspended" : "bundled/standalone")}\n";

            output += $"  STP: {p.stpRole} / {p.stpState}\n\n";
        }

        foreach (var pc in _sw.portChannels)
        {
            if (pc == null) continue;

            output += $"{pc.name} is {(pc.protocolUp ? "up" : "down")}, line protocol is {(pc.protocolUp ? "up" : "down")}\n";
            output += $"  switchport mode: {pc.mode}\n";
            if (pc.mode == SwitchportMode.Access)
                output += $"  access vlan: {pc.accessVlan}\n";
            else
                output += $"  native vlan: {pc.trunkNativeVlan}\n  allowed vlans: {pc.trunkAllowedVlans}\n";
            output += $"  STP: {pc.stpRole} / {pc.stpState}\n\n";
        }

        return output.TrimEnd('\n');
    }

    private string BuildShowMacTable()
    {
        _sw.PurgeAgedMacs();
        string output = "Vlan    Mac Address       Type        Ports\n";
        output += "----    -----------       --------    -----\n";
        foreach (var e in _sw.macTable)
        {
            if (e == null) continue;
            output += $"{e.vlan.ToString().PadRight(8)}{e.mac.PadRight(18)}DYNAMIC     {SwitchDevice.ShortIfName(e.portIfName)}\n";
        }
        return output.TrimEnd('\n');
    }

    private string BuildShowVlanBrief()
    {
        string output = "VLAN  Name                             Status    Ports\n";
        output += "----  -------------------------------  --------  -------------------------------\n";

        foreach (var v in _sw.vlans)
        {
            if (v == null) continue;
            string status = v.active ? "active" : "suspended";

            string ports = "";
            foreach (var p in _sw.ports)
            {
                if (p == null) continue;
                if (p.mode != SwitchportMode.Access) continue;
                if (p.accessVlan == v.id)
                    ports += $"{SwitchDevice.ShortIfName(p.name)} ";
            }

            output += $"{v.id.ToString().PadRight(4)}  {v.name.PadRight(31)}  {status.PadRight(8)}  {ports.Trim()}\n";
        }

        return output.TrimEnd('\n');
    }
}
