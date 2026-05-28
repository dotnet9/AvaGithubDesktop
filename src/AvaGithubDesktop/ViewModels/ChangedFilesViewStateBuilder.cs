namespace AvaGithubDesktop.ViewModels;

public sealed class ChangedFilesViewStateBuilder
{
    public ChangedFilesFilterResult BuildFilterResult(
        IEnumerable<GitChangeItemViewModel> changes,
        string filterText,
        bool showConflictsOnly,
        string? selectedPath)
    {
        var normalizedFilter = filterText.Trim();
        var filteredChanges = changes
            .Where(change => (!showConflictsOnly || change.IsConflict) && MatchesFilter(change, normalizedFilter))
            .ToArray();

        var selectedChange = !string.IsNullOrWhiteSpace(selectedPath)
            ? filteredChanges.FirstOrDefault(change => change.Path == selectedPath) ?? filteredChanges.FirstOrDefault()
            : filteredChanges.FirstOrDefault();

        return new ChangedFilesFilterResult(filteredChanges, selectedChange);
    }

    public ChangedFilesIncludedState BuildIncludedState(
        IReadOnlyCollection<GitChangeItemViewModel> allChanges,
        IReadOnlyCollection<GitChangeItemViewModel> scopedChanges)
    {
        var includedCount = allChanges.Count(change => change.IsIncluded);
        var scopedIncludedChangesCount = scopedChanges.Count(change => change.IsIncluded);
        var allIncluded = scopedChanges.Count == 0
            ? false
            : scopedIncludedChangesCount == scopedChanges.Count
                ? true
                : scopedIncludedChangesCount == 0
                    ? false
                    : (bool?)null;

        return new ChangedFilesIncludedState(includedCount, allIncluded);
    }

    private static bool MatchesFilter(GitChangeItemViewModel change, string filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return true;
        }

        return change.Path.Contains(filterText, StringComparison.OrdinalIgnoreCase)
            || change.DisplayStatus.Contains(filterText, StringComparison.OrdinalIgnoreCase)
            || change.StatusCode.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }
}
