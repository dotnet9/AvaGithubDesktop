namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryShellService
{
    Task OpenInShellAsync(string repositoryPath);

    Task ShowInFileManagerAsync(string repositoryPath);

    Task OpenUrlAsync(string url);

    Task CopyTextAsync(string text);
}
