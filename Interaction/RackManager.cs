using System.Collections.Generic;
using UnityEngine;

public class RackManager : MonoBehaviour
{
    public List<string> resourcesFolders = new List<string> { "Routers", "Switches" };
    public List<GameObject> manualPrefabs = new List<GameObject>();

    readonly List<RackSlotInteractable> _slots = new List<RackSlotInteractable>();
    readonly Dictionary<int, int> _occupiedByStartIndex = new Dictionary<int, int>();
    readonly Dictionary<int, GameObject> _installedByStartIndex = new Dictionary<int, GameObject>();
    readonly List<RackSlotInteractable.DeviceOption> _catalog = new List<RackSlotInteractable.DeviceOption>();

    bool _dirtyOrder = true;
    bool _catalogBuilt;

    void Awake()
    {
        BuildCatalog();
    }

    public List<RackSlotInteractable.DeviceOption> GetCatalogOptions()
    {
        BuildCatalog();
        return _catalog;
    }

    void BuildCatalog()
    {
        if (_catalogBuilt) return;
        _catalogBuilt = true;

        _catalog.Clear();

        var prefabs = new List<GameObject>();

        if (manualPrefabs != null)
        {
            for (int i = 0; i < manualPrefabs.Count; i++)
                if (manualPrefabs[i] != null)
                    prefabs.Add(manualPrefabs[i]);
        }

        if (prefabs.Count == 0)
        {
            if (resourcesFolders != null)
            {
                for (int f = 0; f < resourcesFolders.Count; f++)
                {
                    var folder = resourcesFolders[f];
                    if (string.IsNullOrWhiteSpace(folder)) continue;

                    var loaded = Resources.LoadAll<GameObject>(folder);
                    if (loaded == null) continue;

                    for (int i = 0; i < loaded.Length; i++)
                        if (loaded[i] != null)
                            prefabs.Add(loaded[i]);
                }
            }
        }

        var seen = new HashSet<GameObject>();
        for (int i = 0; i < prefabs.Count; i++)
        {
            var p = prefabs[i];
            if (p == null) continue;
            if (!seen.Add(p)) continue;
            if (p.GetComponent<RackMountable>() == null) continue;

            var opt = new RackSlotInteractable.DeviceOption();
            opt.label = p.name;
            opt.prefab = p;
            _catalog.Add(opt);
        }

        _catalog.Sort((a, b) => string.Compare(a.label, b.label, System.StringComparison.OrdinalIgnoreCase));
    }

    public void RegisterSlot(RackSlotInteractable slot)
    {
        if (slot == null) return;
        if (_slots.Contains(slot)) return;
        _slots.Add(slot);
        _dirtyOrder = true;
    }

    public void UnregisterSlot(RackSlotInteractable slot)
    {
        if (slot == null) return;
        if (_slots.Remove(slot)) _dirtyOrder = true;
    }

    void EnsureOrdered()
    {
        if (!_dirtyOrder) return;
        _dirtyOrder = false;

        _slots.RemoveAll(s => s == null);
        _slots.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

        for (int i = 0; i < _slots.Count; i++)
            _slots[i].SetRuntimeIndex(i);
    }

    public bool TryInstallFromSlot(RackSlotInteractable startSlot, GameObject prefab)
    {
        if (startSlot == null || prefab == null) return false;
        EnsureOrdered();

        int startIndex = startSlot.RuntimeIndex;
        if (startIndex < 0 || startIndex >= _slots.Count) return false;

        int uHeight = 1;
        var m = prefab.GetComponent<RackMountable>();
        if (m != null && m.uHeight > 0) uHeight = m.uHeight;

        if (!IsRangeFree(startIndex, uHeight)) return false;

        UninstallFromSlot(startSlot);

        var mountPoint = startSlot.mountPoint != null ? startSlot.mountPoint : startSlot.transform;
        Vector3 targetPos = mountPoint.position + mountPoint.forward * startSlot.forwardOffset;
        Quaternion rot = mountPoint.rotation;

        var go = Instantiate(prefab, targetPos, rot);
        go.transform.rotation = rot;

        var fa = startSlot.FindFrontAnchor(go.transform);
        if (fa != null)
        {
            Vector3 delta = targetPos - fa.position;
            go.transform.position += delta;
        }
        else
        {
            go.transform.position = targetPos;
        }

        if (startSlot.spawnedParent != null) go.transform.SetParent(startSlot.spawnedParent, true);
        else go.transform.SetParent(mountPoint, true);

        _installedByStartIndex[startIndex] = go;
        for (int i = 0; i < uHeight; i++)
            _occupiedByStartIndex[startIndex + i] = startIndex;

        for (int i = 0; i < uHeight; i++)
            _slots[startIndex + i].SetPlaceholderVisible(false);

        var proxy = go.GetComponent<RackSlotInstalledProxy>();
        if (proxy == null) proxy = go.AddComponent<RackSlotInstalledProxy>();
        proxy.Bind(startSlot);

        var inst = go.GetComponent<RackInstallation>();
        if (inst == null) inst = go.AddComponent<RackInstallation>();
        inst.Bind(this, startIndex, uHeight);

        return true;
    }

    public void UninstallFromSlot(RackSlotInteractable slot)
    {
        if (slot == null) return;
        EnsureOrdered();

        int slotIndex = slot.RuntimeIndex;
        if (slotIndex < 0) return;

        if (_occupiedByStartIndex.TryGetValue(slotIndex, out int startIndex))
        {
            UninstallByStartIndex(startIndex);
            return;
        }

        if (_installedByStartIndex.TryGetValue(slotIndex, out var go) && go != null)
        {
            Destroy(go);
            _installedByStartIndex.Remove(slotIndex);
        }
    }

    public void UninstallByStartIndex(int startIndex)
    {
        EnsureOrdered();
        if (startIndex < 0) return;

        if (_installedByStartIndex.TryGetValue(startIndex, out var existingGo))
        {
            _installedByStartIndex.Remove(startIndex);
            ClearOccupancyForStartIndex(startIndex);
            if (existingGo != null) Destroy(existingGo);
            return;
        }

        ClearOccupancyForStartIndex(startIndex);
    }

    public void NotifyInstalledDestroyed(int startIndex, GameObject destroyedGo)
    {
        EnsureOrdered();
        if (_installedByStartIndex.TryGetValue(startIndex, out var go))
        {
            if (go != destroyedGo) return;
            _installedByStartIndex.Remove(startIndex);
        }
        ClearOccupancyForStartIndex(startIndex);
    }

    void ClearOccupancyForStartIndex(int startIndex)
    {
        var toClear = new List<int>();
        foreach (var kv in _occupiedByStartIndex)
            if (kv.Value == startIndex) toClear.Add(kv.Key);

        for (int i = 0; i < toClear.Count; i++)
            _occupiedByStartIndex.Remove(toClear[i]);

        for (int i = 0; i < _slots.Count; i++)
            if (!_occupiedByStartIndex.ContainsKey(i))
                _slots[i].SetPlaceholderVisible(true);
    }

    bool IsRangeFree(int startIndex, int uHeight)
    {
        if (uHeight <= 0) return false;
        if (startIndex < 0) return false;
        if (startIndex + uHeight - 1 >= _slots.Count) return false;

        for (int i = 0; i < uHeight; i++)
        {
            if (_occupiedByStartIndex.ContainsKey(startIndex + i)) return false;
            if (_installedByStartIndex.ContainsKey(startIndex + i)) return false;
        }
        return true;
    }
}
