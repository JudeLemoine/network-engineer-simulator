using UnityEngine;

public class InteractRaycaster : MonoBehaviour
{
    public float maxDistance = 3f;

    private void Update()
    {
        if (RuntimePrefabPlacer.IsAnyPlacementUIOpen || RuntimePrefabPlacer.IsPlacingActive)
            return;

        if (TerminalScreen.IsAnyTerminalFocused)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryRackInteraction();
            return;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            TryTerminalInteraction();
            return;
        }

        if (!Input.GetMouseButtonDown(1))
            return;

        Ray ray;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            ray = new Ray(transform.position, transform.forward);
        }
        else
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            ray = cam.ScreenPointToRay(Input.mousePosition);
        }

        var hits = Physics.RaycastAll(ray, maxDistance);
        if (hits == null || hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var cable = hits[i].collider.GetComponentInParent<CableVisual>();
            if (cable != null && CableManager.Instance != null)
            {
                CableManager.Instance.OpenCableMenu(cable);
                return;
            }
        }

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.GetComponentInParent<RackSlotInstalledProxy>() != null)
                continue;

            if (hits[i].collider.GetComponentInParent<RackSlotInteractable>() != null)
                continue;

            var interactable = hits[i].collider.GetComponentInParent<IDeviceInteractable>();
            if (interactable == null)
                continue;

            var host = hits[i].collider.GetComponentInParent<TerminalPopupHost>();
            if (host != null)
                continue;

            interactable.Interact();
            return;
        }
    }

    void TryRackInteraction()
    {
        Ray ray;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            ray = new Ray(transform.position, transform.forward);
        }
        else
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            ray = cam.ScreenPointToRay(Input.mousePosition);
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            return;

        var proxy = hit.collider.GetComponentInParent<RackSlotInstalledProxy>();
        if (proxy != null)
        {
            proxy.Interact();
            return;
        }

        var slot = hit.collider.GetComponentInParent<RackSlotInteractable>();
        if (slot != null)
        {
            slot.Interact();
            return;
        }
    }

    void TryTerminalInteraction()
    {
        Ray ray;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            ray = new Ray(transform.position, transform.forward);
        }
        else
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            ray = cam.ScreenPointToRay(Input.mousePosition);
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            return;

        var host = hit.collider.GetComponentInParent<TerminalPopupHost>();
        if (host != null)
        {
            host.Toggle();
            return;
        }
    }
}
