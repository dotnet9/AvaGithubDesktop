using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.ViewModels;

internal sealed record BranchDialogFilterResult(
    IReadOnlyList<GitBranchItem> Branches,
    GitBranchItem? SelectedBranch);
