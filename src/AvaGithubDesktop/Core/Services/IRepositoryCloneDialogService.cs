using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryCloneDialogService
{
    Task<RepositoryCloneRequest?> ShowCloneRepositoryDialogAsync();
}
