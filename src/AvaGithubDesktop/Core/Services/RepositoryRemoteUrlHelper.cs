namespace AvaGithubDesktop.Core.Services;

public static class RepositoryRemoteUrlHelper
{
    public static bool TryGetGitHubWebUrl(string? remoteUrl, out string webUrl)
    {
        webUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(remoteUrl) || remoteUrl == "-")
        {
            return false;
        }

        var normalized = remoteUrl.Trim();
        if (normalized.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            return TryBuildGitHubUrl(normalized["git@github.com:".Length..], out webUrl);
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return TryBuildGitHubUrl(uri.AbsolutePath.TrimStart('/'), out webUrl);
    }

    private static bool TryBuildGitHubUrl(string ownerAndRepository, out string webUrl)
    {
        webUrl = string.Empty;
        var trimmed = ownerAndRepository.Trim().Trim('/');
        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        var parts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        webUrl = $"https://github.com/{parts[0]}/{parts[1]}";
        return true;
    }
}
