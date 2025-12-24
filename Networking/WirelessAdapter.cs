using UnityEngine;

public class WirelessAdapter : MonoBehaviour
{
    [Header("Capabilities")]
    public bool supports2_4GHz = true;
    public bool supports5GHz = true;

    [Header("Join Settings")]
    public bool autoJoin = true;
    public string desiredSsid = "AP";

    [Tooltip("Stored Wi-Fi key for WPA2-PSK networks (optional).")]
    public string storedKey = "";

    [Tooltip("How often to scan for APs (seconds).")]
    public float scanInterval = 0.75f;

    [Header("Runtime")]
    public bool isAssociated;
    public AccessPointDevice currentAp;

    private Port _wirelessPort;
    private float _nextScan;

    private void Awake()
    {
        EnsureWirelessPort();
    }

    private void Update()
    {
        if (!autoJoin) return;

        if (Time.time < _nextScan) return;
        _nextScan = Time.time + Mathf.Max(0.15f, scanInterval);

        if (currentAp != null)
        {

            if (!currentAp.CanSeeAdapter(this))
            {
                Disconnect();
            }
            return;
        }

        TryAutoJoin();
    }

    private void EnsureWirelessPort()
    {
        if (_wirelessPort != null) return;

        var ports = GetComponentsInChildren<Port>(true);
        foreach (var p in ports)
        {
            if (p != null && p.medium == PortMedium.Wireless)
            {
                _wirelessPort = p;
                return;
            }
        }

        GameObject go = new GameObject("Wireless0");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        _wirelessPort = go.AddComponent<Port>();
        _wirelessPort.owner = GetComponentInParent<Device>();
        _wirelessPort.medium = PortMedium.Wireless;
        _wirelessPort.interfaceName = "Wireless0";
        _wirelessPort.portName = "Wireless0";
    }

    private bool SupportsBand(WifiBand band)
    {
        return band switch
        {
            WifiBand.Band2_4GHz => supports2_4GHz,
            WifiBand.Band5GHz => supports5GHz,
            _ => false
        };
    }

    private void TryAutoJoin()
    {
        EnsureWirelessPort();

        AccessPointDevice best = null;
        float bestDist = float.MaxValue;

        var aps = FindObjectsOfType<AccessPointDevice>(true);
        foreach (var ap in aps)
        {
            if (ap == null) continue;
            if (!ap.IsPoweredOn) continue;

            if (!string.IsNullOrWhiteSpace(desiredSsid) &&
                !string.Equals(ap.ssid, desiredSsid, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (!SupportsBand(ap.band)) continue;
            if (!ap.CanSeeAdapter(this)) continue;

            if (ap.RequiresKey && !ap.ValidateKey(storedKey))
                continue;

            float d = Vector3.Distance(transform.position, ap.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = ap;
            }
        }

        if (best != null)
            Connect(best, null);
    }

    public bool Connect(AccessPointDevice ap, string providedKey)
    {
        if (ap == null) return false;
        if (!SupportsBand(ap.band)) return false;
        if (!ap.CanSeeAdapter(this)) return false;

        EnsureWirelessPort();

        string keyToUse = (providedKey ?? "").Trim();
        if (string.IsNullOrWhiteSpace(keyToUse))
            keyToUse = (storedKey ?? "").Trim();

        if (ap.RequiresKey && !ap.ValidateKey(keyToUse))
            return false;

        Disconnect();

        Port apPort = ap.AttachClient(this);
        if (apPort == null) return false;

        _wirelessPort.connectedTo = apPort;
        apPort.connectedTo = _wirelessPort;

        currentAp = ap;
        isAssociated = true;

        return true;
    }

    public bool Connect(AccessPointDevice ap)
    {
        return Connect(ap, null);
    }

    public void Disconnect()
    {
        if (!isAssociated && currentAp == null) return;

        if (_wirelessPort != null && _wirelessPort.connectedTo != null)
        {
            var apPort = _wirelessPort.connectedTo;
            _wirelessPort.connectedTo = null;

            if (apPort != null)
                apPort.connectedTo = null;
        }

        if (currentAp != null)
        {

            currentAp.DetachClient(this);
        }

        currentAp = null;
        isAssociated = false;
    }

    public void SetStoredKey(string key)
    {
        storedKey = (key ?? "").Trim();
    }

    public void ClearStoredKey()
    {
        storedKey = "";
    }
}
