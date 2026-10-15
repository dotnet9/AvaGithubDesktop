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
AppBuilder.Configure<VerificationApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
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
var repository = Path.Combine(directory, "studio-workspace-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(repository);
RunGit(repository, "init", "--initial-branch=feature/" + new string('f', 120));
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
ControlLocaleVerification.Run(window, ["zh-CN", "en-US"], localizer.SetCulture);
var languageSelector = window.FindControl<ComboBox>("TitleBarLanguageSelector")!;
foreach (var culture in new[] { "en-US", "zh-CN" })
{
    CodeWF.Toolkit.Lang.I18nManager.Instance.Culture = new System.Globalization.CultureInfo(culture);
    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    if (model.SelectedLanguage?.CultureName != culture || (languageSelector.SelectedItem as LanguageOption)?.CultureName != culture)
        throw new InvalidOperationException("The global language did not update the actual title bar selection: " + culture);
    var returnCulture = culture == "en-US" ? "zh-CN" : "en-US";
    languageSelector.SelectedItem = model.Languages.Single(option => option.CultureName == returnCulture);
    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    if (localizer.Culture.Name != returnCulture)
        throw new InvalidOperationException("The title bar selection cannot switch back after a global language change.");
}
Console.WriteLine("PASS global language and title bar selection synchronize in both directions");
foreach (var culture in new[] { "zh-CN", "en-US" })
{
    Task.Run(() => CodeWF.Toolkit.Lang.I18nManager.Instance.Culture = new System.Globalization.CultureInfo(culture))
        .GetAwaiter().GetResult();
    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    if (model.SelectedLanguage?.CultureName != culture || (languageSelector.SelectedItem as LanguageOption)?.CultureName != culture)
        throw new InvalidOperationException("Background global culture did not update the actual title bar selection: " + culture);
    var returnCulture = culture == "en-US" ? "zh-CN" : "en-US";
    languageSelector.SelectedItem = model.Languages.Single(option => option.CultureName == returnCulture);
    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    if (localizer.Culture.Name != returnCulture)
        throw new InvalidOperationException("The same selector cannot switch back after a background culture change.");
}
Console.WriteLine("PASS background global language and actual title bar selection synchronize in both directions");
var helpDocument = new MarkdownDocumentWindow(localizer.Get(AvaGithubDesktopL.Changelog), "# Verification\n\n```text\nRuntime language sample\n```\n");
helpDocument.Show(window); helpDocument.UpdateLayout(); Dispatcher.UIThread.RunJobs();
foreach (var culture in new[] { "zh-CN", "en-US" })
{
    Task.Run(() => CodeWF.Toolkit.Lang.I18nManager.Instance.Culture = new System.Globalization.CultureInfo(culture))
        .GetAwaiter().GetResult();
    Dispatcher.UIThread.RunJobs(); helpDocument.UpdateLayout();
    if (helpDocument.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("DocumentMarkdownViewer")?.Markdown is not { Length: > 0 })
        throw new InvalidOperationException("The live help document lost its Markdown after background culture changes.");
}
helpDocument.Close();
Console.WriteLine("PASS live Markdown help document survives background global language changes");
// 显式打开隔离仓库，后续用例只操作本轮新建的测试数据。
model.OpenRepositoryCommand.Execute().Subscribe();
PumpUntil(() => model.HasRepository && !model.IsLoading, () => $"HasRepository={model.HasRepository}; Loading={model.IsLoading}; Path={model.RepositoryPath}; Root={model.RootPath}; Error={model.ErrorMessage}");
if (model.ChangedFiles.Count != 2 || model.HistoryCommits.Count != 1)
    throw new InvalidOperationException("The real Git workspace did not load both changes and its initial commit.");
window.UpdateLayout();
Dispatcher.UIThread.RunJobs();
model.SelectedChange = model.ChangedFiles.First(change => change.Path == "README.md");
PumpUntil(() => !model.IsDiffLoading && model.DiffText.Contains("visible diff", StringComparison.Ordinal),
    () => $"Diff={model.DiffText}; Title={model.DiffTitle}; Selected={model.SelectedChange?.Path}; Changes={model.IsChangesSelected}; Loading={model.IsDiffLoading}; Error={model.ErrorMessage}");
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
    // 切语会重新加载 Diff，必须等真实内容完成后再做布局和截图检查。
    PumpUntil(() => !model.IsDiffLoading && model.DiffText.Contains("visible diff", StringComparison.Ordinal), () => model.DiffText);
    foreach (var size in new[] { (Width: 1440, Height: 900), (Width: 1120, Height: 900), (Width: 960, Height: 620) })
    {
        var width = size.Width;
        window.Width = width;
        window.Height = size.Height;
        if (width == 960)
        {
            model.CommitSummary = new string('W', 256);
            model.CommitDescription = string.Join(Environment.NewLine, Enumerable.Range(1, 80).Select(line => "Long description line " + line));
        }
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var swatches = window.FindControl<ListBox>("TitleBarPalettes")!;
        var toggle = window.FindControl<Ursa.Controls.ThemeToggleButton>("TitleBarThemeToggle")!;
        var language = window.FindControl<ComboBox>("TitleBarLanguageSelector")!;
        var themeButton = toggle.GetVisualDescendants().OfType<Button>().Single(item => item.Name == "PART_ThemeButton");
        foreach (var palette in model.PaletteOptions)
        {
            swatches.SelectedItem = palette;
            model.ThemeMode = StudioThemeMode.System;
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            foreach (var expected in new[] { StudioThemeMode.Light, StudioThemeMode.Dark, StudioThemeMode.System })
            {
                themeButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                if (model.ThemeMode != expected || !ReferenceEquals(model.SelectedPalette, palette))
                    throw new InvalidOperationException("Title bar Ursa theme cycle or palette preservation failed: " + expected);
                var key = expected switch { StudioThemeMode.Light => AvaGithubDesktopL.ThemeLight, StudioThemeMode.Dark => AvaGithubDesktopL.ThemeDark, _ => AvaGithubDesktopL.ThemeSystem };
                if (ToolTip.GetTip(themeButton) is not TextBlock tip || tip.Text != localizer.Get(key))
                    throw new InvalidOperationException("The existing Ursa tooltip did not follow the current language and mode.");
            }
        }
        language.IsDropDownOpen = true; Dispatcher.UIThread.RunJobs();
        if (!language.IsDropDownOpen || language.ItemCount != model.Languages.Count) throw new InvalidOperationException("Language icon failed to open all languages.");
        language.SelectedItem = model.Languages.Single(option => option.CultureName == culture);
        language.IsDropDownOpen = false; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        if (CodeWF.Toolkit.Lang.I18nManager.Instance.Culture?.Name != culture || swatches.ItemCount != 8 || !ReferenceEquals(swatches.SelectedItem, model.SelectedPalette)
            || window.FindControl<ComboBox>("TitleBarPaletteSelector") is not null)
            throw new InvalidOperationException("Title bar palette selection, localization or duplicate entry failed.");
        var left = (Control)window.LeftContent!;
        var right = (Control)window.RightContent!;
        if (left.TranslatePoint(new Point(left.Bounds.Width, 0), window)!.Value.X > right.TranslatePoint(default, window)!.Value.X)
            throw new InvalidOperationException("Title bar menus and appearance controls overlap.");
        var workspace = window.FindControl<Grid>("WorkspaceGrid")!;
        var commitActions = window.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("commitButton") || button.Classes.Contains("stashButton")).ToArray();
        if (commitActions.Length != 2) throw new InvalidOperationException("Both commit and stash actions must remain present.");
        foreach (var action in commitActions)
            VerifyInside(action, workspace, "Commit action at " + culture + "/" + width);
        var commitButton = commitActions.Single(button => button.Classes.Contains("commitButton"));
        var commitText = commitButton.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == model.CommitButtonText);
        VerifyInside(commitText, commitButton, "Long branch commit text at " + culture + "/" + width);
        if (commitText.TextTrimming != TextTrimming.CharacterEllipsis || ToolTip.GetTip(commitButton)?.ToString() != model.CommitButtonText)
            throw new InvalidOperationException("Long branch names must be trimmed with the complete action in the tooltip.");
        var changes = window.GetVisualDescendants().OfType<ListBox>().Single(list => list.Classes.Contains("changes") && list.IsEffectivelyVisible);
        if (changes.Bounds.Height < 34) throw new InvalidOperationException("The changes list cannot display one complete file row at " + culture + "/" + width);
        if (width == 960)
        {
            var description = window.GetVisualDescendants().OfType<TextBox>().Single(box => box.Classes.Contains("commitDescription") && box.IsEffectivelyVisible);
            var scroll = description.GetVisualDescendants().OfType<ScrollViewer>().Single();
            scroll.ScrollToEnd(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            if (scroll.Offset.Y <= 0) throw new InvalidOperationException("The long commit description cannot scroll to its final lines.");
        }
        Console.WriteLine("PASS workspace content, file list and commit actions fit " + culture + "/" + width + "x" + size.Height);
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, size.Height));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(directory, $"workspace-{culture}-{width}.png"), new PngBitmapEncoderOptions());
        model.CommitSummary = model.CommitDescription = string.Empty;
    }
}
window.Width = 1440;
window.Height = 900;
model.ShowHistoryCommand.Execute().Subscribe();
PumpUntil(() => !model.IsDiffLoading && model.DiffText.Contains("Verification", StringComparison.Ordinal), () => model.DiffText);
window.UpdateLayout();
Dispatcher.UIThread.RunJobs();
using (var bitmap = new RenderTargetBitmap(new PixelSize(1440, 900)))
{
    bitmap.Render(window);
    bitmap.Save(Path.Combine(directory, "workspace-history.png"), new PngBitmapEncoderOptions());
}
foreach (var culture in new[] { "zh-CN", "en-US" })
{
    localizer.SetCulture(culture);
    PumpUntil(() => !model.IsDiffLoading && model.DiffText.Contains("Verification", StringComparison.Ordinal), () => model.DiffText);
    window.Width = 960; window.Height = 620;
    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    var history = window.FindControl<Grid>("HistoryDiffGrid")!;
    var title = window.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Classes.Contains("historyTitle"));
    var originalTitle = title.Text;
    title.SetCurrentValue(TextBlock.TextProperty, string.Join(" ", Enumerable.Repeat("Long commit summary", 40)));
    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    VerifyInside(history, window.FindControl<Grid>("WorkspaceGrid")!, "History workspace at minimum size: " + culture);
    foreach (var column in history.Children.OfType<Control>().Where(control => control is not GridSplitter))
        VerifyInside(column, history, "History column at minimum size: " + culture);
    var historyActions = window.GetVisualDescendants().OfType<Button>()
        .Where(button => button.IsEffectivelyVisible && button.Classes.Contains("inlineAction")).ToArray();
    if (historyActions.Length != 5) throw new InvalidOperationException("All five history actions must remain visible.");
    foreach (var action in historyActions)
        VerifyInside(action, window.FindControl<Grid>("WorkspaceGrid")!, "History action at minimum size: " + culture);
    using var bitmap = new RenderTargetBitmap(new PixelSize(960, 620));
    bitmap.Render(window);
    bitmap.Save(Path.Combine(directory, $"workspace-history-minimum-{culture}.png"), new PngBitmapEncoderOptions());
    title.SetCurrentValue(TextBlock.TextProperty, originalTitle);
    Console.WriteLine("PASS history files and diff fit minimum window: " + culture);
}
window.Width = 1440; window.Height = 900;
model.SelectPaletteCommand.Execute("aurora").Subscribe();
model.SelectThemeCommand.Execute("system").Subscribe();
var saved = new AppSettingsStore().Current;
if (saved.PaletteKey != "aurora" || saved.ThemeKey != "system")
    throw new InvalidOperationException("Independent appearance settings did not survive a new settings store.");

using var updates = new UpdateService(new UpdateServiceOptions { Repository = "AvaGithubDesktop", ProductName = "AvaGithubDesktop" });
using var about = new AboutViewModel(updates, localizer, _ => { }, () => { });
var dialog = new AboutWindow(about);
dialog.Show(window);
Dispatcher.UIThread.RunJobs();
dialog.UpdateLayout();
var minimizeButton = dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_MinimizeButton");
var maximizeButton = dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_MaximizeRestoreButton");
if (minimizeButton.IsEnabled != dialog.CanMinimize || maximizeButton.IsEnabled != (dialog.CanResize && dialog.CanMaximize))
    throw new InvalidOperationException("About caption buttons do not follow the actual window capabilities.");
foreach (var culture in new[] { "zh-CN", "en-US" })
{
    localizer.SetCulture(culture);
    if (CodeWF.Toolkit.Lang.I18nManager.Instance.Culture?.Name != culture || about.ActionText.StartsWith("AvaGithubDesktop.") || about.VersionText != UpdateVersion.Display(typeof(App).Assembly)
        || !DateTime.TryParse(about.CompileTimeText, out _))
        throw new InvalidOperationException("About metadata or localization failed.");
    dialog.UpdateLayout();
    Dispatcher.UIThread.RunJobs();
    using var bitmap = new RenderTargetBitmap(new PixelSize(650, 650));
    bitmap.Render(dialog);
    bitmap.Save(Path.Combine(directory, $"about-{culture}.png"), new PngBitmapEncoderOptions());
    dialog.Width = dialog.MinWidth; dialog.Height = dialog.MinHeight;
    Dispatcher.UIThread.RunJobs(); dialog.UpdateLayout();
    var aboutScroll = dialog.GetVisualDescendants().OfType<ScrollViewer>().Single();
    VerifyInside(aboutScroll, dialog, "About viewport at minimum size: " + culture);
    aboutScroll.Offset = default; Dispatcher.UIThread.RunJobs(); dialog.UpdateLayout();
    VerifyInside(dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Command == about.UpdateCommand),
        aboutScroll, "About update action at minimum size: " + culture);
    aboutScroll.ScrollToEnd(); Dispatcher.UIThread.RunJobs(); dialog.UpdateLayout();
    VerifyInside(dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Command == about.CloseCommand),
        aboutScroll, "About close action after scrolling: " + culture);
    Console.WriteLine("PASS about update and close actions remain reachable at 560x450: " + culture);
    using var narrowBitmap = new RenderTargetBitmap(new PixelSize(560, 450));
    narrowBitmap.Render(dialog);
    narrowBitmap.Save(Path.Combine(directory, $"about-minimum-{culture}.png"), new PngBitmapEncoderOptions());
    dialog.Width = 650; dialog.Height = 650;
}
dialog.Close();
window.Close();
Logger.ShutdownAsync().GetAwaiter().GetResult();
Console.WriteLine("PASS: real Git changes/diff/history, 16 shared palettes, six localized workspace layouts, two minimum history layouts, independent settings restoration and localized about windows at normal/minimum size.");

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

static void VerifyInside(Control control, Control host, string description)
{
    var position = control.TranslatePoint(default, host) ?? throw new InvalidOperationException(description + ": missing visual host");
    if (!control.IsEffectivelyVisible || control.Bounds.Width <= 0 || control.Bounds.Height <= 0
        || position.X < -1 || position.Y < -1 || position.X + control.Bounds.Width > host.Bounds.Width + 1
        || position.Y + control.Bounds.Height > host.Bounds.Height + 1)
        throw new InvalidOperationException($"{description}: {control.Bounds.Size} at {position}, host {host.Bounds.Size}");
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

/// <summary>在 Prism 首次创建外壳前替换持久化服务，隔离真实账号和仓库历史。</summary>
sealed class VerificationApp : App
{
    protected override void RegisterTypes(IContainerRegistry registry)
    {
        base.RegisterTypes(registry);
        registry.RegisterInstance<IRepositoryHistoryService>(new VerificationHistory());
        registry.RegisterInstance<IGitHubAccountService>(new VerificationAccounts());
    }
}

// 外观与Git验证不读取用户账号，也不发起登录或网络请求。
sealed class VerificationAccounts : IGitHubAccountService
{
    public GitHubAccount? CurrentAccount => null;
    public Task<IReadOnlyList<GitHubAccount>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GitHubAccount>>([]);
    public Task<GitHubDeviceAuthorization> BeginDeviceAuthorizationAsync(string endpoint, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<GitHubAccount> CompleteDeviceSignInAsync(GitHubDeviceAuthorization authorization, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<GitHubAccount> SignInWithTokenAsync(string endpoint, string token, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task SignOutAsync(GitHubAccount account, CancellationToken cancellationToken) => throw new NotSupportedException();
}
