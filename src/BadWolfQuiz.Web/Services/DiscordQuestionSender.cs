using BadWolfQuiz.Web.Localization;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Services;

public sealed class DiscordQuestionSender(
    DiscordQuestionBotService bot,
    DiscordQuestionBotSettingsRepository settingsRepository,
    IConfiguration configuration,
    IStringLocalizer<SharedResource> localizer,
    ILogger<DiscordQuestionSender> logger)
{
    public async Task<bool> SendAsync(
        int questionId,
        string? senderName,
        string message,
        CancellationToken cancellationToken = default)
    {
        var settings =
            await settingsRepository.GetAsync(cancellationToken);

        if (settings is null ||
            string.IsNullOrWhiteSpace(settings.GuildId) ||
            string.IsNullOrWhiteSpace(settings.ChannelId))
        {
            logger.LogWarning(
                "The Discord question bot channel is not configured.");

            return false;
        }

        if (bot.Client?.ConnectionState != ConnectionState.Connected)
        {
            logger.LogWarning(
                "The Discord question bot is not connected.");

            return false;
        }

        if (!ulong.TryParse(settings.GuildId, out var guildId) ||
            !ulong.TryParse(settings.ChannelId, out var channelId))
        {
            logger.LogWarning(
                "The Discord question bot channel configuration is invalid.");

            return false;
        }

        var guild = bot.Client.GetGuild(guildId);
        var channel = guild?.GetTextChannel(channelId);

        if (channel is null)
        {
            logger.LogWarning(
                "The configured Discord question channel is unavailable.");

            return false;
        }

        var author = string.IsNullOrWhiteSpace(senderName)
            ? localizer["DiscordQuestion_Anonymous"].Value
            : senderName.Trim();

        var heading = localizer["DiscordQuestion_NewMessageFrom", author].Value;
        var content = $"**{heading}**\n{message.Trim()}";

        var publicBaseUrl = configuration["Game:PublicBaseUrl"]?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            logger.LogWarning("Game:PublicBaseUrl is not configured.");
            return false;
        }

        var replyUrl =
            $"{publicBaseUrl}/Admin/QuestionInbox?questionId={questionId}";

        var components = new ComponentBuilder()
            .WithButton(
                localizer["DiscordQuestion_Reply"].Value,
                style: ButtonStyle.Link,
                url: replyUrl)
            .Build();

        try
        {
            await channel.SendMessageAsync(
                text: content,
                components: components,
                allowedMentions: AllowedMentions.None);

            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to send question {QuestionId} to Discord.",
                questionId);

            return false;
        }
    }
}
