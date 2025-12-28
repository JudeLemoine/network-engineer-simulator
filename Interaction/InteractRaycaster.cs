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
        var hits = Physics.RaycastAll(ray, maxDistance);
        if (hits == null || hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var m = hits[i].collider.GetComponentInParent<RouterModuleSlotInteractable>();
            if (m != null)
            {
                m.Interact();
                return;
            }
        }

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
            var interactable = hits[i].collider.GetComponentInParent<IDeviceInteractable>();
            if (interactable != null)
            {
                interactable.Interact();
                return;
            }
        }
    }
}
