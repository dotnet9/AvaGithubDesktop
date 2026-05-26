using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace AvaGithubDesktop.Controls;

public sealed class UnifiedDiffLinePresenter : TemplatedControl
{
    public static readonly StyledProperty<UnifiedDiffLineKind> KindProperty =
        AvaloniaProperty.Register<UnifiedDiffLinePresenter, UnifiedDiffLineKind>(nameof(Kind), UnifiedDiffLineKind.Context);

    public static readonly StyledProperty<string> OldLineNumberProperty =
        AvaloniaProperty.Register<UnifiedDiffLinePresenter, string>(nameof(OldLineNumber), string.Empty);

    public static readonly StyledProperty<string> NewLineNumberProperty =
        AvaloniaProperty.Register<UnifiedDiffLinePresenter, string>(nameof(NewLineNumber), string.Empty);

    public static readonly StyledProperty<string> PrefixProperty =
        AvaloniaProperty.Register<UnifiedDiffLinePresenter, string>(nameof(Prefix), string.Empty);

    public static readonly StyledProperty<string> ContentProperty =
        AvaloniaProperty.Register<UnifiedDiffLinePresenter, string>(nameof(Content), string.Empty);

    public static readonly StyledProperty<IBrush?> GutterBackgroundProperty =
        AvaloniaProperty.Register<UnifiedDiffLinePresenter, IBrush?>(nameof(GutterBackground));

    public static readonly StyledProperty<IBrush?> GutterForegroundProperty =
        AvaloniaProperty.Register<UnifiedDiffLinePresenter, IBrush?>(nameof(GutterForeground));

    public static readonly StyledProperty<IBrush?> PrefixForegroundProperty =
        AvaloniaProperty.Register<UnifiedDiffLinePresenter, IBrush?>(nameof(PrefixForeground));

    public static readonly StyledProperty<IBrush?> ContentForegroundProperty =
        AvaloniaProperty.Register<UnifiedDiffLinePresenter, IBrush?>(nameof(ContentForeground));

    public UnifiedDiffLineKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string OldLineNumber
    {
        get => GetValue(OldLineNumberProperty);
        set => SetValue(OldLineNumberProperty, value);
    }

    public string NewLineNumber
    {
        get => GetValue(NewLineNumberProperty);
        set => SetValue(NewLineNumberProperty, value);
    }

    public string Prefix
    {
        get => GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    public string Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public IBrush? GutterBackground
    {
        get => GetValue(GutterBackgroundProperty);
        set => SetValue(GutterBackgroundProperty, value);
    }

    public IBrush? GutterForeground
    {
        get => GetValue(GutterForegroundProperty);
        set => SetValue(GutterForegroundProperty, value);
    }

    public IBrush? PrefixForeground
    {
        get => GetValue(PrefixForegroundProperty);
        set => SetValue(PrefixForegroundProperty, value);
    }

    public IBrush? ContentForeground
    {
        get => GetValue(ContentForegroundProperty);
        set => SetValue(ContentForegroundProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == KindProperty)
        {
            UpdatePseudoClasses();
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses()
    {
        SetPseudoClass(":message", Kind == UnifiedDiffLineKind.Message);
        SetPseudoClass(":header", Kind == UnifiedDiffLineKind.Header);
        SetPseudoClass(":hunk", Kind == UnifiedDiffLineKind.Hunk);
        SetPseudoClass(":added", Kind == UnifiedDiffLineKind.Added);
        SetPseudoClass(":removed", Kind == UnifiedDiffLineKind.Removed);
    }

    private void SetPseudoClass(string name, bool isActive)
    {
        if (isActive)
        {
            PseudoClasses.Add(name);
            return;
        }

        PseudoClasses.Remove(name);
    }
}
