namespace AvaGithubDesktop.Core.Models;

public sealed record GitStashEntry(
    string Name,
    string StashSha,
    string BranchName,
    string Message)
{
    public string ShortSha => StashSha.Length <= 7 ? StashSha : StashSha[..7];
}
