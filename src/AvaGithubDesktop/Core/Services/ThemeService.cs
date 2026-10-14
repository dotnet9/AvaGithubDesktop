using Avalonia;
using CodeWF.Avalonia.Controls.Themes;
using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

/// <summary>应用持久化模式和配色，明暗资源及系统变化由共享控件库管理。</summary>
public sealed class ThemeService : IThemeService
{
    private StudioThemeController? _controller;
    private static readonly ThemeOption[] Modes =
    [
        new("system", AvaGithubDesktopL.ThemeSystem, StudioThemeMode.System),
        new("light", AvaGithubDesktopL.ThemeLight, StudioThemeMode.Light),
        new("dark", AvaGithubDesktopL.ThemeDark, StudioThemeMode.Dark),
    ];
    private static readonly PaletteOption[] Palettes =
    [
        new("dotnet", AvaGithubDesktopL.ThemeDotNet, StudioPalette.DotNet),
        new("aurora", AvaGithubDesktopL.ThemeAurora, StudioPalette.Aurora),
        new("ember", AvaGithubDesktopL.ThemeEmber, StudioPalette.Ember),
        new("default", AvaGithubDesktopL.ThemeDefault, StudioPalette.Default),
        new("aquatic", AvaGithubDesktopL.ThemeAquatic, StudioPalette.Aquatic),
        new("desert", AvaGithubDesktopL.ThemeDesert, StudioPalette.Desert),
        new("dusk", AvaGithubDesktopL.ThemeDusk, StudioPalette.Dusk),
        new("night-sky", AvaGithubDesktopL.ThemeNightSky, StudioPalette.NightSky),
    ];

    public IReadOnlyList<ThemeOption> GetThemeOptions() => Modes;
    public IReadOnlyList<PaletteOption> GetPaletteOptions() => Palettes;
    public void ApplyTheme(ThemeOption theme, PaletteOption palette)
    {
        if (Application.Current is not { } app) return;
        _controller ??= new StudioThemeController(app);
        _controller.Palette = palette.Palette;
        _controller.Mode = theme.Mode;
    }
    public void Dispose() => _controller?.Dispose();
}
