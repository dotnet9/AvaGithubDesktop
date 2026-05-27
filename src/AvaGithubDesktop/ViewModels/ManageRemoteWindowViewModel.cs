using System.Reactive;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class ManageRemoteWindowViewModel : ViewModelBase
{
    private readonly IAppLocalizer _localizer;
    private readonly string _existingRemoteName;
    private string _remoteName;
    private string _remoteUrl;

    public ManageRemoteWindowViewModel(
        IAppLocalizer localizer,
        string remoteName,
        string remoteUrl,
        bool hasExistingRemote)
    {
        _localizer = localizer;
        HasExistingRemote = hasExistingRemote && !IsEmptyRemote(remoteName);
        _existingRemoteName = HasExistingRemote ? remoteName.Trim() : string.Empty;
        _remoteName = HasExistingRemote ? _existingRemoteName : "origin";
        _remoteUrl = IsEmptyRemote(remoteUrl) ? string.Empty : remoteUrl.Trim();

        CancelCommand = ReactiveCommand.Create(() => RequestClose(null));
        SaveCommand = ReactiveCommand.Create(SaveRemote, this.WhenAnyValue(model => model.CanSave));
        RemoveCommand = ReactiveCommand.Create(RemoveRemote, this.WhenAnyValue(model => model.CanRemove));
    }

    public event EventHandler<DialogCloseRequestedEventArgs<RepositoryRemoteRequest?>>? CloseRequested;

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveCommand { get; }

    public bool HasExistingRemote { get; }

    public bool IsRemoteNameReadOnly => HasExistingRemote;

    public string Title => _localizer.Get(AvaGithubDesktopL.ManageRemoteTitle);

    public string Description => _localizer.Get(AvaGithubDesktopL.ManageRemoteDescription);

    public string RemoteNameLabel => _localizer.Get(AvaGithubDesktopL.ManageRemoteNameLabel);

    public string RemoteUrlLabel => _localizer.Get(AvaGithubDesktopL.ManageRemoteUrlLabel);

    public string RemoteUrlWatermark => _localizer.Get(AvaGithubDesktopL.ManageRemoteUrlWatermark);

    public string SaveText => _localizer.Get(AvaGithubDesktopL.ManageRemoteSaveButton);

    public string RemoveText => _localizer.Get(AvaGithubDesktopL.ManageRemoteRemoveButton);

    public string CancelText => _localizer.Get(AvaGithubDesktopL.Cancel);

    public string RemoteName
    {
        get => _remoteName;
        set
        {
            this.RaiseAndSetIfChanged(ref _remoteName, value);
            RaiseRemoteStateChanged();
        }
    }

    public string RemoteUrl
    {
        get => _remoteUrl;
        set
        {
            this.RaiseAndSetIfChanged(ref _remoteUrl, value);
            RaiseRemoteStateChanged();
        }
    }

    public string ValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RemoteName))
            {
                return _localizer.Get(AvaGithubDesktopL.RemoteNameRequired);
            }

            if (!IsValidRemoteName(RemoteName))
            {
                return _localizer.Get(AvaGithubDesktopL.RemoteNameInvalid);
            }

            if (string.IsNullOrWhiteSpace(RemoteUrl))
            {
                return _localizer.Get(AvaGithubDesktopL.RemoteUrlRequired);
            }

            return string.Empty;
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool CanSave => !HasValidationMessage;

    public bool CanRemove =>
        HasExistingRemote &&
        string.Equals(RemoteName.Trim(), _existingRemoteName, StringComparison.Ordinal);

    private static bool IsEmptyRemote(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim() == "-";

    private static bool IsValidRemoteName(string value)
    {
        var remoteName = value.Trim();
        return remoteName.Length > 0 &&
               !remoteName.StartsWith("-", StringComparison.Ordinal) &&
               !remoteName.Any(char.IsWhiteSpace);
    }

    private void SaveRemote()
    {
        RequestClose(new RepositoryRemoteRequest(
            RemoteName.Trim(),
            RemoteUrl.Trim(),
            RepositoryRemoteAction.Save));
    }

    private void RemoveRemote()
    {
        RequestClose(new RepositoryRemoteRequest(
            _existingRemoteName,
            string.Empty,
            RepositoryRemoteAction.Remove));
    }

    private void RequestClose(RepositoryRemoteRequest? request)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<RepositoryRemoteRequest?>(request));
    }

    private void RaiseRemoteStateChanged()
    {
        this.RaisePropertyChanged(nameof(ValidationMessage));
        this.RaisePropertyChanged(nameof(HasValidationMessage));
        this.RaisePropertyChanged(nameof(CanSave));
        this.RaisePropertyChanged(nameof(CanRemove));
    }
}
