namespace AvaGithubDesktop.Core.Models;

public sealed record AppSettings
{
    public bool? IsOperationLogVisible { get; init; }

    public bool? HideWhitespaceChanges { get; init; }

    public bool? ShowSideBySideDiff { get; init; }

    public string? CultureName { get; init; }

    public string? ThemeKey { get; init; }

    public string? LastRepositoryPath { get; init; }

    public double? WorkspaceSidebarWidth { get; init; }

    public double? HistoryFileListWidth { get; init; }

    public double? OperationLogHeight { get; init; }

    public double? WindowWidth { get; init; }

    public double? WindowHeight { get; init; }
}
