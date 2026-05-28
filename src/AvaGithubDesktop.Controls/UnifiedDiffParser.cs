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

    public static IReadOnlyList<UnifiedDiffSplitLine> ToSplitLines(IReadOnlyList<UnifiedDiffLine> lines)
    {
        if (lines.Count == 0)
        {
            return Array.Empty<UnifiedDiffSplitLine>();
        }

        var result = new List<UnifiedDiffSplitLine>(lines.Count);
        var removed = new List<UnifiedDiffLine>();
        var added = new List<UnifiedDiffLine>();

        foreach (var line in lines)
        {
            if (line.Kind == UnifiedDiffLineKind.Removed)
            {
                removed.Add(line);
                continue;
            }

            if (line.Kind == UnifiedDiffLineKind.Added)
            {
                added.Add(line);
                continue;
            }

            FlushChangedLines(result, removed, added);
            result.Add(ToFullWidthOrContextSplitLine(line));
        }

        FlushChangedLines(result, removed, added);
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

    private static UnifiedDiffSplitLine ToFullWidthOrContextSplitLine(UnifiedDiffLine line)
    {
        if (line.Kind == UnifiedDiffLineKind.Context)
        {
            return new UnifiedDiffSplitLine(
                false,
                line.Kind,
                line.Prefix,
                line.Content,
                line.Kind,
                line.OldLineNumber,
                line.Prefix,
                line.Content,
                line.Kind,
                line.NewLineNumber,
                line.Prefix,
                line.Content);
        }

        return new UnifiedDiffSplitLine(
            true,
            line.Kind,
            line.Prefix,
            line.Content,
            line.Kind,
            string.Empty,
            string.Empty,
            string.Empty,
            line.Kind,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private static void FlushChangedLines(
        ICollection<UnifiedDiffSplitLine> result,
        IList<UnifiedDiffLine> removed,
        IList<UnifiedDiffLine> added)
    {
        if (removed.Count == 0 && added.Count == 0)
        {
            return;
        }

        var count = Math.Max(removed.Count, added.Count);
        for (var index = 0; index < count; index++)
        {
            var oldLine = index < removed.Count ? removed[index] : null;
            var newLine = index < added.Count ? added[index] : null;
            result.Add(new UnifiedDiffSplitLine(
                false,
                oldLine?.Kind ?? newLine?.Kind ?? UnifiedDiffLineKind.Context,
                string.Empty,
                string.Empty,
                oldLine?.Kind ?? UnifiedDiffLineKind.Context,
                oldLine?.OldLineNumber ?? string.Empty,
                oldLine?.Prefix ?? string.Empty,
                oldLine?.Content ?? string.Empty,
                newLine?.Kind ?? UnifiedDiffLineKind.Context,
                newLine?.NewLineNumber ?? string.Empty,
                newLine?.Prefix ?? string.Empty,
                newLine?.Content ?? string.Empty));
        }

        removed.Clear();
        added.Clear();
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
