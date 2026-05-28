namespace AvaGithubDesktop.Controls;

public sealed record UnifiedDiffSplitLine(
    bool IsFullWidth,
    UnifiedDiffLineKind Kind,
    string Prefix,
    string Content,
    UnifiedDiffLineKind OldKind,
    string OldLineNumber,
    string OldPrefix,
    string OldContent,
    UnifiedDiffLineKind NewKind,
    string NewLineNumber,
    string NewPrefix,
    string NewContent)
{
    public bool IsSplitPair => !IsFullWidth;
}
