using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using AvaGithubDesktop.Views;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryCloneDialogService : IRepositoryCloneDialogService
{
    private readonly IRepositoryPickerService _repositoryPickerService;
    private readonly IAppLocalizer _localizer;

    public RepositoryCloneDialogService(
        IRepositoryPickerService repositoryPickerService,
        IAppLocalizer localizer)
    {
        _repositoryPickerService = repositoryPickerService;
        _localizer = localizer;
    }

    public async Task<RepositoryCloneRequest?> ShowCloneRepositoryDialogAsync()
    {
        if (GetMainWindow() is not { } owner)
        {
            return null;
        }

        var viewModel = new CloneRepositoryWindowViewModel(
            _localizer,
            _repositoryPickerService.PickCloneParentDirectoryAsync,
            ResolveInitialParentDirectory());
        var window = new CloneRepositoryWindow(viewModel);
        return await window.ShowDialog<RepositoryCloneRequest?>(owner);
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
