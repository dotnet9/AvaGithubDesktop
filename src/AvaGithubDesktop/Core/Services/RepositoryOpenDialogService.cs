using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using AvaGithubDesktop.Views;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryOpenDialogService : IRepositoryOpenDialogService
{
    private readonly IRepositoryPickerService _repositoryPickerService;
    private readonly IAppLocalizer _localizer;

    public RepositoryOpenDialogService(
        IRepositoryPickerService repositoryPickerService,
        IAppLocalizer localizer)
    {
        _repositoryPickerService = repositoryPickerService;
        _localizer = localizer;
    }

    public async Task<RepositoryOpenRequest?> ShowOpenRepositoryDialogAsync(string initialPath)
    {
        if (GetMainWindow() is not { } owner)
        {
            return null;
        }

        var viewModel = new OpenRepositoryWindowViewModel(
            _localizer,
            _repositoryPickerService.PickRepositoryAsync,
            initialPath);
        var window = new OpenRepositoryWindow(viewModel);
        return await window.ShowDialog<RepositoryOpenRequest?>(owner);
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
