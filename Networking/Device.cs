using System;
using UnityEngine;

public class Device : MonoBehaviour
{
    public string deviceName = "Device";

    [Header("Power")]
    [Tooltip("Front-panel switch. If false, device is off (links go down, console disabled).")]
    public bool isPoweredOn = true;

    [Tooltip("If true, device needs to be plugged into a live power source AND switched on.")]
    public bool requiresExternalPower = false;

    private bool _receivingExternalPower = true;

    public virtual bool ProvidesPower => false;

    public bool IsReceivingPower => !requiresExternalPower || _receivingExternalPower;

    public bool IsPoweredOn => isPoweredOn && IsReceivingPower;

    public event Action<bool> PowerStateChanged;

    protected virtual void Awake()
    {
        AssignPortsToThisDevice();
        _receivingExternalPower = !requiresExternalPower;
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        AssignPortsToThisDevice();
    }
#endif

    private void AssignPortsToThisDevice()
    {
        var ports = GetComponentsInChildren<Port>(true);
        foreach (var p in ports)
        {
            if (p == null) continue;
            p.owner = this;
        }
    }

    public void SetPower(bool on)
    {
        if (isPoweredOn == on) return;
        isPoweredOn = on;

        OnPowerStateChanged(IsPoweredOn);
        PowerStateChanged?.Invoke(IsPoweredOn);

        if (CableManager.Instance != null)
            CableManager.Instance.OnDevicePowerChanged(this);
    }

    public void TogglePower()
    {
        SetPower(!isPoweredOn);
    }

    public void SetReceivingExternalPower(bool receiving)
    {
        if (!requiresExternalPower)
        {
            receiving = true;
        }

        if (_receivingExternalPower == receiving) return;
        _receivingExternalPower = receiving;

        OnPowerStateChanged(IsPoweredOn);
        PowerStateChanged?.Invoke(IsPoweredOn);

        if (CableManager.Instance != null)
            CableManager.Instance.OnDevicePowerChanged(this);
    }

    protected virtual void OnPowerStateChanged(bool poweredOn)
    {
    }
}
