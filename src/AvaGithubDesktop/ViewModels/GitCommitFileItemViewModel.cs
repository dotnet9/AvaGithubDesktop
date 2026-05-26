using System.Reactive;
using AvaGithubDesktop.Core.Models;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class GitCommitFileItemViewModel : ReactiveObject
{
    private readonly Func<GitCommitFileItemViewModel, Task> _copyFullPathAsync;
    private readonly Func<GitCommitFileItemViewModel, Task> _copyRelativePathAsync;
    private readonly Func<GitCommitFileItemViewModel, Task> _showInFileManagerAsync;
    private readonly Func<GitCommitFileItemViewModel, Task> _openInExternalEditorAsync;

    public GitCommitFileItemViewModel(
        GitCommitFileItem file,
        Func<GitCommitFileItemViewModel, Task> copyFullPathAsync,
        Func<GitCommitFileItemViewModel, Task> copyRelativePathAsync,
        Func<GitCommitFileItemViewModel, Task> showInFileManagerAsync,
        Func<GitCommitFileItemViewModel, Task> openInExternalEditorAsync)
    {
        File = file;
        _copyFullPathAsync = copyFullPathAsync;
        _copyRelativePathAsync = copyRelativePathAsync;
        _showInFileManagerAsync = showInFileManagerAsync;
        _openInExternalEditorAsync = openInExternalEditorAsync;
        CopyFullPathCommand = ReactiveCommand.CreateFromTask(CopyFullPathAsync);
        CopyRelativePathCommand = ReactiveCommand.CreateFromTask(CopyRelativePathAsync);
        ShowInFileManagerCommand = ReactiveCommand.CreateFromTask(ShowInFileManagerAsync);
        OpenInExternalEditorCommand = ReactiveCommand.CreateFromTask(OpenInExternalEditorAsync);
    }

    public GitCommitFileItem File { get; }

    public string StatusCode => File.StatusCode;

    public string Path => File.Path;

    public string GitPath => File.GitPath;

    public string DisplayStatus => File.DisplayStatus;

    public string StatusBackground => File.StatusBackground;

    public string StatusForeground => File.StatusForeground;

    public bool CanOpenFileLocation => !string.Equals(DisplayStatus, "Deleted", StringComparison.Ordinal);

    public ReactiveCommand<Unit, Unit> CopyFullPathCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyRelativePathCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowInFileManagerCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenInExternalEditorCommand { get; }

    private Task CopyFullPathAsync()
    {
        return _copyFullPathAsync(this);
    }

    private Task CopyRelativePathAsync()
    {
        return _copyRelativePathAsync(this);
    }

    private Task ShowInFileManagerAsync()
    {
        return _showInFileManagerAsync(this);
    }

    private Task OpenInExternalEditorAsync()
    {
        return _openInExternalEditorAsync(this);
    }
}
