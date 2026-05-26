using System.Reactive;
using System.Reactive.Linq;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class RepositoryListItemViewModel : ViewModelBase
{
    private readonly Func<RepositoryListItemViewModel, Task> _openAsync;
    private readonly Func<RepositoryListItemViewModel, Task> _openInExternalEditorAsync;
    private readonly Func<RepositoryListItemViewModel, Task> _openInShellAsync;
    private readonly Func<RepositoryListItemViewModel, Task> _showInFileManagerAsync;
    private readonly Func<RepositoryListItemViewModel, Task> _copyNameAsync;
    private readonly Func<RepositoryListItemViewModel, Task> _copyPathAsync;
    private readonly Func<RepositoryListItemViewModel, Task> _viewOnGitHubAsync;
    private bool _isCurrent;

    public RepositoryListItemViewModel(
        RepositoryHistoryEntry entry,
        Func<RepositoryListItemViewModel, Task> openAsync,
        Func<RepositoryListItemViewModel, Task> openInExternalEditorAsync,
        Func<RepositoryListItemViewModel, Task> openInShellAsync,
        Func<RepositoryListItemViewModel, Task> showInFileManagerAsync,
        Func<RepositoryListItemViewModel, Task> copyNameAsync,
        Func<RepositoryListItemViewModel, Task> copyPathAsync,
        Func<RepositoryListItemViewModel, Task> viewOnGitHubAsync)
    {
        Entry = entry;
        _openAsync = openAsync;
        _openInExternalEditorAsync = openInExternalEditorAsync;
        _openInShellAsync = openInShellAsync;
        _showInFileManagerAsync = showInFileManagerAsync;
        _copyNameAsync = copyNameAsync;
        _copyPathAsync = copyPathAsync;
        _viewOnGitHubAsync = viewOnGitHubAsync;
        OpenCommand = ReactiveCommand.CreateFromTask(OpenAsync);
        OpenInExternalEditorCommand = ReactiveCommand.CreateFromTask(OpenInExternalEditorAsync);
        OpenInShellCommand = ReactiveCommand.CreateFromTask(OpenInShellAsync);
        ShowInFileManagerCommand = ReactiveCommand.CreateFromTask(ShowInFileManagerAsync);
        CopyNameCommand = ReactiveCommand.CreateFromTask(CopyNameAsync);
        CopyPathCommand = ReactiveCommand.CreateFromTask(CopyPathAsync);
        ViewOnGitHubCommand = ReactiveCommand.CreateFromTask(ViewOnGitHubAsync, Observable.Return(CanViewOnGitHub));
    }

    public RepositoryHistoryEntry Entry { get; }

    public string Name => Entry.Name;

    public string Path => Entry.Path;

    public string GroupName => Entry.GroupName;

    public DateTimeOffset LastOpenedAt => Entry.LastOpenedAt;

    public bool CanViewOnGitHub => RepositoryRemoteUrlHelper.TryGetGitHubWebUrl(Entry.RemoteUrl, out _);

    public bool IsCurrent
    {
        get => _isCurrent;
        set => this.RaiseAndSetIfChanged(ref _isCurrent, value);
    }

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenInExternalEditorCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenInShellCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowInFileManagerCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyNameCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyPathCommand { get; }

    public ReactiveCommand<Unit, Unit> ViewOnGitHubCommand { get; }

    private Task OpenAsync()
    {
        return _openAsync(this);
    }

    private Task OpenInExternalEditorAsync()
    {
        return _openInExternalEditorAsync(this);
    }

    private Task OpenInShellAsync()
    {
        return _openInShellAsync(this);
    }

    private Task ShowInFileManagerAsync()
    {
        return _showInFileManagerAsync(this);
    }

    private Task CopyNameAsync()
    {
        return _copyNameAsync(this);
    }

    private Task CopyPathAsync()
    {
        return _copyPathAsync(this);
    }

    private Task ViewOnGitHubAsync()
    {
        return _viewOnGitHubAsync(this);
    }
}
