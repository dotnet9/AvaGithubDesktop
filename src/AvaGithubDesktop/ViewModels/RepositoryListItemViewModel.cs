using System.Reactive;
using AvaGithubDesktop.Core.Models;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class RepositoryListItemViewModel : ViewModelBase
{
    private readonly Func<RepositoryListItemViewModel, Task> _openAsync;
    private readonly Func<RepositoryListItemViewModel, Task> _openInShellAsync;
    private readonly Func<RepositoryListItemViewModel, Task> _showInFileManagerAsync;
    private bool _isCurrent;

    public RepositoryListItemViewModel(
        RepositoryHistoryEntry entry,
        Func<RepositoryListItemViewModel, Task> openAsync,
        Func<RepositoryListItemViewModel, Task> openInShellAsync,
        Func<RepositoryListItemViewModel, Task> showInFileManagerAsync)
    {
        Entry = entry;
        _openAsync = openAsync;
        _openInShellAsync = openInShellAsync;
        _showInFileManagerAsync = showInFileManagerAsync;
        OpenCommand = ReactiveCommand.CreateFromTask(OpenAsync);
        OpenInShellCommand = ReactiveCommand.CreateFromTask(OpenInShellAsync);
        ShowInFileManagerCommand = ReactiveCommand.CreateFromTask(ShowInFileManagerAsync);
    }

    public RepositoryHistoryEntry Entry { get; }

    public string Name => Entry.Name;

    public string Path => Entry.Path;

    public string GroupName => Entry.GroupName;

    public DateTimeOffset LastOpenedAt => Entry.LastOpenedAt;

    public bool IsCurrent
    {
        get => _isCurrent;
        set => this.RaiseAndSetIfChanged(ref _isCurrent, value);
    }

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenInShellCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowInFileManagerCommand { get; }

    private Task OpenAsync()
    {
        return _openAsync(this);
    }

    private Task OpenInShellAsync()
    {
        return _openInShellAsync(this);
    }

    private Task ShowInFileManagerAsync()
    {
        return _showInFileManagerAsync(this);
    }
}
