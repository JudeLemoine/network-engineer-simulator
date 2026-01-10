using UnityEngine;

public class TerminalPopupHost : MonoBehaviour
{
    public GameObject terminalPrefab;
    public Transform mountPoint;
    public Vector3 localOffset = new Vector3(0f, 1.1f, 0.9f);
    public Vector3 localEuler = new Vector3(0f, 180f, 0f);
    public bool spawnAsChild = true;

    public float mountForwardClearance = 0.06f;

    public GameObject currentTerminalInstance;

    public bool IsOpen => currentTerminalInstance != null;

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        if (terminalPrefab == null) return;

        Vector3 pos;
        Quaternion rot;

        if (mountPoint != null)
        {
            pos = mountPoint.position + (mountPoint.forward * mountForwardClearance);
            rot = mountPoint.rotation;
        }
        else
        {
            pos = transform.TransformPoint(localOffset);
            rot = transform.rotation * Quaternion.Euler(localEuler);
        }

        currentTerminalInstance = Object.Instantiate(terminalPrefab, pos, rot, spawnAsChild ? transform : null);
        LinkDevice(currentTerminalInstance);
    }

    void LinkDevice(GameObject instance)
    {
        if (instance == null) return;

        var screen = instance.GetComponentInChildren<TerminalScreen>(true);
        if (screen == null) return;

        screen.routerDevice = GetComponent<RouterDevice>();
        screen.switchDevice = GetComponent<SwitchDevice>();
        screen.pcDevice = GetComponent<PcDevice>();
    }

    public void Close()
    {
        if (!IsOpen) return;
        Object.Destroy(currentTerminalInstance);
        currentTerminalInstance = null;
    }

    void OnDisable()
    {
        Close();
    }

    void OnDestroy()
    {
        Close();
    }
}
