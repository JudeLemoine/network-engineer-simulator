using UnityEngine;

public class DevicePowerSwitch : MonoBehaviour, IDeviceInteractable
{
    [Header("Target Device")]
    public Device targetDevice;

    [Header("Optional Switch Visual")]
    [Tooltip("Optional handle/lever transform to rotate when toggled.")]
    public Transform switchHandle;

    [Tooltip("Local rotation when POWER ON.")]
    public Vector3 onEuler = new Vector3(0, 0, 0);

    [Tooltip("Local rotation when POWER OFF.")]
    public Vector3 offEuler = new Vector3(0, 0, -30);

    [Tooltip("If true, switch rotates instantly. If false, it will snap anyway (animation later).")]
    public bool instant = true;

    private void Awake()
    {
        if (targetDevice == null)
            targetDevice = GetComponentInParent<Device>();

        SyncVisual();
    }

    public void Interact()
    {
        if (targetDevice == null)
        {
            Debug.LogWarning("DevicePowerSwitch: No targetDevice assigned.");
            return;
        }

        targetDevice.TogglePower();
        SyncVisual();
    }

    private void SyncVisual()
    {
        if (targetDevice == null || switchHandle == null) return;

        var euler = targetDevice.isPoweredOn ? onEuler : offEuler;
        switchHandle.localRotation = Quaternion.Euler(euler);
    }
}
