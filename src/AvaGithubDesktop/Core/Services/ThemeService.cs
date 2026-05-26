using Avalonia;
using Avalonia.Styling;
using AvaGithubDesktop.Core.Models;
using Semi.Avalonia;

namespace AvaGithubDesktop.Core.Services;

public sealed class ThemeService : IThemeService
{
    private static readonly ThemeOption[] Themes =
    [
        new("system", AvaGithubDesktopL.ThemeSystem, ThemeVariant.Default),
        new("light", AvaGithubDesktopL.ThemeLight, ThemeVariant.Light),
        new("dark", AvaGithubDesktopL.ThemeDark, ThemeVariant.Dark),
        new("aquatic", AvaGithubDesktopL.ThemeAquatic, SemiTheme.Aquatic),
        new("desert", AvaGithubDesktopL.ThemeDesert, SemiTheme.Desert),
        new("dusk", AvaGithubDesktopL.ThemeDusk, SemiTheme.Dusk),
        new("night-sky", AvaGithubDesktopL.ThemeNightSky, SemiTheme.NightSky)
    ];

    public IReadOnlyList<ThemeOption> GetThemeOptions()
    {
        return Themes;
    }

    public void ApplyTheme(ThemeOption theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme.ThemeVariant;
        }
    }
}
