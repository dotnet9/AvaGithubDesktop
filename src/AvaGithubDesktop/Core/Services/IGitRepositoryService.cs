using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IGitRepositoryService
{
    Task<GitRepositorySnapshot> LoadRepositoryAsync(string repositoryPath, CancellationToken cancellationToken);

    Task<IReadOnlyList<GitCommitItem>> LoadHistoryAsync(
        string repositoryPath,
        int maxCount,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitBranchItem>> LoadBranchesAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task CheckoutBranchAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken);

    Task<string> LoadWorkingTreeDiffAsync(
        string repositoryPath,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken);

    Task<string> LoadCommitFileDiffAsync(
        string repositoryPath,
        string sha,
        string path,
        CancellationToken cancellationToken);

    Task CommitAsync(
        string repositoryPath,
        IReadOnlyList<string> includedPaths,
        string summary,
        string description,
        CancellationToken cancellationToken);
}
