using System.Reactive;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class OpenRepositoryWindowViewModel : ViewModelBase
{
    private readonly IAppLocalizer _localizer;
    private readonly Func<Task<string?>> _pickRepository;
    private string _repositoryPath;
    private bool _isBrowsingRepository;

    public OpenRepositoryWindowViewModel(
        IAppLocalizer localizer,
        Func<Task<string?>> pickRepository,
        string initialPath)
    {
        _localizer = localizer;
        _pickRepository = pickRepository;
        _repositoryPath = string.IsNullOrWhiteSpace(initialPath) ? string.Empty : initialPath.Trim();

        CancelCommand = ReactiveCommand.Create(() => RequestClose(null));
        BrowseRepositoryCommand = ReactiveCommand.CreateFromTask(BrowseRepositoryAsync);
        OpenCommand = ReactiveCommand.Create(
            OpenRepository,
            this.WhenAnyValue(model => model.CanOpen));
    }

    public event EventHandler<DialogCloseRequestedEventArgs<RepositoryOpenRequest?>>? CloseRequested;

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> BrowseRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    public string Title => _localizer.Get(AvaGithubDesktopL.AddExistingRepositoryTitle);

    public string RepositoryPathLabel => _localizer.Get(AvaGithubDesktopL.AddExistingRepositoryPathLabel);

    public string BrowseButtonText => _localizer.Get(AvaGithubDesktopL.Browse);

    public string RepositoryPath
    {
        get => _repositoryPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _repositoryPath, value);
            RaiseOpenStateChanged();
        }
    }

    public bool IsBrowsingRepository
    {
        get => _isBrowsingRepository;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isBrowsingRepository, value);
            this.RaisePropertyChanged(nameof(CanOpen));
        }
    }

    public string ValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RepositoryPath))
            {
                return _localizer.Get(AvaGithubDesktopL.AddExistingRepositoryPathRequired);
            }

            if (!Directory.Exists(RepositoryPath.Trim()))
            {
                return _localizer.Get(AvaGithubDesktopL.AddExistingRepositoryPathMissing);
            }

            return string.Empty;
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool CanOpen => !IsBrowsingRepository && !HasValidationMessage;

    private async Task BrowseRepositoryAsync()
    {
        IsBrowsingRepository = true;
        try
        {
            var selectedPath = await _pickRepository();
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                RepositoryPath = selectedPath;
            }
        }
        finally
        {
            IsBrowsingRepository = false;
        }
    }

    private void OpenRepository()
    {
        RequestClose(new RepositoryOpenRequest(RepositoryPath.Trim()));
    }

    private void RequestClose(RepositoryOpenRequest? request)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<RepositoryOpenRequest?>(request));
    }

    private void RaiseOpenStateChanged()
    {
        this.RaisePropertyChanged(nameof(ValidationMessage));
        this.RaisePropertyChanged(nameof(HasValidationMessage));
        this.RaisePropertyChanged(nameof(CanOpen));
    }
}
