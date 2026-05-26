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

        _localizer.CultureChanged += (_, _) =>
        {
            UpdateAheadBehindText();
            RaiseLocalizedDerivedText();
        };
    }

    public ObservableCollection<LanguageOption> Languages { get; }

    public ObservableCollection<GitChangeItemViewModel> ChangedFiles { get; } = new();

    public ShellStatusViewModel StatusBar { get; }

    public ReactiveCommand<Unit, Unit> BrowseRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> CommitCommand { get; }

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
        }
    }

    public bool IsCommitting
    {
        get => _isCommitting;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isCommitting, value);
            RaiseCommitStateChanged();
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
            _eventBus.Publish(new RepositoryOpenedCommand(snapshot.RepositoryName, snapshot.ChangedFilesCount));
        }
        catch (Exception ex)
        {
            HasRepository = false;
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
