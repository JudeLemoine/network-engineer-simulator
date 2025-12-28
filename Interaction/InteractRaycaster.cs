using UnityEngine;

public class InteractRaycaster : MonoBehaviour
{
    public float maxDistance = 3f;

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (!Input.GetMouseButtonDown(1))
            return;

        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            var rackProxy = hit.collider.GetComponentInParent<RackSlotInstalledProxy>();
            if (rackProxy != null)
            {
                rackProxy.Interact();
                return;
            }

            var interactable = hit.collider.GetComponentInParent<IDeviceInteractable>();
            interactable?.Interact();
        }
    }
}
