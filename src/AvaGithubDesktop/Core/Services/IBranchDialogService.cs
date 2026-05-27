using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IBranchDialogService
{
    Task<BranchCreationRequest?> ShowCreateBranchDialogAsync(
        string currentBranch,
        IReadOnlyList<GitBranchItem> branches,
        string initialName);

    Task<BranchRenameRequest?> ShowRenameBranchDialogAsync(
        string branchName,
        IReadOnlyList<GitBranchItem> branches);

    Task<BranchMergeRequest?> ShowMergeBranchDialogAsync(
        string currentBranch,
        IReadOnlyList<GitBranchItem> branches);

    Task<bool> ShowDeleteBranchConfirmationAsync(string branchName);
}
