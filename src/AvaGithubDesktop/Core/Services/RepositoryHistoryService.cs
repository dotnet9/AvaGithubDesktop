using System.Diagnostics;
using System.Text.Json;
using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryHistoryService : IRepositoryHistoryService
{
    private const int MaxStoredRepositories = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly string StoreDirectory = Path.Combine(
        ResolveApplicationDataFolder(),
        "CodeWF",
        "AvaGithubDesktop");

    private static readonly string StorePath = Path.Combine(StoreDirectory, "repositories.json");

    public async Task<IReadOnlyList<RepositoryHistoryEntry>> LoadKnownRepositoriesAsync(CancellationToken cancellationToken)
    {
        var entries = await ReadStoredRepositoriesAsync(cancellationToken);
        return entries
            .Where(entry => Directory.Exists(entry.Path))
            .GroupBy(entry => NormalizePath(entry.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.LastOpenedAt).First())
            .OrderByDescending(entry => entry.LastOpenedAt)
            .ThenBy(entry => entry.GroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task AddOrUpdateAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(repositoryPath);
        var entries = await ReadStoredRepositoriesAsync(cancellationToken);
        var entry = CreateEntry(normalizedPath, DateTimeOffset.Now);

        entries.RemoveAll(existing => string.Equals(NormalizePath(existing.Path), normalizedPath, StringComparison.OrdinalIgnoreCase));
        entries.Insert(0, entry);

        await WriteStoredRepositoriesAsync(entries, cancellationToken);
    }

    public async Task RemoveAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(repositoryPath);
        var entries = await ReadStoredRepositoriesAsync(cancellationToken);
        entries.RemoveAll(existing => string.Equals(NormalizePath(existing.Path), normalizedPath, StringComparison.OrdinalIgnoreCase));
        await WriteStoredRepositoriesAsync(entries, cancellationToken);
    }

    private static async Task WriteStoredRepositoriesAsync(
        IReadOnlyList<RepositoryHistoryEntry> entries,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(StoreDirectory);
        await using var stream = File.Create(StorePath);
        await JsonSerializer.SerializeAsync(
            stream,
            entries.Take(MaxStoredRepositories).ToArray(),
            JsonOptions,
            cancellationToken);
    }

    private static async Task<List<RepositoryHistoryEntry>> ReadStoredRepositoriesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StorePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(StorePath);
            var entries = await JsonSerializer.DeserializeAsync<List<RepositoryHistoryEntry>>(stream, JsonOptions, cancellationToken);
            return (entries ?? [])
                .Where(IsValidStoredEntry)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static bool IsValidStoredEntry(RepositoryHistoryEntry? entry)
    {
        // 用户数据文件可能被旧版本、手动编辑或异常退出写坏；这里丢弃不完整记录，避免影响当前仓库打开。
        return entry is not null
               && !string.IsNullOrWhiteSpace(entry.Name)
               && !string.IsNullOrWhiteSpace(entry.Path)
               && !string.IsNullOrWhiteSpace(entry.GroupName);
    }

    private static RepositoryHistoryEntry CreateEntry(string repositoryPath, DateTimeOffset lastOpenedAt)
    {
        var remoteUrl = ReadOriginRemoteUrl(repositoryPath);
        return new RepositoryHistoryEntry(
            Name: new DirectoryInfo(repositoryPath).Name,
            Path: repositoryPath,
            GroupName: ResolveGroupName(repositoryPath, remoteUrl),
            LastOpenedAt: lastOpenedAt,
            RemoteUrl: remoteUrl);
    }

    private static string ResolveGroupName(string repositoryPath, string? remoteUrl)
    {
        if (TryParseGitHubOwner(remoteUrl, out var owner))
        {
            return owner;
        }

        if (Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return Directory.GetParent(repositoryPath)?.Name ?? "Other";
    }

    private static bool TryParseGitHubOwner(string? remoteUrl, out string owner)
    {
        owner = string.Empty;
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return false;
        }

        var normalized = remoteUrl.Trim();
        if (normalized.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["git@github.com:".Length..];
        }
        else if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                 && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            normalized = uri.AbsolutePath.TrimStart('/');
        }
        else
        {
            return false;
        }

        var parts = normalized.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        owner = parts[0];
        return !string.IsNullOrWhiteSpace(owner);
    }

    private static string? ReadOriginRemoteUrl(string repositoryPath)
    {
        var arguments = new[] { "config", "--get", "remote.origin.url" };
        var commandText = GitCommandLog.FormatCommand(repositoryPath, arguments);
        var stopwatch = Stopwatch.StartNew();
        GitCommandLog.LogStarted(commandText);

        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add(repositoryPath);
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                stopwatch.Stop();
                GitCommandLog.LogFailed(
                    commandText,
                    stopwatch.Elapsed,
                    new InvalidOperationException("Unable to start git."));
                return null;
            }

            if (!process.WaitForExit(1500))
            {
                process.Kill(entireProcessTree: true);
                stopwatch.Stop();
                GitCommandLog.LogTimedOut(commandText, stopwatch.Elapsed);
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            stopwatch.Stop();
            GitCommandLog.LogCompleted(commandText, process.ExitCode, stopwatch.Elapsed);
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? output
                : null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            GitCommandLog.LogFailed(commandText, stopwatch.Elapsed, ex);
            return null;
        }
    }

    private static string ResolveApplicationDataFolder()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrWhiteSpace(folder)
            ? AppContext.BaseDirectory
            : folder;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.Trim())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
