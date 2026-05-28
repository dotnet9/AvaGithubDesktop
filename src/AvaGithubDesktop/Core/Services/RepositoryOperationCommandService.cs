using AvaGithubDesktop.Core.Messaging;
using AvaGithubDesktop.Core.Models;
using CodeWF.EventBus;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryOperationCommandService : IRepositoryOperationCommandService
{
    private readonly IEventBus _eventBus;

    public RepositoryOperationCommandService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task<string?> RunAsync(RepositoryOperationCommandRequest request)
    {
        if (!request.CanRun)
        {
            return null;
        }

        request.SetBusy(true);
        _eventBus.Publish(new StatusMessageChangedCommand(request.StartedMessage));

        try
        {
            await request.Operation();
            await request.ReloadWorkspaceAsync();
            _eventBus.Publish(new StatusMessageChangedCommand(request.CreateCompletedMessage()));
            return null;
        }
        catch (Exception ex)
        {
            await request.TryReloadWorkspaceAsync();
            var errorMessage = request.CreateFailedMessage(ex);
            _eventBus.Publish(new StatusMessageChangedCommand(errorMessage));
            return errorMessage;
        }
        finally
        {
            request.SetBusy(false);
        }
    }
}
