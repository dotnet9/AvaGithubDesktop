using System.Diagnostics;
using System.Text.RegularExpressions;
using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public sealed class GitRepositoryService : IGitRepositoryService
{
    private const char CommitRecordSeparator = '\u001e';
    private const char CommitFieldSeparator = '\u001f';

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
        var path = line.Length > 3 ? line[3..] : string.Empty;
        var kind = statusCode == "??"
            ? GitChangeKind.Untracked
            : statusCode[0] != ' '
                ? GitChangeKind.Staged
                : GitChangeKind.Unstaged;

        return new GitChangeItem(statusCode.Trim(), path, kind);
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
            ? $"{parts[1]} -> {parts[2]}"
            : parts[^1];
        return new GitCommitFileItem(statusCode, path);
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
}
