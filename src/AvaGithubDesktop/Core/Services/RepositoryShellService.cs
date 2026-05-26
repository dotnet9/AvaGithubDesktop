using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryShellService : IRepositoryShellService
{
    public Task OpenInShellAsync(string repositoryPath)
    {
        var path = ResolveExistingDirectory(repositoryPath);

        if (OperatingSystem.IsWindows())
        {
            // GitHub Desktop 会按用户配置选择 shell；当前先用系统自带 PowerShell，
            // 保证没有额外配置时也能稳定打开当前仓库目录。
            StartProcess(new ProcessStartInfo("powershell.exe")
            {
                Arguments = "-NoExit",
                WorkingDirectory = path,
                UseShellExecute = true
            });
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsMacOS())
        {
            StartProcess(new ProcessStartInfo("open")
            {
                ArgumentList = { "-a", "Terminal", path },
                UseShellExecute = false
            });
            return Task.CompletedTask;
        }

        var terminal = Environment.GetEnvironmentVariable("TERMINAL");
        StartProcess(new ProcessStartInfo(string.IsNullOrWhiteSpace(terminal) ? "x-terminal-emulator" : terminal)
        {
            WorkingDirectory = path,
            UseShellExecute = false
        });
        return Task.CompletedTask;
    }

    public Task ShowInFileManagerAsync(string repositoryPath)
    {
        var path = ResolveExistingDirectory(repositoryPath);

        if (OperatingSystem.IsWindows())
        {
            StartProcess(new ProcessStartInfo("explorer.exe")
            {
                ArgumentList = { path },
                UseShellExecute = true
            });
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsMacOS())
        {
            StartProcess(new ProcessStartInfo("open")
            {
                ArgumentList = { path },
                UseShellExecute = false
            });
            return Task.CompletedTask;
        }

        StartProcess(new ProcessStartInfo("xdg-open")
        {
            ArgumentList = { path },
            UseShellExecute = false
        });
        return Task.CompletedTask;
    }

    public Task OpenUrlAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Url '{url}' is invalid.");
        }

        if (OperatingSystem.IsWindows())
        {
            StartProcess(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsMacOS())
        {
            StartProcess(new ProcessStartInfo("open")
            {
                ArgumentList = { uri.AbsoluteUri },
                UseShellExecute = false
            });
            return Task.CompletedTask;
        }

        StartProcess(new ProcessStartInfo("xdg-open")
        {
            ArgumentList = { uri.AbsoluteUri },
            UseShellExecute = false
        });
        return Task.CompletedTask;
    }

    public async Task CopyTextAsync(string text)
    {
        var window = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
        var clipboard = window is null ? null : TopLevel.GetTopLevel(window)?.Clipboard;
        if (clipboard is null)
        {
            throw new InvalidOperationException("Clipboard is not available.");
        }

        await clipboard.SetTextAsync(text);
    }

    private static string ResolveExistingDirectory(string repositoryPath)
    {
        var path = Path.GetFullPath(repositoryPath);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }

        return path;
    }

    private static void StartProcess(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Process '{startInfo.FileName}' could not be started.");
        }
    }
}
