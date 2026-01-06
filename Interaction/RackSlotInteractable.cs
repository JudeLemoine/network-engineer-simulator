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

    public RackManager rackManager;

    public int RuntimeIndex { get; private set; } = -1;

    public static bool IsAnyRackMenuOpen;

    GameObject _installed;
    int _selectedIndex;
    bool _menuOpen;

    void Awake()
    {
        AutoLinkChildRefs();

        if (_selectedIndex < 0) _selectedIndex = 0;
        if (rackManager == null) rackManager = GetComponentInParent<RackManager>();

        AutoPopulateOptionsIfEmpty();
    }

    void OnEnable()
    {
        if (rackManager == null) rackManager = GetComponentInParent<RackManager>();
        if (rackManager != null) rackManager.RegisterSlot(this);

        AutoLinkChildRefs();
        AutoPopulateOptionsIfEmpty();
    }

    void OnDisable()
    {
        if (rackManager != null) rackManager.UnregisterSlot(this);
        if (_menuOpen) CloseMenu();
    }

    void AutoLinkChildRefs()
    {
        if (mountPoint == null)
        {
            var mp = FindChildByExactName(transform, "MountPoint");
            mountPoint = mp != null ? mp : transform;
        }

        if (placeholderRoot == null)
        {
            var ph = FindChildByExactName(transform, "Placeholder");
            placeholderRoot = ph != null ? ph.gameObject : gameObject;
        }
    }

    static Transform FindChildByExactName(Transform root, string exactName)
    {
        if (root == null) return null;

        var ts = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < ts.Length; i++)
        {
            if (ts[i] == null) continue;
            if (ts[i].name == exactName) return ts[i];
        }
        return null;
    }

    void AutoPopulateOptionsIfEmpty()
    {
        if (options != null && options.Count > 0) return;
        if (rackManager == null) return;

        var catalog = rackManager.GetCatalogOptions();
        if (catalog == null || catalog.Count == 0) return;

        options = new List<DeviceOption>(catalog.Count);
        for (int i = 0; i < catalog.Count; i++)
        {
            var c = catalog[i];
            if (c == null || c.prefab == null) continue;

            var opt = new DeviceOption();
            opt.label = c.label;
            opt.prefab = c.prefab;
            options.Add(opt);
        }

        if (_selectedIndex >= options.Count) _selectedIndex = 0;
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

        if (rackManager != null)
        {
            rackManager.TryInstallFromSlot(this, opt.prefab);
            return;
        }

        if (_installed != null)
        {
            Destroy(_installed);
            _installed = null;
        }

        SetPlaceholderVisible(false);

        var mp = mountPoint != null ? mountPoint : transform;
        Vector3 targetPos = mp.position + mp.forward * forwardOffset;
        Quaternion rot = mp.rotation;

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
        else go.transform.SetParent(mp, true);

        _installed = go;

        var proxy = go.GetComponent<RackSlotInstalledProxy>();
        if (proxy == null) proxy = go.AddComponent<RackSlotInstalledProxy>();
        proxy.Bind(this);
    }

    public void RestorePlaceholder()
    {
        if (rackManager != null)
        {
            rackManager.UninstallFromSlot(this);
            return;
        }

        if (_installed != null)
        {
            Destroy(_installed);
            _installed = null;
        }

        SetPlaceholderVisible(true);
    }

    public void SetPlaceholderVisible(bool visible)
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

    public void SetRuntimeIndex(int index)
    {
        RuntimeIndex = index;
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

    public Transform FindFrontAnchor(Transform root)
    {
        if (root == null) return null;
        var ts = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < ts.Length; i++)
            if (ts[i] != null && ts[i].name == "FrontAnchor")
                return ts[i];
        return null;
    }
}
