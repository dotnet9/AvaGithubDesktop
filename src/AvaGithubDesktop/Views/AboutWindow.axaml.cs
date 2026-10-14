using CodeWF.Avalonia.Controls.Controls;
using AvaGithubDesktop.ViewModels;

namespace AvaGithubDesktop.Views;

public partial class AboutWindow : CodeWFWindow
{
    public AboutWindow() => InitializeComponent();
    public AboutWindow(AboutViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += OnRequestClose;
        Closed += (_, _) =>
        {
            viewModel.RequestClose -= OnRequestClose;
            viewModel.Dispose();
        };
    }
    private void OnRequestClose(object? sender, object? result) => Close();
}
