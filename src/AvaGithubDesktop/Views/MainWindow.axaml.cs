using AvaGithubDesktop.ViewModels;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class MainWindow : CodeWFWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        Opened += async (_, _) => await viewModel.InitializeAsync();
    }
}
