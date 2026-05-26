using System.Collections.ObjectModel;
using System.Reactive;
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
    private bool _hasRepository;
    private bool _isLoading;
    private bool _isInitialized;
    private int _changedFilesCount;
    private int _stagedCount;
    private int _unstagedCount;
    private int _untrackedCount;
    private int _ahead;
    private int _behind;
    private LanguageOption? _selectedLanguage;

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

        var canExecuteRepositoryCommand = this.WhenAnyValue(model => model.IsLoading, loading => !loading);
        BrowseRepositoryCommand = ReactiveCommand.CreateFromTask(BrowseRepositoryAsync, canExecuteRepositoryCommand);
        OpenRepositoryCommand = ReactiveCommand.CreateFromTask(OpenRepositoryAsync, canExecuteRepositoryCommand);
        RefreshRepositoryCommand = ReactiveCommand.CreateFromTask(OpenRepositoryAsync, canExecuteRepositoryCommand);
        _localizer.CultureChanged += (_, _) => UpdateAheadBehindText();
    }

    public ObservableCollection<LanguageOption> Languages { get; }

    public ObservableCollection<GitChangeItem> ChangedFiles { get; } = new();

    public ShellStatusViewModel StatusBar { get; }

    public ReactiveCommand<Unit, Unit> BrowseRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshRepositoryCommand { get; }

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
        private set => this.RaiseAndSetIfChanged(ref _currentBranch, value);
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

        ChangedFiles.Clear();
        foreach (var change in snapshot.Changes)
        {
            ChangedFiles.Add(change);
        }
    }

    private void UpdateAheadBehindText()
    {
        AheadBehindText = HasRepository
            ? _localizer.Format(AvaGithubDesktopL.AheadBehindFormat, _ahead, _behind)
            : "-";
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
