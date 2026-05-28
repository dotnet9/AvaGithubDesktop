namespace AvaGithubDesktop.Core.Services;

public interface IHelpService
{
    Task ShowChangelogWindowAsync();

    Task ShowKeyboardShortcutsWindowAsync();

    Task ShowLogFolderAsync();

    Task ShowAboutWindowAsync();
}
