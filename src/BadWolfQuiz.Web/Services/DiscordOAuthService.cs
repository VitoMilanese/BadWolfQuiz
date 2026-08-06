using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Services;

public sealed record DiscordOAuthUser(string Id, string UserName);
public sealed record DiscordOAuthSession(
    DiscordOAuthUser User,
    IReadOnlyDictionary<string, string> GuildNames,
    DateTimeOffset ExpiresAtUtc);

public sealed class DiscordOAuthService(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<DiscordIntegrationOptions> options)
{
    private const long ManageGuildPermission = 1L << 5;
    private const long AdministratorPermission = 1L << 3;
    private readonly DiscordIntegrationOptions settings = options.Value;

    public string CreateAuthorizationUrl(string hostId)
    {
        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        cache.Set(StateKey(state), hostId, TimeSpan.FromMinutes(10));
        return QueryHelpers.AddQueryString("https://discord.com/oauth2/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = settings.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = settings.CallbackUrl,
            ["scope"] = "identify guilds",
            ["state"] = state,
            ["prompt"] = "consent"
        });
    }

    public bool ConsumeState(string state, string hostId)
    {
        if (!cache.TryGetValue(StateKey(state), out string? expectedHostId))
        {
            return false;
        }

        cache.Remove(StateKey(state));
        return string.Equals(expectedHostId, hostId, StringComparison.Ordinal);
    }

    public async Task<DiscordOAuthSession> ExchangeAsync(
        string hostId,
        string code,
        CancellationToken cancellationToken)
    {
        using var tokenResponse = await httpClient.PostAsync(
            "https://discord.com/api/v10/oauth2/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = settings.CallbackUrl
            }), cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        using var tokenDocument = JsonDocument.Parse(
            await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString()!;
        var expiresIn = tokenDocument.RootElement.GetProperty("expires_in").GetInt32();

        using var userRequest = AuthorizedRequest("users/@me", accessToken);
        using var guildsRequest = AuthorizedRequest("users/@me/guilds", accessToken);
        using var userResponse = await httpClient.SendAsync(userRequest, cancellationToken);
        using var guildsResponse = await httpClient.SendAsync(guildsRequest, cancellationToken);
        userResponse.EnsureSuccessStatusCode();
        guildsResponse.EnsureSuccessStatusCode();

        using var userDocument = JsonDocument.Parse(
            await userResponse.Content.ReadAsStringAsync(cancellationToken));
        using var guildsDocument = JsonDocument.Parse(
            await guildsResponse.Content.ReadAsStringAsync(cancellationToken));
        var user = new DiscordOAuthUser(
            userDocument.RootElement.GetProperty("id").GetString()!,
            userDocument.RootElement.GetProperty("username").GetString()!);
        var guilds = guildsDocument.RootElement.EnumerateArray()
            .Where(guild =>
            {
                var permissions = long.Parse(guild.GetProperty("permissions").GetString()!);
                return (permissions & (ManageGuildPermission | AdministratorPermission)) != 0;
            })
            .ToDictionary(
                guild => guild.GetProperty("id").GetString()!,
                guild => guild.GetProperty("name").GetString()!);
        var session = new DiscordOAuthSession(
            user, guilds, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        cache.Set(SessionKey(hostId), session, TimeSpan.FromMinutes(30));
        return session;
    }

    public DiscordOAuthSession? GetSession(string hostId) =>
        cache.TryGetValue(SessionKey(hostId), out DiscordOAuthSession? session)
            ? session
            : null;

    public void ClearSession(string hostId) => cache.Remove(SessionKey(hostId));

    public string CreateBotInstallUrl(string? guildId = null)
    {
        const long permissions = 1024L | 1048576L | 4194304L;
        return QueryHelpers.AddQueryString("https://discord.com/oauth2/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = settings.ClientId,
            ["scope"] = "bot",
            ["permissions"] = permissions.ToString(),
            ["guild_id"] = guildId,
            ["disable_guild_select"] = string.IsNullOrWhiteSpace(guildId) ? "false" : "true"
        });
    }

    private static HttpRequestMessage AuthorizedRequest(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v10/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string StateKey(string state) => $"discord-oauth-state:{state}";
    private static string SessionKey(string hostId) => $"discord-oauth-session:{hostId}";
}
