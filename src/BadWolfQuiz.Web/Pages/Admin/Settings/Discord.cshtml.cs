using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages.Admin.Settings;

public sealed class DiscordModel(
    DiscordConnectionRepository repository,
    DiscordOAuthService oauth,
    IDiscordVoiceGateway gateway,
    DiscordMuteCoordinator muteCoordinator,
    CurrentHost currentHost,
    IOptions<DiscordIntegrationOptions> options) : PageModel
{
    private readonly DiscordIntegrationOptions settings = options.Value;

    public HostDiscordConnection? Connection { get; private set; }
    public IReadOnlyList<DiscordGuildOption> Guilds { get; private set; } = [];
    public IReadOnlyList<DiscordVoiceChannelOption> Channels { get; private set; } = [];
    public DiscordConnectionHealth Health { get; private set; } =
        new(false, false, false, false, false, false);
    public bool IsEnabled => settings.Enabled;
    public string BotInstallUrl => oauth.CreateBotInstallUrl(Connection?.GuildId);

    [BindProperty]
    public string? GuildId { get; set; }

    [BindProperty]
    public string? VoiceChannelId { get; set; }

    [BindProperty]
    public bool AutoMuteDuringMedia { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public IActionResult OnPostConnect()
    {
        if (!settings.Enabled)
        {
            return RedirectToPage();
        }
        return Redirect(oauth.CreateAuthorizationUrl(currentHost.RequiredId));
    }

    public async Task<IActionResult> OnGetCallbackAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled || !string.IsNullOrWhiteSpace(error) ||
            string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state) ||
            !oauth.ConsumeState(state, currentHost.RequiredId))
        {
            TempData["DiscordError"] = "Discord authorization was cancelled or expired.";
            return RedirectToPage();
        }

        try
        {
            var session = await oauth.ExchangeAsync(
                currentHost.RequiredId, code, cancellationToken);
            await repository.SaveIdentityAsync(
                currentHost.RequiredId, session.User, cancellationToken);
            TempData["DiscordSuccess"] = "Discord account connected.";
        }
        catch (HttpRequestException)
        {
            TempData["DiscordError"] = "Discord authorization failed.";
        }
        return RedirectToPage();
    }

    public JsonResult OnGetChannels(string guildId)
    {
        var session = oauth.GetSession(currentHost.RequiredId);
        if (session is null || !session.GuildNames.ContainsKey(guildId))
        {
            return new JsonResult(new { error = "Discord authorization expired." })
                { StatusCode = 403 };
        }
        return new JsonResult(gateway.GetVoiceChannels(guildId));
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var session = oauth.GetSession(currentHost.RequiredId);
        var guild = session is not null && GuildId is not null &&
            session.GuildNames.TryGetValue(GuildId, out var guildName)
                ? new DiscordGuildOption(GuildId, guildName)
                : null;
        var channel = GuildId is not null && VoiceChannelId is not null
            ? gateway.GetVoiceChannels(GuildId).SingleOrDefault(x => x.Id == VoiceChannelId)
            : null;
        if (guild is null || channel is null)
        {
            TempData["DiscordError"] = "Select a Discord server and voice channel.";
            return RedirectToPage();
        }

        await repository.SaveSelectionAsync(
            guild.Id, guild.Name, channel.Id, channel.Name,
            AutoMuteDuringMedia, cancellationToken);
        TempData["DiscordSuccess"] = "Discord voice channel saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDisconnectAsync(CancellationToken cancellationToken)
    {
        var connection = await repository.GetAsync(cancellationToken);
        if (connection?.GuildId is not null && connection.VoiceChannelId is not null)
        {
            await gateway.SetMutedAsync(
                connection.GuildId,
                connection.VoiceChannelId,
                connection.DiscordUserId,
                false,
                settings.MaximumParallelOperations,
                cancellationToken);
        }
        await repository.DeleteAsync(cancellationToken);
        oauth.ClearSession(currentHost.RequiredId);
        TempData["DiscordSuccess"] = "Discord disconnected.";
        return RedirectToPage();
    }

    public async Task<JsonResult> OnPostTestAsync(CancellationToken cancellationToken)
    {
        var connection = await repository.GetAsync(cancellationToken);
        if (connection is null)
        {
            return new JsonResult(new { error = "Discord is not configured." }) { StatusCode = 409 };
        }

        var result = await gateway.SetMutedAsync(
            connection.GuildId!, connection.VoiceChannelId!, connection.DiscordUserId,
            true, settings.MaximumParallelOperations, cancellationToken);
        await gateway.SetMutedAsync(
            connection.GuildId!, connection.VoiceChannelId!, connection.DiscordUserId,
            false, settings.MaximumParallelOperations, cancellationToken);
        return new JsonResult(new
        {
            result.TargetCount,
            result.SucceededCount,
            result.FailedCount,
            result.SkippedCount
        });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Connection = await repository.GetAsync(cancellationToken);
        GuildId = Connection?.GuildId;
        VoiceChannelId = Connection?.VoiceChannelId;
        AutoMuteDuringMedia = Connection?.AutoMuteDuringMedia ?? false;
        var session = oauth.GetSession(currentHost.RequiredId);
        Guilds = session is null
            ? []
            : gateway.GetGuilds(session.GuildNames.Keys.ToHashSet(StringComparer.Ordinal));
        Channels = GuildId is null ? [] : gateway.GetVoiceChannels(GuildId);
        Health = gateway.GetHealth(Connection?.GuildId, Connection?.VoiceChannelId);
    }
}
