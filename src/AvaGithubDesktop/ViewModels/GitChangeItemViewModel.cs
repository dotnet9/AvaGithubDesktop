using System.Reactive;
using AvaGithubDesktop.Core.Models;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class GitChangeItemViewModel : ReactiveObject
{
    private readonly Func<GitChangeItemViewModel, Task> _copyFullPathAsync;
    private readonly Func<GitChangeItemViewModel, Task> _copyRelativePathAsync;
    private readonly Func<GitChangeItemViewModel, Task> _showInFileManagerAsync;
    private readonly Func<GitChangeItemViewModel, Task> _discardChangesAsync;
    private bool _isIncluded;

    public GitChangeItemViewModel(
        GitChangeItem change,
        Func<GitChangeItemViewModel, Task> copyFullPathAsync,
        Func<GitChangeItemViewModel, Task> copyRelativePathAsync,
        Func<GitChangeItemViewModel, Task> showInFileManagerAsync,
        Func<GitChangeItemViewModel, Task> discardChangesAsync)
    {
        Change = change;
        _copyFullPathAsync = copyFullPathAsync;
        _copyRelativePathAsync = copyRelativePathAsync;
        _showInFileManagerAsync = showInFileManagerAsync;
        _discardChangesAsync = discardChangesAsync;
        _isIncluded = true;
        CopyFullPathCommand = ReactiveCommand.CreateFromTask(CopyFullPathAsync);
        CopyRelativePathCommand = ReactiveCommand.CreateFromTask(CopyRelativePathAsync);
        ShowInFileManagerCommand = ReactiveCommand.CreateFromTask(ShowInFileManagerAsync);
        DiscardChangesCommand = ReactiveCommand.CreateFromTask(DiscardChangesAsync);
    }

    public GitChangeItem Change { get; }

    public string StatusCode => Change.StatusCode;

    public string Path => Change.Path;

    public IReadOnlyList<string> GitPaths => Change.GitPaths;

    public GitChangeKind Kind => Change.Kind;

    public string DisplayStatus => Change.DisplayStatus;

    public string StatusBackground => Change.StatusBackground;

    public string StatusForeground => Change.StatusForeground;

    public ReactiveCommand<Unit, Unit> CopyFullPathCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyRelativePathCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowInFileManagerCommand { get; }

    public ReactiveCommand<Unit, Unit> DiscardChangesCommand { get; }

    public bool IsIncluded
    {
        get => _isIncluded;
        set => this.RaiseAndSetIfChanged(ref _isIncluded, value);
    }

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

    private Task DiscardChangesAsync()
    {
        return _discardChangesAsync(this);
    }
}
