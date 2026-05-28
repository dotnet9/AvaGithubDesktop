using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaGithubDesktop.Views;

namespace AvaGithubDesktop.Core.Services;

public sealed class HelpService : IHelpService
{
    private static readonly string DocumentsFolder = Path.Combine(AppContext.BaseDirectory, "docs");
    private readonly IAppLocalizer _localizer;

    public HelpService(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public Task ShowChangelogWindowAsync()
    {
        var path = ResolveDocumentPath("更新日志.md");
        var markdown = File.ReadAllText(path);
        ShowWindow(new MarkdownDocumentWindow(
            _localizer.Get(AvaGithubDesktopL.Changelog),
            markdown,
            Path.GetDirectoryName(path)));
        return Task.CompletedTask;
    }

    public Task ShowKeyboardShortcutsWindowAsync()
    {
        var path = ResolveDocumentPath("快捷键.md");
        var markdown = File.ReadAllText(path);
        ShowWindow(new MarkdownDocumentWindow(
            _localizer.Get(AvaGithubDesktopL.KeyboardShortcuts),
            markdown,
            Path.GetDirectoryName(path)));
        return Task.CompletedTask;
    }

    public Task ShowAboutWindowAsync()
    {
        ShowWindow(new MarkdownDocumentWindow(
            _localizer.Get(AvaGithubDesktopL.MenuAbout),
            BuildAboutMarkdown(),
            DocumentsFolder));
        return Task.CompletedTask;
    }

    private string BuildAboutMarkdown()
    {
        var assembly = typeof(App).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                                   ?? assembly.GetName().Version?.ToString()
                                   ?? "-";

        return string.Join(
            Environment.NewLine,
            "# AvaGithubDesktop",
            string.Empty,
            _localizer.Get(AvaGithubDesktopL.AboutDescription),
            string.Empty,
            $"- {_localizer.Get(AvaGithubDesktopL.AboutVersionLabel)}：{informationalVersion}",
            $"- {_localizer.Get(AvaGithubDesktopL.AboutRepositoryLabel)}：https://github.com/dotnet9/AvaGithubDesktop",
            $"- {_localizer.Get(AvaGithubDesktopL.AboutBrandLabel)}：CodeWF");
    }

    private static string ResolveDocumentPath(string fileName)
    {
        foreach (var folder in EnumerateDocumentFolders())
        {
            var path = Path.Combine(folder, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException($"Document '{fileName}' was not found.", fileName);
    }

    private static IEnumerable<string> EnumerateDocumentFolders()
    {
        // 发布后从输出目录读取；开发调试时从仓库 docs 目录兜底，避免菜单窗口因复制遗漏打不开。
        yield return DocumentsFolder;
        yield return Path.Combine(Directory.GetCurrentDirectory(), "docs");
    }

    private static void ShowWindow(Window window)
    {
        if (GetMainWindow() is { } owner)
        {
            window.Show(owner);
            return;
        }

        window.Show();
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
