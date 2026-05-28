using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace AvaGithubDesktop.Controls;

public sealed class UnifiedDiffSideLinePresenter : TemplatedControl
{
    public static readonly StyledProperty<UnifiedDiffLineKind> KindProperty =
        AvaloniaProperty.Register<UnifiedDiffSideLinePresenter, UnifiedDiffLineKind>(nameof(Kind), UnifiedDiffLineKind.Context);

    public static readonly StyledProperty<string> LineNumberProperty =
        AvaloniaProperty.Register<UnifiedDiffSideLinePresenter, string>(nameof(LineNumber), string.Empty);

    public static readonly StyledProperty<string> PrefixProperty =
        AvaloniaProperty.Register<UnifiedDiffSideLinePresenter, string>(nameof(Prefix), string.Empty);

    public static readonly StyledProperty<string> ContentProperty =
        AvaloniaProperty.Register<UnifiedDiffSideLinePresenter, string>(nameof(Content), string.Empty);

    public static readonly StyledProperty<IBrush?> GutterBackgroundProperty =
        AvaloniaProperty.Register<UnifiedDiffSideLinePresenter, IBrush?>(nameof(GutterBackground));

    public static readonly StyledProperty<IBrush?> GutterForegroundProperty =
        AvaloniaProperty.Register<UnifiedDiffSideLinePresenter, IBrush?>(nameof(GutterForeground));

    public static readonly StyledProperty<IBrush?> PrefixForegroundProperty =
        AvaloniaProperty.Register<UnifiedDiffSideLinePresenter, IBrush?>(nameof(PrefixForeground));

    public static readonly StyledProperty<IBrush?> ContentForegroundProperty =
        AvaloniaProperty.Register<UnifiedDiffSideLinePresenter, IBrush?>(nameof(ContentForeground));

    public UnifiedDiffLineKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string LineNumber
    {
        get => GetValue(LineNumberProperty);
        set => SetValue(LineNumberProperty, value);
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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdatePseudoClasses();
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
