public interface ITerminalSession
{
    string Prompt { get; }
    string Execute(string input);
}
