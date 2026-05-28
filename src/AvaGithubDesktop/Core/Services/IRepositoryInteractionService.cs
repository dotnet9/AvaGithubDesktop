namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryInteractionService
{
    Task<string?> CopyTextAsync(string text, string successKey, string failureFormatKey);

    Task<string?> OpenRepositoryInShellAsync(string repositoryPath);

    Task<string?> OpenRepositoryInExternalEditorAsync(string repositoryPath);

    Task<string?> ShowRepositoryInFileManagerAsync(string repositoryPath);

    Task<string?> OpenChangeInExternalEditorAsync(string filePath);

    Task<string?> ShowChangeInFileManagerAsync(string filePath);

    Task<string?> ViewRepositoryOnGitHubAsync(string? remoteUrl);

    Task<string?> OpenIssueCreationOnGitHubAsync(string? remoteUrl);

    Task<string?> ViewCommitOnGitHubAsync(string? remoteUrl, string sha);

    Task<string?> ViewBranchOnGitHubAsync(string? remoteUrl, string upstream);

    Task<string?> CompareBranchOnGitHubAsync(string? remoteUrl, string upstream);

    Task<string?> OpenCreatePullRequestOnGitHubAsync(string? remoteUrl, string upstream, string currentBranch);
}
