using System;

public class ConsoleRouterSession : ITerminalSession
{
    private readonly RouterDevice _router;
    private readonly IosSession _ios;

    private bool _authed;

    public ConsoleRouterSession(RouterDevice router)
    {
        _router = router;
        _ios = new IosSession(router);

        _authed = (_router == null) || !_router.consoleLoginEnabled;
    }

    public string Prompt
    {
        get
        {
            if (_router != null && _router.consoleLoginEnabled && !_authed)
                return "Password:";
            return _ios.Prompt;
        }
    }

    public string Execute(string input)
    {
        input = (input ?? "").TrimEnd();

        if (_router != null && _router.consoleLoginEnabled && !_authed)
        {

            if (string.IsNullOrWhiteSpace(input))
                return "";

            if (string.Equals(input, _router.consolePassword ?? "", StringComparison.Ordinal))
            {
                _authed = true;
                return "";
            }

            return "% Bad passwords";
        }

        return _ios.Execute(input);
    }
}
