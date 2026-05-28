using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryOperationCommandService
{
    Task<string?> RunAsync(RepositoryOperationCommandRequest request);
}
