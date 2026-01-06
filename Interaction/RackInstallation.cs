using UnityEngine;

public class RackInstallation : MonoBehaviour
{
    RackManager _manager;
    int _startIndex;
    int _uHeight;

    public void Bind(RackManager manager, int startIndex, int uHeight)
    {
        _manager = manager;
        _startIndex = startIndex;
        _uHeight = uHeight;
    }

    void OnDestroy()
    {
        if (_manager == null) return;
        _manager.NotifyInstalledDestroyed(_startIndex, gameObject);
    }
}
