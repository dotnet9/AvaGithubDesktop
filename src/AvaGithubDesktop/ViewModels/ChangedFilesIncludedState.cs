namespace AvaGithubDesktop.ViewModels;

public sealed record ChangedFilesIncludedState(
    int IncludedCount,
    bool? AreAllIncluded);
