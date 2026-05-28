using System.Reactive;
using AvaGithubDesktop.Core.Models;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class GitChangeItemViewModel : ReactiveObject
{
    private readonly Func<GitChangeItemViewModel, Task> _copyFullPathAsync;
    private readonly Func<GitChangeItemViewModel, Task> _copyRelativePathAsync;
    private readonly Func<GitChangeItemViewModel, Task> _showInFileManagerAsync;
    private readonly Func<GitChangeItemViewModel, Task> _openInExternalEditorAsync;
    private readonly Func<GitChangeItemViewModel, Task> _discardChangesAsync;
    private readonly Func<GitChangeItemViewModel, Task> _markResolvedAsync;
    private bool _isIncluded;

    public GitChangeItemViewModel(
        GitChangeItem change,
        Func<GitChangeItemViewModel, Task> copyFullPathAsync,
        Func<GitChangeItemViewModel, Task> copyRelativePathAsync,
        Func<GitChangeItemViewModel, Task> showInFileManagerAsync,
        Func<GitChangeItemViewModel, Task> openInExternalEditorAsync,
        Func<GitChangeItemViewModel, Task> discardChangesAsync,
        Func<GitChangeItemViewModel, Task> markResolvedAsync)
    {
        Change = change;
        _copyFullPathAsync = copyFullPathAsync;
        _copyRelativePathAsync = copyRelativePathAsync;
        _showInFileManagerAsync = showInFileManagerAsync;
        _openInExternalEditorAsync = openInExternalEditorAsync;
        _discardChangesAsync = discardChangesAsync;
        _markResolvedAsync = markResolvedAsync;
        _isIncluded = true;
        CopyFullPathCommand = ReactiveCommand.CreateFromTask(CopyFullPathAsync);
        CopyRelativePathCommand = ReactiveCommand.CreateFromTask(CopyRelativePathAsync);
        ShowInFileManagerCommand = ReactiveCommand.CreateFromTask(ShowInFileManagerAsync);
        OpenInExternalEditorCommand = ReactiveCommand.CreateFromTask(OpenInExternalEditorAsync);
        DiscardChangesCommand = ReactiveCommand.CreateFromTask(DiscardChangesAsync);
        MarkResolvedCommand = ReactiveCommand.CreateFromTask(MarkResolvedAsync);
    }

    public GitChangeItem Change { get; }

    public string StatusCode => Change.StatusCode;

    public string Path => Change.Path;

    public IReadOnlyList<string> GitPaths => Change.GitPaths;

    public GitChangeKind Kind => Change.Kind;

    public bool IsConflict => Change.IsConflict;

    public string DisplayStatus => Change.DisplayStatus;

    public bool IsStatusAddedTone => !IsConflict && Kind == GitChangeKind.Staged;

    public bool IsStatusWarningTone => !IsConflict && Kind == GitChangeKind.Untracked;

    public bool IsStatusDangerTone => IsConflict;

    public bool CanOpenInExternalEditor => !string.Equals(StatusCode, "D", StringComparison.Ordinal);

    public ReactiveCommand<Unit, Unit> CopyFullPathCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyRelativePathCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowInFileManagerCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenInExternalEditorCommand { get; }

    public ReactiveCommand<Unit, Unit> DiscardChangesCommand { get; }

    public ReactiveCommand<Unit, Unit> MarkResolvedCommand { get; }

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

    private Task OpenInExternalEditorAsync()
    {
        return _openInExternalEditorAsync(this);
    }

    private Task DiscardChangesAsync()
    {
        return _discardChangesAsync(this);
    }

    private Task MarkResolvedAsync()
    {
        return _markResolvedAsync(this);
    }
}
