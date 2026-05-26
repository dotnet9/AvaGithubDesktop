using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IGitRepositoryService
{
    Task<GitRepositorySnapshot> LoadRepositoryAsync(string repositoryPath, CancellationToken cancellationToken);
}
