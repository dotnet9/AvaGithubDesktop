using Avalonia;
using Avalonia.Controls.Primitives;

namespace AvaGithubDesktop.Controls;

public sealed class UnifiedDiffViewer : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<UnifiedDiffViewer, string?>(nameof(Text));

    public static readonly StyledProperty<IReadOnlyList<UnifiedDiffLine>> LinesProperty =
        AvaloniaProperty.Register<UnifiedDiffViewer, IReadOnlyList<UnifiedDiffLine>>(nameof(Lines), Array.Empty<UnifiedDiffLine>());

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IReadOnlyList<UnifiedDiffLine> Lines
    {
        get => GetValue(LinesProperty);
        private set => SetValue(LinesProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Lines = UnifiedDiffParser.Parse(Text);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            Lines = UnifiedDiffParser.Parse(Text);
        }
    }
}
