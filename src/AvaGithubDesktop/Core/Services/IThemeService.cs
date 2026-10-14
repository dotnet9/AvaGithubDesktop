using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IThemeService : IDisposable
{
    IReadOnlyList<ThemeOption> GetThemeOptions();
    IReadOnlyList<PaletteOption> GetPaletteOptions();
    void ApplyTheme(ThemeOption theme, PaletteOption palette);
}
