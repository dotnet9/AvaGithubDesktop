using Avalonia.Interactivity;
using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class DiscardChangesConfirmationWindow : CodeWFWindow
{
    public DiscardChangesConfirmationWindow()
    {
        InitializeComponent();
    }

    public DiscardChangesConfirmationWindow(
        string title,
        string message,
        string warning,
        IReadOnlyList<string> paths,
        string cancelText,
        string discardText)
        : this()
    {
        Title = title;
        var pathItems = paths
            .Select(path => new DiscardChangesPathItem(path))
            .ToArray();
        DataContext = new DiscardChangesConfirmationWindowModel(
            title,
            message,
            warning,
            pathItems,
            cancelText,
            discardText);
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void Discard_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}

public sealed record DiscardChangesConfirmationWindowModel(
    string Title,
    string Message,
    string Warning,
    IReadOnlyList<DiscardChangesPathItem> Paths,
    string CancelText,
    string DiscardText);

public sealed record DiscardChangesPathItem(string Path);
