using AvaGithubDesktop.Core.Models;
using CodeWF.Tools.Helpers;

namespace AvaGithubDesktop.Core.Services;

public sealed class AppSettingsStore : IAppSettingsStore
{
    private const string IsOperationLogVisibleKey = nameof(AppSettings.IsOperationLogVisible);
    private const string CultureNameKey = nameof(AppSettings.CultureName);
    private readonly object _syncRoot = new();
    private AppSettings? _settings;

    public AppSettings Current
    {
        get
        {
            lock (_syncRoot)
            {
                _settings ??= Load();
                return _settings;
            }
        }
    }

    public AppSettings Update(Func<AppSettings, AppSettings> update)
    {
        lock (_syncRoot)
        {
            _settings = update(Current);
            Save(_settings);
            return _settings;
        }
    }

    private static AppSettings Load()
    {
        var configPath = AppConfigHelper.GetDefaultConfigPath();
        return new AppSettings
        {
            IsOperationLogVisible = Get<bool?>(configPath, IsOperationLogVisibleKey),
            CultureName = Get<string>(configPath, CultureNameKey)
        };
    }

    private static void Save(AppSettings settings)
    {
        try
        {
            var configPath = AppConfigHelper.GetDefaultConfigPath();
            AppConfigHelper.Set(configPath, IsOperationLogVisibleKey, settings.IsOperationLogVisible);
            AppConfigHelper.Set(configPath, CultureNameKey, settings.CultureName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            // 设置保存失败不能打断 Git 操作；失败原因后续由状态栏或日志体系统一呈现。
        }
    }

    private static T? Get<T>(string configPath, string key)
    {
        return AppConfigHelper.TryGet<T>(configPath, key, out var value)
            ? value
            : default;
    }
}
