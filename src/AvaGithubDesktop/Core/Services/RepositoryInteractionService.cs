using AvaGithubDesktop.Core.Messaging;
using CodeWF.EventBus;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryInteractionService : IRepositoryInteractionService
{
    private readonly IRepositoryShellService _repositoryShellService;
    private readonly IAppLocalizer _localizer;
    private readonly IEventBus _eventBus;

    public RepositoryInteractionService(
        IRepositoryShellService repositoryShellService,
        IAppLocalizer localizer,
        IEventBus eventBus)
    {
        _repositoryShellService = repositoryShellService;
        _localizer = localizer;
        _eventBus = eventBus;
    }

    public Task<string?> CopyTextAsync(string text, string successKey, string failureFormatKey)
    {
        return RunShellActionAsync(
            () => _repositoryShellService.CopyTextAsync(text),
            _localizer.Get(successKey),
            failureFormatKey);
    }

    public Task<string?> OpenRepositoryInShellAsync(string repositoryPath)
    {
        return RunShellActionAsync(
            () => _repositoryShellService.OpenInShellAsync(repositoryPath),
            _localizer.Get(AvaGithubDesktopL.StatusOpenedRepositoryShell),
            AvaGithubDesktopL.StatusOpenRepositoryShellFailedFormat);
    }

    public Task<string?> OpenRepositoryInExternalEditorAsync(string repositoryPath)
    {
        return RunShellActionAsync(
            () => _repositoryShellService.OpenItemAsync(repositoryPath),
            _localizer.Get(AvaGithubDesktopL.StatusOpenedRepositoryInExternalEditor),
            AvaGithubDesktopL.StatusOpenRepositoryInExternalEditorFailedFormat);
    }

    public Task<string?> ShowRepositoryInFileManagerAsync(string repositoryPath)
    {
        return RunShellActionAsync(
            () => _repositoryShellService.ShowInFileManagerAsync(repositoryPath),
            _localizer.Get(AvaGithubDesktopL.StatusShowedRepositoryInFileManager),
            AvaGithubDesktopL.StatusShowRepositoryInFileManagerFailedFormat);
    }

    public Task<string?> OpenChangeInExternalEditorAsync(string filePath)
    {
        return RunShellActionAsync(
            () => _repositoryShellService.OpenItemAsync(filePath),
            _localizer.Get(AvaGithubDesktopL.StatusOpenedChangeInExternalEditor),
            AvaGithubDesktopL.StatusOpenChangeInExternalEditorFailedFormat);
    }

    public Task<string?> ShowChangeInFileManagerAsync(string filePath)
    {
        return RunShellActionAsync(
            () => _repositoryShellService.ShowItemInFileManagerAsync(filePath),
            _localizer.Get(AvaGithubDesktopL.StatusShowedChangeInFileManager),
            AvaGithubDesktopL.StatusShowChangeInFileManagerFailedFormat);
    }

    public async Task<string?> ViewRepositoryOnGitHubAsync(string? remoteUrl)
    {
        if (!RepositoryRemoteUrlHelper.TryGetGitHubWebUrl(remoteUrl, out var webUrl))
        {
            return PublishError(_localizer.Get(AvaGithubDesktopL.StatusRepositoryHasNoGitHubRemote));
        }

        return await OpenUrlAsync(
            webUrl,
            _localizer.Get(AvaGithubDesktopL.StatusOpenedRepositoryOnGitHub),
            AvaGithubDesktopL.StatusOpenRepositoryOnGitHubFailedFormat);
    }

    public async Task<string?> OpenIssueCreationOnGitHubAsync(string? remoteUrl)
    {
        if (!RepositoryRemoteUrlHelper.TryGetGitHubIssueCreationUrl(remoteUrl, out var webUrl))
        {
            return PublishError(_localizer.Get(AvaGithubDesktopL.StatusRepositoryHasNoGitHubRemote));
        }

        return await OpenUrlAsync(
            webUrl,
            _localizer.Get(AvaGithubDesktopL.StatusOpenedIssueCreationOnGitHub),
            AvaGithubDesktopL.StatusOpenIssueCreationOnGitHubFailedFormat);
    }

    public async Task<string?> ViewCommitOnGitHubAsync(string? remoteUrl, string sha)
    {
        if (!RepositoryRemoteUrlHelper.TryGetGitHubCommitUrl(remoteUrl, sha, out var webUrl))
        {
            return PublishError(_localizer.Get(AvaGithubDesktopL.StatusRepositoryHasNoGitHubRemote));
        }

        return await OpenUrlAsync(
            webUrl,
            _localizer.Get(AvaGithubDesktopL.StatusOpenedCommitOnGitHub),
            AvaGithubDesktopL.StatusOpenCommitOnGitHubFailedFormat);
    }

    public async Task<string?> ViewBranchOnGitHubAsync(string? remoteUrl, string upstream)
    {
        if (!RepositoryRemoteUrlHelper.TryGetGitHubBranchUrl(remoteUrl, upstream, out var webUrl))
        {
            return PublishError(_localizer.Get(AvaGithubDesktopL.StatusRepositoryHasNoGitHubRemote));
        }

        return await OpenUrlAsync(
            webUrl,
            _localizer.Get(AvaGithubDesktopL.StatusOpenedBranchOnGitHub),
            AvaGithubDesktopL.StatusOpenBranchOnGitHubFailedFormat);
    }

    public async Task<string?> CompareBranchOnGitHubAsync(string? remoteUrl, string upstream)
    {
        if (!RepositoryRemoteUrlHelper.TryGetGitHubCompareUrl(remoteUrl, upstream, out var webUrl))
        {
            return PublishError(_localizer.Get(AvaGithubDesktopL.StatusRepositoryHasNoGitHubRemote));
        }

        return await OpenUrlAsync(
            webUrl,
            _localizer.Get(AvaGithubDesktopL.StatusOpenedBranchCompareOnGitHub),
            AvaGithubDesktopL.StatusOpenBranchCompareOnGitHubFailedFormat);
    }

    public async Task<string?> OpenCreatePullRequestOnGitHubAsync(
        string? remoteUrl,
        string upstream,
        string currentBranch)
    {
        if (!RepositoryRemoteUrlHelper.TryGetGitHubPullRequestUrl(remoteUrl, upstream, currentBranch, out var webUrl))
        {
            return PublishError(_localizer.Get(AvaGithubDesktopL.StatusRepositoryHasNoGitHubRemote));
        }

        return await OpenUrlAsync(
            webUrl,
            _localizer.Get(AvaGithubDesktopL.StatusOpenedCreatePullRequestOnGitHub),
            AvaGithubDesktopL.StatusOpenCreatePullRequestOnGitHubFailedFormat);
    }

    private Task<string?> OpenUrlAsync(string webUrl, string successMessage, string failureFormatKey)
    {
        return RunShellActionAsync(
            () => _repositoryShellService.OpenUrlAsync(webUrl),
            successMessage,
            failureFormatKey);
    }

    private async Task<string?> RunShellActionAsync(
        Func<Task> action,
        string successMessage,
        string failureFormatKey)
    {
        try
        {
            await action();
            _eventBus.Publish(new StatusMessageChangedCommand(successMessage));
            return null;
        }
        catch (Exception ex)
        {
            return PublishError(_localizer.Format(failureFormatKey, ex.Message));
        }
    }

    private string PublishError(string message)
    {
        _eventBus.Publish(new StatusMessageChangedCommand(message));
        return message;
    }
}
