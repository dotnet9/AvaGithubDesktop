using CodeWF.EventBus;

namespace AvaGithubDesktop.Core.Messaging;

public sealed class RepositoryOpenedCommand : Command
{
    public RepositoryOpenedCommand(string repositoryName, int changedFilesCount)
    {
        RepositoryName = repositoryName;
        ChangedFilesCount = changedFilesCount;
    }

    public string RepositoryName { get; }

    public int ChangedFilesCount { get; }
}
