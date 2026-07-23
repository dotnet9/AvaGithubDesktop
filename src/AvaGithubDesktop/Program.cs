using System.Text;
using Avalonia;
using CodeWF.Log.Core;
using ReactiveUI.Avalonia;

namespace AvaGithubDesktop;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Logger.Initialize(new LoggerOptions
        {
            MinimumLevel = LogType.Debug,
            EnableConsole = false,
            RecentUserLogCapacity = 1_200,
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
            .UseReactiveUI(_ => { })
            .LogToTrace();
    }
}
