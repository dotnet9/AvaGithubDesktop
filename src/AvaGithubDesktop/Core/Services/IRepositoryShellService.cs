namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryShellService
{
    Task OpenInShellAsync(string repositoryPath);

    Task ShowInFileManagerAsync(string repositoryPath);

    Task ShowItemInFileManagerAsync(string itemPath);

    Task OpenItemAsync(string itemPath);

    Task OpenUrlAsync(string url);

    Task CopyTextAsync(string text);
}
