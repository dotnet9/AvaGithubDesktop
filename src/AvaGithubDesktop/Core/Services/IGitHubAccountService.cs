using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IGitHubAccountService
{
    GitHubAccount? CurrentAccount { get; }

    Task<IReadOnlyList<GitHubAccount>> LoadAsync(CancellationToken cancellationToken);

    Task<GitHubDeviceAuthorization> BeginDeviceAuthorizationAsync(
        string endpoint,
        CancellationToken cancellationToken);

    Task<GitHubAccount> CompleteDeviceSignInAsync(
        GitHubDeviceAuthorization authorization,
        CancellationToken cancellationToken);

    Task<GitHubAccount> SignInWithTokenAsync(
        string endpoint,
        string token,
        CancellationToken cancellationToken);

    Task SignOutAsync(
        GitHubAccount account,
        CancellationToken cancellationToken);
}
