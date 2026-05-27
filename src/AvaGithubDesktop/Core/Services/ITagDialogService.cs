using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface ITagDialogService
{
    Task<TagCreationRequest?> ShowCreateTagDialogAsync(
        GitCommitItem targetCommit,
        IReadOnlySet<string> existingTagNames);
}
