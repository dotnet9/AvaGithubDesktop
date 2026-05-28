namespace AvaGithubDesktop.Core.Models;

public sealed record GitChangeItem(string StatusCode, string Path, GitChangeKind Kind)
{
    public IReadOnlyList<string> GitPaths => ResolveGitPaths(Path);

    public bool IsConflict => IsConflictStatus(StatusCode);

    public string DisplayStatus => IsConflict
        ? "Conflict"
        : StatusCode.Trim() switch
    {
        "??" => "Untracked",
        "A" => "Added",
        "M" => "Modified",
        "D" => "Deleted",
        "R" => "Renamed",
        "C" => "Copied",
        "U" => "Conflict",
        "" => "Changed",
        var code => code
    };

    private static IReadOnlyList<string> ResolveGitPaths(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<string>();
        }

        var renameSeparatorIndex = path.IndexOf(" -> ", StringComparison.Ordinal);
        if (renameSeparatorIndex < 0)
        {
            return new[] { path };
        }

        var oldPath = path[..renameSeparatorIndex];
        var newPath = path[(renameSeparatorIndex + 4)..];
        return string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath)
            ? new[] { path }
            : new[] { oldPath, newPath };
    }

    private static bool IsConflictStatus(string statusCode)
    {
        return statusCode.Trim() is "DD" or "AU" or "UD" or "UA" or "DU" or "AA" or "UU" or "U";
    }
}
