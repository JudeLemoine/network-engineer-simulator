using System;
using System.Collections.Generic;


public static class NetworkUtils
{
    public const string UnassignedIp = "unassigned";

    public const int MaxVlanId = 4094;
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

    public static bool IsValidVlanId(int vlan) => vlan >= 1 && vlan <= MaxVlanId;

    public static HashSet<int> ParseVlanList(string vlanList)
    {
        var set = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(vlanList)) return set;

        if (vlanList.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            for (int v = 1; v <= MaxVlanId; v++) set.Add(v);
            return set;
        }

        foreach (var token in vlanList.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim();
            if (string.IsNullOrWhiteSpace(t)) continue;

            if (t.Contains('-'))
            {
                var rr = t.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (rr.Length != 2) continue;
                if (!int.TryParse(rr[0].Trim(), out int a) || !int.TryParse(rr[1].Trim(), out int b)) continue;
                if (a > b) { int tmp = a; a = b; b = tmp; }
                for (int v = a; v <= b; v++)
                    if (IsValidVlanId(v)) set.Add(v);
            }
            else
            {
                if (int.TryParse(t, out int v) && IsValidVlanId(v))
                    set.Add(v);
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
            if (v == prev + 1) { prev = v; continue; }
            ranges.Add(start == prev ? start.ToString() : $"{start}-{prev}");
            start = prev = v;
        }

        ranges.Add(start == prev ? start.ToString() : $"{start}-{prev}");
        return string.Join(",", ranges);
    }
}
