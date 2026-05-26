using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IAccountDialogService
{
    Task<GitHubSignInRequest?> ShowSignInDialogAsync(
        string defaultEndpoint,
        CancellationToken cancellationToken);
}
