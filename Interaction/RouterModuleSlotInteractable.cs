using System.Collections.Generic;
using UnityEngine;

public class RouterModuleSlotInteractable : MonoBehaviour, IDeviceInteractable
{
    public static bool IsAnyModuleMenuOpen { get; private set; }

    [Header("UI")]
    public int fontSize = 18;

    [Tooltip("If your list grows, the scroll view keeps the Install/Remove buttons visible.")]
    public Vector2 panelSize = new Vector2(560, 460);

    private RouterDevice _router;
    private RouterModuleSlot _slot;

    private bool _open;
    private RouterModuleType _selected;
    private string _message = "";

    private Vector2 _scroll;

    private void Awake()
    {
        _router = GetComponentInParent<RouterDevice>();
        _slot = GetComponent<RouterModuleSlot>() ?? GetComponentInParent<RouterModuleSlot>();

        if (_router == null || _slot == null)
            Debug.LogWarning("RouterModuleSlotInteractable: Missing RouterDevice or RouterModuleSlot.");
    }

    public void Interact()
    {
        if (_open) return;
        OpenMenu();
    }

    private void Update()
    {
        if (!_open) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TerminalScreen.LastEscapeHandledFrame = Time.frameCount;
            CloseMenu();
        }
    }

    private void OpenMenu()
    {
        _open = true;
        IsAnyModuleMenuOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _selected = _slot.installedModule;
        _message = "";
        _scroll = Vector2.zero;
    }

    private void CloseMenu()
    {
        _open = false;
        IsAnyModuleMenuOpen = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _message = "";
    }

    private void OnGUI()
    {
        if (!_open || _router == null || _slot == null) return;

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

        float pad = 14f;
        float footerHeight = 120f;
        float headerHeight = 90f;

        Rect headerRect = new Rect(panel.x + pad, panel.y + pad, panel.width - pad * 2f, headerHeight);
        Rect listRect = new Rect(panel.x + pad, panel.y + pad + headerHeight, panel.width - pad * 2f, panel.height - pad * 2f - headerHeight - footerHeight);
        Rect footerRect = new Rect(panel.x + pad, panel.y + panel.height - pad - footerHeight, panel.width - pad * 2f, footerHeight);

        GUILayout.BeginArea(headerRect);
        GUILayout.Label($"Module Slot {_slot.slotIndex} ({_slot.slotType})", labelStyle);
        GUILayout.Label($"Current Module: {_slot.installedModule}", labelStyle);
        GUILayout.Label($"Selected: {_selected}", labelStyle);

        if (_router.IsPoweredOn)
            GUILayout.Label("⚠ Power is ON. Power off the router before swapping modules.", labelStyle);

        GUILayout.EndArea();

        List<RouterModuleType> options = GetCompatibleModules(_slot.slotType);

        GUILayout.BeginArea(listRect);
        _scroll = GUILayout.BeginScrollView(_scroll);

        GUILayout.Label("Available modules:", labelStyle);
        GUILayout.Space(6);

        foreach (var opt in options)
        {
            bool selected = opt == _selected;
            string text = selected ? $"▶ {opt}" : opt.ToString();

            if (GUILayout.Button(text, buttonStyle, GUILayout.Height(36)))
            {
                _selected = opt;
                _message = "";
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUILayout.BeginArea(footerRect);

        if (!string.IsNullOrWhiteSpace(_message))
            GUILayout.Label(_message, labelStyle);

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Install Selected", buttonStyle, GUILayout.Height(40)))
            TryInstall();

        if (GUILayout.Button("Remove Module", buttonStyle, GUILayout.Height(40)))
            TryRemove();

        if (GUILayout.Button("Close (ESC)", buttonStyle, GUILayout.Height(40)))
            CloseMenu();

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    private void TryInstall()
    {
        if (_router.IsPoweredOn)
        {
            _message = "Power off the router before installing modules.";
            return;
        }

        bool ok = _router.InstallModule(_slot, _selected, force: true);
        _message = ok ? $"Installed {_selected}." : "Install failed.";
    }

    private void TryRemove()
    {
        if (_router.IsPoweredOn)
        {
            _message = "Power off the router before removing modules.";
            return;
        }

        _router.RemoveModule(_slot, force: true);
        _selected = RouterModuleType.None;
        _message = "Module removed.";
    }

    private static List<RouterModuleType> GetCompatibleModules(RouterModuleSlotType slotType)
    {
        var list = new List<RouterModuleType> { RouterModuleType.None };

        switch (slotType)
        {
            case RouterModuleSlotType.WIC:
                list.Add(RouterModuleType.WIC_COVER);
                list.Add(RouterModuleType.WIC_1T);
                list.Add(RouterModuleType.WIC_2T);
                break;

            case RouterModuleSlotType.HWIC:
                list.Add(RouterModuleType.WIC_COVER);
                list.Add(RouterModuleType.HWIC_2T);
                list.Add(RouterModuleType.HWIC_4ESW);
                list.Add(RouterModuleType.HWIC_1GE_SFP);

                list.Add(RouterModuleType.HWIC_WLAN_AP);
                break;

            case RouterModuleSlotType.EHWIC:
                list.Add(RouterModuleType.WIC_COVER);
                list.Add(RouterModuleType.EHWIC_2T);
                break;
        }

        return list;
    }
}
