using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IThemeService
{
    IReadOnlyList<ThemeOption> GetThemeOptions();

    void ApplyTheme(ThemeOption theme);
}
