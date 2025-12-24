using UnityEngine;

public class ClickRaycaster : MonoBehaviour
{
    public float maxDistance = 5f;

    private void Update()
    {

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (TerminalScreen.IsAnyTerminalFocused)
            return;

        if (RouterModuleSlotInteractable.IsAnyModuleMenuOpen)
            return;

        if (CableManager.IsAnyCableMenuOpen)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            var port = hit.collider.GetComponentInParent<Port>();
            if (port != null && CableManager.Instance != null)
            {
                CableManager.Instance.ClickPort(port);
            }
        }
    }
}
