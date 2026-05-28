using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaGithubDesktop.ViewModels;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class MainWindow : CodeWFWindow
{
    private WindowState _windowStateBeforeFullScreen = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        Opened += async (_, _) => await viewModel.InitializeAsync();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11 && e.KeyModifiers == KeyModifiers.None)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers != KeyModifiers.Control)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.D1:
            case Key.NumPad1:
                ShowChanges();
                e.Handled = true;
                break;
            case Key.D2:
            case Key.NumPad2:
                ShowHistory();
                e.Handled = true;
                break;
            case Key.T:
                ShowRepositorySelector();
                e.Handled = true;
                break;
            case Key.B:
                ShowBranchSelector();
                e.Handled = true;
                break;
            case Key.G:
                FocusCommitSummary();
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

    private void ShowChanges()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            ExecuteCommandIfPossible(viewModel.ShowChangesCommand);
        }
    }

    private void ShowHistory()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            ExecuteCommandIfPossible(viewModel.ShowHistoryCommand);
        }
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

    private void FocusCommitSummary()
    {
        if (!CommitSummaryTextBox.IsEnabled)
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            ExecuteCommandIfPossible(viewModel.ShowChangesCommand);
        }

        Dispatcher.UIThread.Post(() => CommitSummaryTextBox.Focus());
    }

    private void FocusCommitSummaryMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        FocusCommitSummary();
    }

    private void ToggleFullScreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = _windowStateBeforeFullScreen == WindowState.FullScreen
                ? WindowState.Normal
                : _windowStateBeforeFullScreen;
            return;
        }

        _windowStateBeforeFullScreen = WindowState;
        WindowState = WindowState.FullScreen;
    }

    private void ToggleFullScreenMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ToggleFullScreen();
    }

    private void ExitMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private static void ExecuteCommandIfPossible(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
