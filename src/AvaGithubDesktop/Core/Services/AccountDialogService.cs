using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using AvaGithubDesktop.Views;

namespace AvaGithubDesktop.Core.Services;

public sealed class AccountDialogService : IAccountDialogService
{
    private readonly IAppLocalizer _localizer;
    private readonly IRepositoryShellService _repositoryShellService;
    private readonly IGitHubAccountService _gitHubAccountService;

    public AccountDialogService(
        IAppLocalizer localizer,
        IRepositoryShellService repositoryShellService,
        IGitHubAccountService gitHubAccountService)
    {
        _localizer = localizer;
        _repositoryShellService = repositoryShellService;
        _gitHubAccountService = gitHubAccountService;
    }

    public async Task<GitHubAccount?> ShowSignInDialogAsync(
        string defaultEndpoint,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var model = new SignInWindowViewModel(
            _localizer.Get(AvaGithubDesktopL.SignInTitle),
            _localizer.Get(AvaGithubDesktopL.SignInDescription),
            _localizer.Get(AvaGithubDesktopL.SignInEndpointLabel),
            _localizer.Get(AvaGithubDesktopL.SignInEndpointWatermark),
            _localizer.Get(AvaGithubDesktopL.SignInBrowserButton),
            _localizer.Get(AvaGithubDesktopL.SignInDeviceCodeLabel),
            _localizer.Get(AvaGithubDesktopL.SignInWaitingForBrowser),
            _localizer.Get(AvaGithubDesktopL.SignInDeviceCodeInstructionFormat),
            _localizer.Get(AvaGithubDesktopL.Cancel),
            _localizer.Get(AvaGithubDesktopL.SigningIn),
            _localizer.Get(AvaGithubDesktopL.StatusSignInFailedFormat),
            string.IsNullOrWhiteSpace(defaultEndpoint)
                ? GitHubAccountEndpoints.DotComApiEndpoint
                : defaultEndpoint,
            _gitHubAccountService,
            _repositoryShellService);
        var window = new SignInWindow(model);

        if (GetMainWindow() is not { } owner)
        {
            window.Show();
            return null;
        }

        return await window.ShowDialog<GitHubAccount?>(owner);
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
