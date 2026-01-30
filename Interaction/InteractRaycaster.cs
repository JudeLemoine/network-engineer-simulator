using UnityEngine;

public class InteractRaycaster : MonoBehaviour
{
    public float maxDistance = 3f;

    private void Update()
    {
        if (RuntimePrefabPlacer.IsAnyPlacementUIOpen || RuntimePrefabPlacer.IsPlacingActive)
            return;

        if (!Input.GetMouseButtonDown(1))
            return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

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

        if (shift)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                var host = hits[i].collider.GetComponentInParent<TerminalPopupHost>();
                if (host != null)
                {
                    host.Toggle();
                    return;
                }
            }
            return;
        }

        if (alt)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                var p = hits[i].collider.GetComponentInParent<RackSlotInstalledProxy>();
                if (p != null)
                {
                    p.Interact();
                    return;
                }
            }

            for (int i = 0; i < hits.Length; i++)
            {
                var s = hits[i].collider.GetComponentInParent<RackSlotInteractable>();
                if (s != null)
                {
                    s.Interact();
                    return;
                }
            }

            for (int i = 0; i < hits.Length; i++)
            {
                var m = hits[i].collider.GetComponentInParent<RouterModuleSlotInteractable>();
                if (m != null)
                {
                    m.Interact();
                    return;
                }
            }

            return;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            var interactable = hits[i].collider.GetComponentInParent<IDeviceInteractable>();
            if (interactable == null) continue;

            var host = hits[i].collider.GetComponentInParent<TerminalPopupHost>();
            if (host != null)
                continue;

            interactable.Interact();
            return;
        }
    }
}
