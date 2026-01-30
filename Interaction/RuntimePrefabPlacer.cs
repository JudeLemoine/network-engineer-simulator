using System;
using System.Collections.Generic;
using UnityEngine;

public class RuntimePrefabPlacer : MonoBehaviour
{
    public static bool IsAnyPlacementUIOpen { get; private set; }
    public static bool IsPlacingActive { get; private set; }

    public string resourcesRootFolder = "";
    public LayerMask placementMask = ~0;
    public float maxPlaceDistance = 50f;

    public float defaultPlaceDistance = 0.4f;
    public float minPlaceDistance = 0.15f;
    public float maxAdjustPlaceDistance = 10f;
    public float scrollStep = 0.2f;

    public int fontSize = 18;
    public Vector2 panelSize = new Vector2(900, 520);

    Camera _cam;

    class Option
    {
        public string category;
        public string label;
        public GameObject prefab;
        public PlaceableInWorld placeable;
    }

    readonly List<Option> _options = new List<Option>();
    bool _built;

    bool _menuOpen;
    int _selectedCategoryIndex;
    int _selectedOptionIndex;

    GameObject _activePrefab;
    GameObject _preview;
    float _previewYaw;

    float _placeDistance;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
        BuildOptions();
        _placeDistance = defaultPlaceDistance;
    }

    void Update()
    {
        bool anyOtherInteractionOpen =
            TerminalScreen.IsAnyTerminalFocused ||
            RackSlotInteractable.IsAnyRackMenuOpen ||
            CableManager.IsAnyCableMenuOpen ||
            RouterModuleSlotInteractable.IsAnyModuleMenuOpen;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (_menuOpen || IsPlacingActive)
            {
                if (IsPlacingActive)
                {
                    CancelPlacement();
                    CloseMenu();
                    return;
                }

                if (_menuOpen) CloseMenu();
                else OpenMenu();
                return;
            }

            if (anyOtherInteractionOpen)
                return;

            OpenMenu();
            return;
        }

        if (TerminalScreen.IsAnyTerminalFocused) return;

        if (_menuOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TerminalScreen.LastEscapeHandledFrame = Time.frameCount;
                CloseMenu();
            }
            return;
        }

        if (IsPlacingActive)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TerminalScreen.LastEscapeHandledFrame = Time.frameCount;
                CancelPlacement();
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                CancelPlacement();
                return;
            }

            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
            {
                _placeDistance += scroll * scrollStep;
                if (_placeDistance < minPlaceDistance) _placeDistance = minPlaceDistance;
                if (_placeDistance > maxAdjustPlaceDistance) _placeDistance = maxAdjustPlaceDistance;
            }

            if (Input.GetKeyDown(KeyCode.R))
                _previewYaw += 90f;

            if (Input.GetKeyDown(KeyCode.Q)) _previewYaw -= 15f;
            if (Input.GetKeyDown(KeyCode.E)) _previewYaw += 15f;

            UpdatePreviewTransform();

            if (Input.GetMouseButtonDown(0))
            {
                if (_cam == null) _cam = Camera.main;
                if (_cam == null) return;

                if (TryGetPlacementPose(out Vector3 pos, out Quaternion rot))
                {
                    Instantiate(_activePrefab, pos, rot);
                }
            }
        }
    }

    void OpenMenu()
    {
        BuildOptions();

        _menuOpen = true;
        IsAnyPlacementUIOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ClampSelection();
    }

    void CloseMenu()
    {
        _menuOpen = false;
        IsAnyPlacementUIOpen = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void BuildOptions()
    {
        if (_built) return;
        _built = true;

        _options.Clear();

        var loaded = Resources.LoadAll<GameObject>(resourcesRootFolder == null ? "" : resourcesRootFolder);
        if (loaded == null) return;

        var seen = new HashSet<GameObject>();
        for (int i = 0; i < loaded.Length; i++)
        {
            var p = loaded[i];
            if (p == null) continue;
            if (!seen.Add(p)) continue;

            var placeable = p.GetComponent<PlaceableInWorld>();
            if (placeable == null) continue;

            string cat = placeable.category;
            if (string.IsNullOrWhiteSpace(cat)) cat = "Misc";

            var opt = new Option
            {
                category = cat,
                label = p.name,
                prefab = p,
                placeable = placeable
            };

            _options.Add(opt);
        }

        _options.Sort((a, b) =>
        {
            int c = string.Compare(a.category, b.category, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
            return string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase);
        });
    }

    void ClampSelection()
    {
        BuildCategories(_options, out var cats, out var idxByCat);
        if (cats.Count == 0) return;

        if (_selectedCategoryIndex < 0) _selectedCategoryIndex = 0;
        if (_selectedCategoryIndex >= cats.Count) _selectedCategoryIndex = cats.Count - 1;

        string cat = cats[_selectedCategoryIndex];
        if (!idxByCat.TryGetValue(cat, out var list) || list.Count == 0) return;

        if (_selectedOptionIndex < 0 || _selectedOptionIndex >= _options.Count || !list.Contains(_selectedOptionIndex))
            _selectedOptionIndex = list[0];
    }

    static void BuildCategories(List<Option> opts, out List<string> categories, out Dictionary<string, List<int>> indicesByCategory)
    {
        categories = new List<string>();
        indicesByCategory = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        if (opts == null) return;

        for (int i = 0; i < opts.Count; i++)
        {
            var o = opts[i];
            if (o == null || o.prefab == null) continue;

            string cat = string.IsNullOrWhiteSpace(o.category) ? "Misc" : o.category;

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
            indicesByCategory[categories[c]].Sort((ia, ib) => string.Compare(opts[ia].label, opts[ib].label, StringComparison.OrdinalIgnoreCase));
    }

    void BeginPlacement(GameObject prefab)
    {
        if (prefab == null) return;

        _activePrefab = prefab;
        IsPlacingActive = true;
        _previewYaw = 0f;

        _placeDistance = defaultPlaceDistance;
        if (_placeDistance < minPlaceDistance) _placeDistance = minPlaceDistance;
        if (_placeDistance > maxAdjustPlaceDistance) _placeDistance = maxAdjustPlaceDistance;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        CreatePreview();
        UpdatePreviewTransform();
    }

    void CancelPlacement()
    {
        IsPlacingActive = false;
        _activePrefab = null;

        if (_preview != null) Destroy(_preview);
        _preview = null;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void CreatePreview()
    {
        if (_preview != null) Destroy(_preview);

        _preview = Instantiate(_activePrefab);
        _preview.name = _preview.name + "_PREVIEW";

        SetLayerRecursive(_preview.transform, LayerMask.NameToLayer("Ignore Raycast"));

        var cols = _preview.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;

        var rb = _preview.GetComponentInChildren<Rigidbody>();
        if (rb != null) Destroy(rb);
    }

    void SetLayerRecursive(Transform t, int layer)
    {
        if (t == null) return;
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i), layer);
    }

    void UpdatePreviewTransform()
    {
        if (!IsPlacingActive || _preview == null) return;

        if (TryGetPlacementPose(out Vector3 pos, out Quaternion rot))
        {
            _preview.transform.SetPositionAndRotation(pos, rot);
        }
    }

    bool TryGetPlacementPose(out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        float desired = _placeDistance;
        if (desired < minPlaceDistance) desired = minPlaceDistance;
        if (desired > maxAdjustPlaceDistance) desired = maxAdjustPlaceDistance;
        if (desired > maxPlaceDistance) desired = maxPlaceDistance;

        Vector3 targetPoint = ray.GetPoint(desired);

        if (Physics.Raycast(ray, out RaycastHit hit, desired, placementMask, QueryTriggerInteraction.Ignore))
        {
            float offset = GetPreviewOffsetAlongNormal(hit.normal);
            targetPoint = hit.point + hit.normal * offset;
        }

        pos = targetPoint;
        rot = Quaternion.Euler(0f, _previewYaw, 0f);
        return true;
    }

    float GetPreviewOffsetAlongNormal(Vector3 normal)
    {
        float offset = GetPreviewApproxRadius();
        if (offset < 0f) offset = 0f;
        return offset;
    }

    float GetPreviewApproxRadius()
    {
        if (_preview == null) return 0f;

        var rends = _preview.GetComponentsInChildren<Renderer>(true);
        if (rends != null && rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            return Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
        }

        return 0f;
    }

    void OnGUI()
    {
        if (!_menuOpen) return;

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

        GUILayout.Label("Place Prefab", labelStyle);
        GUILayout.Space(6);
        GUILayout.Label("Select a category, pick a prefab, then Begin Placement. TAB closes. ESC closes.", labelStyle);
        GUILayout.Space(10);

        BuildCategories(_options, out var categories, out var byCategory);

        if (categories.Count == 0)
        {
            GUILayout.Label("No placeable prefabs found. Add PlaceableInWorld to prefabs inside Resources.", labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", buttonStyle, GUILayout.Height(38)))
                CloseMenu();
            GUILayout.EndArea();
            return;
        }

        if (_selectedCategoryIndex < 0) _selectedCategoryIndex = 0;
        if (_selectedCategoryIndex >= categories.Count) _selectedCategoryIndex = categories.Count - 1;

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(260));
        GUILayout.Label("Categories", labelStyle);
        for (int i = 0; i < categories.Count; i++)
        {
            bool sel = i == _selectedCategoryIndex;
            if (GUILayout.Toggle(sel, categories[i], "Button", GUILayout.Height(32)))
            {
                if (!sel)
                {
                    _selectedCategoryIndex = i;
                    ClampSelection();
                }
            }
        }
        GUILayout.EndVertical();

        GUILayout.Space(12);

        GUILayout.BeginVertical();
        string activeCat = categories[_selectedCategoryIndex];
        GUILayout.Label(activeCat, labelStyle);

        if (!byCategory.TryGetValue(activeCat, out var idxs) || idxs.Count == 0)
        {
            GUILayout.Label("No prefabs in this category.", labelStyle);
        }
        else
        {
            for (int j = 0; j < idxs.Count; j++)
            {
                int optIndex = idxs[j];
                var opt = _options[optIndex];
                bool sel = optIndex == _selectedOptionIndex;
                if (GUILayout.Toggle(sel, opt.label, "Button", GUILayout.Height(32)))
                    _selectedOptionIndex = optIndex;
            }
        }

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Begin Placement", buttonStyle, GUILayout.Height(40)))
        {
            if (_selectedOptionIndex >= 0 && _selectedOptionIndex < _options.Count && _options[_selectedOptionIndex] != null)
            {
                BeginPlacement(_options[_selectedOptionIndex].prefab);
                CloseMenu();
            }
        }

        if (GUILayout.Button("Close", buttonStyle, GUILayout.Height(40)))
            CloseMenu();

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}
