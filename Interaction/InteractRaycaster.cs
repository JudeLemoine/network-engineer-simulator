using UnityEngine;

public class InteractRaycaster : MonoBehaviour
{
    public float maxDistance = 3f;

    void Update()
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
            TryTerminalToggle();
            return;
        }

        if (!Input.GetMouseButtonDown(1))
            return;

        Ray ray = GetRay();
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            return;

        var cable = hit.collider.GetComponentInParent<CableVisual>();
        if (cable != null && CableManager.Instance != null)
        {
            CableManager.Instance.OpenCableMenu(cable);
            return;
        }

        var clickedTerminal = FindTerminalScreenFromHit(hit.collider.transform);
        if (clickedTerminal != null)
        {
            clickedTerminal.Focus();
            return;
        }

        if (hit.collider.GetComponentInParent<RackSlotInteractable>() != null)
            return;

        if (hit.collider.GetComponentInParent<RackSlotInstalledProxy>() != null)
            return;

        if (hit.collider.GetComponentInParent<TerminalPopupHost>() != null)
            return;

        var interactable = hit.collider.GetComponentInParent<IDeviceInteractable>();
        if (interactable != null)
        {
            interactable.Interact();
            return;
        }
    }

    TerminalScreen FindTerminalScreenFromHit(Transform start)
    {
        Transform t = start;
        while (t != null)
        {
            if (t.GetComponent<Device>() != null)
                break;

            var screen = t.GetComponentInChildren<TerminalScreen>(true);
            if (screen != null)
                return screen;

            t = t.parent;
        }
        return null;
    }

    void TryRackInteraction()
    {
        Ray ray = GetRay();
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

    void TryTerminalToggle()
    {
        Ray ray = GetRay();
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            return;

        var host = hit.collider.GetComponentInParent<TerminalPopupHost>();
        if (host != null)
        {
            host.Toggle();
            return;
        }

        var clickedTerminal = FindTerminalScreenFromHit(hit.collider.transform);
        if (clickedTerminal != null)
        {
            clickedTerminal.Focus();
            return;
        }
    }

    Ray GetRay()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
            return new Ray(transform.position, transform.forward);

        Camera cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        return cam.ScreenPointToRay(Input.mousePosition);
    }
}
