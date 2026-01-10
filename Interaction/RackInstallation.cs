using UnityEngine;

public class RackInstallation : MonoBehaviour
{
    RackManager _manager;
    int _startIndex;
    int _uHeight;

    string _startSlotLabel;
    string _endSlotLabel;

    public int StartIndex => _startIndex;
    public int UHeight => _uHeight;
    public string StartSlotLabel => _startSlotLabel;
    public string EndSlotLabel => _endSlotLabel;

    public void Bind(RackManager manager, int startIndex, int uHeight)
    {
        _manager = manager;
        _startIndex = startIndex;
        _uHeight = uHeight;
        if (string.IsNullOrWhiteSpace(_startSlotLabel)) _startSlotLabel = "";
        if (string.IsNullOrWhiteSpace(_endSlotLabel)) _endSlotLabel = "";
    }

    public void Bind(RackManager manager, int startIndex, int uHeight, string startSlotLabel, string endSlotLabel)
    {
        _manager = manager;
        _startIndex = startIndex;
        _uHeight = uHeight;
        _startSlotLabel = startSlotLabel ?? "";
        _endSlotLabel = endSlotLabel ?? "";
    }

    void OnDestroy()
    {
        if (_manager == null) return;
        _manager.NotifyInstalledDestroyed(_startIndex, gameObject);
    }
}
