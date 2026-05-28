using System.Globalization;
using Avalonia;
using Avalonia.Markup.Xaml;
using AvaGithubDesktop.Core.Services;
using AvaGithubDesktop.ViewModels;
using AvaGithubDesktop.Views;
using CodeWF.EventBus;
using CodeWF.Log.Core;
using CodeWF.Tools.Helpers;
using Lang.Avalonia;
using Lang.Avalonia.Json;
using Prism.DryIoc;
using Prism.Ioc;

namespace AvaGithubDesktop;

public partial class App : PrismApplication
{
    public override void Initialize()
    {
        // CodeWF.Tools.Files 的 AppConfigHelper 支持 APP_CONFIG_FILE；这里固定读取输出目录的 App.config，
        // 便于人工维护 GitHub OAuth Client ID，而不是落到 exe/dll.config 这类生成文件名里。
        AppContext.SetData("APP_CONFIG_FILE", Path.Combine(AppContext.BaseDirectory, "App.config"));
        ConfigureOperationLogger();
        AvaloniaXamlLoader.Load(this);
        var cultureName = GetConfiguredCultureName();
        var langPlugin = new JsonLangPlugin
        {
            ResourceFolder = Path.Combine(AppContext.BaseDirectory, "I18n")
        };
        I18nManager.Instance.Register(langPlugin, new CultureInfo(cultureName), out _);
        AppLocalizer.ApplyThirdPartyCulture(cultureName);
        base.Initialize();
    }

    protected override AvaloniaObject CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
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
        containerRegistry.RegisterSingleton<IRepositoryShellService, RepositoryShellService>();
        containerRegistry.RegisterSingleton<IRepositoryInteractionService, RepositoryInteractionService>();
        containerRegistry.RegisterSingleton<IRepositorySyncStatusService, RepositorySyncStatusService>();
        containerRegistry.RegisterSingleton<IGitHubAccountService, GitHubAccountService>();
        containerRegistry.RegisterSingleton<IAccountDialogService, AccountDialogService>();
        containerRegistry.RegisterSingleton<IHelpService, HelpService>();
        containerRegistry.RegisterSingleton<IConfirmationDialogService, ConfirmationDialogService>();
        containerRegistry.RegisterSingleton<IBranchDialogService, BranchDialogService>();
        containerRegistry.RegisterSingleton<ITagDialogService, TagDialogService>();
        containerRegistry.RegisterSingleton<RepositoryListGroupBuilder>();
        containerRegistry.RegisterSingleton<ShellStatusViewModel>();
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        containerRegistry.Register<MainWindow>();
    }

    private static void ConfigureOperationLogger()
    {
        Logger.Level = LogType.Debug;
        Logger.BatchProcessSize = 80;
        Logger.LogUIDuration = 80;
        Logger.MaxUIDisplayCount = 1200;
        Logger.MaxLogFileSizeMB = 20;
        Logger.TimeFormat = "HH:mm:ss";
        Logger.EnableConsoleOutput = false;
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
}
