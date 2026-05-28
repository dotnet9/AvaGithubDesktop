using System.Text;
using System.Text.RegularExpressions;
using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

internal static class GitRepositoryOutputParser
{
    private const char CommitRecordSeparator = '\u001e';
    private const char CommitFieldSeparator = '\u001f';

    public static IReadOnlyList<GitChangeItem> ParseChanges(string status)
    {
        return status
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith("## ", StringComparison.Ordinal))
            .Select(ParseChange)
            .Where(change => !string.IsNullOrWhiteSpace(change.Path))
            .ToArray();
    }

    public static IReadOnlyList<GitCommitItem> ParseHistory(string history)
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

    public static IReadOnlyList<GitBranchItem> ParseBranches(string branches)
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

    public static IReadOnlyList<GitBranchItem> ParseRemoteBranches(string branches)
    {
        if (string.IsNullOrWhiteSpace(branches))
        {
            return Array.Empty<GitBranchItem>();
        }

        return branches
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseRemoteBranch)
            .Where(branch => !string.IsNullOrWhiteSpace(branch.Name) && !branch.Name.EndsWith("/HEAD", StringComparison.Ordinal))
            .ToArray();
    }

    public static (int Ahead, int Behind) ParseAheadBehind(string status)
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

    public static string FormatLastCommit(string value)
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

    public static string FormatLastCommitSummary(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value.Split('\t');
        return parts.Length >= 2 ? parts[1].Trim() : value.Trim();
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

    private static GitBranchItem ParseRemoteBranch(string line)
    {
        var fields = line.Split('\t');
        if (fields.Length < 1)
        {
            return new GitBranchItem(string.Empty, "-", string.Empty, false);
        }

        return new GitBranchItem(
            Name: fields[0],
            Upstream: "-",
            RelativeDate: fields.Length >= 2 ? fields[1] : string.Empty,
            IsCurrent: false);
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

    private static int ParseIntGroup(string value, string pattern)
    {
        var match = Regex.Match(value, pattern, RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["count"].Value, out var count) ? count : 0;
    }

    private static string DecodeGitQuotedPath(string value)
    {
        var path = value.Trim();
        if (path.Length < 2 || path[0] != '"' || path[^1] != '"')
        {
            return path;
        }

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
}
