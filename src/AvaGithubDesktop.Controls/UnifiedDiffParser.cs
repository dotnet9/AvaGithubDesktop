using System.Text.RegularExpressions;

namespace AvaGithubDesktop.Controls;

internal static partial class UnifiedDiffParser
{
    public static IReadOnlyList<UnifiedDiffLine> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<UnifiedDiffLine>();
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new List<UnifiedDiffLine>(lines.Length);
        var oldLine = 0;
        var newLine = 0;
        var hasHunk = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine ?? string.Empty;
            var hunkMatch = HunkHeaderRegex().Match(line);
            if (hunkMatch.Success)
            {
                oldLine = int.Parse(hunkMatch.Groups["old"].Value);
                newLine = int.Parse(hunkMatch.Groups["new"].Value);
                hasHunk = true;
                result.Add(new UnifiedDiffLine(UnifiedDiffLineKind.Hunk, string.Empty, string.Empty, "@@", line));
                continue;
            }

            if (!hasHunk)
            {
                result.Add(ToMetadataLine(line));
                continue;
            }

            if (line.StartsWith('+') && !line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                result.Add(new UnifiedDiffLine(UnifiedDiffLineKind.Added, string.Empty, newLine.ToString(), "+", TrimDiffPrefix(line)));
                newLine++;
                continue;
            }

            if (line.StartsWith('-') && !line.StartsWith("--- ", StringComparison.Ordinal))
            {
                result.Add(new UnifiedDiffLine(UnifiedDiffLineKind.Removed, oldLine.ToString(), string.Empty, "-", TrimDiffPrefix(line)));
                oldLine++;
                continue;
            }

            if (IsMetadataLine(line))
            {
                result.Add(ToMetadataLine(line));
                continue;
            }

            var content = line.Length > 0 && line[0] == ' '
                ? line[1..]
                : line;
            result.Add(new UnifiedDiffLine(UnifiedDiffLineKind.Context, oldLine.ToString(), newLine.ToString(), string.Empty, content));
            oldLine++;
            newLine++;
        }

        return result;
    }

    private static UnifiedDiffLine ToMetadataLine(string line)
    {
        if (IsMetadataLine(line))
        {
            return new UnifiedDiffLine(UnifiedDiffLineKind.Header, string.Empty, string.Empty, string.Empty, line);
        }

        // 加载中、无差异、错误提示等非 diff 文本走消息样式，避免显示无意义行号。
        return new UnifiedDiffLine(UnifiedDiffLineKind.Message, string.Empty, string.Empty, string.Empty, line);
    }

    private static bool IsMetadataLine(string line)
    {
        return line.StartsWith("diff --git ", StringComparison.Ordinal)
            || line.StartsWith("index ", StringComparison.Ordinal)
            || line.StartsWith("--- ", StringComparison.Ordinal)
            || line.StartsWith("+++ ", StringComparison.Ordinal)
            || line.StartsWith("new file mode ", StringComparison.Ordinal)
            || line.StartsWith("deleted file mode ", StringComparison.Ordinal)
            || line.StartsWith("old mode ", StringComparison.Ordinal)
            || line.StartsWith("new mode ", StringComparison.Ordinal)
            || line.StartsWith("similarity index ", StringComparison.Ordinal)
            || line.StartsWith("dissimilarity index ", StringComparison.Ordinal)
            || line.StartsWith("rename from ", StringComparison.Ordinal)
            || line.StartsWith("rename to ", StringComparison.Ordinal);
    }

    private static string TrimDiffPrefix(string line)
    {
        return line.Length > 0 ? line[1..] : string.Empty;
    }

    [GeneratedRegex(@"^@@\s+-(?<old>\d+)(?:,\d+)?\s+\+(?<new>\d+)(?:,\d+)?\s+@@")]
    private static partial Regex HunkHeaderRegex();
}
