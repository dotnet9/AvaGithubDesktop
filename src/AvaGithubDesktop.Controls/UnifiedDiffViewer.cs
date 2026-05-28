using Avalonia;
using Avalonia.Controls.Primitives;

namespace AvaGithubDesktop.Controls;

public sealed class UnifiedDiffViewer : TemplatedControl
{
    private bool _isUnified = true;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<UnifiedDiffViewer, string?>(nameof(Text));

    public static readonly StyledProperty<IReadOnlyList<UnifiedDiffLine>> LinesProperty =
        AvaloniaProperty.Register<UnifiedDiffViewer, IReadOnlyList<UnifiedDiffLine>>(nameof(Lines), Array.Empty<UnifiedDiffLine>());

    public static readonly StyledProperty<IReadOnlyList<UnifiedDiffSplitLine>> SplitLinesProperty =
        AvaloniaProperty.Register<UnifiedDiffViewer, IReadOnlyList<UnifiedDiffSplitLine>>(nameof(SplitLines), Array.Empty<UnifiedDiffSplitLine>());

    public static readonly StyledProperty<bool> IsSideBySideProperty =
        AvaloniaProperty.Register<UnifiedDiffViewer, bool>(nameof(IsSideBySide));

    public static readonly DirectProperty<UnifiedDiffViewer, bool> IsUnifiedProperty =
        AvaloniaProperty.RegisterDirect<UnifiedDiffViewer, bool>(nameof(IsUnified), viewer => viewer.IsUnified);

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

    public IReadOnlyList<UnifiedDiffSplitLine> SplitLines
    {
        get => GetValue(SplitLinesProperty);
        private set => SetValue(SplitLinesProperty, value);
    }

    public bool IsSideBySide
    {
        get => GetValue(IsSideBySideProperty);
        set => SetValue(IsSideBySideProperty, value);
    }

    public bool IsUnified
    {
        get => _isUnified;
        private set => SetAndRaise(IsUnifiedProperty, ref _isUnified, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ParseText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            ParseText();
        }
        else if (change.Property == IsSideBySideProperty)
        {
            IsUnified = !IsSideBySide;
        }
    }

    private void ParseText()
    {
        var lines = UnifiedDiffParser.Parse(Text);
        Lines = lines;
        SplitLines = UnifiedDiffParser.ToSplitLines(lines);
    }
}
