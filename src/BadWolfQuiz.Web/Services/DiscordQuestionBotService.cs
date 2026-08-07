using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Services;

public sealed class DiscordQuestionBotService : IHostedService, IAsyncDisposable
{
    private readonly DiscordQuestionBotOptions _options;
    private readonly ILogger<DiscordQuestionBotService> _logger;
    private DiscordSocketClient? _client;

    public DiscordQuestionBotService(
        IOptions<DiscordQuestionBotOptions> options,
        ILogger<DiscordQuestionBotService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public DiscordSocketClient? Client => _client;

    public IReadOnlyList<DiscordGuildOption> GetGuilds()
    {
        if (_client?.ConnectionState != ConnectionState.Connected)
        {
            return [];
        }

        return _client.Guilds
            .OrderBy(x => x.Name)
            .Select(x => new DiscordGuildOption(
                x.Id.ToString(),
                x.Name))
            .ToArray();
    }

    public IReadOnlyList<DiscordTextChannelOption> GetTextChannels(string guildId)
    {
        if (_client?.ConnectionState != ConnectionState.Connected ||
            !ulong.TryParse(guildId, out var parsedGuildId))
        {
            return [];
        }

        var guild = _client.GetGuild(parsedGuildId);
        if (guild is null)
        {
            return [];
        }

        return guild.TextChannels
            .Where(x =>
            {
                var permissions = guild.CurrentUser.GetPermissions(x);

                return permissions.ViewChannel &&
                       permissions.SendMessages;
            })
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Name)
            .Select(x => new DiscordTextChannelOption(
                x.Id.ToString(),
                x.Name))
            .ToArray();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Discord question bot is disabled.");
            return;
        }

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
        });

        _client.Log += OnLogAsync;
        _client.Ready += OnReadyAsync;

        await _client.LoginAsync(TokenType.Bot, _options.BotToken);
        await _client.StartAsync();

        _logger.LogInformation("Discord question bot is starting.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return;
        }

        await _client.StopAsync();
        await _client.LogoutAsync();
    }

    private Task OnReadyAsync()
    {
        _logger.LogInformation(
            "Discord question bot is ready as {Username}.",
            _client?.CurrentUser?.Username);

        return Task.CompletedTask;
    }

    private Task OnLogAsync(LogMessage message)
    {
        _logger.LogInformation(
            "Discord question bot: {Message}",
            message.ToString());

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is null)
        {
            return;
        }

        _client.Log -= OnLogAsync;
        _client.Ready -= OnReadyAsync;

        await _client.DisposeAsync();
    }
}
