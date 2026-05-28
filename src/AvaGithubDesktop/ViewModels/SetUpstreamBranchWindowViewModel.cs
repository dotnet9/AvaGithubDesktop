using System.Collections.ObjectModel;
using System.Reactive;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class SetUpstreamBranchWindowViewModel : ViewModelBase
{
    private readonly IAppLocalizer _localizer;
    private readonly IReadOnlyList<GitBranchItem> _availableBranches;
    private string _branchFilterText = string.Empty;
    private GitBranchItem? _selectedBranch;

    public SetUpstreamBranchWindowViewModel(
        string currentBranch,
        IReadOnlyList<GitBranchItem> remoteBranches,
        IAppLocalizer localizer)
    {
        _localizer = localizer;
        CurrentBranch = string.IsNullOrWhiteSpace(currentBranch) ? "-" : currentBranch;
        _availableBranches = remoteBranches
            .Where(branch => !string.IsNullOrWhiteSpace(branch.Name) && !branch.Name.EndsWith("/HEAD", StringComparison.Ordinal))
            .ToArray();

        CancelCommand = ReactiveCommand.Create(() => RequestClose(null));
        SetUpstreamCommand = ReactiveCommand.Create(
            SetUpstream,
            this.WhenAnyValue(model => model.CanSetUpstream));

        ApplyFilter(selectPreferred: true);
    }

    public event EventHandler<DialogCloseRequestedEventArgs<BranchUpstreamRequest?>>? CloseRequested;

    public ObservableCollection<GitBranchItem> FilteredBranches { get; } = new();

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> SetUpstreamCommand { get; }

    public string Title => _localizer.Get(AvaGithubDesktopL.SetUpstreamTitle);

    public string CurrentBranch { get; }

    public string Description => _localizer.Format(AvaGithubDesktopL.SetUpstreamDescriptionFormat, CurrentBranch);

    public string SetUpstreamButtonText => _localizer.Get(AvaGithubDesktopL.SetUpstreamButton);

    public string BranchFilterText
    {
        get => _branchFilterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _branchFilterText, value);
            ApplyFilter(selectPreferred: false);
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

    public bool CanSetUpstream => SelectedBranch is not null;

    private void SetUpstream()
    {
        if (SelectedBranch is null)
        {
            return;
        }

        RequestClose(new BranchUpstreamRequest(SelectedBranch.Name));
    }

    private void ApplyFilter(bool selectPreferred)
    {
        var result = BranchDialogFilter.Build(
            _availableBranches,
            BranchFilterText,
            SelectedBranch?.Name,
            branches => selectPreferred
                ? branches.FirstOrDefault(branch => IsPreferredUpstream(branch.Name, CurrentBranch))
                  ?? branches.FirstOrDefault()
                : branches.FirstOrDefault(),
            matchUpstream: false);

        FilteredBranches.Clear();
        foreach (var branch in result.Branches)
        {
            FilteredBranches.Add(branch);
        }

        SelectedBranch = result.SelectedBranch;
        this.RaisePropertyChanged(nameof(HasNoFilteredBranches));
    }

    private static bool IsPreferredUpstream(string branchName, string currentBranch)
    {
        return !string.IsNullOrWhiteSpace(currentBranch)
               && currentBranch != "-"
               && branchName.EndsWith($"/{currentBranch}", StringComparison.Ordinal);
    }

    private void RequestClose(BranchUpstreamRequest? request)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<BranchUpstreamRequest?>(request));
    }

    private void RaiseSelectionStateChanged()
    {
        this.RaisePropertyChanged(nameof(CanSetUpstream));
    }
}
