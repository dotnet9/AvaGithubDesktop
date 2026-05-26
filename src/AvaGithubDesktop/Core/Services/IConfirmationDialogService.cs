namespace AvaGithubDesktop.Core.Services;

public interface IConfirmationDialogService
{
    Task<bool> ShowDiscardChangesConfirmationAsync(IReadOnlyList<string> paths);
}
