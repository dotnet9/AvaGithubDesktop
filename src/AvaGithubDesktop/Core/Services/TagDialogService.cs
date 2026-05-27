using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using AvaGithubDesktop.Views;

namespace AvaGithubDesktop.Core.Services;

public sealed class TagDialogService : ITagDialogService
{
    private readonly IAppLocalizer _localizer;

    public TagDialogService(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public async Task<TagCreationRequest?> ShowCreateTagDialogAsync(
        GitCommitItem targetCommit,
        IReadOnlySet<string> existingTagNames)
    {
        if (GetMainWindow() is not { } owner)
        {
            return null;
        }

        var viewModel = new CreateTagWindowViewModel(targetCommit, existingTagNames, _localizer);
        var window = new CreateTagWindow(viewModel);
        return await window.ShowDialog<TagCreationRequest?>(owner);
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
