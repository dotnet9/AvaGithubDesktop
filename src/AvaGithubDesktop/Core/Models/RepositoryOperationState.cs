namespace AvaGithubDesktop.Core.Models;

public enum RepositoryOperationState
{
    None,
    Merge,
    Rebase,
    Revert,
    CherryPick
}
