namespace AvaGithubDesktop.Core.Services;

public interface IHelpService
{
    Task ShowChangelogWindowAsync();

    Task ShowAboutWindowAsync();
}
