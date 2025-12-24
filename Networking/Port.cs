using UnityEngine;

public enum PortMedium
{
    Ethernet = 0,
    RS232 = 1,
    Console = 2,
    Serial = 3,

    Wireless = 4,

    Power = 5
}

public class Port : MonoBehaviour
{
    [Header("Port Identity")]
    public string portName = "Gi0/0";

    public string interfaceName = "GigabitEthernet0/0";

    [Header("Port Type")]
    public PortMedium medium = PortMedium.Ethernet;

    [Header("Ownership / Link")]
    public Device owner;
    public Port connectedTo;

    public CableVisual cable;

    public bool IsConnected => connectedTo != null;

    [Header("Link LED / Status Colors")]
    [Tooltip("Renderer to tint for link status. If null, uses this object's Renderer.")]
    public Renderer statusRenderer;

    [Tooltip("If enabled, writes the color to the material's emission too (if supported).")]
    public bool useEmission = false;

    [Tooltip("Disconnected / no cable")]
    public Color colorDisconnected = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Tooltip("Admin down (shutdown)")]
    public Color colorAdminDown = new Color(0.20f, 0.20f, 0.20f, 1f);

    [Tooltip("Cabled but link/protocol down (wrong cable, peer off, etc.)")]
    public Color colorLinkDown = new Color(0.90f, 0.20f, 0.20f, 1f);

    [Tooltip("Operational link up")]
    public Color colorLinkUp = new Color(0.20f, 0.90f, 0.20f, 1f);

    [Tooltip("When this port is selected as the first endpoint.")]
    public Color highlightColor = Color.yellow;

    [Tooltip("How often to refresh link LED state (seconds). Lower = more responsive.")]
    public float statusRefreshInterval = 0.15f;

    private bool _highlighted;
    private float _nextRefresh;

    private void Awake()
    {
        if (statusRenderer == null)
            statusRenderer = GetComponent<Renderer>();

        ApplyStatusColor(ComputeStatusColor());
    }

    private void Update()
    {
        if (Time.time < _nextRefresh) return;
        _nextRefresh = Time.time + Mathf.Max(0.02f, statusRefreshInterval);

        Color c = _highlighted ? highlightColor : ComputeStatusColor();
        ApplyStatusColor(c);
    }

    public void SetHighlighted(bool on)
    {
        _highlighted = on;

        Color c = _highlighted ? highlightColor : ComputeStatusColor();
        ApplyStatusColor(c);
        _nextRefresh = 0f;
    }

    public bool CanConnectTo(Port other, out string reason)
    {
        if (other == null) { reason = "Other port is null"; return false; }
        if (other == this) { reason = "Cannot connect port to itself"; return false; }

        if (owner == null) { reason = "This port owner is NULL"; return false; }
        if (other.owner == null) { reason = "Other port owner is NULL"; return false; }

        if (owner == other.owner) { reason = "Same device (owner == other.owner)"; return false; }

        if (IsConnected) { reason = "This port is already connected"; return false; }
        if (other.IsConnected) { reason = "Other port is already connected"; return false; }

        if (!IsMediumCompatible(this.medium, other.medium))
        {
            reason = $"Incompatible cable/port types: {this.medium} ↔ {other.medium}";
            return false;
        }

        reason = "";
        return true;
    }

    public bool CanConnectTo(Port other)
    {
        return CanConnectTo(other, out _);
    }

    private static bool IsMediumCompatible(PortMedium a, PortMedium b)
    {

        if (a == PortMedium.Wireless || b == PortMedium.Wireless)
            return false;

        if (a == PortMedium.Power || b == PortMedium.Power)
            return a == PortMedium.Power && b == PortMedium.Power;

        if (a == PortMedium.Ethernet || b == PortMedium.Ethernet)
            return a == PortMedium.Ethernet && b == PortMedium.Ethernet;

        if (a == PortMedium.Serial || b == PortMedium.Serial)
            return a == PortMedium.Serial && b == PortMedium.Serial;

        if (a == PortMedium.RS232)
            return b == PortMedium.Console;

        if (b == PortMedium.RS232)
            return a == PortMedium.Console;

        return false;
    }

    private enum LinkState
    {
        Disconnected,
        AdminDown,
        LinkDown,
        LinkUp
    }

    private Color ComputeStatusColor()
    {
        LinkState state = GetLinkState();
        switch (state)
        {
            case LinkState.Disconnected: return colorDisconnected;
            case LinkState.AdminDown: return colorAdminDown;
            case LinkState.LinkDown: return colorLinkDown;
            case LinkState.LinkUp: return colorLinkUp;
            default: return colorDisconnected;
        }
    }

    private LinkState GetLinkState()
    {

        if (!IsConnected || connectedTo == null) return LinkState.Disconnected;

        if (owner == null || connectedTo.owner == null) return LinkState.LinkDown;

        if (medium == PortMedium.Power)
        {
            return (owner.IsPoweredOn && connectedTo.owner.IsPoweredOn) ? LinkState.LinkUp : LinkState.LinkDown;
        }

        if (medium == PortMedium.Wireless)
            return LinkState.LinkDown;

        if (!owner.IsPoweredOn || !connectedTo.owner.IsPoweredOn) return LinkState.LinkDown;

        if (owner is RouterDevice rd)
        {
            var itf = rd.GetInterface(interfaceName);
            if (itf != null)
            {
                if (!itf.adminUp) return LinkState.AdminDown;
                return itf.protocolUp ? LinkState.LinkUp : LinkState.LinkDown;
            }
        }

        if (owner is SwitchDevice sw)
        {
            var p = sw.GetPort(interfaceName);
            if (p == null && !string.IsNullOrWhiteSpace(portName))
                p = sw.GetPort(portName);

            if (p != null)
            {
                if (!p.adminUp) return LinkState.AdminDown;
                return p.protocolUp ? LinkState.LinkUp : LinkState.LinkDown;
            }
        }

        return LinkState.LinkUp;
    }

    private void ApplyStatusColor(Color c)
    {
        if (statusRenderer == null) return;

        var mat = statusRenderer.material;
        mat.color = c;

        if (useEmission)
        {
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c);
            }
        }
    }
}
