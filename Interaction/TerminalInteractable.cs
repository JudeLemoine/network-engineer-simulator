using UnityEngine;

public class TerminalInteractable : MonoBehaviour
{
    [SerializeField] TerminalScreen terminalScreen;

    public void SetTerminalScreen(TerminalScreen screen)
    {
        terminalScreen = screen;
    }

    public TerminalScreen GetTerminalScreen()
    {
        if (terminalScreen == null)
            terminalScreen = GetComponentInChildren<TerminalScreen>(true);
        return terminalScreen;
    }
}
