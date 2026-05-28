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
    private readonly bool _isSquashMerge;
    private string _branchFilterText = string.Empty;
    private GitBranchItem? _selectedBranch;

    public MergeBranchWindowViewModel(
        string currentBranch,
        IReadOnlyList<GitBranchItem> branches,
        IAppLocalizer localizer,
        bool isSquashMerge = false)
    {
        _localizer = localizer;
        _isSquashMerge = isSquashMerge;
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

    public string Title => _isSquashMerge
        ? _localizer.Get(AvaGithubDesktopL.SquashMergeBranchTitle)
        : _localizer.Get(AvaGithubDesktopL.MergeBranchTitle);

    public string CurrentBranch { get; }

    public string Description => _isSquashMerge
        ? _localizer.Format(AvaGithubDesktopL.SquashMergeBranchDescriptionFormat, CurrentBranch)
        : _localizer.Format(AvaGithubDesktopL.MergeBranchDescriptionFormat, CurrentBranch);

    public string MergeButtonText => _isSquashMerge
        ? _localizer.Format(AvaGithubDesktopL.SquashMergeBranchButtonFormat, CurrentBranch)
        : _localizer.Format(AvaGithubDesktopL.MergeBranchButtonFormat, CurrentBranch);

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

    private void RequestClose(BranchMergeRequest? request)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<BranchMergeRequest?>(request));
    }

    private void RaiseSelectionStateChanged()
    {
        this.RaisePropertyChanged(nameof(CanMergeBranch));
    }
}
