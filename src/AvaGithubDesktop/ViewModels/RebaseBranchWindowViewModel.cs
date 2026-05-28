using System.Collections.ObjectModel;
using System.Reactive;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class RebaseBranchWindowViewModel : ViewModelBase
{
    private readonly IAppLocalizer _localizer;
    private readonly IReadOnlyList<GitBranchItem> _availableBranches;
    private string _branchFilterText = string.Empty;
    private GitBranchItem? _selectedBranch;

    public RebaseBranchWindowViewModel(
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
        RebaseCommand = ReactiveCommand.Create(
            RebaseBranch,
            this.WhenAnyValue(model => model.CanRebaseBranch));

        ApplyFilter(selectFirst: true);
    }

    public event EventHandler<DialogCloseRequestedEventArgs<BranchRebaseRequest?>>? CloseRequested;

    public ObservableCollection<GitBranchItem> FilteredBranches { get; } = new();

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> RebaseCommand { get; }

    public string Title => _localizer.Format(AvaGithubDesktopL.RebaseBranchTitleFormat, CurrentBranch);

    public string CurrentBranch { get; }

    public string Description => _localizer.Format(AvaGithubDesktopL.RebaseBranchDescriptionFormat, CurrentBranch);

    public string RebaseButtonText => _localizer.Get(AvaGithubDesktopL.RebaseBranchButton);

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

    public bool CanRebaseBranch => SelectedBranch is not null;

    private void RebaseBranch()
    {
        if (SelectedBranch is null)
        {
            return;
        }

        RequestClose(new BranchRebaseRequest(SelectedBranch.Name));
    }

    private void ApplyFilter(bool selectFirst)
    {
        var result = BranchDialogFilter.Build(
            _availableBranches,
            BranchFilterText,
            SelectedBranch?.Name,
            branches => branches.FirstOrDefault(),
            matchUpstream: true);

        FilteredBranches.Clear();
        foreach (var branch in result.Branches)
        {
            FilteredBranches.Add(branch);
        }

        SelectedBranch = selectFirst
            ? FilteredBranches.FirstOrDefault()
            : result.SelectedBranch;
        this.RaisePropertyChanged(nameof(HasNoFilteredBranches));
    }

    private void RequestClose(BranchRebaseRequest? request)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<BranchRebaseRequest?>(request));
    }

    private void RaiseSelectionStateChanged()
    {
        this.RaisePropertyChanged(nameof(CanRebaseBranch));
    }
}
