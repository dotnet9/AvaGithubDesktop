using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.ViewModels;

public sealed class CommitFilesViewStateBuilder
{
    public CommitFilesViewState Build(
        GitCommitItem? commit,
        string? preferredSelectedPath,
        Func<GitCommitFileItemViewModel, Task> copyFullPathAsync,
        Func<GitCommitFileItemViewModel, Task> copyRelativePathAsync,
        Func<GitCommitFileItemViewModel, Task> showInFileManagerAsync,
        Func<GitCommitFileItemViewModel, Task> openInExternalEditorAsync)
    {
        if (commit is null)
        {
            return new CommitFilesViewState([], null);
        }

        var files = commit.Files
            .Select(file => new GitCommitFileItemViewModel(
                file,
                copyFullPathAsync,
                copyRelativePathAsync,
                showInFileManagerAsync,
                openInExternalEditorAsync))
            .ToArray();

        var selectedFile = !string.IsNullOrWhiteSpace(preferredSelectedPath)
            ? files.FirstOrDefault(file => file.Path == preferredSelectedPath) ?? files.FirstOrDefault()
            : files.FirstOrDefault();

        return new CommitFilesViewState(files, selectedFile);
    }
}
