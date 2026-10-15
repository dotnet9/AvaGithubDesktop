using System.Globalization;
using System.Runtime.CompilerServices;
using AvaGithubDesktop.Core.Services;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;

namespace AvaGithubDesktop;

/// <summary>显式验证正式原生产物，不读取账号或仓库，不修改用户配置。</summary>
internal static class RuntimeVerification
{
    public static void Run(string directory)
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
            throw new InvalidOperationException("Runtime verification requires a NativeAOT executable.");
        var root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        var resources = Path.Combine(AppContext.BaseDirectory, "I18n");
        if (OperatingSystem.IsMacOS() && !Directory.Exists(resources))
            resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "I18n"));
        var manager = I18nManager.Instance;
        manager.Register(new JsonLangPlugin { ResourceFolder = resources }, CultureInfo.GetCultureInfo("en-US"));
        var labels = new HashSet<string>();
        foreach (var culture in new[] { "en-US", "zh-CN" })
        {
            if (!File.Exists(Path.Combine(resources, "AvaGithubDesktop", culture + ".json")))
                throw new InvalidOperationException("Published language file is missing: " + culture);
            manager.Culture = CultureInfo.GetCultureInfo(culture);
            var text = manager.GetResource(AvaGithubDesktopL.Language);
            if (string.IsNullOrWhiteSpace(text) || text == AvaGithubDesktopL.Language)
                throw new InvalidOperationException("Published language resources are missing: " + culture);
            labels.Add(text);
        }
        if (labels.Count != 2) throw new InvalidOperationException("Published languages do not switch.");
        var config = Path.Combine(root, "runtime-" + Guid.NewGuid().ToString("N") + ".config");
        File.WriteAllText(config, "<configuration><appSettings /></configuration>");
        AppContext.SetData("APP_CONFIG_FILE", config);
        new AppSettingsStore().Update(settings => settings with { ThemeKey = "system", PaletteKey = "aurora", CultureName = "en-US" });
        var saved = new AppSettingsStore().Current;
        if (saved.ThemeKey != "system" || saved.PaletteKey != "aurora" || saved.CultureName != "en-US")
            throw new InvalidOperationException("Native appearance preferences did not round-trip.");
        var result = $"NativeAOT runtime verification passed; version={typeof(Program).Assembly.GetName().Version}; languages=2; preferences=isolated";
        File.WriteAllText(Path.Combine(root, "runtime-verification.txt"), result);
        Console.WriteLine(result);
    }
}
