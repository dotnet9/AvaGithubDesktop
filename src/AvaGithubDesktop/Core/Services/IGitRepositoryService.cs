using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IGitRepositoryService
{
    Task CloneRepositoryAsync(
        string sourceUrl,
        string destinationPath,
        CancellationToken cancellationToken);

    Task CreateRepositoryAsync(
        string destinationPath,
        CancellationToken cancellationToken);

    Task<GitRepositorySnapshot> LoadRepositoryAsync(string repositoryPath, CancellationToken cancellationToken);

    Task SetRemoteAsync(
        string repositoryPath,
        string remoteName,
        string remoteUrl,
        CancellationToken cancellationToken);

    Task RemoveRemoteAsync(
        string repositoryPath,
        string remoteName,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitCommitItem>> LoadHistoryAsync(
        string repositoryPath,
        int maxCount,
        CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> LoadTagNamesAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitBranchItem>> LoadBranchesAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task CheckoutBranchAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken);

    Task CreateBranchAsync(
        string repositoryPath,
        string branchName,
        string? startPoint,
        bool checkoutBranch,
        CancellationToken cancellationToken);

    Task RenameBranchAsync(
        string repositoryPath,
        string oldBranchName,
        string newBranchName,
        CancellationToken cancellationToken);

    Task DeleteBranchAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken);

    Task<GitMergeResult> MergeBranchAsync(
        string repositoryPath,
        string sourceBranchName,
        CancellationToken cancellationToken);

    Task<GitMergeResult> SquashMergeBranchAsync(
        string repositoryPath,
        string sourceBranchName,
        CancellationToken cancellationToken);

    Task<GitRebaseResult> RebaseCurrentBranchAsync(
        string repositoryPath,
        string baseBranchName,
        CancellationToken cancellationToken);

    Task ContinueMergeAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task AbortMergeAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task ContinueRebaseAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task SkipRebaseAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task AbortRebaseAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task ContinueRevertAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task AbortRevertAsync(
        string repositoryPath,
        CancellationToken cancellationToken);

    Task FetchAsync(
        string repositoryPath,
        string remoteName,
        CancellationToken cancellationToken);

    Task PullAsync(
        string repositoryPath,
        string remoteName,
        CancellationToken cancellationToken);

    Task PushAsync(
        string repositoryPath,
        string remoteName,
        string branchName,
        CancellationToken cancellationToken);

    Task PushTagsAsync(
        string repositoryPath,
        string remoteName,
        CancellationToken cancellationToken);

    Task<bool> CreateStashAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken);

    Task RestoreStashAsync(
        string repositoryPath,
        string stashName,
        CancellationToken cancellationToken);

    Task DiscardStashAsync(
        string repositoryPath,
        string stashName,
        CancellationToken cancellationToken);

    Task DiscardChangesAsync(
        string repositoryPath,
        IReadOnlyList<GitChangeItem> changes,
        CancellationToken cancellationToken);

    Task<string> LoadWorkingTreeDiffAsync(
        string repositoryPath,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken);

    Task<GitFileDiffPreview> LoadWorkingTreeDiffPreviewAsync(
        string repositoryPath,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken);

    Task<string> LoadCommitFileDiffAsync(
        string repositoryPath,
        string sha,
        string path,
        CancellationToken cancellationToken);

    Task<GitFileDiffPreview> LoadCommitFileDiffPreviewAsync(
        string repositoryPath,
        string sha,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken);

    Task CommitAsync(
        string repositoryPath,
        IReadOnlyList<string> includedPaths,
        string summary,
        string description,
        bool amendLastCommit,
        CancellationToken cancellationToken);

    Task CreateTagAsync(
        string repositoryPath,
        string tagName,
        string message,
        string targetSha,
        CancellationToken cancellationToken);

    Task RevertCommitAsync(
        string repositoryPath,
        string sha,
        CancellationToken cancellationToken);
}
