namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryPickerService
{
    Task<string?> PickRepositoryAsync();

    Task<string?> PickCloneParentDirectoryAsync();

    Task<string?> PickCreateRepositoryParentDirectoryAsync();
}
