using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryOpenDialogService
{
    Task<RepositoryOpenRequest?> ShowOpenRepositoryDialogAsync(string initialPath);
}
