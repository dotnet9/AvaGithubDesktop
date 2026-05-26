namespace AvaGithubDesktop.Core.Models;

public sealed record RepositoryHistoryEntry(
    string Name,
    string Path,
    string GroupName,
    DateTimeOffset LastOpenedAt,
    string? RemoteUrl);
