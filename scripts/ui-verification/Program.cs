using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.ReactiveUI;
using System.Reactive.Linq;
using AvaGithubDesktop;
using AvaGithubDesktop.Core.Services;
using AvaGithubDesktop.ViewModels;
using AvaGithubDesktop.Views;
using CodeWF.Avalonia.Controls.Themes;
using CodeWF.Toolkit.Core.UpdateChecking;
using CodeWF.Toolkit.Logging;
using Prism.Ioc;

// 实际 XAML 与资源的离屏回归，不点击系统浏览器，不读取用户工作区。
var directory = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/ui-verification");
Directory.CreateDirectory(directory);
Logger.Initialize(new LoggerOptions { EnableConsole = false });
AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia().WithInterFont().UseReactiveUI().SetupWithoutStarting();
var app = (App)Application.Current!;
var model = app.Container.Resolve<MainWindowViewModel>();
var localizer = app.Container.Resolve<IAppLocalizer>();
var window = new MainWindow(model) { Width = 1440, Height = 900 };
window.Show();
var renders = 0;
foreach (var palette in model.PaletteOptions)
{
    model.SelectPaletteCommand.Execute(palette.Key).Subscribe();
    foreach (var mode in model.ThemeOptions.Where(option => option.Key != "system"))
    {
        model.SelectThemeCommand.Execute(mode.Key).Subscribe();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        if (app.ActualThemeVariant != StudioTheme.GetVariant(palette.Palette, mode.Mode == StudioThemeMode.Dark))
            throw new InvalidOperationException("Theme mode and palette diverged.");
        app.TryFindResource("StudioSurfaceColor", app.ActualThemeVariant, out var expected);
        app.TryFindResource("PanelBackgroundBrush", app.ActualThemeVariant, out var resource);
        if (expected is not Color
            || resource is not ISolidColorBrush brush || brush.Color != (Color)expected!)
            throw new InvalidOperationException($"Application surface did not follow shared palette: {palette.Key}/{mode.Key}, expected {expected}, resource {resource}.");
        var logView = window.GetVisualDescendants().OfType<CodeWF.Avalonia.Log.LogView>().Single();
        app.TryFindResource("CodeWFLogViewContentForeground", app.ActualThemeVariant, out var logForeground);
        if (logForeground is not ISolidColorBrush logBrush || logView.ContentForeground is not ISolidColorBrush actualLogBrush
            || actualLogBrush.Color != logBrush.Color)
            throw new InvalidOperationException($"Log foreground did not follow the active theme: {palette.Key}/{mode.Key}.");
        using var bitmap = new RenderTargetBitmap(new PixelSize(1440, 900));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(directory, $"main-{palette.Key}-{mode.Key}.png"), new PngBitmapEncoderOptions());
        renders++;
    }
}
if (renders != 16) throw new InvalidOperationException("Expected eight independent light/dark palettes.");
model.SelectPaletteCommand.Execute("aurora").Subscribe();
model.SelectThemeCommand.Execute("system").Subscribe();
var saved = new AppSettingsStore().Current;
if (saved.PaletteKey != "aurora" || saved.ThemeKey != "system")
    throw new InvalidOperationException("Independent appearance settings did not survive a new settings store.");

using var updates = new UpdateService(new UpdateServiceOptions { Repository = "AvaGithubDesktop", ProductName = "AvaGithubDesktop" });
using var about = new AboutViewModel(updates, localizer, _ => { }, () => { });
var dialog = new AboutWindow(about);
dialog.Show(window);
foreach (var culture in new[] { "zh-CN", "en-US" })
{
    localizer.SetCulture(culture);
    if (about.ActionText.StartsWith("AvaGithubDesktop.") || about.VersionText != UpdateVersion.Display(typeof(App).Assembly)
        || !DateTime.TryParse(about.CompileTimeText, out _))
        throw new InvalidOperationException("About metadata or localization failed.");
    dialog.UpdateLayout();
    Dispatcher.UIThread.RunJobs();
    using var bitmap = new RenderTargetBitmap(new PixelSize(650, 650));
    bitmap.Render(dialog);
    bitmap.Save(Path.Combine(directory, $"about-{culture}.png"), new PngBitmapEncoderOptions());
}
dialog.Close();
window.Close();
Logger.ShutdownAsync().GetAwaiter().GetResult();
Console.WriteLine("PASS: 16 shared palette renders, independent settings restoration, two localized about windows and runtime metadata.");
