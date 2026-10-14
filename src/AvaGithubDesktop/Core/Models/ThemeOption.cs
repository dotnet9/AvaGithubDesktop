using CodeWF.Avalonia.Controls.Themes;

namespace AvaGithubDesktop.Core.Models;

public sealed record ThemeOption(string Key, string DisplayNameResourceKey, StudioThemeMode Mode);
public sealed record PaletteOption(string Key, string DisplayNameResourceKey, StudioPalette Palette);
