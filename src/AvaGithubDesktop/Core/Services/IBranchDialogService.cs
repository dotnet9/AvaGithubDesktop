using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IBranchDialogService
{
    Task<BranchCreationRequest?> ShowCreateBranchDialogAsync(
        string currentBranch,
        IReadOnlyList<GitBranchItem> branches,
        string initialName);
}
