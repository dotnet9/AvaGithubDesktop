namespace AvaGithubDesktop.Core.Models;

public sealed record GitFileDiffPreview(
    GitDiffPreviewKind Kind,
    string Text,
    string? PreviousImagePath,
    string? CurrentImagePath,
    string? WorkingTreePath)
{
    public static GitFileDiffPreview TextDiff(string text) =>
        new(GitDiffPreviewKind.Text, text, null, null, null);

    public static GitFileDiffPreview ImageDiff(
        string? previousImagePath,
        string? currentImagePath,
        string? workingTreePath) =>
        new(GitDiffPreviewKind.Image, string.Empty, previousImagePath, currentImagePath, workingTreePath);

    public static GitFileDiffPreview BinaryDiff(string? workingTreePath) =>
        new(GitDiffPreviewKind.Binary, string.Empty, null, null, workingTreePath);
}
