using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace AvaGithubDesktop.Controls;

public sealed class BinaryDiffViewer : TemplatedControl
{
    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<BinaryDiffViewer, string>(nameof(Message), "This binary file has changed.");

    public static readonly StyledProperty<string> OpenTextProperty =
        AvaloniaProperty.Register<BinaryDiffViewer, string>(nameof(OpenText), "Open file in external program.");

    public static readonly StyledProperty<ICommand?> OpenCommandProperty =
        AvaloniaProperty.Register<BinaryDiffViewer, ICommand?>(nameof(OpenCommand));

    public static readonly StyledProperty<bool> CanOpenProperty =
        AvaloniaProperty.Register<BinaryDiffViewer, bool>(nameof(CanOpen));

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string OpenText
    {
        get => GetValue(OpenTextProperty);
        set => SetValue(OpenTextProperty, value);
    }

    public ICommand? OpenCommand
    {
        get => GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    public bool CanOpen
    {
        get => GetValue(CanOpenProperty);
        set => SetValue(CanOpenProperty, value);
    }
}
