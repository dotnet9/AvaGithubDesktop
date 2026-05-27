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

    public static bool TryGetGitHubCommitUrl(string? remoteUrl, string? sha, out string webUrl)
    {
        webUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(sha) || !TryGetGitHubWebUrl(remoteUrl, out var repositoryUrl))
        {
            return false;
        }

        // GitHub Desktop 的 History 提交操作会直接跳转到仓库提交页，这里只拼接稳定的 commit 路由。
        webUrl = $"{repositoryUrl}/commit/{Uri.EscapeDataString(sha.Trim())}";
        return true;
    }

    public static bool TryGetGitHubBranchUrl(string? remoteUrl, string? upstream, out string webUrl)
    {
        return TryGetGitHubBranchRouteUrl(remoteUrl, upstream, "tree", out webUrl);
    }

    public static bool TryGetGitHubCompareUrl(string? remoteUrl, string? upstream, out string webUrl)
    {
        return TryGetGitHubBranchRouteUrl(remoteUrl, upstream, "compare", out webUrl);
    }

    public static bool TryGetGitHubPullRequestUrl(string? remoteUrl, string? upstream, string? currentBranch, out string webUrl)
    {
        webUrl = string.Empty;
        if (!TryGetGitHubWebUrl(remoteUrl, out var repositoryUrl))
        {
            return false;
        }

        var branchName = GetBranchNameWithoutRemote(upstream) ?? GetCurrentBranchName(currentBranch);
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return false;
        }

        // GitHub Desktop 的 Create Pull Request 会打开 /pull/new/{branch}，让 GitHub 决定默认 base 分支。
        webUrl = $"{repositoryUrl}/pull/new/{Uri.EscapeDataString(branchName)}";
        return true;
    }

    public static bool TryGetGitHubIssueCreationUrl(string? remoteUrl, out string webUrl)
    {
        webUrl = string.Empty;
        if (!TryGetGitHubWebUrl(remoteUrl, out var repositoryUrl))
        {
            return false;
        }

        // GitHub Desktop 会打开 issue 模板选择页，兼容配置了 issue forms/templates 的仓库。
        webUrl = $"{repositoryUrl}/issues/new/choose";
        return true;
    }

    private static bool TryGetGitHubBranchRouteUrl(string? remoteUrl, string? upstream, string route, out string webUrl)
    {
        webUrl = string.Empty;
        if (!TryGetGitHubWebUrl(remoteUrl, out var repositoryUrl))
        {
            return false;
        }

        var branchName = GetBranchNameWithoutRemote(upstream);
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return false;
        }

        // GitHub Desktop 只对有 upstream 的分支展示入口，并使用去掉远端名前缀后的真实远端分支名。
        webUrl = $"{repositoryUrl}/{route}/{Uri.EscapeDataString(branchName)}";
        return true;
    }

    private static string? GetBranchNameWithoutRemote(string? upstream)
    {
        if (string.IsNullOrWhiteSpace(upstream) || upstream == "-")
        {
            return null;
        }

        var trimmed = upstream.Trim();
        var separatorIndex = trimmed.IndexOf('/', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
        {
            return null;
        }

        return trimmed[(separatorIndex + 1)..];
    }

    private static string? GetCurrentBranchName(string? currentBranch)
    {
        if (string.IsNullOrWhiteSpace(currentBranch) || currentBranch == "-")
        {
            return null;
        }

        var trimmed = currentBranch.Trim();
        return trimmed.StartsWith("HEAD", StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
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
