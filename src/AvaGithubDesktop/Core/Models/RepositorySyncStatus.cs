namespace AvaGithubDesktop.Core.Models;

public sealed record RepositorySyncStatus(
    bool HasRemote,
    bool IsSyncing,
    bool CanPublishCurrentBranch,
    int Ahead,
    int Behind,
    DateTimeOffset? LastFetchedAt,
    string RemoteName,
    string RemoteUrl,
    RepositorySyncOperation ActiveOperation);
