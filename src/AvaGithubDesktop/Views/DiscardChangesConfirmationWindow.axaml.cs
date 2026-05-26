using AvaGithubDesktop.ViewModels;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class DiscardChangesConfirmationWindow : CodeWFWindow
{
    public DiscardChangesConfirmationWindow()
    {
        InitializeComponent();
    }

    public DiscardChangesConfirmationWindow(
        string title,
        string message,
        string warning,
        IReadOnlyList<string> paths,
        string cancelText,
        string discardText)
        : this()
    {
        Title = title;
        var viewModel = new DiscardChangesConfirmationWindowViewModel(
            title,
            message,
            warning,
            paths,
            cancelText,
            discardText);
        viewModel.CloseRequested += ViewModel_OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= ViewModel_OnCloseRequested;
        DataContext = viewModel;
    }

    private void ViewModel_OnCloseRequested(
        object? sender,
        DialogCloseRequestedEventArgs<bool> e)
    {
        Close(e.Result);
    }
}
