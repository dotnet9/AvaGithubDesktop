using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryPickerService : IRepositoryPickerService
{
    private readonly IAppLocalizer _localizer;

    public RepositoryPickerService(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public async Task<string?> PickRepositoryAsync()
    {
        return await PickFolderAsync(_localizer.Get(AvaGithubDesktopL.DialogOpenRepositoryTitle));
    }

    public async Task<string?> PickCloneParentDirectoryAsync()
    {
        return await PickFolderAsync(_localizer.Get(AvaGithubDesktopL.DialogCloneParentDirectoryTitle));
    }

    private static async Task<string?> PickFolderAsync(string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
