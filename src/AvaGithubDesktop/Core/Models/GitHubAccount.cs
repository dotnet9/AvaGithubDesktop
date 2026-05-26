using System.Text.Json.Serialization;

namespace AvaGithubDesktop.Core.Models;

public sealed record GitHubAccountEmail(
    string Email,
    bool Verified,
    bool Primary,
    string Visibility);

public sealed record GitHubAccount(
    string Login,
    string Endpoint,
    string Token,
    IReadOnlyList<GitHubAccountEmail> Emails,
    string AvatarUrl,
    long Id,
    string Name,
    string Plan)
{
    [JsonIgnore]
    public bool IsDotCom => GitHubAccountEndpoints.IsDotComEndpoint(Endpoint);

    [JsonIgnore]
    public string FriendlyName => string.IsNullOrWhiteSpace(Name) ? Login : Name;

    [JsonIgnore]
    public string FriendlyEndpoint => GitHubAccountEndpoints.GetFriendlyEndpoint(Endpoint);

    [JsonIgnore]
    public string Initials => BuildInitials(FriendlyName);

    public GitHubAccount WithToken(string token) =>
        this with { Token = token };

    private static string BuildInitials(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return "GH";
        }

        var parts = trimmed
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]))
            .ToArray();
        return parts.Length == 0 ? "GH" : new string(parts);
    }
}

public sealed record GitHubSignInRequest(string Endpoint, string Token);

public sealed record GitHubDeviceAuthorization(
    string Endpoint,
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    string VerificationUriComplete,
    int ExpiresIn,
    int Interval);

public static class GitHubAccountEndpoints
{
    public const string DotComApiEndpoint = "https://api.github.com";
    public const string DotComHtmlEndpoint = "https://github.com";

    public static string NormalizeApiEndpoint(string endpointInput)
    {
        var endpoint = endpointInput.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return DotComApiEndpoint;
        }

        if (!endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = $"https://{endpoint}";
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException("GitHub endpoint is invalid.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("GitHub endpoint must use HTTPS.");
        }

        if (IsDotComHost(uri.Host))
        {
            return DotComApiEndpoint;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = uri.IsDefaultPort ? -1 : uri.Port
        };
        var path = builder.Path.TrimEnd('/');

        // GitHub Enterprise 用户通常输入站点首页地址，实际 API 端点需要追加 /api/v3。
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            builder.Path = "/api/v3";
        }
        else if (!path.EndsWith("/api/v3", StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = $"{path}/api/v3";
        }
        else
        {
            builder.Path = path;
        }

        return builder.Uri.ToString().TrimEnd('/');
    }

    public static string GetFriendlyEndpoint(string apiEndpoint)
    {
        if (IsDotComEndpoint(apiEndpoint))
        {
            return "GitHub.com";
        }

        return Uri.TryCreate(GetHtmlEndpoint(apiEndpoint), UriKind.Absolute, out var uri)
            ? uri.Host
            : apiEndpoint;
    }

    public static bool IsDotComEndpoint(string apiEndpoint) =>
        string.Equals(NormalizeApiEndpoint(apiEndpoint), DotComApiEndpoint, StringComparison.OrdinalIgnoreCase);

    public static string GetHtmlEndpoint(string apiEndpoint)
    {
        var normalized = NormalizeApiEndpoint(apiEndpoint);
        if (string.Equals(normalized, DotComApiEndpoint, StringComparison.OrdinalIgnoreCase))
        {
            return DotComHtmlEndpoint;
        }

        var uri = new Uri(normalized);
        var host = uri.Host.StartsWith("api.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
        var builder = new UriBuilder(Uri.UriSchemeHttps, host, uri.IsDefaultPort ? -1 : uri.Port);
        return builder.Uri.ToString().TrimEnd('/');
    }

    public static string BuildNewTokenUrl(string endpointInput)
    {
        var htmlEndpoint = GetHtmlEndpoint(NormalizeApiEndpoint(endpointInput));
        var scopes = Uri.EscapeDataString("repo,user,workflow");
        var description = Uri.EscapeDataString("AvaGithubDesktop");
        return new Uri(
            new Uri($"{htmlEndpoint.TrimEnd('/')}/"),
            $"settings/tokens/new?scopes={scopes}&description={description}").ToString();
    }

    public static Uri BuildApiUri(string endpoint, string relativePath) =>
        new(new Uri($"{NormalizeApiEndpoint(endpoint).TrimEnd('/')}/"), relativePath.TrimStart('/'));

    private static bool IsDotComHost(string host) =>
        string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "api.github.com", StringComparison.OrdinalIgnoreCase);
}
