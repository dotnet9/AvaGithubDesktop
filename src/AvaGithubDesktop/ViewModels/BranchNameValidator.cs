using System.Text.RegularExpressions;
using AvaGithubDesktop.Core.Services;

namespace AvaGithubDesktop.ViewModels;

internal static partial class BranchNameValidator
{
    public static string GetValidationError(
        string branchName,
        IReadOnlySet<string> branchNames,
        string? ignoredBranchName,
        IAppLocalizer localizer)
    {
        var trimmedName = branchName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return localizer.Get(AvaGithubDesktopL.BranchNameRequired);
        }

        var alreadyExists = branchNames.Any(name =>
            !string.Equals(name, ignoredBranchName, StringComparison.Ordinal)
            && string.Equals(name, trimmedName, StringComparison.Ordinal));
        if (alreadyExists)
        {
            return localizer.Format(AvaGithubDesktopL.BranchNameAlreadyExistsFormat, trimmedName);
        }

        if (!IsLikelyValidBranchName(trimmedName))
        {
            return localizer.Get(AvaGithubDesktopL.BranchNameInvalid);
        }

        return string.Empty;
    }

    private static bool IsLikelyValidBranchName(string branchName)
    {
        // 这里覆盖 Git ref 常见限制，最终仍由 git check-ref-format 做权威校验。
        return !branchName.StartsWith("/", StringComparison.Ordinal)
            && !branchName.EndsWith("/", StringComparison.Ordinal)
            && !branchName.EndsWith(".", StringComparison.Ordinal)
            && !branchName.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)
            && !branchName.Contains("..", StringComparison.Ordinal)
            && !branchName.Contains("//", StringComparison.Ordinal)
            && !branchName.Contains("@{", StringComparison.Ordinal)
            && !InvalidBranchNameCharactersRegex().IsMatch(branchName);
    }

    [GeneratedRegex(@"[\000-\037\177 ~^:?*\[\\]")]
    private static partial Regex InvalidBranchNameCharactersRegex();
}
