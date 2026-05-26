using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public sealed class GitHubAccountService : IGitHubAccountService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly string StoreFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AvaGithubDesktop");

    private static readonly string StorePath = Path.Combine(StoreFolder, "accounts.json");

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(25)
    };

    private IReadOnlyList<GitHubAccount> _accounts = [];
    private bool _isLoaded;

    public GitHubAccount? CurrentAccount => _accounts.FirstOrDefault();

    public async Task<IReadOnlyList<GitHubAccount>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded)
        {
            return _accounts;
        }

        if (!File.Exists(StorePath))
        {
            _accounts = [];
            _isLoaded = true;
            return _accounts;
        }

        await using var stream = File.OpenRead(StorePath);
        var document = await JsonSerializer.DeserializeAsync<AccountStoreDocument>(
            stream,
            JsonOptions,
            cancellationToken);
        _accounts = SortAccounts(document?.Accounts ?? []);
        _isLoaded = true;
        return _accounts;
    }

    public async Task<GitHubAccount> SignInWithTokenAsync(
        string endpoint,
        string token,
        CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        var trimmedToken = token.Trim();
        if (string.IsNullOrWhiteSpace(trimmedToken))
        {
            throw new InvalidOperationException("GitHub token is required.");
        }

        var normalizedEndpoint = GitHubAccountEndpoints.NormalizeApiEndpoint(endpoint);
        var account = await FetchAccountAsync(normalizedEndpoint, trimmedToken, cancellationToken);

        // Desktop 对同一个 endpoint 只保留一个账户；这里保持相同策略，重新登录会替换旧账户。
        var nextAccounts = _accounts
            .Where(existing => !string.Equals(existing.Endpoint, account.Endpoint, StringComparison.OrdinalIgnoreCase))
            .Append(account)
            .ToArray();
        _accounts = SortAccounts(nextAccounts);
        await SaveAsync(cancellationToken);
        return account;
    }

    public async Task SignOutAsync(GitHubAccount account, CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        _accounts = _accounts
            .Where(existing => existing.Endpoint != account.Endpoint || existing.Id != account.Id)
            .ToArray();
        await SaveAsync(cancellationToken);
    }

    private async Task<GitHubAccount> FetchAccountAsync(
        string endpoint,
        string token,
        CancellationToken cancellationToken)
    {
        var user = await SendGitHubApiAsync<GitHubUserApiResponse>(
            endpoint,
            "user",
            token,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(user.Login) || user.Id <= 0)
        {
            throw new InvalidOperationException("GitHub account response is incomplete.");
        }

        var emails = await FetchEmailsAsync(endpoint, token, cancellationToken);
        return new GitHubAccount(
            user.Login,
            endpoint,
            token,
            emails,
            user.AvatarUrl ?? string.Empty,
            user.Id,
            user.Name ?? string.Empty,
            user.Plan?.Name ?? string.Empty);
    }

    private async Task<IReadOnlyList<GitHubAccountEmail>> FetchEmailsAsync(
        string endpoint,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var emails = await SendGitHubApiAsync<IReadOnlyList<GitHubEmailApiResponse>>(
                endpoint,
                "user/emails",
                token,
                cancellationToken);
            return emails
                .Select(email => new GitHubAccountEmail(
                    email.Email ?? string.Empty,
                    email.Verified,
                    email.Primary,
                    email.Visibility ?? string.Empty))
                .Where(email => !string.IsNullOrWhiteSpace(email.Email))
                .ToArray();
        }
        catch
        {
            // 邮箱列表不是登录成功的硬性条件；令牌缺少 user:email 权限时仍允许账户进入应用。
            return [];
        }
    }

    private async Task<T> SendGitHubApiAsync<T>(
        string endpoint,
        string relativePath,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            GitHubAccountEndpoints.BuildApiUri(endpoint, relativePath));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("AvaGithubDesktop");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(BuildApiErrorMessage(response, body));
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(
                   responseStream,
                   JsonOptions,
                   cancellationToken)
               ?? throw new InvalidOperationException("GitHub API returned an empty response.");
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(StoreFolder);
        var document = new AccountStoreDocument(_accounts);
        var tempPath = $"{StorePath}.tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, StorePath, overwrite: true);
    }

    private static IReadOnlyList<GitHubAccount> SortAccounts(IEnumerable<GitHubAccount> accounts) =>
        accounts
            .Select((account, index) => new { Account = account, Index = index })
            .OrderByDescending(item => item.Account.IsDotCom)
            .ThenBy(item => item.Index)
            .Select(item => item.Account)
            .ToArray();

    private static string BuildApiErrorMessage(HttpResponseMessage response, string body)
    {
        var message = TryReadGitHubErrorMessage(body);
        return string.IsNullOrWhiteSpace(message)
            ? $"GitHub API returned {(int)response.StatusCode} {response.ReasonPhrase}."
            : $"GitHub API returned {(int)response.StatusCode} {response.ReasonPhrase}: {message}";
    }

    private static string? TryReadGitHubErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record AccountStoreDocument(IReadOnlyList<GitHubAccount> Accounts);

    private sealed record GitHubUserApiResponse(
        [property: JsonPropertyName("login")] string? Login,
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("plan")] GitHubPlanApiResponse? Plan);

    private sealed record GitHubPlanApiResponse(
        [property: JsonPropertyName("name")] string? Name);

    private sealed record GitHubEmailApiResponse(
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("verified")] bool Verified,
        [property: JsonPropertyName("primary")] bool Primary,
        [property: JsonPropertyName("visibility")] string? Visibility);
}
