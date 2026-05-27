namespace AvaGithubDesktop.Core.Models;

public sealed record BranchRenameRequest(
    string OldBranchName,
    string NewBranchName);
