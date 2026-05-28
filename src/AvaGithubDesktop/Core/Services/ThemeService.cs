using Avalonia;
using Avalonia.Styling;
using AvaGithubDesktop.Controls.Themes;
using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public sealed class ThemeService : IThemeService
{
    private static readonly ThemeOption[] Themes =
    [
        new("system", AvaGithubDesktopL.ThemeSystem, ThemeVariant.Default),
        new("light", AvaGithubDesktopL.ThemeLight, ThemeVariant.Light),
        new("dark", AvaGithubDesktopL.ThemeDark, ThemeVariant.Dark),
        new("aquatic", AvaGithubDesktopL.ThemeAquatic, AvaGithubDesktopThemeVariants.Aquatic),
        new("desert", AvaGithubDesktopL.ThemeDesert, AvaGithubDesktopThemeVariants.Desert),
        new("dusk", AvaGithubDesktopL.ThemeDusk, AvaGithubDesktopThemeVariants.Dusk),
        new("night-sky", AvaGithubDesktopL.ThemeNightSky, AvaGithubDesktopThemeVariants.NightSky)
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
