namespace AvaGithubDesktop.Core.Models;

public sealed record GitRepositorySnapshot(
    string RepositoryName,
    string RootPath,
    string CurrentBranch,
    string DefaultBranch,
    string Upstream,
    string RemoteName,
    string RemoteUrl,
    DateTimeOffset? LastFetchedAt,
    string LastCommit,
    int Ahead,
    int Behind,
    RepositoryOperationState OperationState,
    GitStashEntry? CurrentBranchStash,
    IReadOnlyList<GitChangeItem> Changes)
{
    public int ChangedFilesCount => Changes.Count;

    public int StagedCount => Changes.Count(change => change.Kind == GitChangeKind.Staged);

    public int UnstagedCount => Changes.Count(change => change.Kind == GitChangeKind.Unstaged);

    public int UntrackedCount => Changes.Count(change => change.Kind == GitChangeKind.Untracked);
}
