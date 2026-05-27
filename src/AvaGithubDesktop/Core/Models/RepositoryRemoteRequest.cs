namespace AvaGithubDesktop.Core.Models;

public sealed record RepositoryRemoteRequest(
    string RemoteName,
    string RemoteUrl,
    RepositoryRemoteAction Action);

public enum RepositoryRemoteAction
{
    Save,
    Remove
}
