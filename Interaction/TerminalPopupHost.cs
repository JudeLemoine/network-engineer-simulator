using UnityEngine;

public class TerminalPopupHost : MonoBehaviour
{
    [SerializeField] GameObject terminalPrefab;
    [SerializeField] Transform mountPoint;
    [SerializeField] Vector3 localOffset = Vector3.zero;
    [SerializeField] Vector3 localEuler;
    [SerializeField] bool spawnAsChild = true;
    [SerializeField] float mountForwardClearance = 0.06f;

    [SerializeField] GameObject currentTerminalInstance;

    public GameObject CurrentTerminalInstance => currentTerminalInstance;

    public void Toggle()
    {
        if (currentTerminalInstance != null)
        {
            Destroy(currentTerminalInstance);
            currentTerminalInstance = null;
            LinkTerminalScreen(null);
            return;
        }

        if (terminalPrefab == null)
            return;

        Transform parent = spawnAsChild ? transform : null;
        currentTerminalInstance = Instantiate(terminalPrefab, parent);

        Transform mp = mountPoint != null ? mountPoint : transform;

        Vector3 pos = mp.position;
        if (mountForwardClearance != 0f)
            pos += mp.forward * mountForwardClearance;

        pos += mp.TransformDirection(localOffset);

        currentTerminalInstance.transform.position = pos;
        currentTerminalInstance.transform.rotation = mp.rotation * Quaternion.Euler(localEuler);

        var screen = currentTerminalInstance.GetComponentInChildren<TerminalScreen>(true);
        LinkTerminalScreen(screen);
    }

    void LinkTerminalScreen(TerminalScreen screen)
    {
        var ti = GetComponent<TerminalInteractable>();
        if (ti != null)
            ti.SetTerminalScreen(screen);
    }
}
