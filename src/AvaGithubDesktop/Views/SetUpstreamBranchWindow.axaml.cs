using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class SetUpstreamBranchWindow : CodeWFWindow
{
    public SetUpstreamBranchWindow()
    {
        InitializeComponent();
    }

    public SetUpstreamBranchWindow(SetUpstreamBranchWindowViewModel viewModel)
        : this()
    {
        viewModel.CloseRequested += ViewModel_OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= ViewModel_OnCloseRequested;
        DataContext = viewModel;
    }

    private void ViewModel_OnCloseRequested(
        object? sender,
        DialogCloseRequestedEventArgs<BranchUpstreamRequest?> e)
    {
        Close(e.Result);
    }
}
