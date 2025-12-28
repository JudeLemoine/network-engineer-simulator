using UnityEngine;

public class RackInstalledDeviceProxy : MonoBehaviour
{
    RackSlotInteractable _slot;

    public void Bind(RackSlotInteractable slot)
    {
        _slot = slot;
    }

    public void OpenRackMenu()
    {
        if (_slot != null)
            _slot.OpenMenuFromWorld();
    }
}
