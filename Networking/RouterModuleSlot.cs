using System.Collections.Generic;
using UnityEngine;

public enum RouterModuleSlotType
{
    WIC,
    HWIC,
    EHWIC
}

public enum RouterModuleType
{
    None,

    WIC_COVER,
    WIC_1T,
    WIC_2T,

    HWIC_2T,
    HWIC_4ESW,
    HWIC_1GE_SFP,

    HWIC_WLAN_AP,

    EHWIC_2T
}

public class RouterModuleSlot : MonoBehaviour
{
    [Header("Slot Identity")]
    public int slotIndex = 0;
    public RouterModuleSlotType slotType = RouterModuleSlotType.WIC;

    [Header("Installed Module (Authoring)")]
    public RouterModuleType installedModule = RouterModuleType.None;

    [Header("Port Spawning")]
    [Tooltip("Optional parent transform where ports will spawn. If null, ports spawn under this slot object.")]
    public Transform portParent;

    [Tooltip("Optional port prefab. If null, RouterDevice.defaultPortPrefab is used; otherwise a cube is created.")]
    public GameObject portPrefab;

    [Tooltip("Local X spacing between spawned ports.")]
    public float portSpacing = 0.03f;

    [HideInInspector] public List<Port> spawnedPorts = new List<Port>();
    [HideInInspector] public List<string> spawnedInterfaceNames = new List<string>();

    [HideInInspector] public List<GameObject> spawnedObjects = new List<GameObject>();

    public Transform GetPortParent()
    {
        return portParent != null ? portParent : transform;
    }

    public void ClearRuntimeLists()
    {
        if (spawnedPorts == null) spawnedPorts = new List<Port>();
        else spawnedPorts.Clear();

        if (spawnedInterfaceNames == null) spawnedInterfaceNames = new List<string>();
        else spawnedInterfaceNames.Clear();

        if (spawnedObjects == null) spawnedObjects = new List<GameObject>();
        else spawnedObjects.Clear();
    }
}
