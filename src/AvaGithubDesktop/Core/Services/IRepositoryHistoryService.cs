using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryHistoryService
{
    Task<IReadOnlyList<RepositoryHistoryEntry>> LoadKnownRepositoriesAsync(CancellationToken cancellationToken);

    Task AddOrUpdateAsync(string repositoryPath, CancellationToken cancellationToken);
}
