namespace AvaGithubDesktop.Core.Models;

public sealed record RepositoryCloneRequest(
    string SourceUrl,
    string DestinationPath);
