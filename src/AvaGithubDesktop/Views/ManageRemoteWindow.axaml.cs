using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class ManageRemoteWindow : CodeWFWindow
{
    public ManageRemoteWindow()
    {
        InitializeComponent();
    }

    public ManageRemoteWindow(ManageRemoteWindowViewModel viewModel)
        : this()
    {
        viewModel.CloseRequested += ViewModel_OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= ViewModel_OnCloseRequested;
        DataContext = viewModel;
    }

    private void ViewModel_OnCloseRequested(
        object? sender,
        DialogCloseRequestedEventArgs<RepositoryRemoteRequest?> e)
    {
        Close(e.Result);
    }
}
