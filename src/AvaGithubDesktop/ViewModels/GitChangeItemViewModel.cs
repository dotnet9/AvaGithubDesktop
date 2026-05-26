using AvaGithubDesktop.Core.Models;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class GitChangeItemViewModel : ReactiveObject
{
    private bool _isIncluded;

    public GitChangeItemViewModel(GitChangeItem change)
    {
        Change = change;
        _isIncluded = true;
    }

    public GitChangeItem Change { get; }

    public string StatusCode => Change.StatusCode;

    public string Path => Change.Path;

    public IReadOnlyList<string> GitPaths => Change.GitPaths;

    public GitChangeKind Kind => Change.Kind;

    public string DisplayStatus => Change.DisplayStatus;

    public string StatusBackground => Change.StatusBackground;

    public string StatusForeground => Change.StatusForeground;

    public bool IsIncluded
    {
        get => _isIncluded;
        set => this.RaiseAndSetIfChanged(ref _isIncluded, value);
    }
}
