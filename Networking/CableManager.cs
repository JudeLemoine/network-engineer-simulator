using System.Collections.Generic;
using UnityEngine;

public class CableManager : MonoBehaviour
{
    public static CableManager Instance;

    public static bool IsAnyCableMenuOpen { get; private set; }

    [Header("Prefab")]
    public GameObject cablePrefab;

    [Header("UI")]
    public int fontSize = 18;
    public Vector2 panelSize = new Vector2(520, 360);

    [Header("Debug / Safety")]
    public bool resetConnectionsOnPlay = true;

    private Port _selectedPort;

    private bool _pickerOpen;
    private Port _pickerPort;
    private CableVisual.CableType _pickedType = CableVisual.CableType.Auto;

    private string _toast = "";
    private float _toastUntil = 0f;

    void Awake()
    {
        Instance = this;

        if (resetConnectionsOnPlay)
            ResetAllConnections();
    }

    private void Update()
    {
        if (!_pickerOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TerminalScreen.LastEscapeHandledFrame = Time.frameCount;
            ClosePicker(clearSelection: true);
        }
    }

    public void ClickPort(Port port)
    {
        if (port == null) return;

        if (_pickerOpen)
            return;

        if (_selectedPort == null && port.IsConnected)
        {
            Disconnect(port);
            return;
        }

        if (_selectedPort == null)
        {
            _pickerPort = port;
            OpenPickerForPort(_pickerPort);
            return;
        }

        if (_selectedPort == port)
        {
            _selectedPort.SetHighlighted(false);
            _selectedPort = null;
            return;
        }

        TryConnect(_selectedPort, port, _pickedType);

        _selectedPort.SetHighlighted(false);
        _selectedPort = null;
    }

    private void OpenPickerForPort(Port port)
    {
        if (port == null) return;

        _pickerOpen = true;
        IsAnyCableMenuOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _pickedType = CableVisual.CableType.Auto;
    }

    private void ClosePicker(bool clearSelection)
    {
        _pickerOpen = false;
        IsAnyCableMenuOpen = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (clearSelection)
        {
            _pickerPort = null;
            _pickedType = CableVisual.CableType.Auto;
        }
    }

    private void TryConnect(Port a, Port b, CableVisual.CableType picked)
    {
        if (a == null || b == null) return;

        if (!a.CanConnectTo(b, out string reason))
        {
            Toast(reason);
            return;
        }

        PortMedium medium = a.medium;

        if (medium == PortMedium.Power)
        {
            if (picked != CableVisual.CableType.Auto)
            {
                Toast("Power ports currently use Auto (Power Cable).");
                picked = CableVisual.CableType.Auto;
            }

            Connect(a, b, CableVisual.CableType.Auto, serialEndAIsDCE: false, linkUp: true);
            UpdatePowerAfterChange(a, b);
            return;
        }

        var recommended = GetRecommendedCableType(a, b, medium);

        CableVisual.CableType effective = picked == CableVisual.CableType.Auto ? recommended : picked;

        if (!IsCableTypeAllowedForMedium(effective, medium))
        {
            Toast($"That cable type does not work for {medium} ports.");
            return;
        }

        bool linkUp = true;
        if (medium == PortMedium.Ethernet)
        {
            if (effective != recommended)
            {
                linkUp = false;
                Toast($"Not recommended: {effective} for this connection. Use {recommended} instead (or Auto). Link will stay down.");
            }
        }

        bool serialEndAIsDCE = true;
        if (medium == PortMedium.Serial)
        {
            if (picked == CableVisual.CableType.Auto)
            {
                serialEndAIsDCE = true;
                effective = recommended;
            }
            else
            {
                serialEndAIsDCE = (effective == CableVisual.CableType.SerialDCE);
                Toast("Serial note: DCE side provides clocking (clock rate). Typically one side is DCE and the other is DTE.");
            }
        }

        if (medium == PortMedium.Console || medium == PortMedium.RS232)
        {
            if (picked == CableVisual.CableType.Auto)
                effective = CableVisual.CableType.ConsoleRollover;
        }

        Connect(a, b, effective, serialEndAIsDCE, linkUp);
    }

    private void Connect(Port a, Port b, CableVisual.CableType cableType, bool serialEndAIsDCE, bool linkUp)
    {
        a.connectedTo = b;
        b.connectedTo = a;

        var cableObj = Instantiate(cablePrefab);
        var cable = cableObj.GetComponent<CableVisual>();
        if (cable == null)
            cable = cableObj.AddComponent<CableVisual>();

        cable.Initialize(a, b, cableType, serialEndAIsDCE);

        a.cable = cable;
        b.cable = cable;

        UpdateLinkState(a, linkUp);
        UpdateLinkState(b, linkUp);

        if (a.medium == PortMedium.Power)
            UpdatePowerAfterChange(a, b);
    }

    private void Disconnect(Port a)
    {
        if (a == null || a.connectedTo == null) return;

        Port b = a.connectedTo;

        var cable = a.cable != null ? a.cable : b.cable;
        if (cable != null)
            Destroy(cable.gameObject);

        a.connectedTo = null;
        b.connectedTo = null;

        a.cable = null;
        b.cable = null;

        UpdateLinkState(a, false);
        UpdateLinkState(b, false);

        if (a.medium == PortMedium.Power || b.medium == PortMedium.Power)
        {
            RecomputeReceivingPowerForDevice(a.owner);
            RecomputeReceivingPowerForDevice(b.owner);
        }
    }

    private void ResetAllConnections()
    {
        var ports = FindObjectsOfType<Port>(true);
        foreach (var p in ports)
        {
            p.connectedTo = null;
            p.cable = null;
        }

        var cables = FindObjectsOfType<CableVisual>(true);
        foreach (var c in cables)
            Destroy(c.gameObject);

        var devices = FindObjectsOfType<Device>(true);
        foreach (var d in devices)
            RecomputeReceivingPowerForDevice(d);
    }

    private void UpdateLinkState(Port port, bool linked)
    {
        if (port == null) return;

        if (port.medium == PortMedium.Power) return;

        if (string.IsNullOrWhiteSpace(port.interfaceName)) return;

        var router = port.owner as RouterDevice;
        if (router != null)
        {
            var iface = router.GetInterface(port.interfaceName);
            if (iface == null) return;

            iface.protocolUp = linked && iface.adminUp;
            return;
        }

        var sw = port.owner as SwitchDevice;
        if (sw != null)
        {
            var p = sw.GetPort(port.interfaceName);
            if (p == null) return;

            p.protocolUp = linked && p.adminUp;
            return;
        }
    }

    private void UpdatePowerAfterChange(Port a, Port b)
    {
        if (a == null || b == null) return;
        if (a.owner == null || b.owner == null) return;

        RecomputeReceivingPowerForDevice(a.owner);
        RecomputeReceivingPowerForDevice(b.owner);
    }

    private void RecomputeReceivingPowerForDevice(Device d)
    {
        if (d == null) return;

        if (!d.requiresExternalPower)
        {
            d.SetReceivingExternalPower(true);
            return;
        }

        bool receiving = false;

        var ports = d.GetComponentsInChildren<Port>(true);
        foreach (var p in ports)
        {
            if (p == null) continue;
            if (p.owner != d) continue;
            if (p.medium != PortMedium.Power) continue;
            if (!p.IsConnected || p.connectedTo == null) continue;
            if (p.connectedTo.owner == null) continue;

            var otherDev = p.connectedTo.owner;

            if (otherDev.ProvidesPower && otherDev.IsPoweredOn)
            {
                receiving = true;
                break;
            }
        }

        d.SetReceivingExternalPower(receiving);
    }

    private void Toast(string message, float seconds = 3.0f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _toast = message;
        _toastUntil = Time.time + Mathf.Max(0.25f, seconds);
        Debug.Log($"[CableManager] {message}");
    }

    private void OnGUI()
    {

        if (_pickerOpen && _pickerPort != null)
        {
            GUI.depth = -1000;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = true
            };

            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize
            };

            float x = (Screen.width - panelSize.x) * 0.5f;
            float y = (Screen.height - panelSize.y) * 0.5f;
            Rect panel = new Rect(x, y, panelSize.x, panelSize.y);

            GUI.Box(panel, "");

            GUILayout.BeginArea(new Rect(panel.x + 14, panel.y + 14, panel.width - 28, panel.height - 28));

            GUILayout.Label("Select cable type", labelStyle);
            GUILayout.Space(6);

            string portLabel = _pickerPort.owner != null ? _pickerPort.owner.name : "Device";
            GUILayout.Label($"Start: {portLabel}/{_pickerPort.portName} ({_pickerPort.medium})", labelStyle);

            GUILayout.Space(10);

            foreach (var opt in GetCableOptionsForPort(_pickerPort))
            {
                bool selected = opt == _pickedType;

                string display = (opt == CableVisual.CableType.Auto && _pickerPort.medium == PortMedium.Power)
                    ? "Power Cable"
                    : FormatCableType(opt);

                string text = selected ? $"▶ {display}" : display;

                if (GUILayout.Button(text, buttonStyle, GUILayout.Height(34)))
                    _pickedType = opt;
            }

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Start Cable", buttonStyle, GUILayout.Height(36)))
            {
                _selectedPort = _pickerPort;
                _selectedPort.SetHighlighted(true);

                ClosePicker(clearSelection: false);
                _pickerPort = null;
            }

            if (GUILayout.Button("Cancel (ESC)", buttonStyle, GUILayout.Height(36)))
            {
                ClosePicker(clearSelection: true);
            }

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        if (!string.IsNullOrWhiteSpace(_toast) && Time.time <= _toastUntil)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            float width = Mathf.Min(900, Screen.width - 40);
            Rect rect = new Rect((Screen.width - width) * 0.5f, 20f, width, 60f);
            GUI.Box(new Rect(rect.x - 10, rect.y - 10, rect.width + 20, rect.height + 20), GUIContent.none);
            GUI.Label(rect, _toast, style);
        }
    }

    private static bool IsCableTypeAllowedForMedium(CableVisual.CableType type, PortMedium medium)
    {
        if (type == CableVisual.CableType.Auto)
            return true;

        switch (medium)
        {
            case PortMedium.Ethernet:
                return type == CableVisual.CableType.CopperStraightThrough || type == CableVisual.CableType.CopperCrossover;

            case PortMedium.Serial:
                return type == CableVisual.CableType.SerialDCE || type == CableVisual.CableType.SerialDTE;

            case PortMedium.Console:
            case PortMedium.RS232:
                return type == CableVisual.CableType.ConsoleRollover;

            case PortMedium.Power:

                return false;

            default:
                return false;
        }
    }

    private static CableVisual.CableType GetRecommendedCableType(Port a, Port b, PortMedium medium)
    {
        switch (medium)
        {
            case PortMedium.Ethernet:
                {
                    bool aIsMdix = (a.owner is SwitchDevice);
                    bool bIsMdix = (b.owner is SwitchDevice);

                    bool needsCrossover = (aIsMdix == bIsMdix);
                    return needsCrossover ? CableVisual.CableType.CopperCrossover : CableVisual.CableType.CopperStraightThrough;
                }

            case PortMedium.Serial:
                return CableVisual.CableType.SerialDCE;

            case PortMedium.Console:
            case PortMedium.RS232:
                return CableVisual.CableType.ConsoleRollover;

            case PortMedium.Power:
                return CableVisual.CableType.Auto;

            default:
                return CableVisual.CableType.Auto;
        }
    }

    private static List<CableVisual.CableType> GetCableOptionsForPort(Port port)
    {
        var list = new List<CableVisual.CableType>();
        if (port == null) return list;

        list.Add(CableVisual.CableType.Auto);

        switch (port.medium)
        {
            case PortMedium.Ethernet:
                list.Add(CableVisual.CableType.CopperStraightThrough);
                list.Add(CableVisual.CableType.CopperCrossover);
                break;

            case PortMedium.Serial:
                list.Add(CableVisual.CableType.SerialDCE);
                list.Add(CableVisual.CableType.SerialDTE);
                break;

            case PortMedium.Console:
            case PortMedium.RS232:
                list.Add(CableVisual.CableType.ConsoleRollover);
                break;

            case PortMedium.Power:

                break;
        }

        return list;
    }

    private static string FormatCableType(CableVisual.CableType type)
    {
        switch (type)
        {
            case CableVisual.CableType.Auto: return "Auto";
            case CableVisual.CableType.CopperStraightThrough: return "Copper Straight-Through";
            case CableVisual.CableType.CopperCrossover: return "Copper Crossover";
            case CableVisual.CableType.ConsoleRollover: return "Console (Rollover)";
            case CableVisual.CableType.SerialDCE: return "Serial DCE";
            case CableVisual.CableType.SerialDTE: return "Serial DTE";
            default: return type.ToString();
        }
    }
}
