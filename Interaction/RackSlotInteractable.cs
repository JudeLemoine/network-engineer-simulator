using System;
using System.Collections.Generic;
using UnityEngine;

public class RackSlotInteractable : MonoBehaviour, IDeviceInteractable
{
    [Serializable]
    public class DeviceOption
    {
        public string label;
        public GameObject prefab;
    }

    public Transform mountPoint;
    public Transform spawnedParent;
    public GameObject placeholderRoot;
    public float forwardOffset = 0.02f;
    public List<DeviceOption> options = new List<DeviceOption>();

    public static bool IsAnyRackMenuOpen;

    GameObject _installed;
    int _selectedIndex;
    bool _menuOpen;

    void Awake()
    {
        if (mountPoint == null) mountPoint = transform;
        if (placeholderRoot == null) placeholderRoot = gameObject;
        if (_selectedIndex < 0) _selectedIndex = 0;
    }

    public void Interact()
    {
        OpenMenuFromWorld();
    }

    public void OpenMenuFromWorld()
    {
        _menuOpen = true;
        IsAnyRackMenuOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OpenFromProxy()
    {
        OpenMenuFromWorld();
    }

    void CloseMenu()
    {
        _menuOpen = false;
        IsAnyRackMenuOpen = false;
    }

    void InstallSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= options.Count) return;
        var opt = options[_selectedIndex];
        if (opt == null || opt.prefab == null) return;

        if (_installed != null)
        {
            Destroy(_installed);
            _installed = null;
        }

        SetPlaceholderVisible(false);

        Vector3 targetPos = mountPoint.position + mountPoint.forward * forwardOffset;
        Quaternion rot = mountPoint.rotation;

        var go = Instantiate(opt.prefab, targetPos, rot);
        var fa = FindFrontAnchor(go.transform);
        go.transform.rotation = rot;

        if (fa != null)
        {
            Vector3 delta = targetPos - fa.position;
            go.transform.position += delta;
        }
        else
        {
            go.transform.position = targetPos;
        }

        if (spawnedParent != null) go.transform.SetParent(spawnedParent, true);
        else go.transform.SetParent(mountPoint, true);

        _installed = go;
var proxy = go.GetComponent<RackSlotInstalledProxy>();
        if (proxy == null) proxy = go.AddComponent<RackSlotInstalledProxy>();
        proxy.Bind(this);
    }

    public void RestorePlaceholder()
    {
        if (_installed != null)
        {
            Destroy(_installed);
            _installed = null;
        }

        SetPlaceholderVisible(true);
    }

    void SetPlaceholderVisible(bool visible)
    {
        if (placeholderRoot == null) return;

        if (placeholderRoot != gameObject)
        {
            if (placeholderRoot.activeSelf != visible) placeholderRoot.SetActive(visible);
            return;
        }

        var rends = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++) rends[i].enabled = visible;

        var cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = visible;
    }

void OnDisable()
    {
        if (_menuOpen) CloseMenu();
    }

    void OnGUI()
    {
        if (!_menuOpen) return;

        float w = 380f;
        float h = 220f;
        Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.Box(r, "");
        GUILayout.BeginArea(r);
        GUILayout.Label("Rack Slot - Select Device");
        GUILayout.Label("Choose a device to install or restore placeholder.");

        GUILayout.Space(10);

        for (int i = 0; i < options.Count; i++)
        {
            var lab = options[i] != null ? options[i].label : "";
            if (string.IsNullOrWhiteSpace(lab)) lab = "Device";
            bool sel = i == _selectedIndex;
            if (GUILayout.Toggle(sel, lab, "Button"))
                _selectedIndex = i;
        }

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Install Selected", GUILayout.Height(26)))
            InstallSelected();
        if (GUILayout.Button("Restore Placeholder", GUILayout.Height(26)))
            RestorePlaceholder();
        if (GUILayout.Button("Close (ESC)", GUILayout.Height(26)))
            CloseMenu();
        GUILayout.EndHorizontal();

        GUILayout.EndArea();

        if (Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            CloseMenu();
    }
    Transform FindFrontAnchor(Transform root)
    {
        if (root == null) return null;
        var ts = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < ts.Length; i++)
        {
            if (ts[i] != null && ts[i].name == "FrontAnchor") return ts[i];
        }
        return null;
    }

}
