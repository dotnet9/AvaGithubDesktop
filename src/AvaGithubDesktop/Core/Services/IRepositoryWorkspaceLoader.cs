using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryWorkspaceLoader
{
    Task<RepositoryWorkspaceState> LoadAsync(
        string repositoryPath,
        int historyCommitLimit,
        CancellationToken cancellationToken);
}
