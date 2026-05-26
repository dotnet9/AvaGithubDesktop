namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryShellService
{
    Task OpenInShellAsync(string repositoryPath);

    Task ShowInFileManagerAsync(string repositoryPath);
}
