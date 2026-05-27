using System.Reactive;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class RenameBranchWindowViewModel : ViewModelBase
{
    private readonly HashSet<string> _branchNames;
    private readonly IAppLocalizer _localizer;
    private string _branchName;

    public RenameBranchWindowViewModel(
        string oldBranchName,
        IReadOnlyList<GitBranchItem> branches,
        IAppLocalizer localizer)
    {
        _localizer = localizer;
        OldBranchName = oldBranchName;
        _branchName = oldBranchName;
        _branchNames = branches
            .Select(branch => branch.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);

        CancelCommand = ReactiveCommand.Create(() => RequestClose(null));
        RenameCommand = ReactiveCommand.Create(
            RenameBranch,
            this.WhenAnyValue(model => model.CanRenameBranch));
    }

    public event EventHandler<DialogCloseRequestedEventArgs<BranchRenameRequest?>>? CloseRequested;

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> RenameCommand { get; }

    public string Title => _localizer.Get(AvaGithubDesktopL.RenameBranchTitle);

    public string OldBranchName { get; }

    public string BranchName
    {
        get => _branchName;
        set
        {
            this.RaiseAndSetIfChanged(ref _branchName, value);
            RaiseBranchNameStateChanged();
        }
    }

    public string RenameBranchDescription =>
        _localizer.Format(AvaGithubDesktopL.RenameBranchDescriptionFormat, OldBranchName);

    public string BranchNameError
    {
        get
        {
            if (string.Equals(BranchName.Trim(), OldBranchName, StringComparison.Ordinal))
            {
                return _localizer.Get(AvaGithubDesktopL.BranchNameUnchanged);
            }

            return BranchNameValidator.GetValidationError(
                BranchName,
                _branchNames,
                OldBranchName,
                _localizer);
        }
    }

    public bool HasBranchNameError => !string.IsNullOrWhiteSpace(BranchNameError);

    public bool CanRenameBranch => !HasBranchNameError;

    private void RenameBranch()
    {
        RequestClose(new BranchRenameRequest(OldBranchName, BranchName.Trim()));
    }

    private void RequestClose(BranchRenameRequest? request)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<BranchRenameRequest?>(request));
    }

    private void RaiseBranchNameStateChanged()
    {
        this.RaisePropertyChanged(nameof(BranchNameError));
        this.RaisePropertyChanged(nameof(HasBranchNameError));
        this.RaisePropertyChanged(nameof(CanRenameBranch));
    }
}
