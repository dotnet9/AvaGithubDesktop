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
    private readonly IAppLocalizer _localizer;
    private readonly IEventBus _eventBus;
    private string _repositoryPath;
    private string _repositoryName = "-";
    private string _rootPath = "-";
    private string _currentBranch = "-";
    private string _upstream = "-";
    private string _remoteUrl = "-";
    private string _lastCommit = "-";
    private string _aheadBehindText = "-";
    private string _errorMessage = string.Empty;
    private string _commitSummary = string.Empty;
    private string _commitDescription = string.Empty;
    private bool _hasRepository;
    private bool _isLoading;
    private bool _isCommitting;
    private bool _isCheckingOutBranch;
    private bool _isInitialized;
    private bool _isBulkUpdatingIncludedChanges;
    private int _changedFilesCount;
    private int _stagedCount;
    private int _unstagedCount;
    private int _untrackedCount;
    private int _includedChangesCount;
    private int _ahead;
    private int _behind;
    private bool? _areAllChangesIncluded = false;
    private LanguageOption? _selectedLanguage;
    private GitChangeItemViewModel? _selectedChange;
    private GitCommitItem? _selectedCommit;
    private GitCommitFileItem? _selectedCommitFile;
    private GitBranchItem? _selectedBranch;
    private RepositorySection _selectedSection = RepositorySection.Changes;
    private int _historyCommitCount;
    private int _diffRequestVersion;
    private bool _isDiffLoading;
    private string _diffTitle = string.Empty;
    private string _diffText = string.Empty;
    private CompositeDisposable _changeSubscriptions = new();

    public MainWindowViewModel(
        IGitRepositoryService gitRepositoryService,
        IRepositoryPickerService repositoryPickerService,
        IAppLocalizer localizer,
        IEventBus eventBus,
        ShellStatusViewModel statusBar)
    {
        _gitRepositoryService = gitRepositoryService;
        _repositoryPickerService = repositoryPickerService;
        _localizer = localizer;
        _eventBus = eventBus;
        StatusBar = statusBar;
        _repositoryPath = ResolveDefaultRepositoryPath();

        Languages = new ObservableCollection<LanguageOption>
        {
            new("zh-CN", _localizer.Get(AvaGithubDesktopL.SimplifiedChinese)),
            new("en-US", _localizer.Get(AvaGithubDesktopL.English))
        };
        _selectedLanguage = Languages.FirstOrDefault(option => option.CultureName == _localizer.Culture.Name) ?? Languages[0];

        var canExecuteRepositoryCommand = this.WhenAnyValue(
            model => model.IsLoading,
            model => model.IsCommitting,
            (loading, committing) => !loading && !committing);
        BrowseRepositoryCommand = ReactiveCommand.CreateFromTask(BrowseRepositoryAsync, canExecuteRepositoryCommand);
        OpenRepositoryCommand = ReactiveCommand.CreateFromTask(OpenRepositoryAsync, canExecuteRepositoryCommand);
        RefreshRepositoryCommand = ReactiveCommand.CreateFromTask(OpenRepositoryAsync, canExecuteRepositoryCommand);
        ShowChangesCommand = ReactiveCommand.Create(ShowChanges);
        ShowHistoryCommand = ReactiveCommand.Create(ShowHistory);

        var canCommit = this.WhenAnyValue(
            model => model.HasRepository,
            model => model.IsLoading,
            model => model.IsCommitting,
            model => model.CommitSummary,
            model => model.IncludedChangesCount,
            (hasRepository, isLoading, isCommitting, summary, includedChangesCount) =>
                hasRepository &&
                !isLoading &&
                !isCommitting &&
                includedChangesCount > 0 &&
                !string.IsNullOrWhiteSpace(summary));
        CommitCommand = ReactiveCommand.CreateFromTask(CommitChangesAsync, canCommit);

        var canCheckoutBranch = this.WhenAnyValue(
            model => model.HasRepository,
            model => model.IsLoading,
            model => model.IsCommitting,
            model => model.IsCheckingOutBranch,
            model => model.SelectedBranch,
            (hasRepository, isLoading, isCommitting, isCheckingOutBranch, selectedBranch) =>
                hasRepository &&
                !isLoading &&
                !isCommitting &&
                !isCheckingOutBranch &&
                selectedBranch is not null &&
                !selectedBranch.IsCurrent);
        CheckoutBranchCommand = ReactiveCommand.CreateFromTask(CheckoutSelectedBranchAsync, canCheckoutBranch);

        _localizer.CultureChanged += (_, _) =>
        {
            UpdateAheadBehindText();
            RaiseLocalizedDerivedText();
            QueueDiffLoad();
        };
    }

    public ObservableCollection<LanguageOption> Languages { get; }

    public ObservableCollection<GitChangeItemViewModel> ChangedFiles { get; } = new();

    public ObservableCollection<GitCommitItem> HistoryCommits { get; } = new();

    public ObservableCollection<GitBranchItem> Branches { get; } = new();

    public ShellStatusViewModel StatusBar { get; }

    public ReactiveCommand<Unit, Unit> BrowseRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> CommitCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowChangesCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowHistoryCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckoutBranchCommand { get; }

    public string RepositoryPath
    {
        get => _repositoryPath;
        set => this.RaiseAndSetIfChanged(ref _repositoryPath, value);
    }

    public string RepositoryName
    {
        get => _repositoryName;
        private set => this.RaiseAndSetIfChanged(ref _repositoryName, value);
    }

    public string RootPath
    {
        get => _rootPath;
        private set => this.RaiseAndSetIfChanged(ref _rootPath, value);
    }

    public string CurrentBranch
    {
        get => _currentBranch;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentBranch, value);
            this.RaisePropertyChanged(nameof(CommitButtonText));
        }
    }

    public string Upstream
    {
        get => _upstream;
        private set => this.RaiseAndSetIfChanged(ref _upstream, value);
    }

    public string RemoteUrl
    {
        get => _remoteUrl;
        private set => this.RaiseAndSetIfChanged(ref _remoteUrl, value);
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

    public bool HasRepository
    {
        get => _hasRepository;
        private set
        {
            this.RaiseAndSetIfChanged(ref _hasRepository, value);
            this.RaisePropertyChanged(nameof(ShowEmptyRepository));
            RaiseCommitStateChanged();
            RaiseBranchStateChanged();
        }
    }

    public bool ShowEmptyRepository => !HasRepository && !IsLoading;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isLoading, value);
            this.RaisePropertyChanged(nameof(ShowEmptyRepository));
            RaiseCommitStateChanged();
            RaiseBranchStateChanged();
        }
    }

    public bool IsCommitting
    {
        get => _isCommitting;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isCommitting, value);
            RaiseCommitStateChanged();
            RaiseBranchStateChanged();
        }
    }

    public bool IsCheckingOutBranch
    {
        get => _isCheckingOutBranch;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isCheckingOutBranch, value);
            RaiseBranchStateChanged();
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

    public string ChangesTabBackground => IsChangesSelected ? "#FFFFFF" : "#F1F4F7";

    public string HistoryTabBackground => IsHistorySelected ? "#FFFFFF" : "#F1F4F7";

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
            this.RaiseAndSetIfChanged(ref _selectedCommit, value);
            this.RaisePropertyChanged(nameof(HasSelectedCommit));
            this.RaisePropertyChanged(nameof(SelectedCommitChangedFilesHeader));
            if (value is null)
            {
                SelectedCommitFile = null;
                return;
            }

            if (SelectedCommitFile is null || !value.Files.Any(file => file.Path == SelectedCommitFile.Path))
            {
                SelectedCommitFile = value.Files.FirstOrDefault();
            }
            else if (IsHistorySelected)
            {
                QueueDiffLoad();
            }
        }
    }

    public bool HasSelectedCommit => SelectedCommit is not null;

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

    public GitBranchItem? SelectedBranch
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
        !IsLoading &&
        !IsCommitting &&
        !IsCheckingOutBranch &&
        SelectedBranch is not null &&
        !SelectedBranch.IsCurrent;

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

    public GitCommitFileItem? SelectedCommitFile
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
        !IsLoading &&
        !IsCommitting &&
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
            var formatKey = ChangedFilesCount == 1
                ? AvaGithubDesktopL.ChangedFileCountFormat
                : AvaGithubDesktopL.ChangedFilesCountFormat;
            return _localizer.Format(formatKey, ChangedFilesCount);
        }
    }

    public string SelectedChangesStatusText =>
        _localizer.Format(AvaGithubDesktopL.SelectedFilesCountFormat, IncludedChangesCount, ChangedFilesCount);

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
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(AvaGithubDesktopL.StatusReady)));
        }
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
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
            _eventBus.Publish(new RepositoryOpenedCommand(snapshot.RepositoryName, snapshot.ChangedFilesCount));
        }
        catch (Exception ex)
        {
            HasRepository = false;
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

    private void ApplySnapshot(GitRepositorySnapshot snapshot)
    {
        HasRepository = true;
        RepositoryName = snapshot.RepositoryName;
        RootPath = snapshot.RootPath;
        CurrentBranch = snapshot.CurrentBranch;
        Upstream = snapshot.Upstream;
        RemoteUrl = snapshot.RemoteUrl;
        LastCommit = snapshot.LastCommit;
        ChangedFilesCount = snapshot.ChangedFilesCount;
        StagedCount = snapshot.StagedCount;
        UnstagedCount = snapshot.UnstagedCount;
        UntrackedCount = snapshot.UntrackedCount;
        _ahead = snapshot.Ahead;
        _behind = snapshot.Behind;
        UpdateAheadBehindText();

        var selectedPath = SelectedChange?.Path;
        _changeSubscriptions.Dispose();
        _changeSubscriptions = new CompositeDisposable();
        ChangedFiles.Clear();
        foreach (var change in snapshot.Changes)
        {
            var changeViewModel = new GitChangeItemViewModel(change);
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
        SelectedChange = !string.IsNullOrWhiteSpace(selectedPath)
            ? ChangedFiles.FirstOrDefault(change => change.Path == selectedPath) ?? ChangedFiles.FirstOrDefault()
            : ChangedFiles.FirstOrDefault();
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

    private void ApplyBranches(IReadOnlyList<GitBranchItem> branches)
    {
        var selectedName = SelectedBranch?.Name;
        Branches.Clear();
        foreach (var branch in branches)
        {
            Branches.Add(branch);
        }

        SelectedBranch = Branches.FirstOrDefault(branch => branch.IsCurrent)
            ?? (!string.IsNullOrWhiteSpace(selectedName)
                ? Branches.FirstOrDefault(branch => branch.Name == selectedName)
                : null)
            ?? Branches.FirstOrDefault();
    }

    private void UpdateAheadBehindText()
    {
        AheadBehindText = HasRepository
            ? _localizer.Format(AvaGithubDesktopL.AheadBehindFormat, _ahead, _behind)
            : "-";
    }

    private void SetAllChangesIncluded(bool include)
    {
        _isBulkUpdatingIncludedChanges = true;
        try
        {
            foreach (var change in ChangedFiles)
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

    private void UpdateIncludedState()
    {
        IncludedChangesCount = ChangedFiles.Count(change => change.IsIncluded);
        var allIncluded = ChangedFiles.Count == 0
            ? false
            : IncludedChangesCount == ChangedFiles.Count
                ? true
                : IncludedChangesCount == 0
                    ? false
                    : (bool?)null;
        this.RaiseAndSetIfChanged(ref _areAllChangesIncluded, allIncluded, nameof(AreAllChangesIncluded));
    }

    private void RaiseCommitStateChanged()
    {
        this.RaisePropertyChanged(nameof(CanCommit));
        this.RaisePropertyChanged(nameof(CommitButtonText));
        this.RaisePropertyChanged(nameof(SelectedChangesStatusText));
    }

    private void RaiseLocalizedDerivedText()
    {
        this.RaisePropertyChanged(nameof(CommitButtonText));
        this.RaisePropertyChanged(nameof(ChangedFilesHeaderText));
        this.RaisePropertyChanged(nameof(SelectedChangesStatusText));
        this.RaisePropertyChanged(nameof(HistoryHeaderText));
        this.RaisePropertyChanged(nameof(SelectedCommitChangedFilesHeader));
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
        DiffText = _localizer.Get(AvaGithubDesktopL.LoadingDiff);

        try
        {
            var (title, diff) = IsHistorySelected
                ? await LoadSelectedHistoryDiffAsync()
                : await LoadSelectedWorkingTreeDiffAsync();

            if (requestVersion != _diffRequestVersion)
            {
                return;
            }

            DiffTitle = title;
            DiffText = string.IsNullOrWhiteSpace(diff)
                ? _localizer.Get(AvaGithubDesktopL.NoDiffAvailable)
                : diff;
        }
        catch (Exception ex)
        {
            if (requestVersion == _diffRequestVersion)
            {
                DiffText = _localizer.Format(AvaGithubDesktopL.StatusRepositoryLoadFailedFormat, ex.Message);
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

    private async Task<(string Title, string Diff)> LoadSelectedWorkingTreeDiffAsync()
    {
        if (SelectedChange is null || !HasRepository)
        {
            return (_localizer.Get(AvaGithubDesktopL.NoFileSelected), _localizer.Get(AvaGithubDesktopL.NoFileSelected));
        }

        var diff = await _gitRepositoryService.LoadWorkingTreeDiffAsync(
            RootPath,
            SelectedChange.GitPaths,
            CancellationToken.None);
        return (SelectedChange.Path, diff);
    }

    private async Task<(string Title, string Diff)> LoadSelectedHistoryDiffAsync()
    {
        if (SelectedCommit is null || SelectedCommitFile is null || !HasRepository)
        {
            return (_localizer.Get(AvaGithubDesktopL.NoFileSelected), _localizer.Get(AvaGithubDesktopL.NoFileSelected));
        }

        var diff = await _gitRepositoryService.LoadCommitFileDiffAsync(
            RootPath,
            SelectedCommit.Sha,
            SelectedCommitFile.GitPath,
            CancellationToken.None);
        return (SelectedCommitFile.Path, diff);
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
