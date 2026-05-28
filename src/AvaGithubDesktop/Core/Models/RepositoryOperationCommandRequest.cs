namespace AvaGithubDesktop.Core.Models;

public sealed record RepositoryOperationCommandRequest(
    bool CanRun,
    string StartedKey,
    string CompletedKey,
    string FailedFormatKey,
    Func<Task> Operation,
    Func<Task> ReloadWorkspaceAsync,
    Func<Task> TryReloadWorkspaceAsync,
    Action<bool> SetBusy);
