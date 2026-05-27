using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public sealed class GitRepositoryService : IGitRepositoryService
{
    private const char CommitRecordSeparator = '\u001e';
    private const char CommitFieldSeparator = '\u001f';
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
        var remote = remoteName == "-"
            ? string.Empty
            : await RunOptionalGitAsync(root, cancellationToken, "remote", "get-url", remoteName);
        var lastCommit = await RunOptionalGitAsync(root, cancellationToken, "log", "-1", "--pretty=format:%h%x09%s%x09%cr");
        var changes = ParseChanges(status);
        var (ahead, behind) = ParseAheadBehind(status);
        var lastFetchedAt = await ResolveLastFetchedAtAsync(root, cancellationToken);
        var currentBranchStash = await LoadCurrentBranchStashAsync(root, branch, cancellationToken);

        return new GitRepositorySnapshot(
            RepositoryName: new DirectoryInfo(root).Name,
            RootPath: root,
            CurrentBranch: branch,
            Upstream: string.IsNullOrWhiteSpace(upstream) ? "-" : upstream,
            RemoteName: remoteName,
            RemoteUrl: string.IsNullOrWhiteSpace(remote) ? "-" : remote,
            LastFetchedAt: lastFetchedAt,
            LastCommit: FormatLastCommit(lastCommit),
            Ahead: ahead,
            Behind: behind,
            CurrentBranchStash: currentBranchStash,
            Changes: changes);
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

        return ParseHistory(history);
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
        return ParseBranches(branches);
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

        if (normalizedIncludedPaths.Length == 0)
        {
            throw new InvalidOperationException("Select one or more files to commit.");
        }

        var root = await RunRequiredGitAsync(repositoryPath, cancellationToken, "rev-parse", "--show-toplevel");
        var status = await RunRequiredGitAsync(root, cancellationToken, "status", "--porcelain=v1");
        // Desktop 的提交面板允许只提交勾选文件。先把已跟踪文件全部退回，再只 add 勾选路径。
        var trackedPaths = ParseChanges(status)
            .Where(change => change.Kind != GitChangeKind.Untracked)
            .SelectMany(change => change.GitPaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (trackedPaths.Length > 0)
        {
            await RunRequiredGitAsync(root, cancellationToken, CreatePathArguments("reset", trackedPaths));
        }

        await RunRequiredGitAsync(root, cancellationToken, CreatePathArguments("add", normalizedIncludedPaths));

        var commitArguments = new List<string> { "commit", "-m", summary.Trim() };
        if (!string.IsNullOrWhiteSpace(description))
        {
            commitArguments.Add("-m");
            commitArguments.Add(description.Trim());
        }

        await RunRequiredGitAsync(root, cancellationToken, commitArguments.ToArray());
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

    private static IReadOnlyList<GitChangeItem> ParseChanges(string status)
    {
        return status
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith("## ", StringComparison.Ordinal))
            .Select(ParseChange)
            .Where(change => !string.IsNullOrWhiteSpace(change.Path))
            .ToArray();
    }

    private static GitChangeItem ParseChange(string line)
    {
        var statusCode = line.Length >= 2 ? line[..2] : line.Trim();
        var path = line.Length > 3 ? DecodeGitStatusPath(line[3..]) : string.Empty;
        var kind = statusCode == "??"
            ? GitChangeKind.Untracked
            : statusCode[0] != ' '
                ? GitChangeKind.Staged
                : GitChangeKind.Unstaged;

        return new GitChangeItem(statusCode.Trim(), path, kind);
    }

    private static string DecodeGitStatusPath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        var renameSeparatorIndex = rawPath.IndexOf(" -> ", StringComparison.Ordinal);
        if (renameSeparatorIndex < 0)
        {
            return DecodeGitQuotedPath(rawPath);
        }

        var oldPath = DecodeGitQuotedPath(rawPath[..renameSeparatorIndex]);
        var newPath = DecodeGitQuotedPath(rawPath[(renameSeparatorIndex + 4)..]);
        return string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath)
            ? DecodeGitQuotedPath(rawPath)
            : $"{oldPath} -> {newPath}";
    }

    private static IReadOnlyList<GitCommitItem> ParseHistory(string history)
    {
        if (string.IsNullOrWhiteSpace(history))
        {
            return Array.Empty<GitCommitItem>();
        }

        return history
            .Split(CommitRecordSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseCommit)
            .Where(commit => !string.IsNullOrWhiteSpace(commit.Sha))
            .ToArray();
    }

    private static GitCommitItem ParseCommit(string record)
    {
        var lines = record
            .Trim('\r', '\n')
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
        {
            return EmptyCommit();
        }

        var fields = lines[0].Split(CommitFieldSeparator);
        if (fields.Length < 7)
        {
            return EmptyCommit();
        }

        var files = lines
            .Skip(1)
            .Select(ParseCommitFile)
            .Where(file => !string.IsNullOrWhiteSpace(file.Path))
            .ToArray();

        return new GitCommitItem(
            Sha: fields[0],
            ShortSha: fields[1],
            AuthorName: fields[2],
            AuthorEmail: fields[3],
            Date: fields[4],
            RelativeDate: fields[5],
            Summary: string.IsNullOrWhiteSpace(fields[6]) ? "Empty commit message" : fields[6],
            Files: files);
    }

    private static GitCommitFileItem ParseCommitFile(string line)
    {
        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return new GitCommitFileItem(string.Empty, string.Empty);
        }

        var statusCode = parts[0];
        var path = (statusCode.StartsWith('R') || statusCode.StartsWith('C')) && parts.Length >= 3
            ? $"{DecodeGitQuotedPath(parts[1])} -> {DecodeGitQuotedPath(parts[2])}"
            : DecodeGitQuotedPath(parts[^1]);
        return new GitCommitFileItem(statusCode, path);
    }

    private static string DecodeGitQuotedPath(string value)
    {
        var path = value.Trim();
        if (path.Length < 2 || path[0] != '"' || path[^1] != '"')
        {
            return path;
        }

        // Git 默认 core.quotePath=true，中文路径会以 C-style 八进制字节输出；
        // 这里按 UTF-8 字节还原，避免提交时把转义文本当成真实 pathspec。
        var content = path[1..^1];
        var builder = new StringBuilder(content.Length);
        var pendingBytes = new List<byte>();

        for (var index = 0; index < content.Length; index++)
        {
            var current = content[index];
            if (current != '\\')
            {
                FlushPendingBytes(builder, pendingBytes);
                builder.Append(current);
                continue;
            }

            if (index == content.Length - 1)
            {
                FlushPendingBytes(builder, pendingBytes);
                builder.Append('\\');
                break;
            }

            var escaped = content[++index];
            if (escaped is >= '0' and <= '7')
            {
                var byteValue = escaped - '0';
                var consumedDigits = 1;
                while (consumedDigits < 3
                       && index + 1 < content.Length
                       && content[index + 1] is >= '0' and <= '7')
                {
                    byteValue = (byteValue * 8) + (content[++index] - '0');
                    consumedDigits++;
                }

                pendingBytes.Add((byte)byteValue);
                continue;
            }

            FlushPendingBytes(builder, pendingBytes);
            builder.Append(escaped switch
            {
                'a' => '\a',
                'b' => '\b',
                'f' => '\f',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'v' => '\v',
                '\\' => '\\',
                '"' => '"',
                _ => escaped
            });
        }

        FlushPendingBytes(builder, pendingBytes);
        return builder.ToString();
    }

    private static void FlushPendingBytes(StringBuilder builder, List<byte> pendingBytes)
    {
        if (pendingBytes.Count == 0)
        {
            return;
        }

        builder.Append(Encoding.UTF8.GetString(pendingBytes.ToArray()));
        pendingBytes.Clear();
    }

    private static GitCommitItem EmptyCommit()
    {
        return new GitCommitItem(
            Sha: string.Empty,
            ShortSha: string.Empty,
            Summary: string.Empty,
            AuthorName: string.Empty,
            AuthorEmail: string.Empty,
            Date: string.Empty,
            RelativeDate: string.Empty,
            Files: Array.Empty<GitCommitFileItem>());
    }

    private static IReadOnlyList<GitBranchItem> ParseBranches(string branches)
    {
        if (string.IsNullOrWhiteSpace(branches))
        {
            return Array.Empty<GitBranchItem>();
        }

        return branches
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseBranch)
            .Where(branch => !string.IsNullOrWhiteSpace(branch.Name))
            .ToArray();
    }

    private static GitBranchItem ParseBranch(string line)
    {
        var fields = line.Split('\t');
        if (fields.Length < 1)
        {
            return new GitBranchItem(string.Empty, "-", string.Empty, false);
        }

        var upstream = fields.Length >= 2 && !string.IsNullOrWhiteSpace(fields[1]) ? fields[1] : "-";
        var relativeDate = fields.Length >= 3 ? fields[2] : string.Empty;
        return new GitBranchItem(
            Name: fields[0],
            Upstream: upstream,
            RelativeDate: relativeDate,
            IsCurrent: fields.Length >= 4 && fields[3].Trim() == "*");
    }

    private static (int Ahead, int Behind) ParseAheadBehind(string status)
    {
        var branchLine = status
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith("## ", StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(branchLine))
        {
            return (0, 0);
        }

        var ahead = ParseIntGroup(branchLine, "ahead (?<count>\\d+)");
        var behind = ParseIntGroup(branchLine, "behind (?<count>\\d+)");
        return (ahead, behind);
    }

    private static int ParseIntGroup(string value, string pattern)
    {
        var match = Regex.Match(value, pattern, RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["count"].Value, out var count) ? count : 0;
    }

    private static string FormatLastCommit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var parts = value.Split('\t');
        if (parts.Length < 3)
        {
            return value.Trim();
        }

        return $"{parts[0]} {parts[1]} ({parts[2]})";
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

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0 && !allowFailure)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "Git command failed." : stderr);
        }

        return process.ExitCode == 0 ? stdout : string.Empty;
    }

    private static async Task<bool> RunGitToFileAsync(
        string workingDirectory,
        string outputPath,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
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

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git.");

        await using var outputStream = File.Create(outputPath);
        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await stdoutTask;
        await outputStream.FlushAsync(cancellationToken);
        await stderrTask;

        if (process.ExitCode != 0)
        {
            return false;
        }

        return new FileInfo(outputPath).Length > 0;
    }
}
