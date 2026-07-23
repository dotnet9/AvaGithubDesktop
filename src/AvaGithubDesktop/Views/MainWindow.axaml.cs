using System.ComponentModel;
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
    private const double DefaultOperationLogHeight = 180;
    private const double OperationLogMinHeight = 80;
    private const double WorkspaceSidebarMinWidth = 260;
    private const double WorkspaceSidebarMaxWidth = 560;
    private const double HistoryFileListMinWidth = 220;
    private const double HistoryFileListMaxWidth = 480;
    private const double MinPersistedWindowWidth = 960;
    private const double MinPersistedWindowHeight = 620;
    private const double ResizableStep = 40;
    private WindowState _windowStateBeforeFullScreen = WindowState.Normal;
    private TextBox? _lastFocusedTextBox;
    private GridLength _lastVisibleOperationLogHeight = new(DefaultOperationLogHeight);
    private INotifyPropertyChanged? _subscribedViewModel;
    private RowDefinition OperationLogRow => MainLayoutGrid.RowDefinitions[3];
    private ColumnDefinition WorkspaceSidebarColumn => WorkspaceGrid.ColumnDefinitions[0];
    private ColumnDefinition HistoryFileListColumn => HistoryDiffGrid.ColumnDefinitions[0];

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(GotFocusEvent, OnGotFocus, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => SubscribeToViewModel();
        Closed += (_, _) =>
        {
            SaveWindowSize();
            SaveWorkspaceLayout();
            UnsubscribeFromViewModel();
        };
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        Opened += async (_, _) => await viewModel.InitializeAsync();
    }

    private void SubscribeToViewModel()
    {
        UnsubscribeFromViewModel();

        if (DataContext is MainWindowViewModel viewModel)
        {
            _subscribedViewModel = viewModel;
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyWindowSize(viewModel);
            ApplyWorkspaceLayout(viewModel);
            SyncOperationLogRow(viewModel.IsOperationLogVisible);
        }
    }

    private void UnsubscribeFromViewModel()
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsOperationLogVisible)
            && sender is MainWindowViewModel viewModel)
        {
            SyncOperationLogRow(viewModel.IsOperationLogVisible);
        }
    }

    private void SyncOperationLogRow(bool isVisible)
    {
        if (isVisible)
        {
            OperationLogRow.MinHeight = OperationLogMinHeight;
            OperationLogRow.Height = _lastVisibleOperationLogHeight.Value > 0
                ? _lastVisibleOperationLogHeight
                : new GridLength(DefaultOperationLogHeight);
            return;
        }

        if (OperationLogRow.Height.Value > 0)
        {
            _lastVisibleOperationLogHeight = OperationLogRow.Height;
        }

        OperationLogRow.MinHeight = 0;
        OperationLogRow.Height = new GridLength(0);
    }

    private void ApplyWorkspaceLayout(MainWindowViewModel viewModel)
    {
        ApplyColumnWidth(
            WorkspaceSidebarColumn,
            viewModel.WorkspaceSidebarWidth,
            WorkspaceSidebarMinWidth,
            WorkspaceSidebarMaxWidth);
        ApplyColumnWidth(
            HistoryFileListColumn,
            viewModel.HistoryFileListWidth,
            HistoryFileListMinWidth,
            HistoryFileListMaxWidth);

        if (TryClamp(viewModel.OperationLogHeight, OperationLogMinHeight, double.PositiveInfinity, out var operationLogHeight))
        {
            _lastVisibleOperationLogHeight = new GridLength(operationLogHeight);
        }
    }

    private void SaveWorkspaceLayout()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SaveWorkspaceLayout(
            WorkspaceSidebarColumn.Width.Value,
            HistoryFileListColumn.Width.Value,
            GetCurrentOperationLogHeight());
    }

    private double GetCurrentOperationLogHeight()
    {
        return OperationLogRow.Height.Value > 0
            ? OperationLogRow.Height.Value
            : Math.Max(OperationLogMinHeight, _lastVisibleOperationLogHeight.Value);
    }

    private static void ApplyColumnWidth(ColumnDefinition column, double? width, double min, double max)
    {
        if (TryClamp(width, min, max, out var value))
        {
            column.Width = new GridLength(value);
        }
    }

    private static bool TryClamp(double? value, double min, double max, out double clamped)
    {
        clamped = 0;
        if (value is not { } actual || double.IsNaN(actual) || double.IsInfinity(actual))
        {
            return false;
        }

        clamped = Math.Clamp(actual, min, max);
        return true;
    }

    private void ApplyWindowSize(MainWindowViewModel viewModel)
    {
        if (TryClamp(viewModel.WindowWidth, MinPersistedWindowWidth, double.PositiveInfinity, out var width))
        {
            Width = width;
        }

        if (TryClamp(viewModel.WindowHeight, MinPersistedWindowHeight, double.PositiveInfinity, out var height))
        {
            Height = height;
        }
    }

    private void SaveWindowSize()
    {
        if (DataContext is not MainWindowViewModel viewModel || WindowState != WindowState.Normal)
        {
            return;
        }

        viewModel.SaveWindowSize(
            Math.Max(MinPersistedWindowWidth, Bounds.Width),
            Math.Max(MinPersistedWindowHeight, Bounds.Height));
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
            case Key.F:
                FocusChangesFilter();
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
            case Key.D9:
            case Key.NumPad9:
                ExpandActiveResizable();
                e.Handled = true;
                break;
            case Key.D8:
            case Key.NumPad8:
                ContractActiveResizable();
                e.Handled = true;
                break;
        }
    }

    private void OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox textBox)
        {
            _lastFocusedTextBox = textBox;
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

    private void ExpandActiveResizableMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ExpandActiveResizable();
    }

    private void ContractActiveResizableMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ContractActiveResizable();
    }

    private void ExpandActiveResizable()
    {
        AdjustActiveResizable(ResizableStep);
    }

    private void ContractActiveResizable()
    {
        AdjustActiveResizable(-ResizableStep);
    }

    private void AdjustActiveResizable(double delta)
    {
        if (DataContext is MainWindowViewModel { IsHistorySelected: true })
        {
            AdjustColumnWidth(HistoryFileListColumn, delta, HistoryFileListMinWidth, HistoryFileListMaxWidth);
        }
        else
        {
            AdjustColumnWidth(WorkspaceSidebarColumn, delta, WorkspaceSidebarMinWidth, WorkspaceSidebarMaxWidth);
        }

        SaveWorkspaceLayout();
    }

    private static void AdjustColumnWidth(ColumnDefinition column, double delta, double min, double max)
    {
        var currentWidth = column.Width.Value > 0 ? column.Width.Value : min;
        column.Width = new GridLength(Math.Clamp(currentWidth + delta, min, max));
    }

    private void ExitMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UndoMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ExecuteTextBoxAction(textBox => textBox.Undo());
    }

    private void RedoMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ExecuteTextBoxAction(textBox => textBox.Redo());
    }

    private void CutMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ExecuteTextBoxAction(textBox => textBox.Cut());
    }

    private void CopyMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ExecuteTextBoxAction(textBox => textBox.Copy());
    }

    private void PasteMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ExecuteTextBoxAction(textBox => textBox.Paste());
    }

    private void SelectAllMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        ExecuteTextBoxAction(textBox => textBox.SelectAll());
    }

    private void ExecuteTextBoxAction(Action<TextBox> action)
    {
        if (GetEditTargetTextBox() is { } textBox)
        {
            action(textBox);
        }
    }

    private TextBox? GetEditTargetTextBox()
    {
        return TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as TextBox
               ?? _lastFocusedTextBox;
    }

    private static void ExecuteCommandIfPossible(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
