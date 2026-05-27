using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using AvaGithubDesktop.Views;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryRemoteDialogService : IRepositoryRemoteDialogService
{
    private readonly IAppLocalizer _localizer;

    public RepositoryRemoteDialogService(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public async Task<RepositoryRemoteRequest?> ShowManageRemoteDialogAsync(
        string remoteName,
        string remoteUrl,
        bool hasRemote)
    {
        if (GetMainWindow() is not { } owner)
        {
            return null;
        }

        var viewModel = new ManageRemoteWindowViewModel(_localizer, remoteName, remoteUrl, hasRemote);
        var window = new ManageRemoteWindow(viewModel);
        return await window.ShowDialog<RepositoryRemoteRequest?>(owner);
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
