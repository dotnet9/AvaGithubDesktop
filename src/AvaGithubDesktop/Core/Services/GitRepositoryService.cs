using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public sealed class GitRepositoryService : IGitRepositoryService
{
    private const string DesktopStashEntryMarker = "!!GitHub_Desktop";
    private static readonly HashSet<string> ImageFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".ico",
        ".webp",
        ".bmp",
        ".avif"
    };
    private static readonly Regex DesktopStashEntryMessageRegex = new(
        "!!GitHub_Desktop<(?<branch>.+)>$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task CloneRepositoryAsync(
        string sourceUrl,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            throw new ArgumentException("A repository URL is required.", nameof(sourceUrl));
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("A destination path is required.", nameof(destinationPath));
        }

        var normalizedDestination = Path.GetFullPath(destinationPath);
        if (Directory.Exists(normalizedDestination) || File.Exists(normalizedDestination))
        {
            throw new IOException($"The destination path already exists: {normalizedDestination}");
        }

        var parentDirectory = Path.GetDirectoryName(normalizedDestination);
        if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
        {
            throw new DirectoryNotFoundException(parentDirectory);
        }

        await RunRequiredGitAsync(
            parentDirectory,
            cancellationToken,
            "clone",
            "--",
            sourceUrl.Trim(),
            normalizedDestination);
    }

    public async Task CreateRepositoryAsync(
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("A destination path is required.", nameof(destinationPath));
        }

        var normalizedDestination = Path.GetFullPath(destinationPath);
        if (Directory.Exists(normalizedDestination) || File.Exists(normalizedDestination))
        {
            throw new IOException($"The destination path already exists: {normalizedDestination}");
        }

        var parentDirectory = Path.GetDirectoryName(normalizedDestination);
        if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
        {
            throw new DirectoryNotFoundException(parentDirectory);
        }

        Directory.CreateDirectory(normalizedDestination);
        try
        {
            await RunRequiredGitAsync(normalizedDestination, cancellationToken, "init");
        }
        catch
        {
            TryDeleteDirectory(normalizedDestination);
            throw;
        }
    }

    public async Task<GitRepositorySnapshot> LoadRepositoryAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var branch = await ResolveBranchAsync(root, cancellationToken);
        var status = await RunRequiredGitAsync(root, cancellationToken, "status", "--porcelain=v1", "-b");
        var upstream = await RunOptionalGitAsync(root, cancellationToken, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}");
        var remotes = await RunOptionalGitAsync(root, cancellationToken, "remote");
        var remoteName = ResolveRemoteName(upstream, remotes);
        var defaultBranch = await ResolveDefaultBranchAsync(root, remoteName, cancellationToken);
        var remote = remoteName == "-"
            ? string.Empty
            : await RunOptionalGitAsync(root, cancellationToken, "remote", "get-url", remoteName);
        var lastCommit = await RunOptionalGitAsync(root, cancellationToken, "log", "-1", "--pretty=format:%h%x09%s%x09%cr");
        var changes = GitRepositoryOutputParser.ParseChanges(status);
        var (ahead, behind) = GitRepositoryOutputParser.ParseAheadBehind(status);
        var lastFetchedAt = await ResolveLastFetchedAtAsync(root, cancellationToken);
        var operationState = await ResolveOperationStateAsync(root, cancellationToken);
        var currentBranchStash = await LoadCurrentBranchStashAsync(root, branch, cancellationToken);

        return new GitRepositorySnapshot(
            RepositoryName: new DirectoryInfo(root).Name,
            RootPath: root,
            CurrentBranch: branch,
            DefaultBranch: defaultBranch,
            Upstream: string.IsNullOrWhiteSpace(upstream) ? "-" : upstream,
            RemoteName: remoteName,
            RemoteUrl: string.IsNullOrWhiteSpace(remote) ? "-" : remote,
            LastFetchedAt: lastFetchedAt,
            LastCommit: GitRepositoryOutputParser.FormatLastCommit(lastCommit),
            LastCommitSummary: GitRepositoryOutputParser.FormatLastCommitSummary(lastCommit),
            Ahead: ahead,
            Behind: behind,
            OperationState: operationState,
            CurrentBranchStash: currentBranchStash,
            Changes: changes);
    }

    public async Task SetRemoteAsync(
        string repositoryPath,
        string remoteName,
        string remoteUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        var normalizedRemoteName = NormalizeRemoteName(remoteName);
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            throw new ArgumentException("A remote URL is required.", nameof(remoteUrl));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var remotes = await RunOptionalGitAsync(root, cancellationToken, "remote");
        if (RemoteExists(remotes, normalizedRemoteName))
        {
            await RunRequiredGitAsync(root, cancellationToken, "remote", "set-url", normalizedRemoteName, remoteUrl.Trim());
            return;
        }

        await RunRequiredGitAsync(root, cancellationToken, "remote", "add", normalizedRemoteName, remoteUrl.Trim());
    }

    public async Task RemoveRemoteAsync(
        string repositoryPath,
        string remoteName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        var normalizedRemoteName = NormalizeRemoteName(remoteName);
        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var remotes = await RunOptionalGitAsync(root, cancellationToken, "remote");
        if (!RemoteExists(remotes, normalizedRemoteName))
        {
            return;
        }

        await RunRequiredGitAsync(root, cancellationToken, "remote", "remove", normalizedRemoteName);
    }

    public async Task SetDefaultBranchAsync(
        string repositoryPath,
        string remoteName,
        string branchName,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        var normalizedRemoteName = NormalizeRemoteName(remoteName);
        if (string.IsNullOrWhiteSpace(branchName) || branchName.StartsWith("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A local branch name is required.", nameof(branchName));
        }

        await RunRequiredGitAsync(root, cancellationToken, "remote", "set-head", normalizedRemoteName, branchName.Trim());
    }

    public async Task<IReadOnlyList<GitCommitItem>> LoadHistoryAsync(
        string repositoryPath,
        int maxCount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var history = await RunOptionalGitAsync(
            root,
            cancellationToken,
            "log",
            $"--max-count={Math.Max(1, maxCount)}",
            "--date=iso-strict",
            "--diff-merges=first-parent",
            $"--pretty=format:%x1e%H%x1f%h%x1f%an%x1f%ae%x1f%ad%x1f%ar%x1f%s",
            "--name-status");

        return GitRepositoryOutputParser.ParseHistory(history);
    }

    public async Task<IReadOnlySet<string>> LoadTagNamesAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        var tags = await RunOptionalGitAsync(root, cancellationToken, "tag", "--list");
        return tags
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<GitBranchItem>> LoadBranchesAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var branches = await RunOptionalGitAsync(
            root,
            cancellationToken,
            "for-each-ref",
            "--format=%(refname:short)%09%(upstream:short)%09%(committerdate:relative)%09%(HEAD)",
            "--sort=-committerdate",
            "refs/heads");
        return GitRepositoryOutputParser.ParseBranches(branches);
    }

    public async Task<IReadOnlyList<GitBranchItem>> LoadRemoteBranchesAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        var branches = await RunOptionalGitAsync(
            root,
            cancellationToken,
            "for-each-ref",
            "--format=%(refname:short)%09%(committerdate:relative)",
            "--sort=-committerdate",
            "refs/remotes");
        return GitRepositoryOutputParser.ParseRemoteBranches(branches);
    }

    public async Task CheckoutBranchAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(branchName))
        {
            throw new ArgumentException("A branch name is required.", nameof(branchName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        await RunRequiredGitAsync(root, cancellationToken, "checkout", branchName);
    }

    public async Task CreateBranchAsync(
        string repositoryPath,
        string branchName,
        string? startPoint,
        bool checkoutBranch,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(branchName))
        {
            throw new ArgumentException("A branch name is required.", nameof(branchName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        // 先让 Git 校验 ref 名称，避免 UI 规则和 Git 自身规则不一致时创建出异常分支。
        await RunRequiredGitAsync(root, cancellationToken, "check-ref-format", "--branch", branchName.Trim());

        if (string.IsNullOrWhiteSpace(startPoint))
        {
            await RunRequiredGitAsync(root, cancellationToken, "branch", branchName.Trim());
        }
        else
        {
            await RunRequiredGitAsync(root, cancellationToken, "branch", branchName.Trim(), startPoint.Trim());
        }

        if (checkoutBranch)
        {
            await RunRequiredGitAsync(root, cancellationToken, "checkout", branchName.Trim());
        }
    }

    public async Task RenameBranchAsync(
        string repositoryPath,
        string oldBranchName,
        string newBranchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(oldBranchName))
        {
            throw new ArgumentException("A source branch name is required.", nameof(oldBranchName));
        }

        if (string.IsNullOrWhiteSpace(newBranchName))
        {
            throw new ArgumentException("A target branch name is required.", nameof(newBranchName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        // rename 同样使用 Git 自带校验，避免 UI 层遗漏 ref 边界规则。
        var normalizedOldName = oldBranchName.Trim();
        var normalizedNewName = newBranchName.Trim();
        await RunRequiredGitAsync(root, cancellationToken, "check-ref-format", "--branch", normalizedNewName);
        // 大小写-only 重命名在 Windows/macOS 的大小写不敏感文件系统上需要强制参数，Desktop 也对这类场景做了特殊处理。
        var renameFlag = string.Equals(normalizedOldName, normalizedNewName, StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(normalizedOldName, normalizedNewName, StringComparison.Ordinal)
            ? "-M"
            : "-m";
        await RunRequiredGitAsync(root, cancellationToken, "branch", renameFlag, normalizedOldName, normalizedNewName);
    }

    public async Task DeleteBranchAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(branchName))
        {
            throw new ArgumentException("A branch name is required.", nameof(branchName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        await RunRequiredGitAsync(root, cancellationToken, "branch", "-D", branchName.Trim());
    }

    public async Task UnsetUpstreamAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(branchName) || branchName.StartsWith("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A local branch name is required.", nameof(branchName));
        }

        await RunRequiredGitAsync(root, cancellationToken, "branch", "--unset-upstream", branchName.Trim());
    }

    public async Task SetUpstreamAsync(
        string repositoryPath,
        string branchName,
        string upstreamBranchName,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(branchName) || branchName.StartsWith("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A local branch name is required.", nameof(branchName));
        }

        if (string.IsNullOrWhiteSpace(upstreamBranchName) || upstreamBranchName.StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException("An upstream branch name is required.", nameof(upstreamBranchName));
        }

        await RunRequiredGitAsync(
            root,
            cancellationToken,
            "branch",
            "--set-upstream-to",
            upstreamBranchName.Trim(),
            branchName.Trim());
    }

    public async Task<GitMergeResult> MergeBranchAsync(
        string repositoryPath,
        string sourceBranchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(sourceBranchName))
        {
            throw new ArgumentException("A source branch name is required.", nameof(sourceBranchName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var normalizedSource = sourceBranchName.Trim();
        // GitHub Desktop 调用的是普通 git merge <branch>，让 Git 自己决定 fast-forward、merge commit 或冲突状态。
        var output = await RunRequiredGitAsync(root, cancellationToken, "merge", normalizedSource);
        return output.Contains("Already up to date", StringComparison.OrdinalIgnoreCase)
            ? GitMergeResult.AlreadyUpToDate
            : GitMergeResult.Success;
    }

    public async Task<GitMergeResult> SquashMergeBranchAsync(
        string repositoryPath,
        string sourceBranchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(sourceBranchName))
        {
            throw new ArgumentException("A source branch name is required.", nameof(sourceBranchName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var normalizedSource = sourceBranchName.Trim();
        // 对齐 GitHub Desktop：先执行 squash merge，Git 生成 SQUASH_MSG 后再用 --no-edit 创建压缩合并提交。
        var output = await RunRequiredGitAsync(root, cancellationToken, "merge", "--squash", normalizedSource);
        if (output.Contains("Already up to date", StringComparison.OrdinalIgnoreCase))
        {
            return GitMergeResult.AlreadyUpToDate;
        }

        await RunRequiredGitAsync(root, cancellationToken, "commit", "--no-edit");
        return GitMergeResult.Success;
    }

    public async Task<GitRebaseResult> RebaseCurrentBranchAsync(
        string repositoryPath,
        string baseBranchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(baseBranchName))
        {
            throw new ArgumentException("A base branch name is required.", nameof(baseBranchName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var normalizedBase = baseBranchName.Trim();
        // 当前分支已经由 UI 保证签出；对齐 Desktop 的核心行为，把当前分支 rebase 到所选基准分支之上。
        var output = await RunRequiredGitAsync(root, cancellationToken, "rebase", normalizedBase);
        return output.Contains("up to date", StringComparison.OrdinalIgnoreCase)
            ? GitRebaseResult.AlreadyUpToDate
            : GitRebaseResult.Success;
    }

    public async Task ContinueMergeAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "commit", "--no-edit");
    }

    public async Task AbortMergeAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "merge", "--abort");
    }

    public async Task ContinueRebaseAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "rebase", "--continue");
    }

    public async Task SkipRebaseAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "rebase", "--skip");
    }

    public async Task AbortRebaseAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "rebase", "--abort");
    }

    public async Task ContinueRevertAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "revert", "--continue");
    }

    public async Task AbortRevertAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "revert", "--abort");
    }

    public async Task ContinueCherryPickAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "cherry-pick", "--continue");
    }

    public async Task AbortCherryPickAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "cherry-pick", "--abort");
    }

    public async Task FetchAsync(
        string repositoryPath,
        string remoteName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(remoteName) || remoteName == "-")
        {
            throw new ArgumentException("A remote is required.", nameof(remoteName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        // 对齐 GitHub Desktop 的默认 fetch 行为：清理已删除远端分支，并按需更新子模块。
        await RunRequiredGitAsync(root, cancellationToken, "fetch", "--prune", "--recurse-submodules=on-demand", remoteName);
    }

    public async Task FetchAllAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "fetch", "--all", "--prune", "--recurse-submodules=on-demand");
    }

    public async Task FetchLfsAsync(
        string repositoryPath,
        string remoteName,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        var normalizedRemoteName = NormalizeRemoteName(remoteName);
        await RunRequiredGitAsync(root, cancellationToken, "lfs", "fetch", normalizedRemoteName);
    }

    public async Task PullLfsAsync(
        string repositoryPath,
        string remoteName,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        var normalizedRemoteName = NormalizeRemoteName(remoteName);
        await RunRequiredGitAsync(root, cancellationToken, "lfs", "pull", normalizedRemoteName);
    }

    public async Task UpdateSubmodulesAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "submodule", "update", "--init", "--recursive");
    }

    public async Task PullAsync(
        string repositoryPath,
        string remoteName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(remoteName) || remoteName == "-")
        {
            throw new ArgumentException("A remote is required.", nameof(remoteName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        // 未显式配置 pull.ff 时，Desktop 采用 fast-forward 优先；这里固定使用 --ff，避免静默 rebase。
        await RunRequiredGitAsync(root, cancellationToken, "pull", "--ff", "--recurse-submodules", remoteName);
    }

    public async Task PushAsync(
        string repositoryPath,
        string remoteName,
        string branchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(remoteName) || remoteName == "-")
        {
            throw new ArgumentException("A remote is required.", nameof(remoteName));
        }

        if (string.IsNullOrWhiteSpace(branchName) || branchName.StartsWith("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A local branch is required.", nameof(branchName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var upstream = await RunOptionalGitAsync(root, cancellationToken, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}");
        if (string.IsNullOrWhiteSpace(upstream))
        {
            // 首次推送本地分支时建立 upstream，后续 ahead/behind 才能通过 status -b 正确计算。
            await RunRequiredGitAsync(root, cancellationToken, "push", "--set-upstream", remoteName, branchName);
            return;
        }

        await RunRequiredGitAsync(root, cancellationToken, "push", remoteName, branchName);
    }

    public async Task PushTagsAsync(
        string repositoryPath,
        string remoteName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteName) || remoteName == "-")
        {
            throw new ArgumentException("A remote name is required.", nameof(remoteName));
        }

        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredGitAsync(root, cancellationToken, "push", remoteName.Trim(), "--tags");
    }

    public async Task<bool> CreateStashAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(branchName) || branchName.StartsWith("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A local branch is required.", nameof(branchName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        // Desktop 使用特殊 marker 识别自己创建的 stash；这里保持兼容，后续才能只显示当前分支相关的 stash。
        var message = CreateDesktopStashMessage(branchName);
        // 使用 --include-untracked，避免未跟踪文件被遗留在工作区，符合“Stash all changes”的用户预期。
        var result = await RunRequiredGitAsync(root, cancellationToken, "stash", "push", "--include-untracked", "-m", message);
        return !string.Equals(result, "No local changes to save", StringComparison.OrdinalIgnoreCase);
    }

    public async Task RestoreStashAsync(
        string repositoryPath,
        string stashName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(stashName))
        {
            throw new ArgumentException("A stash name is required.", nameof(stashName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        // pop 会恢复文件并删除 stash；如果发生冲突，Git 会保留未成功删除的 stash 并把错误返回给上层 UI。
        await RunRequiredGitAsync(root, cancellationToken, "stash", "pop", "--quiet", stashName);
    }

    public async Task DiscardStashAsync(
        string repositoryPath,
        string stashName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(stashName))
        {
            throw new ArgumentException("A stash name is required.", nameof(stashName));
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        await RunRequiredGitAsync(root, cancellationToken, "stash", "drop", stashName);
    }

    public async Task DiscardChangesAsync(
        string repositoryPath,
        IReadOnlyList<GitChangeItem> changes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        var normalizedChanges = changes
            .Where(change => change.GitPaths.Count > 0)
            .ToArray();

        if (normalizedChanges.Length == 0)
        {
            return;
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var trackedPaths = normalizedChanges
            .Where(change => change.Kind != GitChangeKind.Untracked)
            .SelectMany(change => change.GitPaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (trackedPaths.Length > 0)
        {
            // 同时还原 index 和工作区，覆盖已暂存、未暂存和重命名场景，行为接近 Desktop 的 discard changes。
            await RunRequiredGitAsync(root, cancellationToken, CreatePathArguments(new[] { "restore", "--staged", "--worktree" }, trackedPaths));
        }

        foreach (var change in normalizedChanges.Where(change => change.Kind == GitChangeKind.Untracked))
        {
            foreach (var path in change.GitPaths)
            {
                DeleteUntrackedPath(root, path);
            }
        }
    }

    public async Task MarkConflictsResolvedAsync(
        string repositoryPath,
        IReadOnlyList<GitChangeItem> changes,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        var paths = NormalizeGitPaths(changes
            .Where(change => change.IsConflict)
            .SelectMany(change => change.GitPaths)
            .ToArray());
        if (paths.Length == 0)
        {
            return;
        }

        await RunRequiredGitAsync(
            root,
            cancellationToken,
            CreatePathArguments(new[] { "add" }, paths));
    }

    public async Task<string> LoadWorkingTreeDiffAsync(
        string repositoryPath,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        var normalizedPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedPaths.Length == 0)
        {
            return string.Empty;
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var unstaged = await RunOptionalGitAsync(
            root,
            cancellationToken,
            CreatePathArguments(new[] { "diff" }, normalizedPaths));
        var staged = await RunOptionalGitAsync(
            root,
            cancellationToken,
            CreatePathArguments(new[] { "diff", "--cached" }, normalizedPaths));

        return JoinDiffs(unstaged, staged);
    }

    public async Task<GitFileDiffPreview> LoadWorkingTreeDiffPreviewAsync(
        string repositoryPath,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        var normalizedPaths = NormalizeGitPaths(paths);
        if (normalizedPaths.Length == 0)
        {
            return GitFileDiffPreview.TextDiff(string.Empty);
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var oldPath = normalizedPaths[0];
        var currentPath = normalizedPaths[^1];
        var workingTreePath = ResolveRepositoryItemPath(root, currentPath);

        if (IsImagePath(currentPath) || IsImagePath(oldPath))
        {
            // 图片差异需要同时拿到 HEAD 中的旧图和工作区中的新图；旧图写入临时缓存，避免污染仓库。
            var previousImagePath = await ExportBlobToDiffCacheAsync(root, "HEAD", oldPath, cancellationToken);
            var currentImagePath = File.Exists(workingTreePath) ? workingTreePath : null;
            return GitFileDiffPreview.ImageDiff(previousImagePath, currentImagePath, currentImagePath);
        }

        if (File.Exists(workingTreePath)
            && !await IsTrackedPathAsync(root, currentPath, cancellationToken))
        {
            if (LooksLikeBinaryFile(workingTreePath))
            {
                return GitFileDiffPreview.BinaryDiff(workingTreePath);
            }

            return GitFileDiffPreview.TextDiff(
                await CreateNewFileDiffAsync(currentPath, workingTreePath, cancellationToken));
        }

        if (await HasWorkingTreeBinaryDiffAsync(root, normalizedPaths, cancellationToken))
        {
            return GitFileDiffPreview.BinaryDiff(File.Exists(workingTreePath) ? workingTreePath : null);
        }

        var diff = await LoadWorkingTreeDiffAsync(repositoryPath, normalizedPaths, cancellationToken);
        return GitFileDiffPreview.TextDiff(diff);
    }

    public async Task<string> LoadCommitFileDiffAsync(
        string repositoryPath,
        string sha,
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(sha) || string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        return await RunOptionalGitAsync(
            root,
            cancellationToken,
            "show",
            "--format=",
            "--diff-merges=first-parent",
            "--find-renames",
            sha,
            "--",
            path);
    }

    public async Task<GitFileDiffPreview> LoadCommitFileDiffPreviewAsync(
        string repositoryPath,
        string sha,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(sha))
        {
            return GitFileDiffPreview.TextDiff(string.Empty);
        }

        var normalizedPaths = NormalizeGitPaths(paths);
        if (normalizedPaths.Length == 0)
        {
            return GitFileDiffPreview.TextDiff(string.Empty);
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var oldPath = normalizedPaths[0];
        var currentPath = normalizedPaths[^1];

        if (IsImagePath(currentPath) || IsImagePath(oldPath))
        {
            // 提交历史中的图片来自对象库，必须导出到缓存文件后再交给 Avalonia Image 控件加载。
            var previousImagePath = await ExportBlobToDiffCacheAsync(root, $"{sha}^", oldPath, cancellationToken);
            var currentImagePath = await ExportBlobToDiffCacheAsync(root, sha, currentPath, cancellationToken);
            return GitFileDiffPreview.ImageDiff(previousImagePath, currentImagePath, null);
        }

        if (await HasCommitBinaryDiffAsync(root, sha, normalizedPaths, cancellationToken))
        {
            return GitFileDiffPreview.BinaryDiff(null);
        }

        var diff = await RunOptionalGitAsync(
            root,
            cancellationToken,
            CreatePathArguments(
                new[] { "show", "--format=", "--diff-merges=first-parent", "--find-renames", sha },
                normalizedPaths));
        return GitFileDiffPreview.TextDiff(diff);
    }

    public async Task CommitAsync(
        string repositoryPath,
        IReadOnlyList<string> includedPaths,
        string summary,
        string description,
        bool amendLastCommit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("A commit summary is required.", nameof(summary));
        }

        var normalizedIncludedPaths = includedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (!amendLastCommit && normalizedIncludedPaths.Length == 0)
        {
            throw new InvalidOperationException("Select one or more files to commit.");
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var status = await RunRequiredGitAsync(root, cancellationToken, "status", "--porcelain=v1");
        // Desktop 的提交面板允许只提交勾选文件。先把已跟踪文件全部退回，再只 add 勾选路径。
        var trackedPaths = GitRepositoryOutputParser.ParseChanges(status)
            .Where(change => change.Kind != GitChangeKind.Untracked)
            .SelectMany(change => change.GitPaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (trackedPaths.Length > 0)
        {
            await RunRequiredGitAsync(root, cancellationToken, CreatePathArguments("reset", trackedPaths));
        }

        if (normalizedIncludedPaths.Length > 0)
        {
            await RunRequiredGitAsync(root, cancellationToken, CreatePathArguments("add", normalizedIncludedPaths));
        }

        var commitArguments = new List<string> { "commit" };
        if (amendLastCommit)
        {
            commitArguments.Add("--amend");
        }

        commitArguments.Add("-m");
        commitArguments.Add(summary.Trim());
        if (!string.IsNullOrWhiteSpace(description))
        {
            commitArguments.Add("-m");
            commitArguments.Add(description.Trim());
        }

        await RunRequiredGitAsync(root, cancellationToken, commitArguments.ToArray());
    }

    public async Task CreateTagAsync(
        string repositoryPath,
        string tagName,
        string message,
        string targetSha,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        var normalizedTagName = tagName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTagName))
        {
            throw new ArgumentException("A tag name is required.", nameof(tagName));
        }

        if (string.IsNullOrWhiteSpace(targetSha))
        {
            throw new ArgumentException("A target commit is required.", nameof(targetSha));
        }

        await RunRequiredGitAsync(root, cancellationToken, "check-ref-format", $"refs/tags/{normalizedTagName}");
        if (string.IsNullOrWhiteSpace(message))
        {
            await RunRequiredGitAsync(root, cancellationToken, "tag", normalizedTagName, targetSha.Trim());
            return;
        }

        await RunRequiredGitAsync(root, cancellationToken, "tag", "-a", normalizedTagName, targetSha.Trim(), "-m", message.Trim());
    }

    public async Task RevertCommitAsync(
        string repositoryPath,
        string sha,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(sha))
        {
            throw new ArgumentException("A commit SHA is required.", nameof(sha));
        }

        await RunRequiredGitAsync(root, cancellationToken, "revert", "--no-edit", sha.Trim());
    }

    public async Task CherryPickCommitAsync(
        string repositoryPath,
        string sha,
        CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(sha))
        {
            throw new ArgumentException("A commit SHA is required.", nameof(sha));
        }

        await RunRequiredGitAsync(root, cancellationToken, "cherry-pick", sha.Trim());
    }

    private static async Task<string> ResolveBranchAsync(string root, CancellationToken cancellationToken)
    {
        var branch = await RunRequiredGitAsync(root, cancellationToken, "rev-parse", "--abbrev-ref", "HEAD");
        if (!branch.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            return branch;
        }

        var shortSha = await RunOptionalGitAsync(root, cancellationToken, "rev-parse", "--short", "HEAD");
        return string.IsNullOrWhiteSpace(shortSha) ? "HEAD" : $"HEAD ({shortSha})";
    }

    private static async Task<string> ResolveRootAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            throw new DirectoryNotFoundException(repositoryPath);
        }

        return await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
    }

    private static string ResolveRemoteName(string upstream, string remotes)
    {
        // 优先使用当前 upstream 的远端名；没有 upstream 时回退到 origin 或第一个远端。
        if (!string.IsNullOrWhiteSpace(upstream))
        {
            var separatorIndex = upstream.IndexOf('/', StringComparison.Ordinal);
            if (separatorIndex > 0)
            {
                return upstream[..separatorIndex];
            }
        }

        var remoteNames = remotes
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (remoteNames.Length == 0)
        {
            return "-";
        }

        return remoteNames.FirstOrDefault(remote => remote.Equals("origin", StringComparison.OrdinalIgnoreCase))
            ?? remoteNames[0];
    }

    private static string NormalizeRemoteName(string remoteName)
    {
        if (string.IsNullOrWhiteSpace(remoteName))
        {
            throw new ArgumentException("A remote name is required.", nameof(remoteName));
        }

        var normalizedRemoteName = remoteName.Trim();
        if (normalizedRemoteName.StartsWith("-", StringComparison.Ordinal)
            || normalizedRemoteName.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("The remote name is invalid.", nameof(remoteName));
        }

        return normalizedRemoteName;
    }

    private static bool RemoteExists(string remotes, string remoteName)
    {
        return remotes
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(remote => string.Equals(remote, remoteName, StringComparison.Ordinal));
    }

    private static async Task<string> ResolveDefaultBranchAsync(
        string root,
        string remoteName,
        CancellationToken cancellationToken)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(remoteName) && remoteName != "-")
        {
            var remoteHead = await RunOptionalGitAsync(
                root,
                cancellationToken,
                "symbolic-ref",
                "--quiet",
                "--short",
                $"refs/remotes/{remoteName}/HEAD");
            var prefix = remoteName + "/";
            if (remoteHead.StartsWith(prefix, StringComparison.Ordinal))
            {
                candidates.Add(remoteHead[prefix.Length..]);
            }
            else if (!string.IsNullOrWhiteSpace(remoteHead))
            {
                candidates.Add(remoteHead.Trim());
            }
        }

        candidates.Add("main");
        candidates.Add("master");
        foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
        {
            if (await LocalBranchExistsAsync(root, candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return "-";
    }

    private static async Task<bool> LocalBranchExistsAsync(
        string root,
        string branchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return false;
        }

        var resolved = await RunOptionalGitAsync(
            root,
            cancellationToken,
            "rev-parse",
            "--verify",
            "--quiet",
            $"refs/heads/{branchName}");
        return !string.IsNullOrWhiteSpace(resolved);
    }

    private static async Task<DateTimeOffset?> ResolveLastFetchedAtAsync(string root, CancellationToken cancellationToken)
    {
        // Git 没有直接记录“最后一次 fetch 时间”的 porcelain 输出，Desktop 也会展示近似状态；
        // 这里用 FETCH_HEAD 的修改时间作为 UI 上的“最近获取”时间。
        var gitDirectory = await RunOptionalGitAsync(root, cancellationToken, "rev-parse", "--git-dir");
        if (string.IsNullOrWhiteSpace(gitDirectory))
        {
            return null;
        }

        var fullGitDirectory = Path.IsPathFullyQualified(gitDirectory)
            ? gitDirectory
            : Path.GetFullPath(Path.Combine(root, gitDirectory));
        var fetchHeadPath = Path.Combine(fullGitDirectory, "FETCH_HEAD");
        return File.Exists(fetchHeadPath)
            ? new DateTimeOffset(File.GetLastWriteTime(fetchHeadPath))
            : null;
    }

    private static async Task<RepositoryOperationState> ResolveOperationStateAsync(
        string root,
        CancellationToken cancellationToken)
    {
        var gitDirectory = await RunOptionalGitAsync(root, cancellationToken, "rev-parse", "--git-dir");
        if (string.IsNullOrWhiteSpace(gitDirectory))
        {
            return RepositoryOperationState.None;
        }

        var fullGitDirectory = Path.IsPathFullyQualified(gitDirectory)
            ? gitDirectory
            : Path.GetFullPath(Path.Combine(root, gitDirectory));
        if (Directory.Exists(Path.Combine(fullGitDirectory, "rebase-merge"))
            || Directory.Exists(Path.Combine(fullGitDirectory, "rebase-apply")))
        {
            return RepositoryOperationState.Rebase;
        }

        if (File.Exists(Path.Combine(fullGitDirectory, "MERGE_HEAD")))
        {
            return RepositoryOperationState.Merge;
        }

        if (File.Exists(Path.Combine(fullGitDirectory, "REVERT_HEAD")))
        {
            return RepositoryOperationState.Revert;
        }

        return File.Exists(Path.Combine(fullGitDirectory, "CHERRY_PICK_HEAD"))
            ? RepositoryOperationState.CherryPick
            : RepositoryOperationState.None;
    }

    private static async Task<GitStashEntry?> LoadCurrentBranchStashAsync(
        string root,
        string branchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(branchName) || branchName.StartsWith("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var stashLog = await RunOptionalGitAsync(
            root,
            cancellationToken,
            "log",
            "-g",
            "--format=%gd%x09%H%x09%gs",
            "refs/stash",
            "--");

        return ParseDesktopStashes(stashLog)
            .FirstOrDefault(stash => string.Equals(stash.BranchName, branchName, StringComparison.Ordinal));
    }

    private static IReadOnlyList<GitStashEntry> ParseDesktopStashes(string stashLog)
    {
        if (string.IsNullOrWhiteSpace(stashLog))
        {
            return Array.Empty<GitStashEntry>();
        }

        return stashLog
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseDesktopStash)
            .Where(stash => stash is not null)
            .Cast<GitStashEntry>()
            .ToArray();
    }

    private static GitStashEntry? ParseDesktopStash(string line)
    {
        var fields = line.Split('\t', 3);
        if (fields.Length < 3)
        {
            return null;
        }

        var match = DesktopStashEntryMessageRegex.Match(fields[2]);
        if (!match.Success)
        {
            return null;
        }

        return new GitStashEntry(
            Name: fields[0],
            StashSha: fields[1],
            BranchName: match.Groups["branch"].Value,
            Message: fields[2]);
    }

    private static string CreateDesktopStashMessage(string branchName) =>
        $"{DesktopStashEntryMarker}<{branchName}>";

    private static Task<string> RunRequiredGitAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        return RunGitAsync(workingDirectory, arguments, cancellationToken, allowFailure: false);
    }

    private static Task<string> RunOptionalGitAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        return RunGitAsync(workingDirectory, arguments, cancellationToken, allowFailure: true);
    }

    private static string[] CreatePathArguments(string command, IReadOnlyList<string> paths)
    {
        return CreatePathArguments(new[] { command }, paths);
    }

    private static string[] CreatePathArguments(IReadOnlyList<string> prefix, IReadOnlyList<string> paths)
    {
        var arguments = new List<string>(prefix.Count + paths.Count + 1);
        arguments.AddRange(prefix);
        arguments.Add("--");
        arguments.AddRange(paths);
        return arguments.ToArray();
    }

    private static string[] NormalizeGitPaths(IReadOnlyList<string> paths)
    {
        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsImagePath(string path) =>
        ImageFileExtensions.Contains(Path.GetExtension(path));

    private static async Task<bool> HasWorkingTreeBinaryDiffAsync(
        string root,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var numstat = await RunOptionalGitAsync(
            root,
            cancellationToken,
            CreatePathArguments(new[] { "diff", "--numstat", "HEAD" }, paths));
        return ContainsBinaryNumstat(numstat);
    }

    private static async Task<bool> IsTrackedPathAsync(
        string root,
        string path,
        CancellationToken cancellationToken)
    {
        var trackedPath = await RunOptionalGitAsync(
            root,
            cancellationToken,
            "ls-files",
            "--error-unmatch",
            "--",
            path);
        return !string.IsNullOrWhiteSpace(trackedPath);
    }

    private static bool LooksLikeBinaryFile(string filePath)
    {
        Span<byte> buffer = stackalloc byte[8192];
        using var stream = File.OpenRead(filePath);
        var read = stream.Read(buffer);
        return buffer[..read].Contains((byte)0);
    }

    private static async Task<string> CreateNewFileDiffAsync(
        string gitPath,
        string filePath,
        CancellationToken cancellationToken)
    {
        var relativePath = NormalizeDiffPath(gitPath);
        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine($"diff --git a/{relativePath} b/{relativePath}");
        builder.AppendLine("new file mode 100644");
        builder.AppendLine("--- /dev/null");
        builder.AppendLine($"+++ b/{relativePath}");

        if (lines.Length == 0)
        {
            return builder.ToString();
        }

        // Git 不会为未跟踪文件输出 diff；这里生成最小 unified diff，让 UI 与 Desktop 一样展示新增全文。
        builder.AppendLine($"@@ -0,0 +1,{lines.Length} @@");
        foreach (var line in lines)
        {
            builder.Append('+');
            builder.AppendLine(line);
        }

        return builder.ToString();

        static string NormalizeDiffPath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
        }
    }

    private static async Task<bool> HasCommitBinaryDiffAsync(
        string root,
        string sha,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var numstat = await RunOptionalGitAsync(
            root,
            cancellationToken,
            CreatePathArguments(new[] { "show", "--format=", "--numstat", sha }, paths));
        return ContainsBinaryNumstat(numstat);
    }

    private static bool ContainsBinaryNumstat(string numstat)
    {
        return numstat
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.StartsWith("-\t-\t", StringComparison.Ordinal));
    }

    private static string ResolveRepositoryItemPath(string root, string gitPath)
    {
        if (string.IsNullOrWhiteSpace(gitPath))
        {
            return root;
        }

        if (Path.IsPathFullyQualified(gitPath))
        {
            throw new InvalidOperationException($"Refusing to resolve a rooted path: {gitPath}");
        }

        var fullPath = Path.GetFullPath(Path.Combine(root, gitPath));
        var relativePath = Path.GetRelativePath(root, fullPath);
        if (IsOutsideRepository(relativePath))
        {
            throw new InvalidOperationException($"Refusing to resolve a path outside the repository: {gitPath}");
        }

        return fullPath;
    }

    private static async Task<string?> ExportBlobToDiffCacheAsync(
        string root,
        string revision,
        string gitPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(revision) || string.IsNullOrWhiteSpace(gitPath))
        {
            return null;
        }

        var extension = Path.GetExtension(gitPath);
        var cacheDirectory = GetDiffCacheDirectory(root);
        Directory.CreateDirectory(cacheDirectory);
        var outputPath = Path.Combine(cacheDirectory, $"{Guid.NewGuid():N}{extension}");
        var ok = await RunGitToFileAsync(
            root,
            outputPath,
            cancellationToken,
            "show",
            $"{revision}:{gitPath}");
        if (!ok)
        {
            TryDeleteFile(outputPath);
            return null;
        }

        return outputPath;
    }

    private static string GetDiffCacheDirectory(string root)
    {
        var cacheRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(cacheRoot))
        {
            cacheRoot = Path.GetTempPath();
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root)))[..16];
        return Path.Combine(cacheRoot, "AvaGithubDesktop", "DiffCache", hash);
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // 清理缓存失败不应打断差异查看，后续缓存目录仍可复用。
        }
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
            // git init 失败时的清理只做 best effort，真正的错误继续交给调用方展示。
        }
    }

    private static void DeleteUntrackedPath(string root, string gitPath)
    {
        if (string.IsNullOrWhiteSpace(gitPath))
        {
            return;
        }

        if (Path.IsPathFullyQualified(gitPath))
        {
            throw new InvalidOperationException($"Refusing to delete a rooted path: {gitPath}");
        }

        var fullPath = Path.GetFullPath(Path.Combine(root, gitPath));
        var relativePath = Path.GetRelativePath(root, fullPath);
        if (IsOutsideRepository(relativePath))
        {
            throw new InvalidOperationException($"Refusing to delete a path outside the repository: {gitPath}");
        }

        // 未跟踪文件不受 Git 版本库保护，删除前必须确认路径仍在仓库根目录之内。
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return;
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }

    private static bool IsOutsideRepository(string relativePath)
    {
        return Path.IsPathFullyQualified(relativePath)
            || relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string JoinDiffs(string unstaged, string staged)
    {
        if (string.IsNullOrWhiteSpace(unstaged))
        {
            return staged;
        }

        if (string.IsNullOrWhiteSpace(staged))
        {
            return unstaged;
        }

        return $"{unstaged}{Environment.NewLine}{Environment.NewLine}{staged}";
    }

    private static async Task<string> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool allowFailure)
    {
        var commandText = GitCommandLog.FormatCommand(workingDirectory, arguments);
        var stopwatch = Stopwatch.StartNew();
        GitCommandLog.LogStarted(commandText);
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["GIT_EDITOR"] = "true";
        startInfo.Environment["GIT_MERGE_AUTOEDIT"] = "no";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        int exitCode;
        string stdout;
        string stderr;
        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start git.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            stdout = (await stdoutTask).Trim();
            stderr = (await stderrTask).Trim();
            exitCode = process.ExitCode;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            GitCommandLog.LogFailed(commandText, stopwatch.Elapsed, ex);
            throw;
        }

        stopwatch.Stop();
        GitCommandLog.LogCompleted(commandText, exitCode, stopwatch.Elapsed);

        if (exitCode != 0 && !allowFailure)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "Git command failed." : stderr);
        }

        return exitCode == 0 ? stdout : string.Empty;
    }

    private static async Task<bool> RunGitToFileAsync(
        string workingDirectory,
        string outputPath,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var commandText = GitCommandLog.FormatCommand(workingDirectory, arguments);
        var stopwatch = Stopwatch.StartNew();
        GitCommandLog.LogStarted(commandText);
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        int exitCode;
        long outputLength;
        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start git.");

            await using var outputStream = File.Create(outputPath);
            var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await stdoutTask;
            await outputStream.FlushAsync(cancellationToken);
            await stderrTask;
            exitCode = process.ExitCode;
            outputLength = new FileInfo(outputPath).Length;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            GitCommandLog.LogFailed(commandText, stopwatch.Elapsed, ex);
            throw;
        }

        stopwatch.Stop();
        GitCommandLog.LogCompleted(commandText, exitCode, stopwatch.Elapsed);

        return exitCode == 0 && outputLength > 0;
    }
}
