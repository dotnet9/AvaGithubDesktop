using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryWorkspaceLoader : IRepositoryWorkspaceLoader
{
    private readonly IGitRepositoryService _gitRepositoryService;

    public RepositoryWorkspaceLoader(IGitRepositoryService gitRepositoryService)
    {
        _gitRepositoryService = gitRepositoryService;
    }

    public async Task<RepositoryWorkspaceState> LoadAsync(
        string repositoryPath,
        int historyCommitLimit,
        CancellationToken cancellationToken)
    {
        var snapshot = await _gitRepositoryService.LoadRepositoryAsync(repositoryPath, cancellationToken);
        var branches = await _gitRepositoryService.LoadBranchesAsync(snapshot.RootPath, cancellationToken);
        var history = await _gitRepositoryService.LoadHistoryAsync(snapshot.RootPath, historyCommitLimit, cancellationToken);
        return new RepositoryWorkspaceState(snapshot, branches, history);
    }
}
