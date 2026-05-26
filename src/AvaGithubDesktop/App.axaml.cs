using System.Globalization;
using Avalonia;
using Avalonia.Markup.Xaml;
using AvaGithubDesktop.Core.Services;
using AvaGithubDesktop.ViewModels;
using AvaGithubDesktop.Views;
using CodeWF.EventBus;
using Lang.Avalonia;
using Lang.Avalonia.Json;
using Prism.DryIoc;
using Prism.Ioc;

namespace AvaGithubDesktop;

public partial class App : PrismApplication
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        var langPlugin = new JsonLangPlugin
        {
            ResourceFolder = Path.Combine(AppContext.BaseDirectory, "I18n")
        };
        I18nManager.Instance.Register(langPlugin, new CultureInfo("zh-CN"), out _);
        base.Initialize();
    }

    protected override AvaloniaObject CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IEventBus, EventBus>();
        containerRegistry.RegisterSingleton<IAppLocalizer, AppLocalizer>();
        containerRegistry.RegisterSingleton<IGitRepositoryService, GitRepositoryService>();
        containerRegistry.RegisterSingleton<IRepositoryPickerService, RepositoryPickerService>();
        containerRegistry.RegisterSingleton<IRepositoryHistoryService, RepositoryHistoryService>();
        containerRegistry.RegisterSingleton<IRepositoryShellService, RepositoryShellService>();
        containerRegistry.RegisterSingleton<IHelpService, HelpService>();
        containerRegistry.RegisterSingleton<IConfirmationDialogService, ConfirmationDialogService>();
        containerRegistry.RegisterSingleton<ShellStatusViewModel>();
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        containerRegistry.Register<MainWindow>();
    }
}
