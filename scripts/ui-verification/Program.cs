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
using System.Diagnostics;
using AvaGithubDesktop.Core.Models;

// 实际 XAML 与资源的离屏回归，不点击系统浏览器，不读取用户工作区。
var directory = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/ui-verification");
Directory.CreateDirectory(directory);
Logger.Initialize(new LoggerOptions { EnableConsole = false });
AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia().WithInterFont()
    .With(new FontManagerOptions
            {
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily("Microsoft YaHei UI") },
                    new FontFallback { FontFamily = new FontFamily("PingFang SC") },
                    new FontFallback { FontFamily = new FontFamily("Noto Sans CJK SC") }
                ]
            })
    .UseReactiveUI().SetupWithoutStarting();
var app = (App)Application.Current!;
// 仓库历史和配置均隔离到验证目录，避免修改用户最近打开的项目。
AppContext.SetData("APP_CONFIG_FILE", Path.Combine(directory, "App.config"));
((IContainerRegistry)app.Container).RegisterInstance<IRepositoryHistoryService>(new VerificationHistory());
var repository = Path.Combine(directory, "studio-workspace-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(repository);
RunGit(repository, "init", "--initial-branch=main");
File.WriteAllText(Path.Combine(repository, "README.md"), "# Verification\n\nA local Git workspace.\n");
File.WriteAllText(Path.Combine(repository, "settings.json"), "{\n  \"theme\": \"system\"\n}\n");
RunGit(repository, "add", ".");
RunGit(repository, "-c", "user.name=CodeWF Verification", "-c", "user.email=verification@example.invalid", "-c", "commit.gpgsign=false", "commit", "-m", "Create isolated verification workspace");
File.WriteAllText(Path.Combine(repository, "README.md"), "# Verification\n\nA local Git workspace with a visible diff.\n\n- Eight palettes\n- Three appearance modes\n");
File.WriteAllText(Path.Combine(repository, "settings.json"), "{\n  \"theme\": \"dark\",\n  \"palette\": \"dotnet\"\n}\n");
var model = app.Container.Resolve<MainWindowViewModel>();
var localizer = app.Container.Resolve<IAppLocalizer>();
localizer.SetCulture("zh-CN");
model.RepositoryPath = repository;
var window = new MainWindow(model) { Width = 1440, Height = 900 };
window.Show();
PumpUntil(() => model.HasRepository && !model.IsLoading, () => model.ErrorMessage);
if (model.ChangedFiles.Count != 2 || model.HistoryCommits.Count != 1)
    throw new InvalidOperationException("The real Git workspace did not load both changes and its initial commit.");
model.SelectedChange = model.ChangedFiles.First(change => change.Path == "README.md");
PumpUntil(() => !model.IsDiffLoading && model.DiffText.Contains("visible diff", StringComparison.Ordinal), () => model.DiffText);
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
foreach (var culture in new[] { "zh-CN", "en-US" })
{
    localizer.SetCulture(culture);
    foreach (var width in new[] { 1440, 1120 })
    {
        window.Width = width;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var swatches = window.FindControl<ListBox>("TitleBarPalettes")!;
        var selector = window.FindControl<ComboBox>("TitleBarPaletteSelector")!;
        if (swatches.ItemCount != 8 || !ReferenceEquals(selector.SelectedItem, model.SelectedPalette) || !ReferenceEquals(swatches.SelectedItem, model.SelectedPalette)
            || selector.Bounds.Width < 96 || selector.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.StartsWith("AvaGithubDesktop.") == true))
            throw new InvalidOperationException("Title bar palette selection, localization or layout failed.");
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, 900));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(directory, $"workspace-{culture}-{width}.png"), new PngBitmapEncoderOptions());
    }
}
window.Width = 1440;
model.ShowHistoryCommand.Execute().Subscribe();
PumpUntil(() => !model.IsDiffLoading, () => model.DiffText);
window.UpdateLayout();
Dispatcher.UIThread.RunJobs();
using (var bitmap = new RenderTargetBitmap(new PixelSize(1440, 900)))
{
    bitmap.Render(window);
    bitmap.Save(Path.Combine(directory, "workspace-history.png"), new PngBitmapEncoderOptions());
}
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
Console.WriteLine("PASS: real Git changes/diff/history, 16 shared palettes, four localized workspace layouts, independent settings restoration and two localized about windows.");

static void PumpUntil(Func<bool> completed, Func<string> diagnostic)
{
    var deadline = Stopwatch.StartNew();
    while (!completed())
    {
        if (deadline.Elapsed > TimeSpan.FromSeconds(20))
            throw new TimeoutException("UI verification timed out: " + diagnostic());
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
    }
    Dispatcher.UIThread.RunJobs();
}

static void RunGit(string directory, params string[] arguments)
{
    var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start Git.");
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(15000)) throw new TimeoutException("Git verification command timed out.");
    Task.WaitAll(output, error);
    if (process.ExitCode != 0) throw new InvalidOperationException(error.Result);
}

sealed class VerificationHistory : IRepositoryHistoryService
{
    private readonly List<RepositoryHistoryEntry> _entries = [];
    public Task<IReadOnlyList<RepositoryHistoryEntry>> LoadKnownRepositoriesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RepositoryHistoryEntry>>(_entries.ToArray());
    public Task AddOrUpdateAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        _entries.RemoveAll(entry => entry.Path == repositoryPath);
        _entries.Add(new RepositoryHistoryEntry(Path.GetFileName(repositoryPath), repositoryPath, string.Empty, DateTimeOffset.UtcNow, null));
        return Task.CompletedTask;
    }
    public Task RemoveAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        _entries.RemoveAll(entry => entry.Path == repositoryPath);
        return Task.CompletedTask;
    }
}
