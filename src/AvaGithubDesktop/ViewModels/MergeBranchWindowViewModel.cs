using System.Collections.ObjectModel;
using System.Reactive;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class MergeBranchWindowViewModel : ViewModelBase
{
    private readonly IAppLocalizer _localizer;
    private readonly IReadOnlyList<GitBranchItem> _availableBranches;
    private string _branchFilterText = string.Empty;
    private GitBranchItem? _selectedBranch;

    public MergeBranchWindowViewModel(
        string currentBranch,
        IReadOnlyList<GitBranchItem> branches,
        IAppLocalizer localizer)
    {
        _localizer = localizer;
        CurrentBranch = string.IsNullOrWhiteSpace(currentBranch) ? "-" : currentBranch;
        _availableBranches = branches
            .Where(branch => !branch.IsCurrent && !string.Equals(branch.Name, CurrentBranch, StringComparison.Ordinal))
            .ToArray();

        CancelCommand = ReactiveCommand.Create(() => RequestClose(null));
        MergeCommand = ReactiveCommand.Create(
            MergeBranch,
            this.WhenAnyValue(model => model.CanMergeBranch));

        ApplyFilter(selectFirst: true);
    }

    public event EventHandler<DialogCloseRequestedEventArgs<BranchMergeRequest?>>? CloseRequested;

    public ObservableCollection<GitBranchItem> FilteredBranches { get; } = new();

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> MergeCommand { get; }

    public string Title => _localizer.Get(AvaGithubDesktopL.MergeBranchTitle);

    public string CurrentBranch { get; }

    public string Description => _localizer.Format(AvaGithubDesktopL.MergeBranchDescriptionFormat, CurrentBranch);

    public string MergeButtonText => _localizer.Format(AvaGithubDesktopL.MergeBranchButtonFormat, CurrentBranch);

    public string BranchFilterText
    {
        get => _branchFilterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _branchFilterText, value);
            ApplyFilter(selectFirst: false);
        }
    }

    public GitBranchItem? SelectedBranch
    {
        get => _selectedBranch;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedBranch, value);
            RaiseSelectionStateChanged();
        }
    }

    public bool HasNoFilteredBranches => FilteredBranches.Count == 0;

    public bool CanMergeBranch => SelectedBranch is not null;

    private void MergeBranch()
    {
        if (SelectedBranch is null)
        {
            return;
        }

        RequestClose(new BranchMergeRequest(SelectedBranch.Name));
    }

    private void ApplyFilter(bool selectFirst)
    {
        var selectedName = SelectedBranch?.Name;
        var filter = BranchFilterText.Trim();
        var filtered = _availableBranches
            .Where(branch => MatchesFilter(branch, filter))
            .ToArray();

        FilteredBranches.Clear();
        foreach (var branch in filtered)
        {
            FilteredBranches.Add(branch);
        }

        SelectedBranch = selectFirst
            ? FilteredBranches.FirstOrDefault()
            : FilteredBranches.FirstOrDefault(branch => branch.Name == selectedName) ?? FilteredBranches.FirstOrDefault();
        this.RaisePropertyChanged(nameof(HasNoFilteredBranches));
    }

    private static bool MatchesFilter(GitBranchItem branch, string filter)
    {
        return string.IsNullOrWhiteSpace(filter)
               || branch.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || branch.Upstream.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || branch.RelativeDate.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void RequestClose(BranchMergeRequest? request)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<BranchMergeRequest?>(request));
    }

    private void RaiseSelectionStateChanged()
    {
        this.RaisePropertyChanged(nameof(CanMergeBranch));
    }
}
