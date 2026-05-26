namespace AvaGithubDesktop.Controls;

public sealed record UnifiedDiffLine(
    UnifiedDiffLineKind Kind,
    string OldLineNumber,
    string NewLineNumber,
    string Prefix,
    string Content);
