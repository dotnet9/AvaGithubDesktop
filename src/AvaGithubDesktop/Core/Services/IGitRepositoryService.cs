using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IGitRepositoryService
{
    Task<GitRepositorySnapshot> LoadRepositoryAsync(string repositoryPath, CancellationToken cancellationToken);

    Task CommitAsync(
        string repositoryPath,
        IReadOnlyList<string> includedPaths,
        string summary,
        string description,
        CancellationToken cancellationToken);
}
