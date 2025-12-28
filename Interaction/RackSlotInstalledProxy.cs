using UnityEngine;

public class RackSlotInstalledProxy : MonoBehaviour, IDeviceInteractable
{
    RackSlotInteractable _slot;

    public void Bind(RackSlotInteractable slot)
    {
        _slot = slot;
    }

    public void Interact()
    {
        if (_slot != null) _slot.OpenFromProxy();
    }
}
