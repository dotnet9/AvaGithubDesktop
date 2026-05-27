using System.Text.RegularExpressions;
using AvaGithubDesktop.Core.Services;

namespace AvaGithubDesktop.ViewModels;

internal static partial class TagNameValidator
{
    public static string GetValidationError(
        string tagName,
        IReadOnlySet<string> tagNames,
        IAppLocalizer localizer)
    {
        var trimmedName = tagName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return localizer.Get(AvaGithubDesktopL.TagNameRequired);
        }

        if (tagNames.Contains(trimmedName))
        {
            return localizer.Format(AvaGithubDesktopL.TagNameAlreadyExistsFormat, trimmedName);
        }

        if (!IsLikelyValidTagName(trimmedName))
        {
            return localizer.Get(AvaGithubDesktopL.TagNameInvalid);
        }

        return string.Empty;
    }

    private static bool IsLikelyValidTagName(string tagName)
    {
        return !tagName.StartsWith("/", StringComparison.Ordinal)
            && !tagName.EndsWith("/", StringComparison.Ordinal)
            && !tagName.EndsWith(".", StringComparison.Ordinal)
            && !tagName.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)
            && !tagName.Contains("..", StringComparison.Ordinal)
            && !tagName.Contains("//", StringComparison.Ordinal)
            && !tagName.Contains("@{", StringComparison.Ordinal)
            && !InvalidTagNameCharactersRegex().IsMatch(tagName);
    }

    [GeneratedRegex(@"[\000-\037\177 ~^:?*\[\\]")]
    private static partial Regex InvalidTagNameCharactersRegex();
}
