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

    public AccountDialogService(
        IAppLocalizer localizer,
        IRepositoryShellService repositoryShellService)
    {
        _localizer = localizer;
        _repositoryShellService = repositoryShellService;
    }

    public async Task<GitHubSignInRequest?> ShowSignInDialogAsync(
        string defaultEndpoint,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var model = new SignInWindowViewModel(
            _localizer.Get(AvaGithubDesktopL.SignInTitle),
            _localizer.Get(AvaGithubDesktopL.SignInDescription),
            _localizer.Get(AvaGithubDesktopL.SignInEndpointLabel),
            _localizer.Get(AvaGithubDesktopL.SignInEndpointWatermark),
            _localizer.Get(AvaGithubDesktopL.SignInTokenLabel),
            _localizer.Get(AvaGithubDesktopL.SignInTokenWatermark),
            _localizer.Get(AvaGithubDesktopL.SignInTokenHelp),
            _localizer.Get(AvaGithubDesktopL.OpenTokenPage),
            _localizer.Get(AvaGithubDesktopL.Cancel),
            _localizer.Get(AvaGithubDesktopL.SignIn),
            _localizer.Get(AvaGithubDesktopL.SignInTokenRequired),
            _localizer.Get(AvaGithubDesktopL.StatusOpenTokenPageFailedFormat),
            string.IsNullOrWhiteSpace(defaultEndpoint)
                ? GitHubAccountEndpoints.DotComApiEndpoint
                : defaultEndpoint,
            OpenTokenPageAsync);
        var window = new SignInWindow(model);

        if (GetMainWindow() is not { } owner)
        {
            window.Show();
            return null;
        }

        return await window.ShowDialog<GitHubSignInRequest?>(owner);
    }

    private Task OpenTokenPageAsync(string endpoint) =>
        _repositoryShellService.OpenUrlAsync(GitHubAccountEndpoints.BuildNewTokenUrl(endpoint));

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
