using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryRemoteDialogService
{
    Task<RepositoryRemoteRequest?> ShowManageRemoteDialogAsync(
        string remoteName,
        string remoteUrl,
        bool hasRemote);
}
