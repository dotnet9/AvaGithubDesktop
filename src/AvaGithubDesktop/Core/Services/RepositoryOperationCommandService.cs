using AvaGithubDesktop.Core.Messaging;
using AvaGithubDesktop.Core.Models;
using CodeWF.EventBus;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryOperationCommandService : IRepositoryOperationCommandService
{
    private readonly IAppLocalizer _localizer;
    private readonly IEventBus _eventBus;

    public RepositoryOperationCommandService(IAppLocalizer localizer, IEventBus eventBus)
    {
        _localizer = localizer;
        _eventBus = eventBus;
    }

    public async Task<string?> RunAsync(RepositoryOperationCommandRequest request)
    {
        if (!request.CanRun)
        {
            return null;
        }

        request.SetBusy(true);
        _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(request.StartedKey)));

        try
        {
            await request.Operation();
            await request.ReloadWorkspaceAsync();
            _eventBus.Publish(new StatusMessageChangedCommand(_localizer.Get(request.CompletedKey)));
            return null;
        }
        catch (Exception ex)
        {
            await request.TryReloadWorkspaceAsync();
            var errorMessage = _localizer.Format(request.FailedFormatKey, ex.Message);
            _eventBus.Publish(new StatusMessageChangedCommand(errorMessage));
            return errorMessage;
        }
        finally
        {
            request.SetBusy(false);
        }
    }
}
