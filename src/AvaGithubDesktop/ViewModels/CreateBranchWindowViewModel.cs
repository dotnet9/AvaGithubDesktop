using System.Reactive;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class CreateBranchWindowViewModel : ViewModelBase
{
    private readonly HashSet<string> _branchNames;
    private readonly IAppLocalizer _localizer;
    private string _branchName;

    public CreateBranchWindowViewModel(
        string currentBranch,
        IReadOnlyList<GitBranchItem> branches,
        string initialName,
        IAppLocalizer localizer)
    {
        _localizer = localizer;
        CurrentBranch = string.IsNullOrWhiteSpace(currentBranch) ? "-" : currentBranch;
        _branchNames = branches
            .Select(branch => branch.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);
        _branchName = NormalizeInitialName(initialName);

        CancelCommand = ReactiveCommand.Create(() => RequestClose(null));
        CreateCommand = ReactiveCommand.Create(
            CreateBranch,
            this.WhenAnyValue(model => model.CanCreateBranch));
    }

    public event EventHandler<DialogCloseRequestedEventArgs<BranchCreationRequest?>>? CloseRequested;

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> CreateCommand { get; }

    public string Title => _localizer.Get(AvaGithubDesktopL.CreateBranchTitle);

    public string CurrentBranch { get; }

    public string BranchName
    {
        get => _branchName;
        set
        {
            this.RaiseAndSetIfChanged(ref _branchName, value);
            RaiseBranchNameStateChanged();
        }
    }

    public string BranchBaseDescription =>
        _localizer.Format(AvaGithubDesktopL.CreateBranchBaseDescriptionFormat, CurrentBranch);

    public string BranchNameError
    {
        get
        {
            var trimmedName = BranchName.Trim();
            return BranchNameValidator.GetValidationError(
                trimmedName,
                _branchNames,
                ignoredBranchName: null,
                _localizer);
        }
    }

    public bool HasBranchNameError => !string.IsNullOrWhiteSpace(BranchNameError);

    public bool CanCreateBranch => !HasBranchNameError;

    private void CreateBranch()
    {
        // Desktop 创建分支后会立即切换到新分支；这里保留相同的交互预期。
        RequestClose(new BranchCreationRequest(BranchName.Trim(), null, CheckoutBranch: true));
    }

    private void RequestClose(BranchCreationRequest? request)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<BranchCreationRequest?>(request));
    }

    private void RaiseBranchNameStateChanged()
    {
        this.RaisePropertyChanged(nameof(BranchNameError));
        this.RaisePropertyChanged(nameof(HasBranchNameError));
        this.RaisePropertyChanged(nameof(CanCreateBranch));
    }

    private static string NormalizeInitialName(string initialName)
    {
        return string.IsNullOrWhiteSpace(initialName) ? string.Empty : initialName.Trim();
    }
}
