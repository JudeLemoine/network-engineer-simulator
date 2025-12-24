using UnityEngine;

public class PowerOutletDevice : Device
{
    [Header("Outlet Settings")]
    [Tooltip("Outlets are always live for now.")]
    public bool alwaysLive = true;

    public override bool ProvidesPower => alwaysLive;

    protected override void Awake()
    {

        requiresExternalPower = false;

        isPoweredOn = true;

        base.Awake();

        SetReceivingExternalPower(true);
    }
}
