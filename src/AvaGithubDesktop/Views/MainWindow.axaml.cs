using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaGithubDesktop.ViewModels;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class MainWindow : CodeWFWindow
{
    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        Opened += async (_, _) => await viewModel.InitializeAsync();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.T:
                ShowRepositorySelector();
                e.Handled = true;
                break;
            case Key.B:
                ShowBranchSelector();
                e.Handled = true;
                break;
            case Key.L:
                FocusChangesFilter();
                e.Handled = true;
                break;
        }
    }

    private void ShowRepositorySelector()
    {
        RepositorySelectorButton.Flyout?.ShowAt(RepositorySelectorButton);
        Dispatcher.UIThread.Post(() => RepositoryFilterTextBox.Focus());
    }

    private void ShowRepositorySelectorMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ShowRepositorySelector();
    }

    private void ShowBranchSelector()
    {
        if (!BranchSelectorButton.IsEnabled)
        {
            return;
        }

        BranchSelectorButton.Flyout?.ShowAt(BranchSelectorButton);
        Dispatcher.UIThread.Post(() => BranchFilterTextBox.Focus());
    }

    private void ShowBranchSelectorMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ShowBranchSelector();
    }

    private void FocusChangesFilter()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            ExecuteCommandIfPossible(viewModel.ShowChangesCommand);
        }

        Dispatcher.UIThread.Post(() => ChangesFilterTextBox.Focus());
    }

    private void FocusChangesFilterMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        FocusChangesFilter();
    }

    private static void ExecuteCommandIfPossible(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
