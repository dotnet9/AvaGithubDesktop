using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryCreationDialogService
{
    Task<RepositoryCreationRequest?> ShowCreateRepositoryDialogAsync();
}
