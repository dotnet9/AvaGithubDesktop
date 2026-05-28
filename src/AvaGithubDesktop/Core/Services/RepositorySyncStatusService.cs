using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositorySyncStatusService : IRepositorySyncStatusService
{
    private readonly IAppLocalizer _localizer;

    public RepositorySyncStatusService(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public string GetActionTitle(RepositorySyncStatus status)
    {
        if (!status.HasRemote)
        {
            return _localizer.Get(AvaGithubDesktopL.SyncNoRemote);
        }

        if (status.IsSyncing)
        {
            return status.ActiveOperation switch
            {
                RepositorySyncOperation.FetchAll => _localizer.Get(AvaGithubDesktopL.SyncFetchingAllRemotesTitle),
                RepositorySyncOperation.FetchLfs => _localizer.Format(AvaGithubDesktopL.SyncFetchingLfsTitleFormat, status.RemoteName),
                RepositorySyncOperation.PullLfs => _localizer.Format(AvaGithubDesktopL.SyncPullingLfsTitleFormat, status.RemoteName),
                RepositorySyncOperation.UpdateSubmodules => _localizer.Get(AvaGithubDesktopL.SyncUpdatingSubmodulesTitle),
                RepositorySyncOperation.Pull => _localizer.Format(AvaGithubDesktopL.SyncPullingTitleFormat, status.RemoteName),
                RepositorySyncOperation.Publish => _localizer.Get(AvaGithubDesktopL.SyncPublishingBranchTitle),
                RepositorySyncOperation.Push => _localizer.Format(AvaGithubDesktopL.SyncPushingTitleFormat, status.RemoteName),
                _ => _localizer.Format(AvaGithubDesktopL.SyncFetchingTitleFormat, status.RemoteName)
            };
        }

        if (status.CanPublishCurrentBranch)
        {
            return _localizer.Get(AvaGithubDesktopL.SyncPublishBranchTitle);
        }

        if (status.Behind > 0)
        {
            return _localizer.Format(AvaGithubDesktopL.SyncPullTitleFormat, status.RemoteName);
        }

        if (status.Ahead > 0)
        {
            return _localizer.Format(AvaGithubDesktopL.SyncPushTitleFormat, status.RemoteName);
        }

        return _localizer.Format(AvaGithubDesktopL.SyncFetchTitleFormat, status.RemoteName);
    }

    public string GetActionDescription(RepositorySyncStatus status)
    {
        if (!status.HasRemote)
        {
            return _localizer.Get(AvaGithubDesktopL.SyncNoRemoteDescription);
        }

        if (status.IsSyncing)
        {
            return _localizer.Get(AvaGithubDesktopL.SyncInProgressDescription);
        }

        if (status.CanPublishCurrentBranch)
        {
            var descriptionKey = RepositoryRemoteUrlHelper.TryGetGitHubWebUrl(status.RemoteUrl, out _)
                ? AvaGithubDesktopL.SyncPublishBranchToGitHubDescription
                : AvaGithubDesktopL.SyncPublishBranchToRemoteDescription;
            return _localizer.Get(descriptionKey);
        }

        return FormatLastFetched(status.LastFetchedAt);
    }

    public bool IsRefreshIconVisible(RepositorySyncStatus status) =>
        status.IsSyncing ||
        (!status.CanPublishCurrentBranch && status.Behind <= 0 && status.Ahead <= 0);

    public bool IsDownloadIconVisible(RepositorySyncStatus status) =>
        !status.IsSyncing &&
        !status.CanPublishCurrentBranch &&
        status.Behind > 0;

    public bool IsUploadIconVisible(RepositorySyncStatus status) =>
        !status.IsSyncing &&
        (status.CanPublishCurrentBranch || (!status.CanPublishCurrentBranch && status.Behind <= 0 && status.Ahead > 0));

    public bool HasAheadBehind(RepositorySyncStatus status) =>
        !status.IsSyncing &&
        status.HasRemote &&
        (status.Ahead > 0 || status.Behind > 0);

    public bool HasAhead(RepositorySyncStatus status) =>
        !status.IsSyncing &&
        status.HasRemote &&
        status.Ahead > 0;

    public bool HasBehind(RepositorySyncStatus status) =>
        !status.IsSyncing &&
        status.HasRemote &&
        status.Behind > 0;

    public string GetStartedMessage(RepositorySyncOperation operation, string remoteName, string currentBranch) =>
        operation switch
        {
            RepositorySyncOperation.FetchAll => _localizer.Get(AvaGithubDesktopL.StatusFetchingAllRemotes),
            RepositorySyncOperation.FetchLfs => _localizer.Format(AvaGithubDesktopL.StatusFetchingLfsFormat, remoteName),
            RepositorySyncOperation.PullLfs => _localizer.Format(AvaGithubDesktopL.StatusPullingLfsFormat, remoteName),
            RepositorySyncOperation.Pull => _localizer.Format(AvaGithubDesktopL.StatusPullingFormat, remoteName),
            RepositorySyncOperation.Publish => _localizer.Format(AvaGithubDesktopL.StatusPublishingBranchFormat, currentBranch, remoteName),
            RepositorySyncOperation.Push => _localizer.Format(AvaGithubDesktopL.StatusPushingFormat, remoteName),
            _ => _localizer.Format(AvaGithubDesktopL.StatusFetchingFormat, remoteName)
        };

    public string GetCompletedMessage(RepositorySyncOperation operation, string remoteName, string currentBranch) =>
        operation switch
        {
            RepositorySyncOperation.FetchAll => _localizer.Get(AvaGithubDesktopL.StatusFetchedAllRemotes),
            RepositorySyncOperation.FetchLfs => _localizer.Format(AvaGithubDesktopL.StatusFetchedLfsFormat, remoteName),
            RepositorySyncOperation.PullLfs => _localizer.Format(AvaGithubDesktopL.StatusPulledLfsFormat, remoteName),
            RepositorySyncOperation.Pull => _localizer.Format(AvaGithubDesktopL.StatusPulledFormat, remoteName),
            RepositorySyncOperation.Publish => _localizer.Format(AvaGithubDesktopL.StatusPublishedBranchFormat, currentBranch, remoteName),
            RepositorySyncOperation.Push => _localizer.Format(AvaGithubDesktopL.StatusPushedFormat, remoteName),
            _ => _localizer.Format(AvaGithubDesktopL.StatusFetchedFormat, remoteName)
        };

    public string GetFailedFormatKey(RepositorySyncOperation operation) =>
        operation switch
        {
            RepositorySyncOperation.FetchAll => AvaGithubDesktopL.StatusFetchAllRemotesFailedFormat,
            RepositorySyncOperation.FetchLfs => AvaGithubDesktopL.StatusFetchLfsFailedFormat,
            RepositorySyncOperation.PullLfs => AvaGithubDesktopL.StatusPullLfsFailedFormat,
            RepositorySyncOperation.Pull => AvaGithubDesktopL.StatusPullFailedFormat,
            RepositorySyncOperation.Publish => AvaGithubDesktopL.StatusPublishBranchFailedFormat,
            RepositorySyncOperation.Push => AvaGithubDesktopL.StatusPushFailedFormat,
            _ => AvaGithubDesktopL.StatusFetchFailedFormat
        };

    private string FormatLastFetched(DateTimeOffset? lastFetchedAt)
    {
        if (lastFetchedAt is null)
        {
            return _localizer.Get(AvaGithubDesktopL.SyncNeverFetched);
        }

        var elapsed = DateTimeOffset.Now - lastFetchedAt.Value.ToLocalTime();
        if (elapsed.TotalMinutes < 2)
        {
            return _localizer.Get(AvaGithubDesktopL.SyncLastFetchedJustNow);
        }

        if (elapsed.TotalHours < 1)
        {
            return _localizer.Format(AvaGithubDesktopL.SyncLastFetchedMinutesAgoFormat, Math.Max(1, (int)elapsed.TotalMinutes));
        }

        if (elapsed.TotalDays < 1)
        {
            return _localizer.Format(AvaGithubDesktopL.SyncLastFetchedHoursAgoFormat, Math.Max(1, (int)elapsed.TotalHours));
        }

        return _localizer.Format(AvaGithubDesktopL.SyncLastFetchedDaysAgoFormat, Math.Max(1, (int)elapsed.TotalDays));
    }
}
