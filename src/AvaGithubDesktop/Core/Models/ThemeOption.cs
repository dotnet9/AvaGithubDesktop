using CodeWF.Avalonia.Controls.Themes;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaGithubDesktop.Core.Models;

public sealed record ThemeOption(string Key, string DisplayNameResourceKey, StudioThemeMode Mode);
public sealed record PaletteOption(string Key, string DisplayNameResourceKey, StudioPalette Palette)
{
    public IBrush Swatch => global::Avalonia.Application.Current is { } app
        && app.TryFindResource($"StudioSwatch{Palette}", out var resource) && resource is IBrush brush
            ? brush : Brushes.Transparent;
}
