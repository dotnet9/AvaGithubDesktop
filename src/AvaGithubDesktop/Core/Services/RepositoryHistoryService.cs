using System.Diagnostics;
using System.Text.Json;
using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public sealed class RepositoryHistoryService : IRepositoryHistoryService
{
    private const int MaxStoredRepositories = 100;
    private const int MaxDiscoveredRepositories = 80;
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
        var byPath = entries
            .Where(entry => Directory.Exists(entry.Path))
            .GroupBy(entry => NormalizePath(entry.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(entry => entry.LastOpenedAt).First(), StringComparer.OrdinalIgnoreCase);

        // GitHub Desktop 的仓库列表来自持久化数据库；本项目开发期先扫描 D:\github 作为可用种子，
        // 这样首次启动就能看到和截图接近的本地仓库列表，用户手动打开的仓库仍会持久化保存。
        var discoveredEntries = await Task.Run(
            () => DiscoverDevelopmentRepositories()
                .Select(path => CreateEntry(NormalizePath(path), DateTimeOffset.MinValue))
                .ToArray(),
            cancellationToken);

        foreach (var discoveredEntry in discoveredEntries)
        {
            var normalizedPath = NormalizePath(discoveredEntry.Path);
            if (!byPath.ContainsKey(normalizedPath))
            {
                byPath[normalizedPath] = discoveredEntry;
            }
        }

        return byPath.Values
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
            return entries ?? [];
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

    private static IEnumerable<string> DiscoverDevelopmentRepositories()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        const string developmentRoot = @"D:\github";
        if (!Directory.Exists(developmentRoot))
        {
            yield break;
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(developmentRoot).Take(MaxDiscoveredRepositories))
        {
            if (IsGitRepository(childDirectory))
            {
                yield return childDirectory;
            }
        }
    }

    private static bool IsGitRepository(string path)
    {
        return Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git"));
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
        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add(repositoryPath);
            startInfo.ArgumentList.Add("config");
            startInfo.ArgumentList.Add("--get");
            startInfo.ArgumentList.Add("remote.origin.url");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            if (!process.WaitForExit(1500))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? output
                : null;
        }
        catch (Exception)
        {
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
