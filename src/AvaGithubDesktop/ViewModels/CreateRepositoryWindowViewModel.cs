using System.Reactive;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class CreateRepositoryWindowViewModel : ViewModelBase
{
    private readonly IAppLocalizer _localizer;
    private readonly Func<Task<string?>> _pickParentDirectory;
    private string _parentDirectory;
    private string _repositoryName = string.Empty;
    private bool _isBrowsingParentDirectory;

    public CreateRepositoryWindowViewModel(
        IAppLocalizer localizer,
        Func<Task<string?>> pickParentDirectory,
        string initialParentDirectory)
    {
        _localizer = localizer;
        _pickParentDirectory = pickParentDirectory;
        _parentDirectory = initialParentDirectory;

        CancelCommand = ReactiveCommand.Create(() => RequestClose(null));
        BrowseParentDirectoryCommand = ReactiveCommand.CreateFromTask(BrowseParentDirectoryAsync);
        CreateCommand = ReactiveCommand.Create(
            CreateRepository,
            this.WhenAnyValue(model => model.CanCreate));
    }

    public event EventHandler<DialogCloseRequestedEventArgs<RepositoryCreationRequest?>>? CloseRequested;

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> BrowseParentDirectoryCommand { get; }

    public ReactiveCommand<Unit, Unit> CreateCommand { get; }

    public string Title => _localizer.Get(AvaGithubDesktopL.CreateRepositoryTitle);

    public string ParentDirectoryLabel => _localizer.Get(AvaGithubDesktopL.CreateRepositoryParentDirectoryLabel);

    public string RepositoryNameLabel => _localizer.Get(AvaGithubDesktopL.CreateRepositoryNameLabel);

    public string DestinationPathLabel => _localizer.Get(AvaGithubDesktopL.CreateRepositoryDestinationPathLabel);

    public string BrowseButtonText => _localizer.Get(AvaGithubDesktopL.Browse);

    public string ParentDirectory
    {
        get => _parentDirectory;
        set
        {
            this.RaiseAndSetIfChanged(ref _parentDirectory, value);
            RaiseCreationStateChanged();
        }
    }

    public string RepositoryName
    {
        get => _repositoryName;
        set
        {
            this.RaiseAndSetIfChanged(ref _repositoryName, value);
            RaiseCreationStateChanged();
        }
    }

    public bool IsBrowsingParentDirectory
    {
        get => _isBrowsingParentDirectory;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isBrowsingParentDirectory, value);
            this.RaisePropertyChanged(nameof(CanCreate));
        }
    }

    public string DestinationPath
    {
        get
        {
            var parent = ParentDirectory.Trim();
            var name = RepositoryName.Trim();
            return string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name)
                ? string.Empty
                : Path.Combine(parent, name);
        }
    }

    public string ValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ParentDirectory))
            {
                return _localizer.Get(AvaGithubDesktopL.CreateRepositoryParentDirectoryRequired);
            }

            if (!Directory.Exists(ParentDirectory.Trim()))
            {
                return _localizer.Get(AvaGithubDesktopL.CreateRepositoryParentDirectoryMissing);
            }

            if (string.IsNullOrWhiteSpace(RepositoryName))
            {
                return _localizer.Get(AvaGithubDesktopL.CreateRepositoryNameRequired);
            }

            var normalizedName = RepositoryName.Trim();
            if (normalizedName is "." or ".." || normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return _localizer.Get(AvaGithubDesktopL.CreateRepositoryNameInvalid);
            }

            if (Directory.Exists(DestinationPath) || File.Exists(DestinationPath))
            {
                return _localizer.Get(AvaGithubDesktopL.CreateRepositoryDestinationExists);
            }

            return string.Empty;
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool CanCreate => !IsBrowsingParentDirectory && !HasValidationMessage;

    private async Task BrowseParentDirectoryAsync()
    {
        IsBrowsingParentDirectory = true;
        try
        {
            var selectedPath = await _pickParentDirectory();
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                ParentDirectory = selectedPath;
            }
        }
        finally
        {
            IsBrowsingParentDirectory = false;
        }
    }

    private void CreateRepository()
    {
        RequestClose(new RepositoryCreationRequest(DestinationPath));
    }

    private void RequestClose(RepositoryCreationRequest? request)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<RepositoryCreationRequest?>(request));
    }

    private void RaiseCreationStateChanged()
    {
        this.RaisePropertyChanged(nameof(DestinationPath));
        this.RaisePropertyChanged(nameof(ValidationMessage));
        this.RaisePropertyChanged(nameof(HasValidationMessage));
        this.RaisePropertyChanged(nameof(CanCreate));
    }
}
