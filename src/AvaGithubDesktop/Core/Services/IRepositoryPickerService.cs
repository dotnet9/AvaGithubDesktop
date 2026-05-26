namespace AvaGithubDesktop.Core.Services;

public interface IRepositoryPickerService
{
    Task<string?> PickRepositoryAsync();
}
