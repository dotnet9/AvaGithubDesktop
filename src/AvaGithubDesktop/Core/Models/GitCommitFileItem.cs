namespace AvaGithubDesktop.Core.Models;

public sealed record GitCommitFileItem(string StatusCode, string Path)
{
    public string GitPath => ResolveGitPath(Path);

    public IReadOnlyList<string> GitPaths => ResolveGitPaths(Path);

    public string DisplayStatus => StatusCode.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9') switch
    {
        "A" => "Added",
        "M" => "Modified",
        "D" => "Deleted",
        "R" => "Renamed",
        "C" => "Copied",
        "U" => "Conflict",
        var code when code.StartsWith('R') => "Renamed",
        var code when code.StartsWith('C') => "Copied",
        _ => "Changed"
    };

    private static string ResolveGitPath(string path)
    {
        var renameSeparatorIndex = path.IndexOf(" -> ", StringComparison.Ordinal);
        return renameSeparatorIndex < 0 ? path : path[(renameSeparatorIndex + 4)..];
    }

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
