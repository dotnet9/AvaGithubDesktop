using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AvaGithubDesktop.Core.Messaging;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using CodeWF.EventBus;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private const int HistoryCommitLimit = 50;
    private readonly IGitRepositoryService _gitRepositoryService;
    private readonly IRepositoryPickerService _repositoryPickerService;
    private readonly IRepositoryHistoryService _repositoryHistoryService;
    private readonly IRepositoryShellService _repositoryShellService;
    private readonly IGitHubAccountService _gitHubAccountService;
    private readonly IAccountDialogService _accountDialogService;
    private readonly IHelpService _helpService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly IBranchDialogService _branchDialogService;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly IAppLocalizer _localizer;
    private readonly IEventBus _eventBus;
    private string _repositoryPath;
    private string _repositoryName = "-";
    private string _rootPath = "-";
    private string _currentBranch = "-";
    private string _defaultBranch = "-";
    private string _upstream = "-";
    private string _remoteName = "-";
    private string _remoteUrl = "-";
    private string _lastCommit = "-";
    private string _aheadBehindText = "-";
    private string _errorMessage = string.Empty;
    private string _commitSummary = string.Empty;
    private string _commitDescription = string.Empty;
    private string _changesFilterText = string.Empty;
    private string _branchFilterText = string.Empty;
    private string _repositoryFilterText = string.Empty;
    private bool _hasRepository;
    private bool _isLoading;
    private bool _isCommitting;
    private bool _isCheckingOutBranch;
    private bool _isCreatingBranch;
    private bool _isUpdatingBranch;
    private bool _isMergingBranch;
    private bool _isRebasingBranch;
    private bool _isSyncing;
    private bool _isStashing;
    private bool _isRestoringStash;
    private bool _isDiscardingStash;
    private bool _isDiscardingChanges;
    private bool _isSigningIn;
    private bool _isOperationLogVisible;
    private bool _isInitialized;
    private bool _isBulkUpdatingIncludedChanges;
    private int _changedFilesCount;
    private int _stagedCount;
    private int _unstagedCount;
    private int _untrackedCount;
    private int _includedChangesCount;
    private int _ahead;
    private int _behind;
    private DateTimeOffset? _lastFetchedAt;
    private bool? _areAllChangesIncluded = false;
    private LanguageOption? _selectedLanguage;
    private ThemeOption? _selectedTheme;
    private GitChangeItemViewModel? _selectedChange;
    private GitCommitItem? _selectedCommit;
    private GitCommitFileItemViewModel? _selectedCommitFile;
    private GitBranchItemViewModel? _selectedBranch;
    private GitHubAccount? _currentAccount;
    private GitStashEntry? _currentBranchStash;
    private RepositorySection _selectedSection = RepositorySection.Changes;
    private int _historyCommitCount;
    private int _diffRequestVersion;
    private bool _isDiffLoading;
    private string _diffTitle = string.Empty;
    private string _diffText = string.Empty;
    private GitFileDiffPreview _diffPreview = GitFileDiffPreview.TextDiff(string.Empty);
    private RepositorySyncOperation _activeSyncOperation = RepositorySyncOperation.None;
    private CompositeDisposable _changeSubscriptions = new();
    private IReadOnlyList<RepositoryListItemViewModel> _knownRepositories = [];

    public MainWindowViewModel(
        IGitRepositoryService gitRepositoryService,
        IRepositoryPickerService repositoryPickerService,
        IRepositoryHistoryService repositoryHistoryService,
        IRepositoryShellService repositoryShellService,
        IGitHubAccountService gitHubAccountService,
        IAccountDialogService accountDialogService,
        IHelpService helpService,
        IConfirmationDialogService confirmationDialogService,
        IBranchDialogService branchDialogService,
        IAppSettingsStore settingsStore,
        IThemeService themeService,
        IAppLocalizer localizer,
        IEventBus eventBus,
        ShellStatusViewModel statusBar)
    {
        _gitRepositoryService = gitRepositoryService;
        _repositoryPickerService = repositoryPickerService;
        _repositoryHistoryService = repositoryHistoryService;
        _repositoryShellService = repositoryShellService;
        _gitHubAccountService = gitHubAccountService;
        _accountDialogService = accountDialogService;
        _helpService = helpService;
        _confirmationDialogService = confirmationDialogService;
        _branchDialogService = branchDialogService;
        _settingsStore = settingsStore;
        _themeService = themeService;
        _localizer = localizer;
        _eventBus = eventBus;
        StatusBar = statusBar;
        _isOperationLogVisible = _settingsStore.Current.IsOperationLogVisible ?? true;
        _repositoryPath = ResolveDefaultRepositoryPath();

        Languages = new ObservableCollection<LanguageOption>
        {
            new("zh-CN", _localizer.Get(AvaGithubDesktopL.SimplifiedChinese)),
            new("en-US", _localizer.Get(AvaGithubDesktopL.English))
        };
        _selectedLanguage = Languages.FirstOrDefault(option => option.CultureName == _localizer.Culture.Name) ?? Languages[0];
        ThemeOptions = new ObservableCollection<ThemeOption>(_themeService.GetThemeOptions());
        _selectedTheme = FindTheme(_settingsStore.Current.ThemeKey)
                         ?? FindTheme("system")
                         ?? ThemeOptions.FirstOrDefault();
        if (_selectedTheme is not null)
        {
            _themeService.ApplyTheme(_selectedTheme);
        }

        var canExecuteRepositoryCommand = this.WhenAnyValue(model => model.CanRunRepositoryCommand);
        BrowseRepositoryCommand = ReactiveCommand.CreateFromTask(BrowseRepositoryAsync, canExecuteRepositoryCommand);
        OpenRepositoryCommand = ReactiveCommand.CreateFromTask(OpenRepositoryAsync, canExecuteRepositoryCommand);
        RefreshRepositoryCommand = ReactiveCommand.CreateFromTask(OpenRepositoryAsync, canExecuteRepositoryCommand);
        var canUseCurrentRepository = this.WhenAnyValue(
            model => model.HasRepository,
            model => model.CanRunRepositoryCommand,
            (hasRepository, canRunRepositoryCommand) => hasRepository && canRunRepositoryCommand);
        OpenRepositoryInShellCommand = ReactiveCommand.CreateFromTask(OpenRepositoryInShellAsync, canUseCurrentRepository);
        OpenRepositoryInExternalEditorCommand = ReactiveCommand.CreateFromTask(OpenRepositoryInExternalEditorAsync, canUseCurrentRepository);
        ShowRepositoryInFileManagerCommand = ReactiveCommand.CreateFromTask(ShowRepositoryInFileManagerAsync, canUseCurrentRepository);
        var canViewCurrentRepositoryOnGitHub = this.WhenAnyValue(
            model => model.HasRepository,
            model => model.RemoteUrl,
            model => model.CanRunRepositoryCommand,
            (hasRepository, remoteUrl, canRunRepositoryCommand) =>
                hasRepository && canRunRepositoryCommand && RepositoryRemoteUrlHelper.TryGetGitHubWebUrl(remoteUrl, out _));
        ViewRepositoryOnGitHubCommand = ReactiveCommand.CreateFromTask(ViewRepositoryOnGitHubAsync, canViewCurrentRepositoryOnGitHub);
        ShowChangesCommand = ReactiveCommand.Create(ShowChanges);
        ShowHistoryCommand = ReactiveCommand.Create(ShowHistory);
        CopySelectedCommitShaCommand = ReactiveCommand.CreateFromTask(CopySelectedCommitShaAsync, this.WhenAnyValue(model => model.CanCopySelectedCommitSha));
        ViewSelectedCommitOnGitHubCommand = ReactiveCommand.CreateFromTask(
            ViewSelectedCommitOnGitHubAsync,
            this.WhenAnyValue(model => model.CanViewSelectedCommitOnGitHub));
        ToggleOperationLogCommand = ReactiveCommand.Create(ToggleOperationLog);
        SelectThemeCommand = ReactiveCommand.Create<string?>(SelectThemeByKey);
        SelectSimplifiedChineseCommand = ReactiveCommand.Create(() => SelectLanguageByCulture("zh-CN"));
        SelectEnglishCommand = ReactiveCommand.Create(() => SelectLanguageByCulture("en-US"));

        var canSynchronize = this.WhenAnyValue(model => model.CanSynchronize);
        SynchronizeRepositoryCommand = ReactiveCommand.CreateFromTask(SynchronizeRepositoryAsync, canSynchronize);
        FetchRepositoryCommand = ReactiveCommand.CreateFromTask(FetchRepositoryAsync, canSynchronize);
        PullRepositoryCommand = ReactiveCommand.CreateFromTask(PullRepositoryAsync, canSynchronize);
        PushRepositoryCommand = ReactiveCommand.CreateFromTask(PushRepositoryAsync, canSynchronize);

        var canCommit = this.WhenAnyValue(model => model.CanCommit);
        CommitCommand = ReactiveCommand.CreateFromTask(CommitChangesAsync, canCommit);

        var canCheckoutBranch = this.WhenAnyValue(model => model.CanCheckoutBranch);
        CheckoutBranchCommand = ReactiveCommand.CreateFromTask(CheckoutSelectedBranchAsync, canCheckoutBranch);
        CreateBranchCommand = ReactiveCommand.CreateFromTask(CreateBranchAsync, this.WhenAnyValue(model => model.CanCreateBranch));
        UpdateFromDefaultBranchCommand = ReactiveCommand.CreateFromTask(
            UpdateFromDefaultBranchAsync,
            this.WhenAnyValue(model => model.CanUpdateFromDefaultBranch));
        MergeBranchCommand = ReactiveCommand.CreateFromTask(MergeBranchAsync, this.WhenAnyValue(model => model.CanMergeBranch));
        SquashMergeBranchCommand = ReactiveCommand.CreateFromTask(
            SquashMergeBranchAsync,
            this.WhenAnyValue(model => model.CanMergeBranch));
        RebaseBranchCommand = ReactiveCommand.CreateFromTask(RebaseBranchAsync, this.WhenAnyValue(model => model.CanRebaseBranch));
        CompareCurrentBranchOnGitHubCommand = ReactiveCommand.CreateFromTask(
            CompareCurrentBranchOnGitHubAsync,
            this.WhenAnyValue(
                model => model.HasRepository,
                model => model.RemoteUrl,
                model => model.Upstream,
                model => model.CanRunRepositoryCommand,
                (hasRepository, remoteUrl, upstream, canRunRepositoryCommand) =>
                    hasRepository && canRunRepositoryCommand && RepositoryRemoteUrlHelper.TryGetGitHubCompareUrl(remoteUrl, upstream, out _)));
        ViewCurrentBranchOnGitHubCommand = ReactiveCommand.CreateFromTask(
            ViewCurrentBranchOnGitHubAsync,
            this.WhenAnyValue(
                model => model.HasRepository,
                model => model.RemoteUrl,
                model => model.Upstream,
                model => model.CanRunRepositoryCommand,
                (hasRepository, remoteUrl, upstream, canRunRepositoryCommand) =>
                    hasRepository && canRunRepositoryCommand && RepositoryRemoteUrlHelper.TryGetGitHubBranchUrl(remoteUrl, upstream, out _)));
        StashAllChangesCommand = ReactiveCommand.CreateFromTask(StashAllChangesAsync, this.WhenAnyValue(model => model.CanStashChanges));
        RestoreStashCommand = ReactiveCommand.CreateFromTask(RestoreStashAsync, this.WhenAnyValue(model => model.CanRestoreStash));
        DiscardStashCommand = ReactiveCommand.CreateFromTask(DiscardStashAsync, this.WhenAnyValue(model => model.CanDiscardStash));
        DiscardAllChangesCommand = ReactiveCommand.CreateFromTask(DiscardAllChangesAsync, this.WhenAnyValue(model => model.CanDiscardChanges));
        ShowChangelogCommand = ReactiveCommand.CreateFromTask(ShowChangelogAsync);
        ShowAboutCommand = ReactiveCommand.CreateFromTask(ShowAboutAsync);
        SignInCommand = ReactiveCommand.CreateFromTask(
            SignInAsync,
            this.WhenAnyValue(model => model.IsSigningIn, isSigningIn => !isSigningIn));
        SignOutCommand = ReactiveCommand.CreateFromTask(
            SignOutAsync,
            this.WhenAnyValue(
                model => model.IsSignedIn,
                model => model.IsSigningIn,
                (isSignedIn, isSigningIn) => isSignedIn && !isSigningIn));
        OpenDiffFileInExternalEditorCommand = ReactiveCommand.CreateFromTask(
            OpenDiffFileInExternalEditorAsync,
            this.WhenAnyValue(model => model.CanOpenDiffFileInExternalEditor));

        _localizer.CultureChanged += (_, _) =>
        {
            UpdateAheadBehindText();
            RaiseLocalizedDerivedText();
            RebuildRepositoryGroups();
            this.RaisePropertyChanged(nameof(RepositorySelectorTitle));
            this.RaisePropertyChanged(nameof(RepositorySelectorDetail));
            RaiseSyncStateChanged();
            RaiseAccountStateChanged();
            QueueDiffLoad();
        };
    }

    public ObservableCollection<LanguageOption> Languages { get; }

    public ObservableCollection<ThemeOption> ThemeOptions { get; }

    public ObservableCollection<GitChangeItemViewModel> ChangedFiles { get; } = new();

    public ObservableCollection<GitChangeItemViewModel> FilteredChangedFiles { get; } = new();

    public ObservableCollection<GitCommitItem> HistoryCommits { get; } = new();

    public ObservableCollection<GitCommitFileItemViewModel> SelectedCommitFiles { get; } = new();

    public ObservableCollection<GitBranchItemViewModel> Branches { get; } = new();

    public ObservableCollection<GitBranchItemViewModel> FilteredBranches { get; } = new();

    public ObservableCollection<RepositoryListGroupViewModel> RepositoryGroups { get; } = new();

    public ShellStatusViewModel StatusBar { get; }

    public ReactiveCommand<Unit, Unit> BrowseRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenRepositoryInShellCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenRepositoryInExternalEditorCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowRepositoryInFileManagerCommand { get; }

    public ReactiveCommand<Unit, Unit> ViewRepositoryOnGitHubCommand { get; }

    public ReactiveCommand<Unit, Unit> CommitCommand { get; }

    public ReactiveCommand<Unit, Unit> SynchronizeRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> FetchRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> PullRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> PushRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowChangesCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowHistoryCommand { get; }

    public ReactiveCommand<Unit, Unit> CopySelectedCommitShaCommand { get; }

    public ReactiveCommand<Unit, Unit> ViewSelectedCommitOnGitHubCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleOperationLogCommand { get; }

    public ReactiveCommand<string?, Unit> SelectThemeCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectSimplifiedChineseCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectEnglishCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckoutBranchCommand { get; }

    public ReactiveCommand<Unit, Unit> CreateBranchCommand { get; }

    public ReactiveCommand<Unit, Unit> UpdateFromDefaultBranchCommand { get; }

    public ReactiveCommand<Unit, Unit> MergeBranchCommand { get; }

    public ReactiveCommand<Unit, Unit> SquashMergeBranchCommand { get; }

    public ReactiveCommand<Unit, Unit> RebaseBranchCommand { get; }

    public ReactiveCommand<Unit, Unit> CompareCurrentBranchOnGitHubCommand { get; }

    public ReactiveCommand<Unit, Unit> ViewCurrentBranchOnGitHubCommand { get; }

    public ReactiveCommand<Unit, Unit> StashAllChangesCommand { get; }

    public ReactiveCommand<Unit, Unit> RestoreStashCommand { get; }

    public ReactiveCommand<Unit, Unit> DiscardStashCommand { get; }

    public ReactiveCommand<Unit, Unit> DiscardAllChangesCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowChangelogCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowAboutCommand { get; }

    public ReactiveCommand<Unit, Unit> SignInCommand { get; }

    public ReactiveCommand<Unit, Unit> SignOutCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenDiffFileInExternalEditorCommand { get; }

    public string RepositoryPath
    {
        get => _repositoryPath;
        set => this.RaiseAndSetIfChanged(ref _repositoryPath, value);
    }

    public string RepositoryName
    {
        get => _repositoryName;
        private set
        {
            this.RaiseAndSetIfChanged(ref _repositoryName, value);
            this.RaisePropertyChanged(nameof(RepositorySelectorTitle));
        }
    }

    public string RootPath
    {
        get => _rootPath;
        private set
        {
            this.RaiseAndSetIfChanged(ref _rootPath, value);
            this.RaisePropertyChanged(nameof(RepositorySelectorDetail));
        }
    }

    public string RepositoryFilterText
    {
        get => _repositoryFilterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _repositoryFilterText, value);
            RebuildRepositoryGroups();
        }
    }

    public string RepositorySelectorTitle =>
        HasRepository ? RepositoryName : _localizer.Get(AvaGithubDesktopL.NoRepositoryTitle);

    public string RepositorySelectorDetail =>
        HasRepository ? RootPath : _localizer.Get(AvaGithubDesktopL.RepositorySelectorDetailFallback);

    public bool HasNoRepositoryMatches => RepositoryGroups.Count == 0;

    public string CurrentBranch
    {
        get => _currentBranch;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentBranch, value);
            this.RaisePropertyChanged(nameof(CommitButtonText));
            RaiseDefaultBranchStateChanged();
            RaiseSyncStateChanged();
        }
    }

    public string DefaultBranch
    {
        get => _defaultBranch;
        private set
        {
            this.RaiseAndSetIfChanged(ref _defaultBranch, value);
            RaiseDefaultBranchStateChanged();
        }
    }

    public string Upstream
    {
        get => _upstream;
        private set => this.RaiseAndSetIfChanged(ref _upstream, value);
    }

    public string RemoteName
    {
        get => _remoteName;
        private set
        {
            this.RaiseAndSetIfChanged(ref _remoteName, value);
            RaiseSyncStateChanged();
        }
    }

    public string RemoteUrl
    {
        get => _remoteUrl;
        private set
        {
            this.RaiseAndSetIfChanged(ref _remoteUrl, value);
            this.RaisePropertyChanged(nameof(CanViewSelectedCommitOnGitHub));
        }
    }

    public DateTimeOffset? LastFetchedAt
    {
        get => _lastFetchedAt;
        private set
        {
            this.RaiseAndSetIfChanged(ref _lastFetchedAt, value);
            RaiseSyncStateChanged();
        }
    }

    public string LastCommit
    {
        get => _lastCommit;
        private set => this.RaiseAndSetIfChanged(ref _lastCommit, value);
    }

    public string AheadBehindText
    {
        get => _aheadBehindText;
        private set => this.RaiseAndSetIfChanged(ref _aheadBehindText, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            this.RaisePropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public GitHubAccount? CurrentAccount
    {
        get => _currentAccount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentAccount, value);
            RaiseAccountStateChanged();
        }
    }

    public bool IsSignedIn => CurrentAccount is not null;

    public bool IsSignedOut => !IsSignedIn;

    public bool IsSigningIn
    {
        get => _isSigningIn;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isSigningIn, value);
            RaiseAccountStateChanged();
        }
    }

    public string AccountButtonText =>
        CurrentAccount is null
            ? _localizer.Get(AvaGithubDesktopL.SignInToGitHub)
            : $"@{CurrentAccount.Login}";

    public string AccountNameText =>
        CurrentAccount?.FriendlyName ?? _localizer.Get(AvaGithubDesktopL.NotSignedIn);

    public string AccountEndpointText =>
        CurrentAccount?.FriendlyEndpoint ?? _localizer.Get(AvaGithubDesktopL.NotSignedIn);

    public string AccountInitials => CurrentAccount?.Initials ?? "GH";

    public bool HasRepository
    {
        get => _hasRepository;
        private set
        {
            this.RaiseAndSetIfChanged(ref _hasRepository, value);
            this.RaisePropertyChanged(nameof(ShowEmptyRepository));
            this.RaisePropertyChanged(nameof(RepositorySelectorTitle));
            this.RaisePropertyChanged(nameof(RepositorySelectorDetail));
            UpdateCurrentRepositoryIndicators();
            RaiseOperationStateChanged();
        }
    }

    public bool ShowEmptyRepository => !HasRepository && !IsLoading;

    public bool CanRunRepositoryCommand =>
        !IsLoading &&
        !IsCommitting &&
        !IsCheckingOutBranch &&
        !IsCreatingBranch &&
        !IsUpdatingBranch &&
        !IsMergingBranch &&
        !IsRebasingBranch &&
        !IsSyncing &&
        !IsStashing &&
        !IsRestoringStash &&
        !IsDiscardingStash &&
        !IsDiscardingChanges;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isLoading, value);
            this.RaisePropertyChanged(nameof(ShowEmptyRepository));
            this.RaisePropertyChanged(nameof(HasNoChanges));
            RaiseOperationStateChanged();
        }
    }

    public bool IsCommitting
    {
        get => _isCommitting;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isCommitting, value);
            RaiseOperationStateChanged();
        }
    }

    public bool IsCheckingOutBranch
    {
        get => _isCheckingOutBranch;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isCheckingOutBranch, value);
            RaiseOperationStateChanged();
        }
    }

    public bool IsCreatingBranch
    {
        get => _isCreatingBranch;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isCreatingBranch, value);
            RaiseOperationStateChanged();
        }
    }

    public bool IsUpdatingBranch
    {
        get => _isUpdatingBranch;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isUpdatingBranch, value);
            RaiseOperationStateChanged();
        }
    }

    public bool IsMergingBranch
    {
        get => _isMergingBranch;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isMergingBranch, value);
            RaiseOperationStateChanged();
        }
    }

    public bool IsRebasingBranch
    {
        get => _isRebasingBranch;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isRebasingBranch, value);
            RaiseOperationStateChanged();
        }
    }

    public bool IsSyncing
    {
        get => _isSyncing;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isSyncing, value);
            RaiseOperationStateChanged();
        }
    }

    public bool IsStashing
    {
        get => _isStashing;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isStashing, value);
            RaiseOperationStateChanged();
        }
    }

    public bool IsRestoringStash
    {
        get => _isRestoringStash;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isRestoringStash, value);
            RaiseOperationStateChanged();
        }
    }

    public bool IsDiscardingStash
    {
        get => _isDiscardingStash;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isDiscardingStash, value);
            RaiseOperationStateChanged();
        }
    }

    public bool IsDiscardingChanges
    {
        get => _isDiscardingChanges;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isDiscardingChanges, value);
            RaiseOperationStateChanged();
        }
    }

    public int ChangedFilesCount
    {
        get => _changedFilesCount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _changedFilesCount, value);
            this.RaisePropertyChanged(nameof(HasChanges));
            this.RaisePropertyChanged(nameof(HasNoChanges));
            this.RaisePropertyChanged(nameof(ChangedFilesHeaderText));
            RaiseStashStateChanged();
        }
    }

    public int StagedCount
    {
        get => _stagedCount;
        private set => this.RaiseAndSetIfChanged(ref _stagedCount, value);
    }

    public int UnstagedCount
    {
        get => _unstagedCount;
        private set => this.RaiseAndSetIfChanged(ref _unstagedCount, value);
    }

    public int UntrackedCount
    {
        get => _untrackedCount;
        private set => this.RaiseAndSetIfChanged(ref _untrackedCount, value);
    }

    public bool HasChanges => ChangedFilesCount > 0;

    public bool HasNoChanges => HasRepository && !IsLoading && ChangedFilesCount == 0;

    public RepositorySection SelectedSection
    {
        get => _selectedSection;
        private set
        {
            this.RaiseAndSetIfChanged(ref _selectedSection, value);
            RaiseSectionStateChanged();
        }
    }

    public bool IsChangesSelected => SelectedSection == RepositorySection.Changes;

    public bool IsHistorySelected => SelectedSection == RepositorySection.History;

    public string ChangesTabBackground => "#FFFFFF";

    public string HistoryTabBackground => "#FFFFFF";

    public string ChangesTabForeground => IsChangesSelected ? "#24292F" : "#57606A";

    public string HistoryTabForeground => IsHistorySelected ? "#24292F" : "#57606A";

    public int HistoryCommitCount
    {
        get => _historyCommitCount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _historyCommitCount, value);
            this.RaisePropertyChanged(nameof(HistoryHeaderText));
            this.RaisePropertyChanged(nameof(HasHistory));
            this.RaisePropertyChanged(nameof(HasNoHistory));
        }
    }

    public bool HasHistory => HistoryCommitCount > 0;

    public bool HasNoHistory => HasRepository && !IsLoading && HistoryCommitCount == 0;

    public GitCommitItem? SelectedCommit
    {
        get => _selectedCommit;
        set
        {
            var selectedFilePath = SelectedCommitFile?.Path;
            this.RaiseAndSetIfChanged(ref _selectedCommit, value);
            this.RaisePropertyChanged(nameof(HasSelectedCommit));
            this.RaisePropertyChanged(nameof(SelectedCommitChangedFilesHeader));
            this.RaisePropertyChanged(nameof(CanCopySelectedCommitSha));
            this.RaisePropertyChanged(nameof(CanViewSelectedCommitOnGitHub));
            ApplySelectedCommitFiles(value, selectedFilePath);
        }
    }

    public bool HasSelectedCommit => SelectedCommit is not null;

    public bool CanCopySelectedCommitSha =>
        HasRepository &&
        CanRunRepositoryCommand &&
        SelectedCommit is not null;

    public bool CanViewSelectedCommitOnGitHub =>
        HasRepository &&
        CanRunRepositoryCommand &&
        SelectedCommit is not null &&
        RepositoryRemoteUrlHelper.TryGetGitHubCommitUrl(RemoteUrl, SelectedCommit.Sha, out _);

    public string HistoryHeaderText
    {
        get
        {
            var formatKey = HistoryCommitCount == 1
                ? AvaGithubDesktopL.HistoryCommitCountFormat
                : AvaGithubDesktopL.HistoryCommitsCountFormat;
            return _localizer.Format(formatKey, HistoryCommitCount);
        }
    }

    public string SelectedCommitChangedFilesHeader
    {
        get
        {
            var count = SelectedCommit?.ChangedFilesCount ?? 0;
            var formatKey = count == 1
                ? AvaGithubDesktopL.CommitChangedFileCountFormat
                : AvaGithubDesktopL.CommitChangedFilesCountFormat;
            return _localizer.Format(formatKey, count);
        }
    }

    public GitBranchItemViewModel? SelectedBranch
    {
        get => _selectedBranch;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedBranch, value);
            RaiseBranchStateChanged();
        }
    }

    public bool CanCheckoutBranch =>
        HasRepository &&
        CanRunRepositoryCommand &&
        SelectedBranch is not null &&
        !SelectedBranch.IsCurrent;

    public bool CanCreateBranch =>
        HasRepository &&
        CanRunRepositoryCommand;

    public string UpdateFromDefaultBranchMenuText =>
        string.IsNullOrWhiteSpace(DefaultBranch) || DefaultBranch == "-"
            ? _localizer.Get(AvaGithubDesktopL.MenuUpdateFromDefaultBranch)
            : _localizer.Format(AvaGithubDesktopL.MenuUpdateFromBranchFormat, DefaultBranch);

    public bool CanUpdateFromDefaultBranch =>
        HasRepository &&
        CanRunRepositoryCommand &&
        !string.IsNullOrWhiteSpace(DefaultBranch) &&
        DefaultBranch != "-" &&
        !string.Equals(CurrentBranch, DefaultBranch, StringComparison.Ordinal);

    public bool CanMergeBranch =>
        HasRepository &&
        CanRunRepositoryCommand &&
        Branches.Any(branch => !branch.IsCurrent);

    public bool CanRebaseBranch =>
        HasRepository &&
        CanRunRepositoryCommand &&
        Branches.Any(branch => !branch.IsCurrent);

    public int FilteredBranchesCount => FilteredBranches.Count;

    public bool HasActiveBranchFilter => !string.IsNullOrWhiteSpace(BranchFilterText);

    public bool HasNoFilteredBranches => HasRepository && !IsLoading && FilteredBranches.Count == 0;

    public GitStashEntry? CurrentBranchStash
    {
        get => _currentBranchStash;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentBranchStash, value);
            RaiseStashStateChanged();
        }
    }

    public bool HasCurrentBranchStash => CurrentBranchStash is not null;

    public bool CanStashChanges =>
        HasRepository &&
        CanRunRepositoryCommand &&
        ChangedFilesCount > 0 &&
        !HasCurrentBranchStash;

    public bool CanRestoreStash =>
        HasRepository &&
        CanRunRepositoryCommand &&
        CurrentBranchStash is not null;

    public bool CanDiscardStash => CanRestoreStash;

    public bool CanDiscardChanges =>
        HasRepository &&
        CanRunRepositoryCommand &&
        ChangedFilesCount > 0;

    public string StashAllChangesButtonText
    {
        get
        {
            if (IsStashing)
            {
                return _localizer.Get(AvaGithubDesktopL.StashingChanges);
            }

            return HasCurrentBranchStash
                ? _localizer.Get(AvaGithubDesktopL.CurrentBranchAlreadyHasStash)
                : _localizer.Get(AvaGithubDesktopL.StashAllChanges);
        }
    }

    public string StashDescriptionText => CurrentBranchStash is null
        ? string.Empty
        : _localizer.Format(
            AvaGithubDesktopL.StashedChangesDescriptionFormat,
            CurrentBranchStash.BranchName,
            CurrentBranchStash.ShortSha);

    public string BranchesHeaderText
    {
        get
        {
            if (HasActiveBranchFilter)
            {
                return _localizer.Format(
                    AvaGithubDesktopL.BranchesFilteredCountFormat,
                    FilteredBranchesCount,
                    Branches.Count);
            }

            var formatKey = Branches.Count == 1
                ? AvaGithubDesktopL.BranchCountFormat
                : AvaGithubDesktopL.BranchesCountFormat;
            return _localizer.Format(formatKey, Branches.Count);
        }
    }

    public bool HasRemote => HasRepository && !string.IsNullOrWhiteSpace(RemoteName) && RemoteName != "-";

    public bool CanSynchronize =>
        HasRepository &&
        HasRemote &&
        CanRunRepositoryCommand;

    public bool CanPublishCurrentBranch =>
        HasRemote &&
        string.Equals(Upstream, "-", StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(CurrentBranch) &&
        CurrentBranch != "-" &&
        !CurrentBranch.StartsWith("HEAD", StringComparison.OrdinalIgnoreCase);

    public string SyncActionTitle
    {
        get
        {
            if (!HasRemote)
            {
                return _localizer.Get(AvaGithubDesktopL.SyncNoRemote);
            }

            if (IsSyncing)
            {
                return _activeSyncOperation switch
                {
                    RepositorySyncOperation.Pull => _localizer.Format(AvaGithubDesktopL.SyncPullingTitleFormat, RemoteName),
                    RepositorySyncOperation.Publish => _localizer.Get(AvaGithubDesktopL.SyncPublishingBranchTitle),
                    RepositorySyncOperation.Push => _localizer.Format(AvaGithubDesktopL.SyncPushingTitleFormat, RemoteName),
                    _ => _localizer.Format(AvaGithubDesktopL.SyncFetchingTitleFormat, RemoteName)
                };
            }

            if (CanPublishCurrentBranch)
            {
                return _localizer.Get(AvaGithubDesktopL.SyncPublishBranchTitle);
            }

            if (_behind > 0)
            {
                return _localizer.Format(AvaGithubDesktopL.SyncPullTitleFormat, RemoteName);
            }

            if (_ahead > 0)
            {
                return _localizer.Format(AvaGithubDesktopL.SyncPushTitleFormat, RemoteName);
            }

            return _localizer.Format(AvaGithubDesktopL.SyncFetchTitleFormat, RemoteName);
        }
    }

    public string SyncActionDescription
    {
        get
        {
            if (!HasRemote)
            {
                return _localizer.Get(AvaGithubDesktopL.SyncNoRemoteDescription);
            }

            if (IsSyncing)
            {
                return _localizer.Get(AvaGithubDesktopL.SyncInProgressDescription);
            }

            if (CanPublishCurrentBranch)
            {
                var descriptionKey = RepositoryRemoteUrlHelper.TryGetGitHubWebUrl(RemoteUrl, out _)
                    ? AvaGithubDesktopL.SyncPublishBranchToGitHubDescription
                    : AvaGithubDesktopL.SyncPublishBranchToRemoteDescription;
                return _localizer.Get(descriptionKey);
            }

            return FormatLastFetched(LastFetchedAt);
        }
    }

    public string SyncActionIcon => IsSyncing
        ? "..."
        : CanPublishCurrentBranch
            ? "↑"
        : _behind > 0
            ? "↓"
            : _ahead > 0
                ? "↑"
                : "↻";

    public GitChangeItemViewModel? SelectedChange
    {
        get => _selectedChange;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedChange, value);
            if (IsChangesSelected)
            {
                QueueDiffLoad();
            }
        }
    }

    public GitCommitFileItemViewModel? SelectedCommitFile
    {
        get => _selectedCommitFile;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCommitFile, value);
            if (IsHistorySelected)
            {
                QueueDiffLoad();
            }
        }
    }

    public bool IsDiffLoading
    {
        get => _isDiffLoading;
        private set => this.RaiseAndSetIfChanged(ref _isDiffLoading, value);
    }

    public string DiffTitle
    {
        get => _diffTitle;
        private set => this.RaiseAndSetIfChanged(ref _diffTitle, value);
    }

    public string DiffText
    {
        get => _diffText;
        private set => this.RaiseAndSetIfChanged(ref _diffText, value);
    }

    public GitFileDiffPreview DiffPreview
    {
        get => _diffPreview;
        private set
        {
            this.RaiseAndSetIfChanged(ref _diffPreview, value);
            DiffText = value.Text;
            RaiseDiffPreviewStateChanged();
        }
    }

    public bool IsTextDiffVisible => DiffPreview.Kind == GitDiffPreviewKind.Text;

    public bool IsImageDiffVisible => DiffPreview.Kind == GitDiffPreviewKind.Image;

    public bool IsBinaryDiffVisible => DiffPreview.Kind == GitDiffPreviewKind.Binary;

    public string? PreviousDiffImagePath => DiffPreview.PreviousImagePath;

    public string? CurrentDiffImagePath => DiffPreview.CurrentImagePath;

    public string BinaryDiffMessage => _localizer.Get(AvaGithubDesktopL.BinaryFileChanged);

    public string BinaryDiffOpenText => _localizer.Get(AvaGithubDesktopL.OpenBinaryFileInExternalProgram);

    public bool CanOpenDiffFileInExternalEditor =>
        CanRunRepositoryCommand &&
        !string.IsNullOrWhiteSpace(DiffPreview.WorkingTreePath) &&
        File.Exists(DiffPreview.WorkingTreePath);

    public string CommitSummary
    {
        get => _commitSummary;
        set
        {
            this.RaiseAndSetIfChanged(ref _commitSummary, value);
            RaiseCommitStateChanged();
        }
    }

    public string CommitDescription
    {
        get => _commitDescription;
        set => this.RaiseAndSetIfChanged(ref _commitDescription, value);
    }

    public string ChangesFilterText
    {
        get => _changesFilterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _changesFilterText, value);
            ApplyChangesFilter();
        }
    }

    public string BranchFilterText
    {
        get => _branchFilterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _branchFilterText, value);
            ApplyBranchFilter();
        }
    }

    public int IncludedChangesCount
    {
        get => _includedChangesCount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _includedChangesCount, value);
            RaiseCommitStateChanged();
        }
    }

    public bool? AreAllChangesIncluded
    {
        get => _areAllChangesIncluded;
        set
        {
            if (value is null)
            {
                this.RaiseAndSetIfChanged(ref _areAllChangesIncluded, value);
                return;
            }

            SetAllChangesIncluded(value.Value);
        }
    }

    public bool CanCommit =>
        HasRepository &&
        CanRunRepositoryCommand &&
        IncludedChangesCount > 0 &&
        !string.IsNullOrWhiteSpace(CommitSummary);

    public string CommitButtonText
    {
        get
        {
            if (IsCommitting)
            {
                return _localizer.Get(AvaGithubDesktopL.Committing);
            }

            if (IncludedChangesCount <= 0)
            {
                return _localizer.Get(AvaGithubDesktopL.SelectFilesToCommit);
            }

            var formatKey = IncludedChangesCount == 1
                ? AvaGithubDesktopL.CommitFileToBranchFormat
                : AvaGithubDesktopL.CommitFilesToBranchFormat;
            return _localizer.Format(formatKey, IncludedChangesCount, CurrentBranch);
        }
    }

    public string ChangedFilesHeaderText
    {
        get
        {
            if (HasActiveChangesFilter)
            {
                return _localizer.Format(
                    AvaGithubDesktopL.ChangedFilesFilteredCountFormat,
                    FilteredChangedFilesCount,
                    ChangedFilesCount);
            }

            var formatKey = ChangedFilesCount == 1
                ? AvaGithubDesktopL.ChangedFileCountFormat
                : AvaGithubDesktopL.ChangedFilesCountFormat;
            return _localizer.Format(formatKey, ChangedFilesCount);
        }
    }

    public int FilteredChangedFilesCount => FilteredChangedFiles.Count;

    public bool HasActiveChangesFilter => !string.IsNullOrWhiteSpace(ChangesFilterText);

    public string SelectedChangesStatusText =>
        _localizer.Format(AvaGithubDesktopL.SelectedFilesCountFormat, IncludedChangesCount, ChangedFilesCount);

    public bool IsOperationLogVisible
    {
        get => _isOperationLogVisible;
        set
        {
            if (_isOperationLogVisible == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _isOperationLogVisible, value);
            _settingsStore.Update(settings => settings with { IsOperationLogVisible = value });
        }
    }

    public ThemeOption? SelectedTheme
    {
        get => _selectedTheme;
        private set
        {
            if (EqualityComparer<ThemeOption?>.Default.Equals(_selectedTheme, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedTheme, value);
            if (value is null)
            {
                return;
            }

            _themeService.ApplyTheme(value);
            _settingsStore.Update(settings => settings with { ThemeKey = value.Key });
            RaiseThemeSelectionChanged();
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Format(AvaGithubDesktopL.StatusThemeSwitchedFormat, _localizer.Get(value.DisplayNameResourceKey))));
        }
    }

    public bool IsSystemThemeSelected => IsThemeSelected("system");

    public bool IsLightThemeSelected => IsThemeSelected("light");

    public bool IsDarkThemeSelected => IsThemeSelected("dark");

    public bool IsAquaticThemeSelected => IsThemeSelected("aquatic");

    public bool IsDesertThemeSelected => IsThemeSelected("desert");

    public bool IsDuskThemeSelected => IsThemeSelected("dusk");

    public bool IsNightSkyThemeSelected => IsThemeSelected("night-sky");

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedLanguage, value);
            if (value is null)
            {
                return;
            }

            _localizer.SetCulture(value.CultureName);
            _settingsStore.Update(settings => settings with { CultureName = value.CultureName });
            this.RaisePropertyChanged(nameof(IsSimplifiedChineseSelected));
            this.RaisePropertyChanged(nameof(IsEnglishSelected));
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Format(AvaGithubDesktopL.StatusLanguageSwitchedFormat, value.DisplayName)));
        }
    }

    public bool IsSimplifiedChineseSelected =>
        string.Equals(SelectedLanguage?.CultureName, "zh-CN", StringComparison.OrdinalIgnoreCase);

    public bool IsEnglishSelected =>
        string.Equals(SelectedLanguage?.CultureName, "en-US", StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await LoadAccountsAsync();
        await LoadRepositoryHistoryAsync();
        if (Directory.Exists(RepositoryPath))
        {
            await OpenRepositoryAsync();
            return;
        }

        _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusReady)));
    }

    private async Task BrowseRepositoryAsync()
    {
        var selectedPath = await _repositoryPickerService.PickRepositoryAsync();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        RepositoryPath = selectedPath;
        await OpenRepositoryAsync();
    }

    private async Task OpenKnownRepositoryAsync(RepositoryListItemViewModel repository)
    {
        RepositoryFilterText = string.Empty;
        RepositoryPath = repository.Path;
        await OpenRepositoryAsync();
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            var accounts = await _gitHubAccountService.LoadAsync(CancellationToken.None);
            CurrentAccount = accounts.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusLoadAccountFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task SignInAsync()
    {
        if (IsSigningIn)
        {
            return;
        }

        IsSigningIn = true;
        ErrorMessage = string.Empty;

        try
        {
            var account = await _accountDialogService.ShowSignInDialogAsync(
                CurrentAccount?.Endpoint ?? GitHubAccountEndpoints.DotComApiEndpoint,
                CancellationToken.None);
            if (account is null)
            {
                _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusSignInCanceled)));
                return;
            }

            CurrentAccount = account;
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Format(AvaGithubDesktopL.StatusSignedInGitHubFormat, account.Login, account.FriendlyEndpoint)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusSignInFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    private async Task SignOutAsync()
    {
        if (CurrentAccount is null || IsSigningIn)
        {
            return;
        }

        IsSigningIn = true;
        ErrorMessage = string.Empty;

        try
        {
            // 退出登录只移除本地保存的账户和令牌，不会撤销 GitHub 站点上的 PAT。
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusSigningOutGitHub)));
            await _gitHubAccountService.SignOutAsync(CurrentAccount, CancellationToken.None);
            CurrentAccount = _gitHubAccountService.CurrentAccount;
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusSignedOutGitHub)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusSignOutFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    private async Task OpenRepositoryInShellAsync()
    {
        await OpenRepositoryPathInShellAsync(RootPath);
    }

    private async Task OpenRepositoryInExternalEditorAsync()
    {
        await OpenRepositoryPathInExternalEditorAsync(RootPath);
    }

    private async Task ShowRepositoryInFileManagerAsync()
    {
        await ShowRepositoryPathInFileManagerAsync(RootPath);
    }

    private async Task ViewRepositoryOnGitHubAsync()
    {
        await ViewRepositoryRemoteOnGitHubAsync(RemoteUrl);
    }

    private void ToggleOperationLog()
    {
        IsOperationLogVisible = !IsOperationLogVisible;
        var messageKey = IsOperationLogVisible
            ? AvaGithubDesktopL.StatusOperationLogShown
            : AvaGithubDesktopL.StatusOperationLogHidden;
        _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(messageKey)));
    }

    private void SelectThemeByKey(string? key)
    {
        var theme = FindTheme(key);
        if (theme is not null)
        {
            SelectedTheme = theme;
        }
    }

    private ThemeOption? FindTheme(string? key)
    {
        return string.IsNullOrWhiteSpace(key)
            ? null
            : ThemeOptions.FirstOrDefault(option => option.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsThemeSelected(string key)
    {
        return string.Equals(SelectedTheme?.Key, key, StringComparison.OrdinalIgnoreCase);
    }

    private void RaiseThemeSelectionChanged()
    {
        this.RaisePropertyChanged(nameof(IsSystemThemeSelected));
        this.RaisePropertyChanged(nameof(IsLightThemeSelected));
        this.RaisePropertyChanged(nameof(IsDarkThemeSelected));
        this.RaisePropertyChanged(nameof(IsAquaticThemeSelected));
        this.RaisePropertyChanged(nameof(IsDesertThemeSelected));
        this.RaisePropertyChanged(nameof(IsDuskThemeSelected));
        this.RaisePropertyChanged(nameof(IsNightSkyThemeSelected));
    }

    private void SelectLanguageByCulture(string cultureName)
    {
        var language = Languages.FirstOrDefault(option => option.CultureName.Equals(cultureName, StringComparison.OrdinalIgnoreCase));
        if (language is not null)
        {
            SelectedLanguage = language;
        }
    }

    private async Task ViewSelectedCommitOnGitHubAsync()
    {
        if (SelectedCommit is null)
        {
            return;
        }

        await ViewCommitOnGitHubAsync(RemoteUrl, SelectedCommit.Sha);
    }

    private async Task OpenRepositoryItemInShellAsync(RepositoryListItemViewModel repository)
    {
        await OpenRepositoryPathInShellAsync(repository.Path);
    }

    private async Task OpenRepositoryItemInExternalEditorAsync(RepositoryListItemViewModel repository)
    {
        await OpenRepositoryPathInExternalEditorAsync(repository.Path);
    }

    private async Task ShowRepositoryItemInFileManagerAsync(RepositoryListItemViewModel repository)
    {
        await ShowRepositoryPathInFileManagerAsync(repository.Path);
    }

    private async Task CopyRepositoryNameAsync(RepositoryListItemViewModel repository)
    {
        await CopyTextAsync(
            repository.Name,
            AvaGithubDesktopL.StatusCopiedRepositoryName,
            AvaGithubDesktopL.StatusCopyRepositoryTextFailedFormat);
    }

    private async Task CopyRepositoryPathAsync(RepositoryListItemViewModel repository)
    {
        await CopyTextAsync(
            repository.Path,
            AvaGithubDesktopL.StatusCopiedRepositoryPath,
            AvaGithubDesktopL.StatusCopyRepositoryTextFailedFormat);
    }

    private async Task CopyBranchNameAsync(GitBranchItemViewModel branch)
    {
        await CopyTextAsync(
            branch.Name,
            AvaGithubDesktopL.StatusCopiedBranchName,
            AvaGithubDesktopL.StatusCopyBranchNameFailedFormat);
    }

    private async Task ViewBranchOnGitHubAsync(GitBranchItemViewModel branch)
    {
        await ViewBranchOnGitHubAsync(RemoteUrl, branch.Upstream);
    }

    private async Task ViewCurrentBranchOnGitHubAsync()
    {
        await ViewBranchOnGitHubAsync(RemoteUrl, Upstream);
    }

    private async Task CompareCurrentBranchOnGitHubAsync()
    {
        await CompareBranchOnGitHubAsync(RemoteUrl, Upstream);
    }

    private async Task ViewRepositoryItemOnGitHubAsync(RepositoryListItemViewModel repository)
    {
        await ViewRepositoryRemoteOnGitHubAsync(repository.Entry.RemoteUrl);
    }

    private async Task CopyChangeRelativePathAsync(GitChangeItemViewModel change)
    {
        await CopyTextAsync(
            change.Path,
            AvaGithubDesktopL.StatusCopiedChangePath,
            AvaGithubDesktopL.StatusCopyChangePathFailedFormat);
    }

    private async Task CopyChangeFullPathAsync(GitChangeItemViewModel change)
    {
        var relativePath = change.GitPaths.LastOrDefault() ?? change.Path;
        await CopyTextAsync(
            Path.Combine(RootPath, relativePath),
            AvaGithubDesktopL.StatusCopiedChangeFullPath,
            AvaGithubDesktopL.StatusCopyChangeFullPathFailedFormat);
    }

    private async Task CopySelectedCommitShaAsync()
    {
        if (SelectedCommit is null)
        {
            return;
        }

        await CopyTextAsync(
            SelectedCommit.Sha,
            AvaGithubDesktopL.StatusCopiedCommitSha,
            AvaGithubDesktopL.StatusCopyCommitShaFailedFormat);
    }

    private async Task CopyCommitFileRelativePathAsync(GitCommitFileItemViewModel file)
    {
        await CopyTextAsync(
            file.GitPath,
            AvaGithubDesktopL.StatusCopiedChangePath,
            AvaGithubDesktopL.StatusCopyChangePathFailedFormat);
    }

    private async Task CopyCommitFileFullPathAsync(GitCommitFileItemViewModel file)
    {
        await CopyTextAsync(
            Path.Combine(RootPath, file.GitPath),
            AvaGithubDesktopL.StatusCopiedChangeFullPath,
            AvaGithubDesktopL.StatusCopyChangeFullPathFailedFormat);
    }

    private async Task ShowCommitFileInFileManagerAsync(GitCommitFileItemViewModel file)
    {
        try
        {
            await _repositoryShellService.ShowItemInFileManagerAsync(Path.Combine(RootPath, file.GitPath));
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusShowedChangeInFileManager)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusShowChangeInFileManagerFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task OpenCommitFileInExternalEditorAsync(GitCommitFileItemViewModel file)
    {
        try
        {
            await _repositoryShellService.OpenItemAsync(Path.Combine(RootPath, file.GitPath));
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusOpenedChangeInExternalEditor)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusOpenChangeInExternalEditorFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task ShowChangeInFileManagerAsync(GitChangeItemViewModel change)
    {
        var relativePath = change.GitPaths.LastOrDefault() ?? change.Path;
        var fullPath = Path.Combine(RootPath, relativePath);

        try
        {
            await _repositoryShellService.ShowItemInFileManagerAsync(fullPath);
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusShowedChangeInFileManager)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusShowChangeInFileManagerFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task OpenChangeInExternalEditorAsync(GitChangeItemViewModel change)
    {
        var relativePath = change.GitPaths.LastOrDefault() ?? change.Path;
        var fullPath = Path.Combine(RootPath, relativePath);

        try
        {
            await _repositoryShellService.OpenItemAsync(fullPath);
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusOpenedChangeInExternalEditor)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusOpenChangeInExternalEditorFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task DiscardChangeAsync(GitChangeItemViewModel change)
    {
        await DiscardChangesAsync(new[] { change });
    }

    private async Task DiscardAllChangesAsync()
    {
        if (!CanDiscardChanges)
        {
            return;
        }

        await DiscardChangesAsync(ChangedFiles.ToArray());
    }

    private async Task DiscardChangesAsync(IReadOnlyList<GitChangeItemViewModel> changes)
    {
        if (!HasRepository || !CanRunRepositoryCommand || changes.Count == 0)
        {
            return;
        }

        var confirmed = await _confirmationDialogService.ShowDiscardChangesConfirmationAsync(
            changes.Select(change => change.Path).ToArray());
        if (!confirmed)
        {
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusDiscardChangesCanceled)));
            return;
        }

        if (!HasRepository || !CanRunRepositoryCommand)
        {
            return;
        }

        IsDiscardingChanges = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(GetDiscardChangesStartedMessage(changes)));

        try
        {
            await _gitRepositoryService.DiscardChangesAsync(
                RootPath,
                changes.Select(change => change.Change).ToArray(),
                CancellationToken.None);
            await ReloadRepositoryWorkspaceAsync();
            _eventBus.Publish(new StatusMessageChangedCommand(GetDiscardChangesCompletedMessage(changes)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusDiscardChangesFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsDiscardingChanges = false;
        }
    }

    private string GetDiscardChangesStartedMessage(IReadOnlyList<GitChangeItemViewModel> changes)
    {
        return changes.Count == 1
            ? _localizer.Format(AvaGithubDesktopL.StatusDiscardingChangesFormat, changes[0].Path)
            : _localizer.Format(AvaGithubDesktopL.StatusDiscardingChangesCountFormat, changes.Count);
    }

    private string GetDiscardChangesCompletedMessage(IReadOnlyList<GitChangeItemViewModel> changes)
    {
        return changes.Count == 1
            ? _localizer.Format(AvaGithubDesktopL.StatusDiscardedChangesFormat, changes[0].Path)
            : _localizer.Format(AvaGithubDesktopL.StatusDiscardedChangesCountFormat, changes.Count);
    }

    private async Task CopyTextAsync(string text, string successKey, string failureFormatKey)
    {
        try
        {
            await _repositoryShellService.CopyTextAsync(text);
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(successKey)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(failureFormatKey, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task ViewRepositoryRemoteOnGitHubAsync(string? remoteUrl)
    {
        if (!RepositoryRemoteUrlHelper.TryGetGitHubWebUrl(remoteUrl, out var webUrl))
        {
            ErrorMessage = _localizer.Get(AvaGithubDesktopL.StatusRepositoryHasNoGitHubRemote);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
            return;
        }

        try
        {
            await _repositoryShellService.OpenUrlAsync(webUrl);
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusOpenedRepositoryOnGitHub)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusOpenRepositoryOnGitHubFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task ViewCommitOnGitHubAsync(string? remoteUrl, string sha)
    {
        if (!RepositoryRemoteUrlHelper.TryGetGitHubCommitUrl(remoteUrl, sha, out var webUrl))
        {
            ErrorMessage = _localizer.Get(AvaGithubDesktopL.StatusRepositoryHasNoGitHubRemote);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
            return;
        }

        try
        {
            await _repositoryShellService.OpenUrlAsync(webUrl);
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusOpenedCommitOnGitHub)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusOpenCommitOnGitHubFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task ViewBranchOnGitHubAsync(string? remoteUrl, string upstream)
    {
        if (!RepositoryRemoteUrlHelper.TryGetGitHubBranchUrl(remoteUrl, upstream, out var webUrl))
        {
            ErrorMessage = _localizer.Get(AvaGithubDesktopL.StatusRepositoryHasNoGitHubRemote);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
            return;
        }

        try
        {
            await _repositoryShellService.OpenUrlAsync(webUrl);
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusOpenedBranchOnGitHub)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusOpenBranchOnGitHubFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task CompareBranchOnGitHubAsync(string? remoteUrl, string upstream)
    {
        if (!RepositoryRemoteUrlHelper.TryGetGitHubCompareUrl(remoteUrl, upstream, out var webUrl))
        {
            ErrorMessage = _localizer.Get(AvaGithubDesktopL.StatusRepositoryHasNoGitHubRemote);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
            return;
        }

        try
        {
            await _repositoryShellService.OpenUrlAsync(webUrl);
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusOpenedBranchCompareOnGitHub)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusOpenBranchCompareOnGitHubFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task OpenRepositoryPathInShellAsync(string repositoryPath)
    {
        try
        {
            await _repositoryShellService.OpenInShellAsync(repositoryPath);
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusOpenedRepositoryShell)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusOpenRepositoryShellFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task OpenRepositoryPathInExternalEditorAsync(string repositoryPath)
    {
        try
        {
            await _repositoryShellService.OpenItemAsync(repositoryPath);
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusOpenedRepositoryInExternalEditor)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusOpenRepositoryInExternalEditorFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task ShowRepositoryPathInFileManagerAsync(string repositoryPath)
    {
        try
        {
            await _repositoryShellService.ShowInFileManagerAsync(repositoryPath);
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusShowedRepositoryInFileManager)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusShowRepositoryInFileManagerFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task ShowChangelogAsync()
    {
        try
        {
            await _helpService.ShowChangelogWindowAsync();
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusOpenedChangelog)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusOpenHelpFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task ShowAboutAsync()
    {
        try
        {
            await _helpService.ShowAboutWindowAsync();
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusOpenedAbout)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusOpenHelpFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
    }

    private async Task OpenRepositoryAsync()
    {
        var path = RepositoryPath.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            ErrorMessage = _localizer.Get(AvaGithubDesktopL.ErrorInvalidRepository);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Format(AvaGithubDesktopL.StatusLoadingRepositoryFormat, path)));

        try
        {
            var snapshot = await _gitRepositoryService.LoadRepositoryAsync(path, CancellationToken.None);
            ApplySnapshot(snapshot);
            var branches = await _gitRepositoryService.LoadBranchesAsync(snapshot.RootPath, CancellationToken.None);
            ApplyBranches(branches);
            var history = await _gitRepositoryService.LoadHistoryAsync(snapshot.RootPath, HistoryCommitLimit, CancellationToken.None);
            ApplyHistory(history);
            await _repositoryHistoryService.AddOrUpdateAsync(snapshot.RootPath, CancellationToken.None);
            await LoadRepositoryHistoryAsync();
            _eventBus.Publish(new RepositoryOpenedCommand(snapshot.RepositoryName, snapshot.ChangedFilesCount));
        }
        catch (Exception ex)
        {
            HasRepository = false;
            RemoteName = "-";
            RemoteUrl = "-";
            LastFetchedAt = null;
            CurrentBranchStash = null;
            ApplyBranches(Array.Empty<GitBranchItem>());
            ApplyHistory(Array.Empty<GitCommitItem>());
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusRepositoryLoadFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CommitChangesAsync()
    {
        if (!CanCommit)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(CommitSummary)
                ? _localizer.Get(AvaGithubDesktopL.CommitSummaryRequired)
                : _localizer.Get(AvaGithubDesktopL.NoFilesSelected);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
            return;
        }

        var includedPaths = ChangedFiles
            .Where(change => change.IsIncluded)
            .SelectMany(change => change.GitPaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var committedSummary = CommitSummary.Trim();

        IsCommitting = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(
            _localizer.Format(AvaGithubDesktopL.StatusCommittingFormat, IncludedChangesCount, CurrentBranch)));

        try
        {
            await _gitRepositoryService.CommitAsync(
                RootPath,
                includedPaths,
                CommitSummary,
                CommitDescription,
                CancellationToken.None);

            CommitSummary = string.Empty;
            CommitDescription = string.Empty;
            var snapshot = await _gitRepositoryService.LoadRepositoryAsync(RootPath, CancellationToken.None);
            ApplySnapshot(snapshot);
            var branches = await _gitRepositoryService.LoadBranchesAsync(snapshot.RootPath, CancellationToken.None);
            ApplyBranches(branches);
            var history = await _gitRepositoryService.LoadHistoryAsync(snapshot.RootPath, HistoryCommitLimit, CancellationToken.None);
            ApplyHistory(history);
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Format(AvaGithubDesktopL.StatusCommittedFormat, committedSummary)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusCommitFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsCommitting = false;
        }
    }

    private Task SynchronizeRepositoryAsync()
    {
        if (CanPublishCurrentBranch)
        {
            return PublishBranchAsync();
        }

        if (_behind > 0)
        {
            return PullRepositoryAsync();
        }

        if (_ahead > 0)
        {
            return PushRepositoryAsync();
        }

        return FetchRepositoryAsync();
    }

    private Task FetchRepositoryAsync() =>
        RunRemoteOperationAsync(RepositorySyncOperation.Fetch);

    private Task PullRepositoryAsync() =>
        RunRemoteOperationAsync(RepositorySyncOperation.Pull);

    private Task PushRepositoryAsync() =>
        RunRemoteOperationAsync(RepositorySyncOperation.Push);

    private Task PublishBranchAsync() =>
        RunRemoteOperationAsync(RepositorySyncOperation.Publish);

    private async Task RunRemoteOperationAsync(RepositorySyncOperation operation)
    {
        // 所有远端同步动作共用同一条状态机，保证 toolbar、菜单和状态栏不会出现并发操作。
        if (!CanSynchronize)
        {
            ErrorMessage = _localizer.Get(AvaGithubDesktopL.SyncNoRemoteDescription);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
            return;
        }

        _activeSyncOperation = operation;
        IsSyncing = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(GetRemoteOperationStartedMessage(operation)));

        try
        {
            switch (operation)
            {
                case RepositorySyncOperation.Pull:
                    await _gitRepositoryService.PullAsync(RootPath, RemoteName, CancellationToken.None);
                    break;
                case RepositorySyncOperation.Push:
                case RepositorySyncOperation.Publish:
                    await _gitRepositoryService.PushAsync(RootPath, RemoteName, CurrentBranch, CancellationToken.None);
                    break;
                default:
                    await _gitRepositoryService.FetchAsync(RootPath, RemoteName, CancellationToken.None);
                    break;
            }

            var snapshot = await _gitRepositoryService.LoadRepositoryAsync(RootPath, CancellationToken.None);
            ApplySnapshot(snapshot);
            var branches = await _gitRepositoryService.LoadBranchesAsync(snapshot.RootPath, CancellationToken.None);
            ApplyBranches(branches);
            var history = await _gitRepositoryService.LoadHistoryAsync(snapshot.RootPath, HistoryCommitLimit, CancellationToken.None);
            ApplyHistory(history);
            _eventBus.Publish(new StatusMessageChangedCommand(GetRemoteOperationCompletedMessage(operation)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(GetRemoteOperationFailedKey(operation), ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            _activeSyncOperation = RepositorySyncOperation.None;
            IsSyncing = false;
        }
    }

    private string GetRemoteOperationStartedMessage(RepositorySyncOperation operation) =>
        operation switch
        {
            RepositorySyncOperation.Pull => _localizer.Format(AvaGithubDesktopL.StatusPullingFormat, RemoteName),
            RepositorySyncOperation.Publish => _localizer.Format(AvaGithubDesktopL.StatusPublishingBranchFormat, CurrentBranch, RemoteName),
            RepositorySyncOperation.Push => _localizer.Format(AvaGithubDesktopL.StatusPushingFormat, RemoteName),
            _ => _localizer.Format(AvaGithubDesktopL.StatusFetchingFormat, RemoteName)
        };

    private string GetRemoteOperationCompletedMessage(RepositorySyncOperation operation) =>
        operation switch
        {
            RepositorySyncOperation.Pull => _localizer.Format(AvaGithubDesktopL.StatusPulledFormat, RemoteName),
            RepositorySyncOperation.Publish => _localizer.Format(AvaGithubDesktopL.StatusPublishedBranchFormat, CurrentBranch, RemoteName),
            RepositorySyncOperation.Push => _localizer.Format(AvaGithubDesktopL.StatusPushedFormat, RemoteName),
            _ => _localizer.Format(AvaGithubDesktopL.StatusFetchedFormat, RemoteName)
        };

    private static string GetRemoteOperationFailedKey(RepositorySyncOperation operation) =>
        operation switch
        {
            RepositorySyncOperation.Pull => AvaGithubDesktopL.StatusPullFailedFormat,
            RepositorySyncOperation.Publish => AvaGithubDesktopL.StatusPublishBranchFailedFormat,
            RepositorySyncOperation.Push => AvaGithubDesktopL.StatusPushFailedFormat,
            _ => AvaGithubDesktopL.StatusFetchFailedFormat
        };

    private async Task StashAllChangesAsync()
    {
        if (!CanStashChanges)
        {
            return;
        }

        IsStashing = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Format(AvaGithubDesktopL.StatusStashingChangesFormat, CurrentBranch)));

        try
        {
            // Stash all changes 是工作区级操作，不读取文件勾选状态，避免用户误以为只会 stash 已勾选文件。
            var stashed = await _gitRepositoryService.CreateStashAsync(RootPath, CurrentBranch, CancellationToken.None);
            await ReloadRepositoryWorkspaceAsync();
            _eventBus.Publish(new StatusMessageChangedCommand(
                stashed
                    ? _localizer.Format(AvaGithubDesktopL.StatusStashedChangesFormat, CurrentBranch)
                    : _localizer.Get(AvaGithubDesktopL.StatusNoChangesToStash)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusStashFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsStashing = false;
        }
    }

    private async Task RestoreStashAsync()
    {
        if (!CanRestoreStash || CurrentBranchStash is null)
        {
            return;
        }

        var stashName = CurrentBranchStash.Name;
        IsRestoringStash = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusRestoringStash)));

        try
        {
            await _gitRepositoryService.RestoreStashAsync(RootPath, stashName, CancellationToken.None);
            await ReloadRepositoryWorkspaceAsync();
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusRestoredStash)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusRestoreStashFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsRestoringStash = false;
        }
    }

    private async Task DiscardStashAsync()
    {
        if (!CanDiscardStash || CurrentBranchStash is null)
        {
            return;
        }

        var stashName = CurrentBranchStash.Name;
        IsDiscardingStash = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusDiscardingStash)));

        try
        {
            await _gitRepositoryService.DiscardStashAsync(RootPath, stashName, CancellationToken.None);
            await ReloadRepositoryWorkspaceAsync();
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusDiscardedStash)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusDiscardStashFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsDiscardingStash = false;
        }
    }

    private async Task ReloadRepositoryWorkspaceAsync()
    {
        var snapshot = await _gitRepositoryService.LoadRepositoryAsync(RootPath, CancellationToken.None);
        ApplySnapshot(snapshot);
        var branches = await _gitRepositoryService.LoadBranchesAsync(snapshot.RootPath, CancellationToken.None);
        ApplyBranches(branches);
        var history = await _gitRepositoryService.LoadHistoryAsync(snapshot.RootPath, HistoryCommitLimit, CancellationToken.None);
        ApplyHistory(history);
    }

    private async Task TryReloadRepositoryWorkspaceAsync()
    {
        try
        {
            await ReloadRepositoryWorkspaceAsync();
        }
        catch
        {
            // 刷新失败不能覆盖原始 Git 错误；调用方会继续显示真正的操作失败原因。
        }
    }

    private async Task LoadRepositoryHistoryAsync()
    {
        var entries = await _repositoryHistoryService.LoadKnownRepositoriesAsync(CancellationToken.None);
        _knownRepositories = entries
            .Select(entry => new RepositoryListItemViewModel(
                entry,
                OpenKnownRepositoryAsync,
                OpenRepositoryItemInExternalEditorAsync,
                OpenRepositoryItemInShellAsync,
                ShowRepositoryItemInFileManagerAsync,
                CopyRepositoryNameAsync,
                CopyRepositoryPathAsync,
                ViewRepositoryItemOnGitHubAsync))
            .ToArray();
        UpdateCurrentRepositoryIndicators();
        RebuildRepositoryGroups();
    }

    private void RebuildRepositoryGroups()
    {
        RepositoryGroups.Clear();

        var filteredRepositories = _knownRepositories
            .Where(MatchesRepositoryFilter)
            .ToArray();

        if (filteredRepositories.Length > 1)
        {
            AddRepositoryGroup(
                _localizer.Get(AvaGithubDesktopL.RecentRepositories),
                filteredRepositories
                    .OrderByDescending(repository => repository.LastOpenedAt)
                    .ThenBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(5));
        }

        foreach (var group in filteredRepositories
                     .GroupBy(repository => repository.GroupName)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            AddRepositoryGroup(
                group.Key,
                group.OrderBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase));
        }

        this.RaisePropertyChanged(nameof(HasNoRepositoryMatches));
    }

    private void AddRepositoryGroup(string header, IEnumerable<RepositoryListItemViewModel> repositories)
    {
        var items = repositories.ToArray();
        if (items.Length == 0)
        {
            return;
        }

        RepositoryGroups.Add(new RepositoryListGroupViewModel(header, items));
    }

    private bool MatchesRepositoryFilter(RepositoryListItemViewModel repository)
    {
        var filter = RepositoryFilterText.Trim();
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return repository.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || repository.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || repository.GroupName.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateCurrentRepositoryIndicators()
    {
        var currentPath = HasRepository ? RootPath : RepositoryPath;
        var normalizedCurrentPath = NormalizePathForComparison(currentPath);
        foreach (var repository in _knownRepositories)
        {
            repository.IsCurrent = string.Equals(
                NormalizePathForComparison(repository.Path),
                normalizedCurrentPath,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string NormalizePathForComparison(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.GetFullPath(path.Trim())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private void ApplySnapshot(GitRepositorySnapshot snapshot)
    {
        // 每次打开、提交、切分支、同步后都从 Git 重新读取快照，避免 UI 自己推断仓库状态。
        HasRepository = true;
        RepositoryName = snapshot.RepositoryName;
        RootPath = snapshot.RootPath;
        UpdateCurrentRepositoryIndicators();
        CurrentBranch = snapshot.CurrentBranch;
        DefaultBranch = snapshot.DefaultBranch;
        Upstream = snapshot.Upstream;
        RemoteName = snapshot.RemoteName;
        RemoteUrl = snapshot.RemoteUrl;
        LastFetchedAt = snapshot.LastFetchedAt;
        LastCommit = snapshot.LastCommit;
        ChangedFilesCount = snapshot.ChangedFilesCount;
        StagedCount = snapshot.StagedCount;
        UnstagedCount = snapshot.UnstagedCount;
        UntrackedCount = snapshot.UntrackedCount;
        CurrentBranchStash = snapshot.CurrentBranchStash;
        _ahead = snapshot.Ahead;
        _behind = snapshot.Behind;
        UpdateAheadBehindText();
        RaiseSyncStateChanged();

        var selectedPath = SelectedChange?.Path;
        _changeSubscriptions.Dispose();
        _changeSubscriptions = new CompositeDisposable();
        ChangedFiles.Clear();
        foreach (var change in snapshot.Changes)
        {
            var changeViewModel = new GitChangeItemViewModel(
                change,
                CopyChangeFullPathAsync,
                CopyChangeRelativePathAsync,
                ShowChangeInFileManagerAsync,
                OpenChangeInExternalEditorAsync,
                DiscardChangeAsync);
            // 单个文件勾选变化会影响提交按钮、全选三态和“已选择”文案，需要集中刷新派生状态。
            var subscription = changeViewModel
                .WhenAnyValue(model => model.IsIncluded)
                .Skip(1)
                .Subscribe(_ =>
                {
                    if (!_isBulkUpdatingIncludedChanges)
                    {
                        UpdateIncludedState();
                    }
                });
            _changeSubscriptions.Add(subscription);
            ChangedFiles.Add(changeViewModel);
        }

        UpdateIncludedState();
        ApplyChangesFilter(selectedPath);
    }

    private void ApplyHistory(IReadOnlyList<GitCommitItem> commits)
    {
        var selectedSha = SelectedCommit?.Sha;
        HistoryCommits.Clear();
        foreach (var commit in commits)
        {
            HistoryCommits.Add(commit);
        }

        HistoryCommitCount = HistoryCommits.Count;
        SelectedCommit = !string.IsNullOrWhiteSpace(selectedSha)
            ? HistoryCommits.FirstOrDefault(commit => commit.Sha == selectedSha) ?? HistoryCommits.FirstOrDefault()
            : HistoryCommits.FirstOrDefault();
    }

    private void ApplySelectedCommitFiles(GitCommitItem? commit, string? preferredSelectedPath)
    {
        // History 文件项也需要右键菜单命令，因此用轻量 ViewModel 包装模型并集中注入外部操作回调。
        SelectedCommitFiles.Clear();
        if (commit is null)
        {
            SelectedCommitFile = null;
            return;
        }

        foreach (var file in commit.Files)
        {
            SelectedCommitFiles.Add(new GitCommitFileItemViewModel(
                file,
                CopyCommitFileFullPathAsync,
                CopyCommitFileRelativePathAsync,
                ShowCommitFileInFileManagerAsync,
                OpenCommitFileInExternalEditorAsync));
        }

        SelectedCommitFile = !string.IsNullOrWhiteSpace(preferredSelectedPath)
            ? SelectedCommitFiles.FirstOrDefault(file => file.Path == preferredSelectedPath) ?? SelectedCommitFiles.FirstOrDefault()
            : SelectedCommitFiles.FirstOrDefault();
    }

    private void ApplyBranches(IReadOnlyList<GitBranchItem> branches)
    {
        var selectedName = SelectedBranch?.Name;
        Branches.Clear();
        foreach (var branch in branches)
        {
            Branches.Add(new GitBranchItemViewModel(
                branch,
                RenameBranchAsync,
                CopyBranchNameAsync,
                ViewBranchOnGitHubAsync,
                DeleteBranchAsync,
                RepositoryRemoteUrlHelper.TryGetGitHubBranchUrl(RemoteUrl, branch.Upstream, out _)));
        }

        ApplyBranchFilter(selectedName, preferCurrentBranch: true);
    }

    private void ApplyBranchFilter(string? preferredSelectedName = null, bool preferCurrentBranch = false)
    {
        // Desktop 的分支弹出层允许输入关键字缩小范围；过滤只影响弹出层列表，不改动真实分支集合。
        var filterText = BranchFilterText.Trim();
        var selectedName = preferredSelectedName ?? SelectedBranch?.Name;
        FilteredBranches.Clear();

        foreach (var branch in Branches.Where(branch => MatchesBranchFilter(branch, filterText)))
        {
            FilteredBranches.Add(branch);
        }

        var currentBranch = preferCurrentBranch
            ? FilteredBranches.FirstOrDefault(branch => branch.IsCurrent)
            : null;
        var selectedBranch = !string.IsNullOrWhiteSpace(selectedName)
            ? FilteredBranches.FirstOrDefault(branch => branch.Name == selectedName)
            : null;
        SelectedBranch = currentBranch ?? selectedBranch ?? FilteredBranches.FirstOrDefault();

        RaiseBranchStateChanged();
    }

    private static bool MatchesBranchFilter(GitBranchItemViewModel branch, string filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return true;
        }

        return branch.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)
            || branch.Upstream.Contains(filterText, StringComparison.OrdinalIgnoreCase)
            || branch.RelativeDate.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateAheadBehindText()
    {
        AheadBehindText = HasRepository
            ? _localizer.Format(AvaGithubDesktopL.AheadBehindFormat, _ahead, _behind)
            : "-";
        RaiseSyncStateChanged();
    }

    private string FormatLastFetched(DateTimeOffset? lastFetchedAt)
    {
        if (lastFetchedAt is null)
        {
            return _localizer.Get(AvaGithubDesktopL.SyncNeverFetched);
        }

        var elapsed = DateTimeOffset.Now - lastFetchedAt.Value.ToLocalTime();
        if (elapsed.TotalMinutes < 2)
        {
            return _localizer.Get(AvaGithubDesktopL.SyncLastFetchedJustNow);
        }

        if (elapsed.TotalHours < 1)
        {
            return _localizer.Format(AvaGithubDesktopL.SyncLastFetchedMinutesAgoFormat, Math.Max(1, (int)elapsed.TotalMinutes));
        }

        if (elapsed.TotalDays < 1)
        {
            return _localizer.Format(AvaGithubDesktopL.SyncLastFetchedHoursAgoFormat, Math.Max(1, (int)elapsed.TotalHours));
        }

        return _localizer.Format(AvaGithubDesktopL.SyncLastFetchedDaysAgoFormat, Math.Max(1, (int)elapsed.TotalDays));
    }

    private void SetAllChangesIncluded(bool include)
    {
        _isBulkUpdatingIncludedChanges = true;
        try
        {
            // 有过滤条件时，全选只作用于当前可见文件，保留被过滤隐藏文件原来的勾选状态。
            var changes = HasActiveChangesFilter
                ? FilteredChangedFiles
                : ChangedFiles;
            foreach (var change in changes)
            {
                change.IsIncluded = include;
            }
        }
        finally
        {
            _isBulkUpdatingIncludedChanges = false;
        }

        UpdateIncludedState();
    }

    private void ApplyChangesFilter(string? preferredSelectedPath = null)
    {
        // 过滤集合单独维护，提交仍读取全量 ChangedFiles，避免隐藏文件被意外移出提交选择。
        var filterText = ChangesFilterText.Trim();
        var selectedPath = preferredSelectedPath ?? SelectedChange?.Path;
        FilteredChangedFiles.Clear();

        foreach (var change in ChangedFiles.Where(change => MatchesChangesFilter(change, filterText)))
        {
            FilteredChangedFiles.Add(change);
        }

        this.RaisePropertyChanged(nameof(FilteredChangedFilesCount));
        this.RaisePropertyChanged(nameof(HasActiveChangesFilter));
        this.RaisePropertyChanged(nameof(ChangedFilesHeaderText));

        SelectedChange = !string.IsNullOrWhiteSpace(selectedPath)
            ? FilteredChangedFiles.FirstOrDefault(change => change.Path == selectedPath) ?? FilteredChangedFiles.FirstOrDefault()
            : FilteredChangedFiles.FirstOrDefault();
        UpdateIncludedState();
    }

    private static bool MatchesChangesFilter(GitChangeItemViewModel change, string filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return true;
        }

        return change.Path.Contains(filterText, StringComparison.OrdinalIgnoreCase)
            || change.DisplayStatus.Contains(filterText, StringComparison.OrdinalIgnoreCase)
            || change.StatusCode.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateIncludedState()
    {
        IncludedChangesCount = ChangedFiles.Count(change => change.IsIncluded);
        // 全选框反映当前筛选范围；提交按钮数量反映全量已勾选文件，两者职责不同。
        var scopedChanges = HasActiveChangesFilter
            ? FilteredChangedFiles
            : ChangedFiles;
        var scopedIncludedChangesCount = scopedChanges.Count(change => change.IsIncluded);
        var allIncluded = scopedChanges.Count == 0
            ? false
            : scopedIncludedChangesCount == scopedChanges.Count
                ? true
                : scopedIncludedChangesCount == 0
                    ? false
                    : (bool?)null;
        this.RaiseAndSetIfChanged(ref _areAllChangesIncluded, allIncluded, nameof(AreAllChangesIncluded));
        this.RaisePropertyChanged(nameof(ChangedFilesHeaderText));
    }

    private void RaiseOperationStateChanged()
    {
        this.RaisePropertyChanged(nameof(CanRunRepositoryCommand));
        this.RaisePropertyChanged(nameof(CanOpenDiffFileInExternalEditor));
        RaiseCommitStateChanged();
        RaiseBranchStateChanged();
        RaiseSyncStateChanged();
        RaiseStashStateChanged();
    }

    private void RaiseCommitStateChanged()
    {
        this.RaisePropertyChanged(nameof(CanCommit));
        this.RaisePropertyChanged(nameof(CanCopySelectedCommitSha));
        this.RaisePropertyChanged(nameof(CanViewSelectedCommitOnGitHub));
        this.RaisePropertyChanged(nameof(CommitButtonText));
        this.RaisePropertyChanged(nameof(SelectedChangesStatusText));
    }

    private void RaiseLocalizedDerivedText()
    {
        this.RaisePropertyChanged(nameof(CommitButtonText));
        this.RaisePropertyChanged(nameof(ChangedFilesHeaderText));
        this.RaisePropertyChanged(nameof(FilteredChangedFilesCount));
        this.RaisePropertyChanged(nameof(HasActiveChangesFilter));
        this.RaisePropertyChanged(nameof(SelectedChangesStatusText));
        this.RaisePropertyChanged(nameof(HistoryHeaderText));
        this.RaisePropertyChanged(nameof(SelectedCommitChangedFilesHeader));
        this.RaisePropertyChanged(nameof(BranchesHeaderText));
        this.RaisePropertyChanged(nameof(StashAllChangesButtonText));
        this.RaisePropertyChanged(nameof(StashDescriptionText));
        this.RaisePropertyChanged(nameof(RepositorySelectorTitle));
        this.RaisePropertyChanged(nameof(RepositorySelectorDetail));
        this.RaisePropertyChanged(nameof(BinaryDiffMessage));
        this.RaisePropertyChanged(nameof(BinaryDiffOpenText));
        RaiseAccountStateChanged();
    }

    private void RaiseDiffPreviewStateChanged()
    {
        this.RaisePropertyChanged(nameof(IsTextDiffVisible));
        this.RaisePropertyChanged(nameof(IsImageDiffVisible));
        this.RaisePropertyChanged(nameof(IsBinaryDiffVisible));
        this.RaisePropertyChanged(nameof(PreviousDiffImagePath));
        this.RaisePropertyChanged(nameof(CurrentDiffImagePath));
        this.RaisePropertyChanged(nameof(CanOpenDiffFileInExternalEditor));
    }

    private void RaiseSyncStateChanged()
    {
        this.RaisePropertyChanged(nameof(HasRemote));
        this.RaisePropertyChanged(nameof(CanSynchronize));
        this.RaisePropertyChanged(nameof(CanPublishCurrentBranch));
        this.RaisePropertyChanged(nameof(SyncActionTitle));
        this.RaisePropertyChanged(nameof(SyncActionDescription));
        this.RaisePropertyChanged(nameof(SyncActionIcon));
    }

    private void ShowChanges()
    {
        SelectedSection = RepositorySection.Changes;
        QueueDiffLoad();
    }

    private void ShowHistory()
    {
        SelectedSection = RepositorySection.History;
        QueueDiffLoad();
    }

    private void RaiseSectionStateChanged()
    {
        this.RaisePropertyChanged(nameof(IsChangesSelected));
        this.RaisePropertyChanged(nameof(IsHistorySelected));
        this.RaisePropertyChanged(nameof(ChangesTabBackground));
        this.RaisePropertyChanged(nameof(HistoryTabBackground));
        this.RaisePropertyChanged(nameof(ChangesTabForeground));
        this.RaisePropertyChanged(nameof(HistoryTabForeground));
    }

    private void RaiseBranchStateChanged()
    {
        this.RaisePropertyChanged(nameof(CanCheckoutBranch));
        this.RaisePropertyChanged(nameof(CanCreateBranch));
        this.RaisePropertyChanged(nameof(CanUpdateFromDefaultBranch));
        this.RaisePropertyChanged(nameof(UpdateFromDefaultBranchMenuText));
        this.RaisePropertyChanged(nameof(CanMergeBranch));
        this.RaisePropertyChanged(nameof(CanRebaseBranch));
        this.RaisePropertyChanged(nameof(FilteredBranchesCount));
        this.RaisePropertyChanged(nameof(HasActiveBranchFilter));
        this.RaisePropertyChanged(nameof(HasNoFilteredBranches));
        this.RaisePropertyChanged(nameof(BranchesHeaderText));
    }

    private void RaiseStashStateChanged()
    {
        this.RaisePropertyChanged(nameof(HasCurrentBranchStash));
        this.RaisePropertyChanged(nameof(CanStashChanges));
        this.RaisePropertyChanged(nameof(CanRestoreStash));
        this.RaisePropertyChanged(nameof(CanDiscardStash));
        this.RaisePropertyChanged(nameof(CanDiscardChanges));
        this.RaisePropertyChanged(nameof(StashAllChangesButtonText));
        this.RaisePropertyChanged(nameof(StashDescriptionText));
    }

    private void RaiseDefaultBranchStateChanged()
    {
        this.RaisePropertyChanged(nameof(CanUpdateFromDefaultBranch));
        this.RaisePropertyChanged(nameof(UpdateFromDefaultBranchMenuText));
    }

    private void RaiseAccountStateChanged()
    {
        this.RaisePropertyChanged(nameof(IsSignedIn));
        this.RaisePropertyChanged(nameof(IsSignedOut));
        this.RaisePropertyChanged(nameof(AccountButtonText));
        this.RaisePropertyChanged(nameof(AccountNameText));
        this.RaisePropertyChanged(nameof(AccountEndpointText));
        this.RaisePropertyChanged(nameof(AccountInitials));
    }

    private async Task RenameBranchAsync(GitBranchItemViewModel branch)
    {
        if (!HasRepository || !CanRunRepositoryCommand || !branch.CanRename)
        {
            return;
        }

        var request = await _branchDialogService.ShowRenameBranchDialogAsync(
            branch.Name,
            Branches.Select(item => item.Branch).ToArray());
        if (request is null)
        {
            return;
        }

        IsUpdatingBranch = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(
            _localizer.Format(AvaGithubDesktopL.StatusRenamingBranchFormat, request.OldBranchName, request.NewBranchName)));

        try
        {
            await _gitRepositoryService.RenameBranchAsync(
                RootPath,
                request.OldBranchName,
                request.NewBranchName,
                CancellationToken.None);
            await ReloadRepositoryWorkspaceAsync();
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Format(AvaGithubDesktopL.StatusRenamedBranchFormat, request.OldBranchName, request.NewBranchName)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusRenameBranchFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsUpdatingBranch = false;
        }
    }

    private async Task DeleteBranchAsync(GitBranchItemViewModel branch)
    {
        if (!HasRepository || !CanRunRepositoryCommand || !branch.CanDelete)
        {
            return;
        }

        var confirmed = await _branchDialogService.ShowDeleteBranchConfirmationAsync(branch.Name);
        if (!confirmed)
        {
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusDeleteBranchCanceled)));
            return;
        }

        IsUpdatingBranch = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(
            _localizer.Format(AvaGithubDesktopL.StatusDeletingBranchFormat, branch.Name)));

        try
        {
            await _gitRepositoryService.DeleteBranchAsync(RootPath, branch.Name, CancellationToken.None);
            await ReloadRepositoryWorkspaceAsync();
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Format(AvaGithubDesktopL.StatusDeletedBranchFormat, branch.Name)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusDeleteBranchFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsUpdatingBranch = false;
        }
    }

    private async Task UpdateFromDefaultBranchAsync()
    {
        if (!CanUpdateFromDefaultBranch)
        {
            return;
        }

        var defaultBranch = DefaultBranch;
        IsUpdatingBranch = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(
            _localizer.Format(AvaGithubDesktopL.StatusUpdatingFromDefaultBranchFormat, defaultBranch, CurrentBranch)));

        try
        {
            var result = await _gitRepositoryService.MergeBranchAsync(
                RootPath,
                defaultBranch,
                CancellationToken.None);
            await ReloadRepositoryWorkspaceAsync();
            var messageKey = result == GitMergeResult.AlreadyUpToDate
                ? AvaGithubDesktopL.StatusUpdateFromDefaultBranchAlreadyUpToDateFormat
                : AvaGithubDesktopL.StatusUpdatedFromDefaultBranchFormat;
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Format(messageKey, defaultBranch, CurrentBranch)));
        }
        catch (Exception ex)
        {
            // Update from default branch 底层是一次普通 merge；冲突时保留 Git 状态并刷新 Changes 方便继续解决。
            await TryReloadRepositoryWorkspaceAsync();
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusUpdateFromDefaultBranchFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsUpdatingBranch = false;
        }
    }

    private async Task MergeBranchAsync()
    {
        if (!CanMergeBranch)
        {
            return;
        }

        var request = await _branchDialogService.ShowMergeBranchDialogAsync(
            CurrentBranch,
            Branches.Select(branch => branch.Branch).ToArray());
        if (request is null)
        {
            return;
        }

        IsMergingBranch = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(
            _localizer.Format(AvaGithubDesktopL.StatusMergingBranchFormat, request.SourceBranchName, CurrentBranch)));

        try
        {
            var result = await _gitRepositoryService.MergeBranchAsync(
                RootPath,
                request.SourceBranchName,
                CancellationToken.None);
            await ReloadRepositoryWorkspaceAsync();
            var messageKey = result == GitMergeResult.AlreadyUpToDate
                ? AvaGithubDesktopL.StatusMergeBranchAlreadyUpToDateFormat
                : AvaGithubDesktopL.StatusMergedBranchFormat;
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Format(messageKey, request.SourceBranchName, CurrentBranch)));
        }
        catch (Exception ex)
        {
            // merge 冲突会让 Git 留在合并状态；刷新工作区可以立即展示冲突文件，便于后续继续处理。
            await TryReloadRepositoryWorkspaceAsync();
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusMergeBranchFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsMergingBranch = false;
        }
    }

    private async Task SquashMergeBranchAsync()
    {
        if (!CanMergeBranch)
        {
            return;
        }

        var request = await _branchDialogService.ShowSquashMergeBranchDialogAsync(
            CurrentBranch,
            Branches.Select(branch => branch.Branch).ToArray());
        if (request is null)
        {
            return;
        }

        IsMergingBranch = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(
            _localizer.Format(AvaGithubDesktopL.StatusSquashMergingBranchFormat, request.SourceBranchName, CurrentBranch)));

        try
        {
            var result = await _gitRepositoryService.SquashMergeBranchAsync(
                RootPath,
                request.SourceBranchName,
                CancellationToken.None);
            await ReloadRepositoryWorkspaceAsync();
            var messageKey = result == GitMergeResult.AlreadyUpToDate
                ? AvaGithubDesktopL.StatusSquashMergeBranchAlreadyUpToDateFormat
                : AvaGithubDesktopL.StatusSquashMergedBranchFormat;
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Format(messageKey, request.SourceBranchName, CurrentBranch)));
        }
        catch (Exception ex)
        {
            // squash merge 失败后可能留下 SQUASH_MSG 或冲突文件，刷新工作区可以让用户继续处理当前 Git 状态。
            await TryReloadRepositoryWorkspaceAsync();
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusSquashMergeBranchFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsMergingBranch = false;
        }
    }

    private async Task RebaseBranchAsync()
    {
        if (!CanRebaseBranch)
        {
            return;
        }

        var request = await _branchDialogService.ShowRebaseBranchDialogAsync(
            CurrentBranch,
            Branches.Select(branch => branch.Branch).ToArray());
        if (request is null)
        {
            return;
        }

        IsRebasingBranch = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(
            _localizer.Format(AvaGithubDesktopL.StatusRebasingBranchFormat, CurrentBranch, request.BaseBranchName)));

        try
        {
            var result = await _gitRepositoryService.RebaseCurrentBranchAsync(
                RootPath,
                request.BaseBranchName,
                CancellationToken.None);
            await ReloadRepositoryWorkspaceAsync();
            var messageKey = result == GitRebaseResult.AlreadyUpToDate
                ? AvaGithubDesktopL.StatusRebaseBranchAlreadyUpToDateFormat
                : AvaGithubDesktopL.StatusRebasedBranchFormat;
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Format(messageKey, CurrentBranch, request.BaseBranchName)));
        }
        catch (Exception ex)
        {
            // rebase 冲突会让 Git 留在变基状态；刷新工作区后用户可以在 Changes 中继续处理冲突文件。
            await TryReloadRepositoryWorkspaceAsync();
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusRebaseBranchFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsRebasingBranch = false;
        }
    }

    private async Task CreateBranchAsync()
    {
        if (!CanCreateBranch)
        {
            return;
        }

        var request = await _branchDialogService.ShowCreateBranchDialogAsync(
            CurrentBranch,
            Branches.Select(branch => branch.Branch).ToArray(),
            BranchFilterText);
        if (request is null)
        {
            return;
        }

        IsCreatingBranch = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(
            _localizer.Format(AvaGithubDesktopL.StatusCreatingBranchFormat, request.BranchName, CurrentBranch)));

        try
        {
            await _gitRepositoryService.CreateBranchAsync(
                RootPath,
                request.BranchName,
                request.StartPoint,
                request.CheckoutBranch,
                CancellationToken.None);

            BranchFilterText = string.Empty;
            var snapshot = await _gitRepositoryService.LoadRepositoryAsync(RootPath, CancellationToken.None);
            ApplySnapshot(snapshot);
            var branches = await _gitRepositoryService.LoadBranchesAsync(snapshot.RootPath, CancellationToken.None);
            ApplyBranches(branches);
            var history = await _gitRepositoryService.LoadHistoryAsync(snapshot.RootPath, HistoryCommitLimit, CancellationToken.None);
            ApplyHistory(history);
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Format(AvaGithubDesktopL.StatusCreatedBranchFormat, request.BranchName)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusCreateBranchFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsCreatingBranch = false;
        }
    }

    private async Task CheckoutSelectedBranchAsync()
    {
        if (!CanCheckoutBranch || SelectedBranch is null)
        {
            return;
        }

        var targetBranch = SelectedBranch.Name;
        IsCheckingOutBranch = true;
        ErrorMessage = string.Empty;
        _eventBus.Publish(new StatusMessageChangedCommand(
            _localizer.Format(AvaGithubDesktopL.StatusCheckingOutBranchFormat, targetBranch)));

        try
        {
            await _gitRepositoryService.CheckoutBranchAsync(RootPath, targetBranch, CancellationToken.None);
            var snapshot = await _gitRepositoryService.LoadRepositoryAsync(RootPath, CancellationToken.None);
            ApplySnapshot(snapshot);
            var branches = await _gitRepositoryService.LoadBranchesAsync(snapshot.RootPath, CancellationToken.None);
            ApplyBranches(branches);
            var history = await _gitRepositoryService.LoadHistoryAsync(snapshot.RootPath, HistoryCommitLimit, CancellationToken.None);
            ApplyHistory(history);
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Format(AvaGithubDesktopL.StatusCheckedOutBranchFormat, targetBranch)));
        }
        catch (Exception ex)
        {
            ErrorMessage = _localizer.Format(AvaGithubDesktopL.StatusCheckoutBranchFailedFormat, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(ErrorMessage));
        }
        finally
        {
            IsCheckingOutBranch = false;
        }
    }

    private void QueueDiffLoad()
    {
        _ = LoadSelectedDiffAsync();
    }

    private async Task LoadSelectedDiffAsync()
    {
        var requestVersion = ++_diffRequestVersion;
        IsDiffLoading = true;
        DiffPreview = GitFileDiffPreview.TextDiff(_localizer.Get(AvaGithubDesktopL.LoadingDiff));

        try
        {
            var (title, preview) = IsHistorySelected
                ? await LoadSelectedHistoryDiffAsync()
                : await LoadSelectedWorkingTreeDiffAsync();

            if (requestVersion != _diffRequestVersion)
            {
                return;
            }

            DiffTitle = title;
            DiffPreview = NormalizeDiffPreview(preview);
        }
        catch (Exception ex)
        {
            if (requestVersion == _diffRequestVersion)
            {
                DiffPreview = GitFileDiffPreview.TextDiff(
                    _localizer.Format(AvaGithubDesktopL.StatusRepositoryLoadFailedFormat, ex.Message));
            }
        }
        finally
        {
            if (requestVersion == _diffRequestVersion)
            {
                IsDiffLoading = false;
            }
        }
    }

    private async Task<(string Title, GitFileDiffPreview Preview)> LoadSelectedWorkingTreeDiffAsync()
    {
        if (SelectedChange is null || !HasRepository)
        {
            var message = _localizer.Get(AvaGithubDesktopL.NoFileSelected);
            return (message, GitFileDiffPreview.TextDiff(message));
        }

        var preview = await _gitRepositoryService.LoadWorkingTreeDiffPreviewAsync(
            RootPath,
            SelectedChange.GitPaths,
            CancellationToken.None);
        return (SelectedChange.Path, preview);
    }

    private async Task<(string Title, GitFileDiffPreview Preview)> LoadSelectedHistoryDiffAsync()
    {
        if (SelectedCommit is null || SelectedCommitFile is null || !HasRepository)
        {
            var message = _localizer.Get(AvaGithubDesktopL.NoFileSelected);
            return (message, GitFileDiffPreview.TextDiff(message));
        }

        var preview = await _gitRepositoryService.LoadCommitFileDiffPreviewAsync(
            RootPath,
            SelectedCommit.Sha,
            SelectedCommitFile.GitPaths,
            CancellationToken.None);
        return (SelectedCommitFile.Path, preview);
    }

    private GitFileDiffPreview NormalizeDiffPreview(GitFileDiffPreview preview)
    {
        if (preview.Kind == GitDiffPreviewKind.Text && string.IsNullOrWhiteSpace(preview.Text))
        {
            return GitFileDiffPreview.TextDiff(_localizer.Get(AvaGithubDesktopL.NoDiffAvailable));
        }

        if (preview.Kind == GitDiffPreviewKind.Image
            && string.IsNullOrWhiteSpace(preview.PreviousImagePath)
            && string.IsNullOrWhiteSpace(preview.CurrentImagePath))
        {
            return GitFileDiffPreview.TextDiff(_localizer.Get(AvaGithubDesktopL.NoDiffAvailable));
        }

        return preview;
    }

    private async Task OpenDiffFileInExternalEditorAsync()
    {
        if (!CanOpenDiffFileInExternalEditor || string.IsNullOrWhiteSpace(DiffPreview.WorkingTreePath))
        {
            return;
        }

        try
        {
            await _repositoryShellService.OpenItemAsync(DiffPreview.WorkingTreePath);
            _eventBus.Publish(new StatusMessageChangedCommand(
                _localizer.Get(AvaGithubDesktopL.StatusOpenedChangeInExternalEditor)));
        }
        catch (Exception ex)
        {
            var message = _localizer.Format(AvaGithubDesktopL.StatusOpenChangeInExternalEditorFailedFormat, ex.Message);
            ErrorMessage = message;
            _eventBus.Publish(new StatusMessageChangedCommand(message));
        }
    }

    private static string ResolveDefaultRepositoryPath()
    {
        const string desktopRepositoryPath = @"D:\github\desktop";
        if (OperatingSystem.IsWindows() && Directory.Exists(desktopRepositoryPath))
        {
            return desktopRepositoryPath;
        }

        return Directory.GetCurrentDirectory();
    }
}

public enum RepositorySection
{
    Changes,
    History
}

public enum RepositorySyncOperation
{
    None,
    Fetch,
    Pull,
    Publish,
    Push
}
