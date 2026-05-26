using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AvaGithubDesktop.Core.Models;
using CodeWF.Tools.Helpers;

namespace AvaGithubDesktop.Core.Services;

public sealed class GitHubAccountService : IGitHubAccountService
{
    private const string OAuthClientIdKey = "GitHubOAuthClientId";
    private const string DefaultOAuthClientId = "Ov23liudx8joiwZ3Ka4s";
    private const string DeviceGrantType = "urn:ietf:params:oauth:grant-type:device_code";
    private const string OAuthScopes = "repo user workflow";

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

    public async Task<GitHubDeviceAuthorization> BeginDeviceAuthorizationAsync(
        string endpoint,
        CancellationToken cancellationToken)
    {
        var normalizedEndpoint = GitHubAccountEndpoints.NormalizeApiEndpoint(endpoint);
        var clientId = GetOAuthClientId();
        var response = await SendOAuthFormAsync<GitHubDeviceCodeApiResponse>(
            normalizedEndpoint,
            "login/device/code",
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["scope"] = OAuthScopes
            },
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response.DeviceCode)
            || string.IsNullOrWhiteSpace(response.UserCode)
            || string.IsNullOrWhiteSpace(response.VerificationUri))
        {
            throw new InvalidOperationException("GitHub OAuth device response is incomplete.");
        }

        return new GitHubDeviceAuthorization(
            normalizedEndpoint,
            response.DeviceCode,
            response.UserCode,
            response.VerificationUri,
            response.VerificationUriComplete ?? response.VerificationUri,
            response.ExpiresIn,
            Math.Max(1, response.Interval));
    }

    public async Task<GitHubAccount> CompleteDeviceSignInAsync(
        GitHubDeviceAuthorization authorization,
        CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        var clientId = GetOAuthClientId();
        var interval = Math.Max(1, authorization.Interval);
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(authorization.ExpiresIn, interval));

        while (DateTimeOffset.UtcNow < expiresAt)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);

            var tokenResponse = await SendOAuthFormAsync<GitHubOAuthTokenApiResponse>(
                authorization.Endpoint,
                "login/oauth/access_token",
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["device_code"] = authorization.DeviceCode,
                    ["grant_type"] = DeviceGrantType
                },
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                var account = await FetchAccountAsync(
                    authorization.Endpoint,
                    tokenResponse.AccessToken,
                    cancellationToken);
                await AddOrUpdateAccountAsync(account, cancellationToken);
                return account;
            }

            switch (tokenResponse.Error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += 5;
                    continue;
                case "access_denied":
                    throw new InvalidOperationException("GitHub sign in was denied in the browser.");
                case "expired_token":
                    throw new InvalidOperationException("GitHub sign in code has expired.");
                default:
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(tokenResponse.ErrorDescription)
                            ? $"GitHub OAuth failed: {tokenResponse.Error}"
                            : tokenResponse.ErrorDescription);
            }
        }

        throw new InvalidOperationException("GitHub sign in code has expired.");
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
        await AddOrUpdateAccountAsync(account, cancellationToken);
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

    private async Task AddOrUpdateAccountAsync(GitHubAccount account, CancellationToken cancellationToken)
    {
        var nextAccounts = _accounts
            .Where(existing => !string.Equals(existing.Endpoint, account.Endpoint, StringComparison.OrdinalIgnoreCase))
            .Append(account)
            .ToArray();
        _accounts = SortAccounts(nextAccounts);
        await SaveAsync(cancellationToken);
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

    private async Task<T> SendOAuthFormAsync<T>(
        string endpoint,
        string relativePath,
        IReadOnlyDictionary<string, string> formValues,
        CancellationToken cancellationToken)
    {
        var htmlEndpoint = GitHubAccountEndpoints.GetHtmlEndpoint(endpoint);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri($"{htmlEndpoint.TrimEnd('/')}/"), relativePath));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("AvaGithubDesktop");
        request.Content = new FormUrlEncodedContent(formValues);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildApiErrorMessage(response, body));
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions)
               ?? throw new InvalidOperationException("GitHub OAuth returned an empty response.");
    }

    private static string GetOAuthClientId()
    {
        var configPath = AppConfigHelper.GetDefaultConfigPath();
        if (AppConfigHelper.TryGet(configPath, OAuthClientIdKey, out string? clientId)
            && !string.IsNullOrWhiteSpace(clientId))
        {
            return clientId.Trim();
        }

        // 首次调试或配置文件缺失时自动补齐，后续仍从 App.config 读取。
        AppConfigHelper.Set(configPath, OAuthClientIdKey, DefaultOAuthClientId);
        return DefaultOAuthClientId;
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

    private sealed record GitHubDeviceCodeApiResponse(
        [property: JsonPropertyName("device_code")] string? DeviceCode,
        [property: JsonPropertyName("user_code")] string? UserCode,
        [property: JsonPropertyName("verification_uri")] string? VerificationUri,
        [property: JsonPropertyName("verification_uri_complete")] string? VerificationUriComplete,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("interval")] int Interval);

    private sealed record GitHubOAuthTokenApiResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);
}
