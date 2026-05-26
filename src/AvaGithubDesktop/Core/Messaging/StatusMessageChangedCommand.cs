using CodeWF.EventBus;

namespace AvaGithubDesktop.Core.Messaging;

public sealed class StatusMessageChangedCommand : Command
{
    public StatusMessageChangedCommand(string message)
    {
        Message = message;
    }

    public string Message { get; }
}
