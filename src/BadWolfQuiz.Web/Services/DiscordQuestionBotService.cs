using BadWolfQuiz.Web.Localization;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Services;

public sealed class DiscordQuestionBotService : IHostedService, IAsyncDisposable
{
    private const string DeletePrefix = "question-delete:";
    private const string DeleteConfirmPrefix = "question-delete-confirm:";
    private const string DeleteCancelPrefix = "question-delete-cancel:";

    private readonly DiscordQuestionBotOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<DiscordQuestionBotService> _logger;
    private DiscordSocketClient? _client;

    public DiscordQuestionBotService(
        IOptions<DiscordQuestionBotOptions> options,
        IServiceScopeFactory scopeFactory,
        IStringLocalizer<SharedResource> localizer,
        ILogger<DiscordQuestionBotService> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _localizer = localizer;
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
        _client.ButtonExecuted += OnButtonExecutedAsync;

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

    private async Task OnButtonExecutedAsync(SocketMessageComponent component)
    {
        try
        {
            var customId = component.Data.CustomId;

            if (customId.StartsWith(DeletePrefix, StringComparison.Ordinal))
            {
                if (!TryGetQuestionId(customId, DeletePrefix, out var questionId))
                {
                    return;
                }

                var components = new ComponentBuilder()
                    .WithButton(
                        _localizer["Button_Cancel"].Value,
                        customId: $"{DeleteCancelPrefix}{questionId}",
                        style: ButtonStyle.Secondary)
                    .WithButton(
                        _localizer["Button_Delete"].Value,
                        customId: $"{DeleteConfirmPrefix}{questionId}",
                        style: ButtonStyle.Danger)
                    .Build();

                var confirmationText =
                    $"**{_localizer["QuestionInbox_DeleteConfirmTitle"].Value}**\n" +
                    _localizer["QuestionInbox_DeleteConfirmText"].Value;

                await component.RespondAsync(
                    text: confirmationText,
                    components: components,
                    ephemeral: true);

                return;
            }

            if (customId.StartsWith(DeleteCancelPrefix, StringComparison.Ordinal))
            {
                await component.DeferAsync(ephemeral: true);
                await component.DeleteOriginalResponseAsync();
                return;
            }

            if (!customId.StartsWith(DeleteConfirmPrefix, StringComparison.Ordinal) ||
                !TryGetQuestionId(
                    customId,
                    DeleteConfirmPrefix,
                    out var confirmedQuestionId))
            {
                return;
            }

            await component.DeferAsync(ephemeral: true);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var deletionService = scope.ServiceProvider
                .GetRequiredService<UserQuestionDeletionService>();

            var deleted = await deletionService.DeleteAsync(confirmedQuestionId);

            if (deleted)
            {
                await component.DeleteOriginalResponseAsync();
                return;
            }

            await component.ModifyOriginalResponseAsync(properties =>
            {
                properties.Content =
                    _localizer["QuestionInbox_DeleteNotFound"].Value;
                properties.Components = new ComponentBuilder().Build();
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to handle a Discord question bot interaction.");
        }
    }

    private static bool TryGetQuestionId(
        string customId,
        string prefix,
        out int questionId)
    {
        return int.TryParse(
            customId.AsSpan(prefix.Length),
            out questionId);
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
        _client.ButtonExecuted -= OnButtonExecutedAsync;

        await _client.DisposeAsync();
    }
}
