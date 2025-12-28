using System;

public interface ITerminalSession
{
    string Prompt { get; }
    string Execute(string cmd);
}

public interface IDeviceInteractable
{
    void Interact();
}

[Serializable]
public class StaticRoute
{
    public string network;
    public string subnetMask;
    public string nextHop;
    public string exitInterface;
}
