using System.Globalization;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AvaGithubDesktop.Core.Services;
using AvaGithubDesktop.ViewModels;
using AvaGithubDesktop.Views;
using CodeWF.Toolkit.EventBus;
using CodeWF.Toolkit.Core.UpdateChecking;
using CodeWF.Toolkit.Files.Helpers;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using Prism.DryIoc;
using Prism.Ioc;

namespace AvaGithubDesktop;

public partial class App : PrismApplication
{
    public override void Initialize()
    {
        var resourceDirectory = AppContext.BaseDirectory;
        var bundleResources = Path.GetFullPath(Path.Combine(resourceDirectory, "..", "Resources"));
        if (OperatingSystem.IsMacOS() && Directory.Exists(Path.Combine(bundleResources, "I18n")))
            resourceDirectory = bundleResources;
        // CodeWF.Toolkit.Core.Files 的 AppConfigHelper 支持 APP_CONFIG_FILE；这里固定读取输出目录的 App.config，
        // 便于人工维护 GitHub OAuth Client ID，而不是落到 exe/dll.config 这类生成文件名里。
        AppContext.SetData("APP_CONFIG_FILE", Path.Combine(resourceDirectory, "App.config"));
        AvaloniaXamlLoader.Load(this);
        var cultureName = GetConfiguredCultureName();
        var langPlugin = new JsonLangPlugin
        {
            ResourceFolder = Path.Combine(resourceDirectory, "I18n")
        };
        I18nManager.Instance.Register(langPlugin, new CultureInfo(cultureName), out _);
        I18nManager.Instance.CultureChanged += OnCultureChanged;
        ApplyControlCulture();
        base.Initialize();
    }

    protected override AvaloniaObject CreateShell()
    {
        var window = Container.Resolve<MainWindow>();
        window.Closed += (_, _) =>
        {
            Container.Resolve<IThemeService>().Dispose();
            if (Container.Resolve<IUpdateService>() is IDisposable updates) updates.Dispose();
        };
        return window;
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterInstance<IUpdateService>(new UpdateService(new UpdateServiceOptions
        {
            Owner = "dotnet9", Repository = "AvaGithubDesktop", ProductName = "AvaGithubDesktop"
        }));
        containerRegistry.RegisterSingleton<IEventBus, EventBus>();
        containerRegistry.RegisterSingleton<IAppSettingsStore, AppSettingsStore>();
        containerRegistry.RegisterSingleton<IAppLocalizer, AppLocalizer>();
        containerRegistry.RegisterSingleton<IThemeService, ThemeService>();
        containerRegistry.RegisterSingleton<IGitRepositoryService, GitRepositoryService>();
        containerRegistry.RegisterSingleton<IRepositoryPickerService, RepositoryPickerService>();
        containerRegistry.RegisterSingleton<IRepositoryCloneDialogService, RepositoryCloneDialogService>();
        containerRegistry.RegisterSingleton<IRepositoryCreationDialogService, RepositoryCreationDialogService>();
        containerRegistry.RegisterSingleton<IRepositoryOpenDialogService, RepositoryOpenDialogService>();
        containerRegistry.RegisterSingleton<IRepositoryRemoteDialogService, RepositoryRemoteDialogService>();
        containerRegistry.RegisterSingleton<IRepositoryHistoryService, RepositoryHistoryService>();
        containerRegistry.RegisterSingleton<IRepositoryWorkspaceLoader, RepositoryWorkspaceLoader>();
        containerRegistry.RegisterSingleton<IRepositoryShellService, RepositoryShellService>();
        containerRegistry.RegisterSingleton<IRepositoryInteractionService, RepositoryInteractionService>();
        containerRegistry.RegisterSingleton<IRepositoryOperationCommandService, RepositoryOperationCommandService>();
        containerRegistry.RegisterSingleton<IRepositorySyncStatusService, RepositorySyncStatusService>();
        containerRegistry.RegisterSingleton<IGitHubAccountService, GitHubAccountService>();
        containerRegistry.RegisterSingleton<IAccountDialogService, AccountDialogService>();
        containerRegistry.RegisterSingleton<IHelpService, HelpService>();
        containerRegistry.RegisterSingleton<IConfirmationDialogService, ConfirmationDialogService>();
        containerRegistry.RegisterSingleton<IBranchDialogService, BranchDialogService>();
        containerRegistry.RegisterSingleton<ITagDialogService, TagDialogService>();
        containerRegistry.RegisterSingleton<BranchesViewStateBuilder>();
        containerRegistry.RegisterSingleton<ChangedFilesViewStateBuilder>();
        containerRegistry.RegisterSingleton<CommitFilesViewStateBuilder>();
        containerRegistry.RegisterSingleton<RepositoryListGroupBuilder>();
        containerRegistry.RegisterSingleton<ShellStatusViewModel>();
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        containerRegistry.Register<MainWindow>();
    }

    private static string GetConfiguredCultureName()
    {
        var configPath = AppConfigHelper.GetDefaultConfigPath();
        if (AppConfigHelper.TryGet(configPath, nameof(Core.Models.AppSettings.CultureName), out string? cultureName)
            && IsSupportedCulture(cultureName))
        {
            return cultureName!;
        }

        return "zh-CN";
    }

    private static bool IsSupportedCulture(string? cultureName) =>
        string.Equals(cultureName, "zh-CN", StringComparison.OrdinalIgnoreCase)
        || string.Equals(cultureName, "en-US", StringComparison.OrdinalIgnoreCase);
    private static void OnCultureChanged(object? sender, EventArgs args)
    {
        if (Dispatcher.UIThread.CheckAccess()) ApplyControlCulture();
        else Dispatcher.UIThread.Post(ApplyControlCulture);
    }

    /// <summary>更新主题内置文案，保留宿主资源的同键覆盖。</summary>
    private static void ApplyControlCulture()
    {
        if (Application.Current is not { } application || I18nManager.Instance.Culture is not { } culture) return;
        foreach (var theme in application.Styles.OfType<Semi.Avalonia.SemiTheme>()) theme.Locale = culture;
        foreach (var theme in application.Styles.OfType<Ursa.Themes.Semi.UrsaSemiTheme>()) theme.Locale = culture;
    }

}
