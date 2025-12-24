using UnityEngine;

public class RouterHostedDevicePowerLink : MonoBehaviour
{
    public Device hostRouter;
    public Device hostedDevice;

    private void Start()
    {
        if (hostRouter == null) hostRouter = GetComponentInParent<RouterDevice>();
        if (hostedDevice == null) hostedDevice = GetComponent<Device>();

        if (hostRouter == null || hostedDevice == null)
        {
            Debug.LogWarning("RouterHostedDevicePowerLink: Missing hostRouter or hostedDevice.");
            return;
        }

        hostedDevice.SetPower(hostRouter.IsPoweredOn);

        hostRouter.PowerStateChanged += OnHostPowerChanged;
    }

    private void OnDestroy()
    {
        if (hostRouter != null)
            hostRouter.PowerStateChanged -= OnHostPowerChanged;
    }

    private void OnHostPowerChanged(bool on)
    {
        if (hostedDevice != null)
            hostedDevice.SetPower(on);
    }
}
