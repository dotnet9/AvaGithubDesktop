namespace AvaGithubDesktop.Core.Models;

public sealed record AppSettings
{
    public bool? IsOperationLogVisible { get; init; }

    public string? CultureName { get; init; }

    public string? ThemeKey { get; init; }

    public string? LastRepositoryPath { get; init; }
}
