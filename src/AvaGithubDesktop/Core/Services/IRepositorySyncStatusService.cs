using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IRepositorySyncStatusService
{
    string GetActionTitle(RepositorySyncStatus status);

    string GetActionDescription(RepositorySyncStatus status);

    bool IsRefreshIconVisible(RepositorySyncStatus status);

    bool IsDownloadIconVisible(RepositorySyncStatus status);

    bool IsUploadIconVisible(RepositorySyncStatus status);

    bool HasAheadBehind(RepositorySyncStatus status);

    bool HasAhead(RepositorySyncStatus status);

    bool HasBehind(RepositorySyncStatus status);

    string GetStartedMessage(RepositorySyncOperation operation, string remoteName, string currentBranch);

    string GetCompletedMessage(RepositorySyncOperation operation, string remoteName, string currentBranch);

    string GetFailedFormatKey(RepositorySyncOperation operation);
}
