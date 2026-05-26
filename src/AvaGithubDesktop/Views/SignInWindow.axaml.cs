using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class SignInWindow : CodeWFWindow
{
    public SignInWindow()
    {
        InitializeComponent();
    }

    public SignInWindow(SignInWindowViewModel viewModel)
        : this()
    {
        viewModel.CloseRequested += ViewModel_OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= ViewModel_OnCloseRequested;
        DataContext = viewModel;
        Title = viewModel.Title;
    }

    private void ViewModel_OnCloseRequested(
        object? sender,
        DialogCloseRequestedEventArgs<GitHubAccount?> e)
    {
        Close(e.Result);
    }
}
