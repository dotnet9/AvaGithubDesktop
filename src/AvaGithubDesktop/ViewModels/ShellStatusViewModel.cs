using Avalonia.Threading;
using AvaGithubDesktop.Core.Messaging;
using AvaGithubDesktop.Core.Services;
using CodeWF.EventBus;
using CodeWF.Log.Core;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class ShellStatusViewModel : ViewModelBase
{
    private readonly IAppLocalizer _localizer;
    private string _statusText;

    public ShellStatusViewModel(IAppLocalizer localizer, IEventBus eventBus)
    {
        _localizer = localizer;
        _statusText = _localizer.Get(AvaGithubDesktopL.StatusReady);
        eventBus.Subscribe(this);
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    [EventHandler]
    private void Handle(StatusMessageChangedCommand command)
    {
        SetStatus(command.Message);
    }

    [EventHandler]
    private void Handle(RepositoryOpenedCommand command)
    {
        SetStatus(_localizer.Format(
            AvaGithubDesktopL.StatusLoadedRepositoryFormat,
            command.RepositoryName,
            command.ChangedFilesCount));
    }

    private void SetStatus(string status)
    {
        Logger.Info(status, status, log2UI: true, log2File: true, log2Console: false);
        if (Dispatcher.UIThread.CheckAccess())
        {
            StatusText = status;
            return;
        }

        Dispatcher.UIThread.Post(() => StatusText = status);
    }
}
