using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class CreateRepositoryWindow : CodeWFWindow
{
    public CreateRepositoryWindow()
    {
        InitializeComponent();
    }

    public CreateRepositoryWindow(CreateRepositoryWindowViewModel viewModel)
        : this()
    {
        viewModel.CloseRequested += ViewModel_OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= ViewModel_OnCloseRequested;
        DataContext = viewModel;
    }

    private void ViewModel_OnCloseRequested(
        object? sender,
        DialogCloseRequestedEventArgs<RepositoryCreationRequest?> e)
    {
        Close(e.Result);
    }
}
