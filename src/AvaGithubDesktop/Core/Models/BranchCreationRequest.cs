namespace AvaGithubDesktop.Core.Models;

public sealed record BranchCreationRequest(
    string BranchName,
    string? StartPoint,
    bool CheckoutBranch);
