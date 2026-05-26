namespace AvaGithubDesktop.Core.Models;

public sealed record GitCommitItem(
    string Sha,
    string ShortSha,
    string Summary,
    string AuthorName,
    string AuthorEmail,
    string Date,
    string RelativeDate,
    IReadOnlyList<GitCommitFileItem> Files)
{
    public int ChangedFilesCount => Files.Count;

    public string AuthorDisplay => string.IsNullOrWhiteSpace(AuthorEmail)
        ? AuthorName
        : $"{AuthorName} <{AuthorEmail}>";
}
