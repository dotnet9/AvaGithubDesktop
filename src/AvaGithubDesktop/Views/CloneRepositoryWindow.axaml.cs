using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class CloneRepositoryWindow : CodeWFWindow
{
    public CloneRepositoryWindow()
    {
        InitializeComponent();
    }

    public CloneRepositoryWindow(CloneRepositoryWindowViewModel viewModel)
        : this()
    {
        viewModel.CloseRequested += ViewModel_OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= ViewModel_OnCloseRequested;
        DataContext = viewModel;
    }

    private void ViewModel_OnCloseRequested(
        object? sender,
        DialogCloseRequestedEventArgs<RepositoryCloneRequest?> e)
    {
        Close(e.Result);
    }
}
