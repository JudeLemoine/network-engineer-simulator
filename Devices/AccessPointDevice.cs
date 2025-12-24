using System.Collections.Generic;
using UnityEngine;

public enum WifiBand
{
    Band2_4GHz = 0,
    Band5GHz = 1
}

public enum WifiSecurityMode
{
    Open = 0,
    WPA2_PSK = 1
}

public class AccessPointDevice : SwitchDevice
{
    [Header("Wi-Fi")]
    public string ssid = "AP";
    public WifiBand band = WifiBand.Band5GHz;

    [Tooltip("Clients must be within this distance to associate.")]
    public float rangeMeters = 12f;

    [Tooltip("Optional: if true, require line-of-sight (simple raycast) to associate.")]
    public bool requireLineOfSight = false;

    [Tooltip("Layer mask for obstacles if using line-of-sight.")]
    public LayerMask obstacleMask = ~0;

    [Header("Security")]
    public WifiSecurityMode securityMode = WifiSecurityMode.Open;

    [Tooltip("Used only when securityMode is WPA2_PSK")]
    public string passphrase = "password123";

    private int _nextClientIndex = 1;

    private readonly Dictionary<int, Port> _clientPorts = new Dictionary<int, Port>();

    public bool CanSeeAdapter(WirelessAdapter adapter)
    {
        if (adapter == null) return false;

        float d = Vector3.Distance(transform.position, adapter.transform.position);
        if (d > rangeMeters) return false;

        if (!requireLineOfSight) return true;

        Vector3 a = transform.position;
        Vector3 b = adapter.transform.position;
        Vector3 dir = (b - a);
        float dist = dir.magnitude;
        if (dist < 0.001f) return true;

        if (Physics.Raycast(a, dir.normalized, dist, obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    public bool RequiresKey => securityMode == WifiSecurityMode.WPA2_PSK;

    public bool ValidateKey(string providedKey)
    {
        if (!RequiresKey) return true;

        providedKey = (providedKey ?? "").Trim();
        string actual = (passphrase ?? "").Trim();

        if (string.IsNullOrWhiteSpace(actual)) return false;
        return string.Equals(providedKey, actual, System.StringComparison.Ordinal);
    }

    public Port AttachClient(WirelessAdapter adapter)
    {
        if (adapter == null) return null;

        int key = adapter.GetInstanceID();
        if (_clientPorts.TryGetValue(key, out Port existing) && existing != null)
            return existing;

        string ifName = $"Wlan0/{_nextClientIndex:00}";
        _nextClientIndex++;

        ports.Add(new SwitchPortConfig { name = ifName, adminUp = true });

        GameObject go = new GameObject(ifName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var p = go.AddComponent<Port>();
        p.owner = this;
        p.medium = PortMedium.Wireless;
        p.interfaceName = ifName;
        p.portName = ifName;

        _clientPorts[key] = p;
        return p;
    }

    public void DetachClient(WirelessAdapter adapter)
    {
        if (adapter == null) return;

        int key = adapter.GetInstanceID();
        if (!_clientPorts.TryGetValue(key, out Port apPort) || apPort == null)
            return;

        if (apPort.connectedTo != null)
        {
            var other = apPort.connectedTo;
            apPort.connectedTo = null;
            other.connectedTo = null;

            apPort.cable = null;
            other.cable = null;
        }

        for (int i = ports.Count - 1; i >= 0; i--)
        {
            if (ports[i] != null && ports[i].name == apPort.interfaceName)
            {
                ports.RemoveAt(i);
                break;
            }
        }

        _clientPorts.Remove(key);

        Destroy(apPort.gameObject);
    }
}
