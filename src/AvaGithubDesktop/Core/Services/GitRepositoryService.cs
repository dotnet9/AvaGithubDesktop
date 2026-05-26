using System.Diagnostics;
using System.Text.RegularExpressions;
using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public sealed class GitRepositoryService : IGitRepositoryService
{
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
        var remote = await RunOptionalGitAsync(root, cancellationToken, "remote", "get-url", "origin");
        var lastCommit = await RunOptionalGitAsync(root, cancellationToken, "log", "-1", "--pretty=format:%h%x09%s%x09%cr");
        var changes = ParseChanges(status);
        var (ahead, behind) = ParseAheadBehind(status);

        return new GitRepositorySnapshot(
            RepositoryName: new DirectoryInfo(root).Name,
            RootPath: root,
            CurrentBranch: branch,
            Upstream: string.IsNullOrWhiteSpace(upstream) ? "-" : upstream,
            RemoteUrl: string.IsNullOrWhiteSpace(remote) ? "-" : remote,
            LastCommit: FormatLastCommit(lastCommit),
            Ahead: ahead,
            Behind: behind,
            Changes: changes);
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
