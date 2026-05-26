namespace AvaGithubDesktop.Core.Models;

public sealed record GitChangeItem(string StatusCode, string Path, GitChangeKind Kind)
{
    public IReadOnlyList<string> GitPaths => ResolveGitPaths(Path);

    public string DisplayStatus => StatusCode.Trim() switch
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

    public string StatusBackground => Kind switch
    {
        GitChangeKind.Staged => "#DFF6DD",
        GitChangeKind.Untracked => "#FFF4CE",
        _ => "#EAF2FF"
    };

    public string StatusForeground => Kind switch
    {
        GitChangeKind.Staged => "#0E6F32",
        GitChangeKind.Untracked => "#8A5A00",
        _ => "#0757A8"
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
}
