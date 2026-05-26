using System.Reactive;
using AvaGithubDesktop.Core.Models;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class GitChangeItemViewModel : ReactiveObject
{
    private readonly Func<GitChangeItemViewModel, Task> _copyRelativePathAsync;
    private readonly Func<GitChangeItemViewModel, Task> _showInFileManagerAsync;
    private bool _isIncluded;

    public GitChangeItemViewModel(
        GitChangeItem change,
        Func<GitChangeItemViewModel, Task> copyRelativePathAsync,
        Func<GitChangeItemViewModel, Task> showInFileManagerAsync)
    {
        Change = change;
        _copyRelativePathAsync = copyRelativePathAsync;
        _showInFileManagerAsync = showInFileManagerAsync;
        _isIncluded = true;
        CopyRelativePathCommand = ReactiveCommand.CreateFromTask(CopyRelativePathAsync);
        ShowInFileManagerCommand = ReactiveCommand.CreateFromTask(ShowInFileManagerAsync);
    }

    public GitChangeItem Change { get; }

    public string StatusCode => Change.StatusCode;

    public string Path => Change.Path;

    public IReadOnlyList<string> GitPaths => Change.GitPaths;

    public GitChangeKind Kind => Change.Kind;

    public string DisplayStatus => Change.DisplayStatus;

    public string StatusBackground => Change.StatusBackground;

    public string StatusForeground => Change.StatusForeground;

    public ReactiveCommand<Unit, Unit> CopyRelativePathCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowInFileManagerCommand { get; }

    public bool IsIncluded
    {
        get => _isIncluded;
        set => this.RaiseAndSetIfChanged(ref _isIncluded, value);
    }

    private Task CopyRelativePathAsync()
    {
        return _copyRelativePathAsync(this);
    }

    private Task ShowInFileManagerAsync()
    {
        return _showInFileManagerAsync(this);
    }
}
