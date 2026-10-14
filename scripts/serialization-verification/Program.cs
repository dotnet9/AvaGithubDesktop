using System.Text.Json;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;

if (JsonSerializer.IsReflectionEnabledByDefault)
    throw new InvalidOperationException("Reflection serialization must be disabled for this regression.");

// 使用内存中的历史样本，验证旧版字段大小写、往返及空记录；不访问真实配置或工作区。
const string historyJson = """
[{"name":"Sample","path":"/sample/repository","groupName":"Samples","lastOpenedAt":"2026-10-14T12:00:00+00:00","remoteUrl":null}]
""";
var history = JsonSerializer.Deserialize(historyJson, RepositoryHistoryJsonContext.Default.ListRepositoryHistoryEntry)!;
if (history.Count != 1 || history[0].Name != "Sample" || history[0].RemoteUrl is not null)
    throw new InvalidOperationException("Repository history compatibility failed.");
var legacy = JsonSerializer.Deserialize(historyJson.Replace("\"name\"", "\"Name\""), RepositoryHistoryJsonContext.Default.ListRepositoryHistoryEntry)!;
if (legacy[0].Name != "Sample") throw new InvalidOperationException("Legacy property casing was not preserved.");
await using var buffer = new MemoryStream();
await JsonSerializer.SerializeAsync(buffer, history.ToArray(), RepositoryHistoryJsonContext.Default.RepositoryHistoryEntryArray);
buffer.Position = 0;
var restored = await JsonSerializer.DeserializeAsync(buffer, RepositoryHistoryJsonContext.Default.ListRepositoryHistoryEntry);
if (restored?.Single() != history[0]) throw new InvalidOperationException("Repository history round-trip failed.");
if (JsonSerializer.Deserialize("[]", RepositoryHistoryJsonContext.Default.ListRepositoryHistoryEntry)?.Count != 0)
    throw new InvalidOperationException("Empty repository history failed.");
try
{
    JsonSerializer.Deserialize("{broken", RepositoryHistoryJsonContext.Default.ListRepositoryHistoryEntry);
    throw new InvalidOperationException("Malformed history unexpectedly succeeded.");
}
catch (JsonException) { }

GitHubAccountService.SerializationContractProbe.Verify();
Console.WriteLine("PASS: reflection disabled; history compatibility/round-trip and account/API/device JSON contracts.");

namespace AvaGithubDesktop.Core.Services
{
    public sealed partial class GitHubAccountService
    {
        internal static class SerializationContractProbe
        {
            internal static void Verify()
            {
                // 全部使用合成数据，不读取账号文件、不连接认证服务、不输出令牌。
                var account = new GitHubAccount("sample", "https://api.github.com", "synthetic-fixture", [], "", 1, "Sample", "free");
                var document = new AccountStoreDocument([account]);
                var json = JsonSerializer.Serialize(document, AccountJsonContext.Default.AccountStoreDocument);
                var restored = JsonSerializer.Deserialize(json, AccountJsonContext.Default.AccountStoreDocument)!;
                if (restored.Accounts.Count != 1 || restored.Accounts[0].Login != account.Login || restored.Accounts[0].Token != account.Token
                    || !json.Contains("\"accounts\"", StringComparison.Ordinal) || json.Contains("isDotCom", StringComparison.Ordinal))
                    throw new InvalidOperationException("Account storage contract failed.");
    
                var user = JsonSerializer.Deserialize("""{"login":"sample","id":1,"avatar_url":"https://example.org/avatar","plan":{"name":"free"}}""", AccountJsonContext.Default.GitHubUserApiResponse)!;
                var emails = JsonSerializer.Deserialize("""[{"email":"sample@example.org","verified":true,"primary":true,"visibility":"private"}]""", AccountJsonContext.Default.IReadOnlyListGitHubEmailApiResponse)!;
                var device = JsonSerializer.Deserialize("""{"device_code":"fixture","user_code":"FIXTURE","verification_uri":"https://github.com/login/device","expires_in":900,"interval":5}""", AccountJsonContext.Default.GitHubDeviceCodeApiResponse)!;
                var token = JsonSerializer.Deserialize("""{"access_token":"synthetic-fixture"}""", AccountJsonContext.Default.GitHubOAuthTokenApiResponse)!;
                var pending = JsonSerializer.Deserialize("""{"error":"authorization_pending","error_description":"Pending"}""", AccountJsonContext.Default.GitHubOAuthTokenApiResponse)!;
                if (user.Login != "sample" || user.Plan?.Name != "free" || emails.Count != 1 || !emails[0].Primary
                    || device.Interval != 5 || device.ExpiresIn != 900 || token.AccessToken is null || pending.Error != "authorization_pending")
                    throw new InvalidOperationException("GitHub response JSON contract failed.");
            }
        }
    }
}
