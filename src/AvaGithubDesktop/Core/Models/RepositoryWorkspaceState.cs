namespace AvaGithubDesktop.Core.Models;

public sealed record RepositoryWorkspaceState(
    GitRepositorySnapshot Snapshot,
    IReadOnlyList<GitBranchItem> Branches,
    IReadOnlyList<GitCommitItem> History);
