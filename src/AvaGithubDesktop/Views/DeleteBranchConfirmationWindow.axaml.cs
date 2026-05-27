using AvaGithubDesktop.ViewModels;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class DeleteBranchConfirmationWindow : CodeWFWindow
{
    public DeleteBranchConfirmationWindow()
    {
        InitializeComponent();
    }

    public DeleteBranchConfirmationWindow(DeleteBranchConfirmationWindowViewModel viewModel)
        : this()
    {
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
