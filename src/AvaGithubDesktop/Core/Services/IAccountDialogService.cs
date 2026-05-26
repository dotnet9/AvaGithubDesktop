using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IAccountDialogService
{
    Task<GitHubAccount?> ShowSignInDialogAsync(
        string defaultEndpoint,
        CancellationToken cancellationToken);
}
