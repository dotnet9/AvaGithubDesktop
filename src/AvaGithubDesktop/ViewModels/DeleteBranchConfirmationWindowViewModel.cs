using System.Reactive;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class DeleteBranchConfirmationWindowViewModel : ViewModelBase
{
    public DeleteBranchConfirmationWindowViewModel(
        string branchName,
        IAppLocalizer localizer)
    {
        BranchName = branchName;
        Title = localizer.Get(AvaGithubDesktopL.DeleteBranchTitle);
        Message = localizer.Format(AvaGithubDesktopL.DeleteBranchMessageFormat, branchName);
        Warning = localizer.Get(AvaGithubDesktopL.DeleteBranchWarning);
        CancelText = localizer.Get(AvaGithubDesktopL.Cancel);
        DeleteText = localizer.Get(AvaGithubDesktopL.DeleteBranchButton);

        CancelCommand = ReactiveCommand.Create(() => RequestClose(false));
        DeleteCommand = ReactiveCommand.Create(() => RequestClose(true));
    }

    public event EventHandler<DialogCloseRequestedEventArgs<bool>>? CloseRequested;

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    public string BranchName { get; }

    public string Title { get; }

    public string Message { get; }

    public string Warning { get; }

    public string CancelText { get; }

    public string DeleteText { get; }

    private void RequestClose(bool result)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<bool>(result));
    }
}
