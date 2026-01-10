using UnityEngine;

public class PowerPduDevice : Device
{
    public override bool ProvidesPower => IsPoweredOn;

    protected override void Awake()
    {
        requiresExternalPower = true;
        base.Awake();
    }
}
