namespace AvaGithubDesktop.Core.Models;

public enum RepositorySyncOperation
{
    None,
    Fetch,
    FetchAll,
    FetchLfs,
    PullLfs,
    UpdateSubmodules,
    Pull,
    Publish,
    Push
}
