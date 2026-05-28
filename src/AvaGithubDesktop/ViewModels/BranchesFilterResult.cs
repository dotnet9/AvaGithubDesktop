namespace AvaGithubDesktop.ViewModels;

public sealed record BranchesFilterResult(
    IReadOnlyList<GitBranchItemViewModel> Branches,
    GitBranchItemViewModel? SelectedBranch);
