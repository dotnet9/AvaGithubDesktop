using System.Text.RegularExpressions;
using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

internal static class GitRepositoryOutputParser
{
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

    private static int ParseIntGroup(string value, string pattern)
    {
        var match = Regex.Match(value, pattern, RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["count"].Value, out var count) ? count : 0;
    }
}
