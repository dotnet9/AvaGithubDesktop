using System.Reactive;
using AvaGithubDesktop.Core.Models;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class RepositoryListItemViewModel : ViewModelBase
{
    private readonly Func<RepositoryListItemViewModel, Task> _openAsync;
    private bool _isCurrent;

    public RepositoryListItemViewModel(
        RepositoryHistoryEntry entry,
        Func<RepositoryListItemViewModel, Task> openAsync)
    {
        Entry = entry;
        _openAsync = openAsync;
        OpenCommand = ReactiveCommand.CreateFromTask(OpenAsync);
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

    private Task OpenAsync()
    {
        return _openAsync(this);
    }
}
