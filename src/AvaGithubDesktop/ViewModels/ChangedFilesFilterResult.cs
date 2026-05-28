namespace AvaGithubDesktop.ViewModels;

public sealed record ChangedFilesFilterResult(
    IReadOnlyList<GitChangeItemViewModel> Changes,
    GitChangeItemViewModel? SelectedChange);
