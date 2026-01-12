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

        var ports = GetComponentsInChildren<Port>(true);
        for (int i = 0; i < ports.Length; i++)
        {
            var p = ports[i];
            if (p == null) continue;
            if (p.owner != null && p.owner != this) continue;
            if (p.medium != PortMedium.Power) continue;
            p.powerRole = PowerPortRole.Outlet;
        }

        SetReceivingExternalPower(true);
    }
}
