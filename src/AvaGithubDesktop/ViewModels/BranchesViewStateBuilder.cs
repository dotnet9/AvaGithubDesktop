namespace AvaGithubDesktop.ViewModels;

public sealed class BranchesViewStateBuilder
{
    public BranchesFilterResult BuildFilterResult(
        IEnumerable<GitBranchItemViewModel> branches,
        string filterText,
        string? selectedName,
        bool preferCurrentBranch)
    {
        var normalizedFilter = filterText.Trim();
        var filteredBranches = branches
            .Where(branch => MatchesFilter(branch, normalizedFilter))
            .ToArray();

        var currentBranch = preferCurrentBranch
            ? filteredBranches.FirstOrDefault(branch => branch.IsCurrent)
            : null;
        var selectedBranch = !string.IsNullOrWhiteSpace(selectedName)
            ? filteredBranches.FirstOrDefault(branch => branch.Name == selectedName)
            : null;

        return new BranchesFilterResult(
            filteredBranches,
            currentBranch ?? selectedBranch ?? filteredBranches.FirstOrDefault());
    }

    private static bool MatchesFilter(GitBranchItemViewModel branch, string filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return true;
        }

        return branch.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)
            || branch.Upstream.Contains(filterText, StringComparison.OrdinalIgnoreCase)
            || branch.RelativeDate.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }
}
