namespace AvaGithubDesktop.Core.Models;

public sealed record GitRepositorySnapshot(
    string RepositoryName,
    string RootPath,
    string CurrentBranch,
    string Upstream,
    string RemoteUrl,
    string LastCommit,
    int Ahead,
    int Behind,
    IReadOnlyList<GitChangeItem> Changes)
{
    public int ChangedFilesCount => Changes.Count;

    public int StagedCount => Changes.Count(change => change.Kind == GitChangeKind.Staged);

    public int UnstagedCount => Changes.Count(change => change.Kind == GitChangeKind.Unstaged);

    public int UntrackedCount => Changes.Count(change => change.Kind == GitChangeKind.Untracked);
}
