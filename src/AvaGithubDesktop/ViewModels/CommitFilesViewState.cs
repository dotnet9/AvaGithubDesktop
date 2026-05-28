namespace AvaGithubDesktop.ViewModels;

public sealed record CommitFilesViewState(
    IReadOnlyList<GitCommitFileItemViewModel> Files,
    GitCommitFileItemViewModel? SelectedFile);
