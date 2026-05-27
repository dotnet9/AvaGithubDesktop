using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using AvaGithubDesktop.Views;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryCreationDialogService : IRepositoryCreationDialogService
{
    private readonly IRepositoryPickerService _repositoryPickerService;
    private readonly IAppLocalizer _localizer;

    public RepositoryCreationDialogService(
        IRepositoryPickerService repositoryPickerService,
        IAppLocalizer localizer)
    {
        _repositoryPickerService = repositoryPickerService;
        _localizer = localizer;
    }

    public async Task<RepositoryCreationRequest?> ShowCreateRepositoryDialogAsync()
    {
        if (GetMainWindow() is not { } owner)
        {
            return null;
        }

        var viewModel = new CreateRepositoryWindowViewModel(
            _localizer,
            _repositoryPickerService.PickCreateRepositoryParentDirectoryAsync,
            ResolveInitialParentDirectory());
        var window = new CreateRepositoryWindow(viewModel);
        return await window.ShowDialog<RepositoryCreationRequest?>(owner);
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }

    private static string ResolveInitialParentDirectory()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)
            ? string.Empty
            : folder;
    }
}
