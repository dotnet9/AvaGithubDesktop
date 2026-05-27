namespace AvaGithubDesktop.Core.Models;

public sealed record TagCreationRequest(
    string TagName,
    string Message,
    string TargetSha);
