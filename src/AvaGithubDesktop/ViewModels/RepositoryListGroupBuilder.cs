using AvaGithubDesktop.Core.Services;

namespace AvaGithubDesktop.ViewModels;

public sealed class RepositoryListGroupBuilder
{
    private readonly IAppLocalizer _localizer;

    public RepositoryListGroupBuilder(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public IReadOnlyList<RepositoryListGroupViewModel> Build(
        IReadOnlyList<RepositoryListItemViewModel> repositories,
        string filterText)
    {
        var groups = new List<RepositoryListGroupViewModel>();
        var filteredRepositories = repositories
            .Where(repository => MatchesRepositoryFilter(repository, filterText))
            .ToArray();

        if (filteredRepositories.Length > 1)
        {
            AddRepositoryGroup(
                groups,
                _localizer.Get(AvaGithubDesktopL.RecentRepositories),
                filteredRepositories
                    .Where(repository => !repository.IsCurrent)
                    .OrderByDescending(repository => repository.LastOpenedAt)
                    .ThenBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(5));
        }

        foreach (var group in filteredRepositories
                     .GroupBy(repository => repository.GroupName)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            AddRepositoryGroup(
                groups,
                group.Key,
                group.OrderBy(repository => repository.Name, StringComparer.OrdinalIgnoreCase));
        }

        return groups;
    }

    public void UpdateCurrentIndicators(
        IReadOnlyList<RepositoryListItemViewModel> repositories,
        string currentPath)
    {
        var normalizedCurrentPath = NormalizePathForComparison(currentPath);
        foreach (var repository in repositories)
        {
            repository.IsCurrent = string.Equals(
                NormalizePathForComparison(repository.Path),
                normalizedCurrentPath,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool PathsEqual(string firstPath, string secondPath) =>
        string.Equals(
            NormalizePathForComparison(firstPath),
            NormalizePathForComparison(secondPath),
            StringComparison.OrdinalIgnoreCase);

    private static void AddRepositoryGroup(
        ICollection<RepositoryListGroupViewModel> groups,
        string header,
        IEnumerable<RepositoryListItemViewModel> repositories)
    {
        var items = repositories.ToArray();
        if (items.Length > 0)
        {
            groups.Add(new RepositoryListGroupViewModel(header, items));
        }
    }

    private static bool MatchesRepositoryFilter(RepositoryListItemViewModel repository, string filterText)
    {
        var filter = filterText.Trim();
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return repository.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || repository.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || repository.GroupName.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathForComparison(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.GetFullPath(path.Trim())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
