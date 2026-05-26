using System.Reactive;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class DiscardChangesConfirmationWindowViewModel : ViewModelBase
{
    public DiscardChangesConfirmationWindowViewModel(
        string title,
        string message,
        string warning,
        IReadOnlyList<string> paths,
        string cancelText,
        string discardText)
    {
        Title = title;
        Message = message;
        Warning = warning;
        Paths = paths
            .Select(path => new DiscardChangesPathItemViewModel(path))
            .ToArray();
        CancelText = cancelText;
        DiscardText = discardText;

        CancelCommand = ReactiveCommand.Create(() => RequestClose(false));
        DiscardCommand = ReactiveCommand.Create(() => RequestClose(true));
    }

    public event EventHandler<DialogCloseRequestedEventArgs<bool>>? CloseRequested;

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> DiscardCommand { get; }

    public string Title { get; }

    public string Message { get; }

    public string Warning { get; }

    public IReadOnlyList<DiscardChangesPathItemViewModel> Paths { get; }

    public string CancelText { get; }

    public string DiscardText { get; }

    private void RequestClose(bool result)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<bool>(result));
    }
}

public sealed record DiscardChangesPathItemViewModel(string Path);
