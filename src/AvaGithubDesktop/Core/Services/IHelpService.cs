namespace AvaGithubDesktop.Core.Services;

public interface IHelpService
{
    Task OpenReportIssueAsync();

    Task OpenContactSupportAsync();

    Task OpenUserGuidesAsync();

    Task ShowChangelogWindowAsync();

    Task ShowKeyboardShortcutsWindowAsync();

    Task ShowLogFolderAsync();

    Task ShowAboutWindowAsync();
}
