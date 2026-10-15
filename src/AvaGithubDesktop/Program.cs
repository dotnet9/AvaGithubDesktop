using System.Text;
using Avalonia;
using Avalonia.Media;
using CodeWF.Toolkit.Logging;
using Microsoft.Extensions.Logging;
using Avalonia.ReactiveUI;

namespace AvaGithubDesktop;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Logger.Initialize(new LoggerOptions
        {
            MinimumLevel = LogLevel.Debug,
            EnableConsole = false,
            RecentEventCapacity = 1_200,
            File = new FileLogOptions
            {
                DirectoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AvaGithubDesktop",
                    "Log"),
                BatchSize = 80,
                MaxFileSizeBytes = 20L * 1024 * 1024,
                TimestampFormat = "HH:mm:ss"
            }
        });

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Logger.ShutdownAsync().GetAwaiter().GetResult();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            // 中文缺字使用无衬线字体回退。
            .With(new FontManagerOptions
            {
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily("Microsoft YaHei UI") },
                    new FontFallback { FontFamily = new FontFamily("PingFang SC") },
                    new FontFallback { FontFamily = new FontFamily("Noto Sans CJK SC") }
                ]
            })
            .UseReactiveUI()
            .LogToTrace();
    }
}
