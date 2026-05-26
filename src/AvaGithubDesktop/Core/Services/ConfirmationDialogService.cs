using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaGithubDesktop.Views;

namespace AvaGithubDesktop.Core.Services;

public sealed class ConfirmationDialogService : IConfirmationDialogService
{
    private readonly IAppLocalizer _localizer;

    public ConfirmationDialogService(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public async Task<bool> ShowDiscardChangesConfirmationAsync(IReadOnlyList<string> paths)
    {
        var normalizedPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedPaths.Length == 0 || GetMainWindow() is not { } owner)
        {
            return false;
        }

        var message = normalizedPaths.Length == 1
            ? _localizer.Get(AvaGithubDesktopL.DiscardChangesConfirmMessage)
            : _localizer.Format(AvaGithubDesktopL.DiscardChangesConfirmManyMessageFormat, normalizedPaths.Length);

        var window = new DiscardChangesConfirmationWindow(
            _localizer.Get(AvaGithubDesktopL.DiscardChangesConfirmTitle),
            message,
            _localizer.Get(AvaGithubDesktopL.DiscardChangesConfirmWarning),
            normalizedPaths,
            _localizer.Get(AvaGithubDesktopL.Cancel),
            _localizer.Get(AvaGithubDesktopL.DiscardChangesConfirmButton));

        return await window.ShowDialog<bool>(owner);
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
