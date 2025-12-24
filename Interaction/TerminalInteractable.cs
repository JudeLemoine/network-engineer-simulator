using UnityEngine;

public class TerminalInteractable : MonoBehaviour, IDeviceInteractable
{
    [Header("Link")]
    public TerminalScreen terminalScreen;

    public void Interact()
    {
        if (terminalScreen == null)
            terminalScreen = GetComponentInChildren<TerminalScreen>(true);

        if (terminalScreen == null)
        {
            Debug.LogWarning("TerminalInteractable: No TerminalScreen found.");
            return;
        }

        terminalScreen.Focus();
    }
}
