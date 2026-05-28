namespace AvaGithubDesktop.Core.Models;

public sealed record RepositoryOperationCommandRequest(
    bool CanRun,
    string StartedMessage,
    Func<string> CreateCompletedMessage,
    Func<Exception, string> CreateFailedMessage,
    Func<Task> Operation,
    Func<Task> ReloadWorkspaceAsync,
    Func<Task> TryReloadWorkspaceAsync,
    Action<bool> SetBusy);
