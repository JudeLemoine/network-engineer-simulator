using System;
using System.Collections.Generic;
using UnityEngine;

public class RackSlotInteractable : MonoBehaviour, IDeviceInteractable
{
    [Serializable]
    public class DeviceOption
    {
        public string label;
        public string category;
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

    int _selectedCategoryIndex;

    void Awake()
    {
        AutoLinkChildRefs();

        if (_selectedIndex < 0) _selectedIndex = 0;
        if (_selectedCategoryIndex < 0) _selectedCategoryIndex = 0;
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
            opt.category = string.IsNullOrWhiteSpace(c.category) ? "Devices" : c.category;
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

        ClampSelectionToCategory();
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
        for (int i = 0; i < rends.Length; i++)
            if (rends[i] != null && rends[i].enabled != visible)
                rends[i].enabled = visible;

        var cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null && cols[i].enabled != visible)
                cols[i].enabled = visible;
    }

    public void SetRuntimeIndex(int idx)
    {
        RuntimeIndex = idx;
    }

    void ClampSelectionToCategory()
    {
        if (options == null || options.Count == 0) return;

        BuildCategories(options, out var cats, out var byCat);
        if (cats.Count == 0) return;

        if (_selectedCategoryIndex < 0) _selectedCategoryIndex = 0;
        if (_selectedCategoryIndex >= cats.Count) _selectedCategoryIndex = cats.Count - 1;

        string cat = cats[_selectedCategoryIndex];
        if (!byCat.TryGetValue(cat, out var list) || list.Count == 0) return;

        if (_selectedIndex < 0 || _selectedIndex >= options.Count || !list.Contains(_selectedIndex))
            _selectedIndex = list[0];
    }

    static void BuildCategories(List<DeviceOption> opts, out List<string> categories, out Dictionary<string, List<int>> indicesByCategory)
    {
        categories = new List<string>();
        indicesByCategory = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        if (opts == null) return;

        for (int i = 0; i < opts.Count; i++)
        {
            var o = opts[i];
            if (o == null || o.prefab == null) continue;

            string cat = string.IsNullOrWhiteSpace(o.category) ? "Devices" : o.category;

            if (!indicesByCategory.TryGetValue(cat, out var list))
            {
                list = new List<int>();
                indicesByCategory[cat] = list;
                categories.Add(cat);
            }

            list.Add(i);
        }

        categories.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));

        for (int c = 0; c < categories.Count; c++)
        {
            var list = indicesByCategory[categories[c]];
            list.Sort((ia, ib) =>
            {
                string la = opts[ia] != null ? opts[ia].label : "";
                string lb = opts[ib] != null ? opts[ib].label : "";
                return string.Compare(la, lb, StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    int GetUHeightForPrefab(GameObject prefab)
    {
        if (prefab == null) return 1;
        var m = prefab.GetComponent<RackMountable>();
        if (m != null && m.uHeight > 0) return m.uHeight;
        return 1;
    }

    void OnGUI()
    {
        if (!_menuOpen) return;

        float w = 820f;
        float h = 430f;
        Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.Box(r, "");
        GUILayout.BeginArea(r);

        GUILayout.Label("Rack Slot - Select Device");

        string startLabel = transform != null ? transform.name : "Slot";
        int slotCount = rackManager != null ? rackManager.GetSlotCount() : 0;

        int u = 1;
        string endLabel = startLabel;

        if (options != null && _selectedIndex >= 0 && _selectedIndex < options.Count && options[_selectedIndex] != null)
        {
            var p = options[_selectedIndex].prefab;
            u = GetUHeightForPrefab(p);

            if (rackManager != null)
            {
                int endIndex = RuntimeIndex + u - 1;
                if (endIndex < 0) endIndex = 0;
                if (slotCount > 0 && endIndex >= slotCount) endIndex = slotCount - 1;

                var lbl = rackManager.GetSlotLabelAtIndex(endIndex);
                if (!string.IsNullOrWhiteSpace(lbl)) endLabel = lbl;
            }
        }

        if (slotCount > 0 && RuntimeIndex >= 0)
            GUILayout.Label($"Selected slot: {startLabel}  (index {RuntimeIndex + 1} / {slotCount})");
        else
            GUILayout.Label($"Selected slot: {startLabel}");

        GUILayout.Label($"Selected device size: {u}U");
        GUILayout.Label($"Will occupy: {startLabel}  →  {endLabel}");

        GUILayout.Space(10);

        BuildCategories(options, out var categories, out var byCategory);

        if (categories.Count == 0)
        {
            GUILayout.Label("No rack-mountable prefabs found.");
            GUILayout.FlexibleSpace();
        }
        else
        {
            if (_selectedCategoryIndex < 0) _selectedCategoryIndex = 0;
            if (_selectedCategoryIndex >= categories.Count) _selectedCategoryIndex = categories.Count - 1;

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(240));
            GUILayout.Label("Categories");
            for (int i = 0; i < categories.Count; i++)
            {
                bool sel = i == _selectedCategoryIndex;
                if (GUILayout.Toggle(sel, categories[i], "Button", GUILayout.Height(28)))
                {
                    if (!sel)
                    {
                        _selectedCategoryIndex = i;
                        ClampSelectionToCategory();
                    }
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(10);

            GUILayout.BeginVertical();
            string activeCat = categories[_selectedCategoryIndex];
            GUILayout.Label(activeCat);

            if (!byCategory.TryGetValue(activeCat, out var idxs) || idxs.Count == 0)
            {
                GUILayout.Label("No devices in this category.");
            }
            else
            {
                for (int j = 0; j < idxs.Count; j++)
                {
                    int optIndex = idxs[j];
                    var opt = options[optIndex];
                    string lab = opt != null ? opt.label : "";
                    if (string.IsNullOrWhiteSpace(lab)) lab = "Device";

                    int du = opt != null ? GetUHeightForPrefab(opt.prefab) : 1;
                    string text = $"{lab} ({du}U)";

                    bool sel = optIndex == _selectedIndex;
                    if (GUILayout.Toggle(sel, text, "Button", GUILayout.Height(28)))
                        _selectedIndex = optIndex;
                }
            }

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Install Selected", GUILayout.Height(32)))
            InstallSelected();
        if (GUILayout.Button("Restore Placeholder", GUILayout.Height(32)))
            RestorePlaceholder();
        if (GUILayout.Button("Close (ESC)", GUILayout.Height(32)))
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
