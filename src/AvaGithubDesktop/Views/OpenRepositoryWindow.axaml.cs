using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class OpenRepositoryWindow : CodeWFWindow
{
    public OpenRepositoryWindow()
    {
        InitializeComponent();
    }

    public OpenRepositoryWindow(OpenRepositoryWindowViewModel viewModel)
        : this()
    {
        viewModel.CloseRequested += ViewModel_OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= ViewModel_OnCloseRequested;
        DataContext = viewModel;
    }

    private void ViewModel_OnCloseRequested(
        object? sender,
        DialogCloseRequestedEventArgs<RepositoryOpenRequest?> e)
    {
        Close(e.Result);
    }
}
