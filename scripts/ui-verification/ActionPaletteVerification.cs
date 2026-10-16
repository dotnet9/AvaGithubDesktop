using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaGithubDesktop.Controls;
using CodeWF.Avalonia.Controls.Themes;

/// <summary>独立检查真实图片模式与提交按钮，不读取账号、仓库或用户配置。</summary>
internal static class ActionPaletteVerification
{
    public static void Run(string output)
    {
        var image = new ImageDiffViewer();
        var commit = new Button { Content = "Commit", Classes = { "commitButton" }, Width = 260 };
        var window = new Window { Width = 1100, Height = 720, Content = new StackPanel { Children = { commit, image } } };
        window.Bind(Window.BackgroundProperty, window.GetResourceObservable("StudioBackground"));
        var rows = new List<string> { "palette\tcontrol\tstate\tforeground\tbackground\tcontrast" };
        var failures = new List<string>();
        try
        {
            window.Show();
            foreach (var palette in Enum.GetValues<StudioPalette>())
            foreach (var dark in new[] { false, true })
            {
                var mode = palette + (dark ? "-dark" : "-light");
                Application.Current!.RequestedThemeVariant = StudioTheme.GetVariant(palette, dark);
                Pump();
                var toggles = image.GetVisualDescendants().OfType<ToggleButton>().Where(button => button.Classes.Contains("imageDiffMode")).ToArray();
                if (toggles.Length != 4) throw new InvalidOperationException("Four real image comparison modes are required.");
                var selected = toggles.Single(button => button.IsChecked == true);
                foreach (var control in new Button[] { commit, selected })
                {
                    window.MouseMove(new Point(1, 1));
                    Pump();
                    Check(control, "normal");
                    var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
                    window.MouseMove(point);
                    Pump();
                    Check(control, "hover");
                    window.MouseDown(point, MouseButton.Left);
                    Pump();
                    Check(control, "pressed");
                    window.MouseMove(new Point(1, 1));
                    window.MouseUp(new Point(1, 1), MouseButton.Left);
                    control.IsEnabled = false;
                    Pump();
                    if (control.IsEffectivelyEnabled) failures.Add(mode + "/Disabled state is ineffective.");
                    control.IsEnabled = true;
                    Pump();
                    Check(control, "reenabled");
                }

                void Check(Button button, string state)
                {
                    var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(item => item.Name == "PART_ContentPresenter");
                    var canvas = ((ISolidColorBrush)window.Background!).Color;
                    var background = Composite(presenter.Background ?? button.Background ?? Brushes.Transparent, canvas);
                    var text = button.GetVisualDescendants().OfType<TextBlock>().First(block => block.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(block.Text));
                    var foreground = Composite(text.Foreground!, background, text.Opacity);
                    var ratio = Contrast(foreground, background);
                    var name = button == commit ? "commit" : "image-mode";
                    rows.Add(string.Join('\t', mode, name, state, foreground, background, ratio.ToString("F3", CultureInfo.InvariantCulture)));
                    if (ratio < 4.5) failures.Add($"{mode}/{name}/{state}: {ratio:F3}:1");
                }
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(output, "action-colors.tsv"), rows);
            File.WriteAllLines(Path.Combine(output, "action-failures.txt"), failures);
            window.Close();
        }
        Console.WriteLine($"Action palette audit: {rows.Count - 1} actual text/background comparisons, {failures.Count} failures.");
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));

        void Pump()
        {
            for (var pass = 0; pass < 3; pass++) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        }
    }

    private static Color Composite(IBrush brush, Color background, double opacity = 1)
    {
        if (brush is not ISolidColorBrush solid) throw new InvalidOperationException("Expected a resolved solid brush: " + brush);
        var alpha = solid.Color.A / 255d * solid.Opacity * opacity;
        byte Blend(byte front, byte back) => (byte)Math.Round(front * alpha + back * (1 - alpha));
        return Color.FromRgb(Blend(solid.Color.R, background.R), Blend(solid.Color.G, background.G), Blend(solid.Color.B, background.B));
    }

    private static double Contrast(Color first, Color second)
    {
        static double Channel(byte channel) { var value = channel / 255d; return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
        static double Luminance(Color value) => Channel(value.R) * .2126 + Channel(value.G) * .7152 + Channel(value.B) * .0722;
        return (Math.Max(Luminance(first), Luminance(second)) + .05) / (Math.Min(Luminance(first), Luminance(second)) + .05);
    }
}
