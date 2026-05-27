using System.Reactive;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class CloneRepositoryWindowViewModel : ViewModelBase
{
    private readonly IAppLocalizer _localizer;
    private readonly Func<Task<string?>> _pickParentDirectory;
    private string _sourceUrl = string.Empty;
    private string _parentDirectory;
    private string _repositoryName = string.Empty;
    private bool _repositoryNameWasEdited;
    private bool _isBrowsingParentDirectory;

    public CloneRepositoryWindowViewModel(
        IAppLocalizer localizer,
        Func<Task<string?>> pickParentDirectory,
        string initialParentDirectory)
    {
        _localizer = localizer;
        _pickParentDirectory = pickParentDirectory;
        _parentDirectory = initialParentDirectory;

        CancelCommand = ReactiveCommand.Create(() => RequestClose(null));
        BrowseParentDirectoryCommand = ReactiveCommand.CreateFromTask(BrowseParentDirectoryAsync);
        CloneCommand = ReactiveCommand.Create(
            CloneRepository,
            this.WhenAnyValue(model => model.CanClone));
    }

    public event EventHandler<DialogCloseRequestedEventArgs<RepositoryCloneRequest?>>? CloseRequested;

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> BrowseParentDirectoryCommand { get; }

    public ReactiveCommand<Unit, Unit> CloneCommand { get; }

    public string Title => _localizer.Get(AvaGithubDesktopL.CloneRepositoryTitle);

    public string SourceUrlLabel => _localizer.Get(AvaGithubDesktopL.CloneRepositorySourceUrlLabel);

    public string ParentDirectoryLabel => _localizer.Get(AvaGithubDesktopL.CloneRepositoryParentDirectoryLabel);

    public string RepositoryNameLabel => _localizer.Get(AvaGithubDesktopL.CloneRepositoryNameLabel);

    public string DestinationPathLabel => _localizer.Get(AvaGithubDesktopL.CloneRepositoryDestinationPathLabel);

    public string BrowseButtonText => _localizer.Get(AvaGithubDesktopL.Browse);

    public string SourceUrl
    {
        get => _sourceUrl;
        set
        {
            this.RaiseAndSetIfChanged(ref _sourceUrl, value);
            if (!_repositoryNameWasEdited)
            {
                var inferredName = InferRepositoryName(value);
                this.RaiseAndSetIfChanged(ref _repositoryName, inferredName, nameof(RepositoryName));
            }

            RaiseCloneStateChanged();
        }
    }

    public string ParentDirectory
    {
        get => _parentDirectory;
        set
        {
            this.RaiseAndSetIfChanged(ref _parentDirectory, value);
            RaiseCloneStateChanged();
        }
    }

    public string RepositoryName
    {
        get => _repositoryName;
        set
        {
            _repositoryNameWasEdited = true;
            this.RaiseAndSetIfChanged(ref _repositoryName, value);
            RaiseCloneStateChanged();
        }
    }

    public bool IsBrowsingParentDirectory
    {
        get => _isBrowsingParentDirectory;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isBrowsingParentDirectory, value);
            this.RaisePropertyChanged(nameof(CanClone));
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
            if (string.IsNullOrWhiteSpace(SourceUrl))
            {
                return _localizer.Get(AvaGithubDesktopL.CloneRepositorySourceUrlRequired);
            }

            if (SourceUrl.Any(char.IsWhiteSpace))
            {
                return _localizer.Get(AvaGithubDesktopL.CloneRepositorySourceUrlInvalid);
            }

            if (string.IsNullOrWhiteSpace(ParentDirectory))
            {
                return _localizer.Get(AvaGithubDesktopL.CloneRepositoryParentDirectoryRequired);
            }

            if (!Directory.Exists(ParentDirectory.Trim()))
            {
                return _localizer.Get(AvaGithubDesktopL.CloneRepositoryParentDirectoryMissing);
            }

            if (string.IsNullOrWhiteSpace(RepositoryName))
            {
                return _localizer.Get(AvaGithubDesktopL.CloneRepositoryNameRequired);
            }

            var normalizedName = RepositoryName.Trim();
            if (normalizedName is "." or ".." || normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return _localizer.Get(AvaGithubDesktopL.CloneRepositoryNameInvalid);
            }

            if (Directory.Exists(DestinationPath) || File.Exists(DestinationPath))
            {
                return _localizer.Get(AvaGithubDesktopL.CloneRepositoryDestinationExists);
            }

            return string.Empty;
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool CanClone => !IsBrowsingParentDirectory && !HasValidationMessage;

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

    private void CloneRepository()
    {
        RequestClose(new RepositoryCloneRequest(SourceUrl.Trim(), DestinationPath));
    }

    private void RequestClose(RepositoryCloneRequest? request)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<RepositoryCloneRequest?>(request));
    }

    private void RaiseCloneStateChanged()
    {
        this.RaisePropertyChanged(nameof(DestinationPath));
        this.RaisePropertyChanged(nameof(ValidationMessage));
        this.RaisePropertyChanged(nameof(HasValidationMessage));
        this.RaisePropertyChanged(nameof(CanClone));
    }

    private static string InferRepositoryName(string sourceUrl)
    {
        var value = sourceUrl.Trim().TrimEnd('/', '\\');
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            value = uri.AbsolutePath.TrimEnd('/');
        }

        var separatorIndex = value.LastIndexOfAny(['/', '\\', ':']);
        var name = separatorIndex >= 0 ? value[(separatorIndex + 1)..] : value;
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return name.Trim();
    }
}
