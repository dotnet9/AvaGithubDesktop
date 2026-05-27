using System.Reactive;
using System.Text.RegularExpressions;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed partial class CreateBranchWindowViewModel : ViewModelBase
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
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                return _localizer.Get(AvaGithubDesktopL.BranchNameRequired);
            }

            if (_branchNames.Contains(trimmedName))
            {
                return _localizer.Format(AvaGithubDesktopL.BranchNameAlreadyExistsFormat, trimmedName);
            }

            if (!IsLikelyValidBranchName(trimmedName))
            {
                return _localizer.Get(AvaGithubDesktopL.BranchNameInvalid);
            }

            return string.Empty;
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

    private static bool IsLikelyValidBranchName(string branchName)
    {
        // 这里覆盖 Git ref 常见限制，最终仍由 git check-ref-format 做权威校验。
        return !branchName.StartsWith("/", StringComparison.Ordinal)
            && !branchName.EndsWith("/", StringComparison.Ordinal)
            && !branchName.EndsWith(".", StringComparison.Ordinal)
            && !branchName.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)
            && !branchName.Contains("..", StringComparison.Ordinal)
            && !branchName.Contains("//", StringComparison.Ordinal)
            && !branchName.Contains("@{", StringComparison.Ordinal)
            && !InvalidBranchNameCharactersRegex().IsMatch(branchName);
    }

    [GeneratedRegex(@"[\000-\037\177 ~^:?*\[\\]")]
    private static partial Regex InvalidBranchNameCharactersRegex();
}
