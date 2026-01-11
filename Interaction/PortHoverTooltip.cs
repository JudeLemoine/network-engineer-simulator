using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class PortHoverTooltip : MonoBehaviour
{
    [Header("Raycast")]
    public float maxDistance = 5f;

    [Header("UI Placement")]
    public Vector2 screenOffset = new Vector2(0, 55);

    [Header("Text")]
    public int baseFontSize = 16;
    public bool showPortConnectionState = false;

    [Header("Device Hover")]
    public int maxInterfacesToShow = 64;
    public bool showDeviceTooltipWhenPoweredOff = true;
    public float maxTooltipWidthPx = 780f;

    [Header("Flicker Reduction")]
    public float hoverHoldSeconds = 0.01f;

    private Port _hoveredPort;
    private RouterModuleSlot _hoveredSlot;
    private DevicePowerSwitch _hoveredPowerSwitch;
    private Device _hoveredDevice;
    private RackSlotInteractable _hoveredRackSlot;
    private RackSlotHoverHighlight _currentSlotHighlight;

    private float _lastValidHoverTime = -999f;

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked || TerminalScreen.IsAnyTerminalFocused)
        {
            ClearHoverImmediate();
            return;
        }

        Ray ray = new Ray(transform.position, transform.forward);
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, maxDistance);

        bool foundValid = false;

        if (hitSomething)
        {
            if (!IsSuppressedSurface(hit.collider))
            {
                Port p = hit.collider.GetComponentInParent<Port>();
                RouterModuleSlot s = null;
                DevicePowerSwitch ps = null;
                RackSlotInteractable rs = null;
                Device d = null;

                if (p == null) s = hit.collider.GetComponentInParent<RouterModuleSlot>();
                if (p == null && s == null) ps = hit.collider.GetComponentInParent<DevicePowerSwitch>();

                if (p == null && s == null && ps == null)
                {
                    rs = hit.collider.GetComponentInParent<RackSlotInteractable>();
                    if (rs != null)
                    {
                        var deviceInSlot = rs.GetComponentInChildren<Device>(true);
                        if (deviceInSlot != null) d = deviceInSlot;
                    }
                }

                if (p == null && s == null && ps == null && d == null)
                    d = hit.collider.GetComponentInParent<Device>();

                if (d != null && !d.IsPoweredOn && !showDeviceTooltipWhenPoweredOff)
                    d = null;

                if (p != null || s != null || ps != null || rs != null || d != null)
                {
                    _hoveredPort = p;
                    _hoveredSlot = s;
                    _hoveredPowerSwitch = ps;
                    _hoveredRackSlot = rs;
                    _hoveredDevice = d;

                    UpdateRackSlotHighlight(rs);

                    foundValid = true;
                    _lastValidHoverTime = Time.unscaledTime;
                }
            }
        }

        if (!foundValid)
        {
            if ((Time.unscaledTime - _lastValidHoverTime) > hoverHoldSeconds)
                ClearHoverImmediate();
        }
    }


private void UpdateRackSlotHighlight(RackSlotInteractable rs)
{
    var next = rs != null ? rs.GetComponent<RackSlotHoverHighlight>() : null;

    if (_currentSlotHighlight == next)
        return;

    if (_currentSlotHighlight != null)
        _currentSlotHighlight.SetHovered(false);

    _currentSlotHighlight = next;

    if (_currentSlotHighlight != null)
        _currentSlotHighlight.SetHovered(true);
}

private void ClearHoverImmediate()
    {
        _hoveredPort = null;
        _hoveredSlot = null;
        _hoveredPowerSwitch = null;
        _hoveredRackSlot = null;
        _hoveredDevice = null;
            UpdateRackSlotHighlight(null);
}

    private void OnGUI()
    {
        string text = null;
        int interfaceCountForSizing = 0;

        if (_hoveredPort != null)
        {
            string portLabel = string.IsNullOrWhiteSpace(_hoveredPort.portName) ? "Port" : _hoveredPort.portName;
            string ifLabel = string.IsNullOrWhiteSpace(_hoveredPort.interfaceName) ? "" : _hoveredPort.interfaceName;

            text = string.IsNullOrWhiteSpace(ifLabel) ? portLabel : $"{portLabel} ({ifLabel})";

            if (showPortConnectionState)
            {
                string state = _hoveredPort.IsConnected ? "connected" : "disconnected";
                text += $"  [{state}]";
            }
        }
        else if (_hoveredSlot != null)
        {
            string slotType = _hoveredSlot.slotType.ToString();
            string installed = _hoveredSlot.installedModule.ToString();
            text = $"Slot{_hoveredSlot.slotIndex} ({slotType}) - {installed}";
        }
        else if (_hoveredPowerSwitch != null)
        {
            Device d = _hoveredPowerSwitch.targetDevice != null
                ? _hoveredPowerSwitch.targetDevice
                : _hoveredPowerSwitch.GetComponentInParent<Device>();

            string state = (d != null && d.IsPoweredOn) ? "ON" : "OFF";
            if (d != null)
                text = $"Power: {state}\n{GetDeviceTypeLabel(d)}: {GetDeviceDisplayName(d)}";
            else
                text = $"Power: {state}";
        }
        else if (_hoveredDevice != null)
        {
            text = BuildDeviceTooltipLive(_hoveredDevice, out interfaceCountForSizing);
        }

        if (string.IsNullOrWhiteSpace(text))
            return;

        int fontSize = ComputeFontSize(baseFontSize, interfaceCountForSizing);

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            alignment = TextAnchor.UpperLeft,
            wordWrap = false
        };

        float maxWidth = Mathf.Min(maxTooltipWidthPx, Screen.width * 0.70f);

        float width = Mathf.Min(maxWidth, style.CalcSize(new GUIContent(GetLongestLine(text))).x);
        width = Mathf.Max(width, 260f);

        int lines = CountLines(text);
        float lineH = style.lineHeight > 0 ? style.lineHeight : (fontSize + 4);
        float height = (lines * lineH) + 2f;

        float x = (Screen.width * 0.5f) + screenOffset.x - (width * 0.5f);
        float y = (Screen.height * 0.5f) + screenOffset.y;

        Rect bg = new Rect(x - 10, y - 8, width + 20, height + 16);
        GUI.Box(bg, GUIContent.none);
        GUI.Label(new Rect(x, y, width, height), text, style);
    }

    private bool IsSuppressedSurface(Collider col)
    {
        if (col == null) return false;

        if (col.GetComponentInParent<SuppressHoverTooltip>() != null)
            return true;

        Transform t = col.transform;
        while (t != null)
        {
            var ts = t.GetComponent<TerminalScreen>();
            if (ts != null)
            {

                if (t.GetComponent<Device>() == null)
                    return true;
            }
            t = t.parent;
        }

        return false;
    }

    private string BuildDeviceTooltipLive(Device d, out int interfaceCountUsed)
    {
        interfaceCountUsed = 0;
        var sb = new StringBuilder();

        string type = GetDeviceTypeLabel(d);
        string name = GetDeviceDisplayName(d);
        string power = d.IsPoweredOn ? "ON" : "OFF";

        sb.AppendLine($"{type}: {name}");
        sb.AppendLine($"Power: {power}");
        

var ri = d.GetComponentInParent<RackInstallation>();
if (ri != null)
{
    string a = ri.StartSlotLabel;
    string b = ri.EndSlotLabel;
    if (!string.IsNullOrWhiteSpace(a) || !string.IsNullOrWhiteSpace(b))
    {
        if (string.IsNullOrWhiteSpace(b) || b == a)
            sb.AppendLine($"Rack: {a}");
        else
            sb.AppendLine($"Rack: {a} - {b}");
    }
}
sb.AppendLine("");
        sb.AppendLine("Interfaces:");

        bool showAll = (d is SwitchDevice);
        int effectiveMax = Mathf.Max(1, maxInterfacesToShow);
        if (showAll) effectiveMax = int.MaxValue;

        var portLines = BuildInterfaceLinesFromPortsOwnedByDevice(d, effectiveMax, out int used);
        interfaceCountUsed = used;

        if (portLines.Count == 0) sb.AppendLine("  (none)");
        else foreach (var line in portLines) sb.AppendLine(line);

        return sb.ToString().TrimEnd();
    }

    private List<string> BuildInterfaceLinesFromPortsOwnedByDevice(Device device, int maxToShow, out int usedCount)
    {
        usedCount = 0;

        var ports = device.GetComponentsInChildren<Port>(true);
        var byIf = new Dictionary<string, Port>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in ports)
        {
            if (p == null) continue;
            if (p.owner != null && p.owner != device) continue;

            string raw = !string.IsNullOrWhiteSpace(p.interfaceName) ? p.interfaceName : p.portName;
            if (string.IsNullOrWhiteSpace(raw)) continue;

            string canon = RouterDevice.NormalizeInterfaceName(raw);
            if (!byIf.ContainsKey(canon)) byIf.Add(canon, p);
        }

        var keys = new List<string>(byIf.Keys);
        keys.Sort(StringComparer.OrdinalIgnoreCase);

        var lines = new List<string>();

        foreach (var ifName in keys)
        {
            if (usedCount >= maxToShow) break;

            var port = byIf[ifName];

            string admin = device.IsPoweredOn ? "up" : "down";
            string proto = (device.IsPoweredOn && port != null && port.connectedTo != null) ? "up" : "down";

            lines.Add($"  {ifName}  {admin}/{proto}");
            usedCount++;
        }

        return lines;
    }

    private static int ComputeFontSize(int baseSize, int interfaceCount)
    {
        if (interfaceCount <= 0) return baseSize;
        if (interfaceCount >= 48) return Mathf.Max(10, baseSize - 6);
        if (interfaceCount >= 36) return Mathf.Max(11, baseSize - 5);
        if (interfaceCount >= 28) return Mathf.Max(12, baseSize - 4);
        if (interfaceCount >= 20) return Mathf.Max(13, baseSize - 3);
        if (interfaceCount >= 14) return Mathf.Max(14, baseSize - 2);
        return baseSize;
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int count = 1;
        for (int i = 0; i < text.Length; i++)
            if (text[i] == '\n') count++;
        return count;
    }

    private static string GetLongestLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string[] lines = text.Split('\n');
        string best = "";
        foreach (var l in lines)
        {
            string s = l.TrimEnd('\r');
            if (s.Length > best.Length) best = s;
        }
        return best;
    }

    private static string GetDeviceDisplayName(Device d)
    {
        if (d == null) return "Unknown";
        return string.IsNullOrWhiteSpace(d.deviceName) ? d.gameObject.name : d.deviceName;
    }

    private static string GetDeviceTypeLabel(Device d)
    {
        if (d == null) return "Device";
        if (d is RouterDevice) return "Router";
        if (d is AccessPointDevice) return "Access Point";
        if (d is SwitchDevice) return "Switch";
        if (d is PcDevice) return "PC";
        return d.GetType().Name;
    }
}
