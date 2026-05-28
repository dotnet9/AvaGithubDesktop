using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.ViewModels;

internal static class BranchDialogFilter
{
    public static BranchDialogFilterResult Build(
        IReadOnlyList<GitBranchItem> branches,
        string filterText,
        string? selectedBranchName,
        Func<IReadOnlyList<GitBranchItem>, GitBranchItem?> chooseFallback,
        bool matchUpstream)
    {
        var normalizedFilter = filterText.Trim();
        var filteredBranches = branches
            .Where(branch => MatchesFilter(branch, normalizedFilter, matchUpstream))
            .ToArray();
        var selectedBranch = !string.IsNullOrWhiteSpace(selectedBranchName)
            ? filteredBranches.FirstOrDefault(branch => branch.Name == selectedBranchName)
            : null;

        return new BranchDialogFilterResult(
            filteredBranches,
            selectedBranch ?? chooseFallback(filteredBranches));
    }

    private static bool MatchesFilter(GitBranchItem branch, string filterText, bool matchUpstream)
    {
        return filterText.Length == 0
               || branch.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)
               || (matchUpstream && branch.Upstream.Contains(filterText, StringComparison.OrdinalIgnoreCase))
               || branch.RelativeDate.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }
}
