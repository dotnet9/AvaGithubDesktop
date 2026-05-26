using Avalonia.Styling;

namespace AvaGithubDesktop.Core.Models;

public sealed record ThemeOption(string Key, string DisplayNameResourceKey, ThemeVariant ThemeVariant);
