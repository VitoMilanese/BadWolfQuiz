using BadWolfQuiz.Web.Data;
using Discord;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class UserQuestionDeletionService(
    QuizDbContext db,
    DiscordQuestionBotService bot,
    DiscordQuestionBotSettingsRepository settingsRepository,
    ILogger<UserQuestionDeletionService> logger)
{
    public async Task<bool> DeleteAsync(
        int questionId,
        CancellationToken cancellationToken = default)
    {
        var question = await db.UserQuestions
            .Include(x => x.Messages)
            .SingleOrDefaultAsync(
                x => x.Id == questionId,
                cancellationToken);

        if (question is null)
        {
            return false;
        }

        var discordMessageIds = question.Messages
            .Where(x => x.DiscordMessageId.HasValue)
            .Select(x => x.DiscordMessageId!.Value)
            .Distinct()
            .ToList();

        if (discordMessageIds.Count > 0)
        {
            await DeleteDiscordMessagesAsync(
                questionId,
                discordMessageIds,
                cancellationToken);
        }

        db.UserQuestions.Remove(question);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task DeleteDiscordMessagesAsync(
        int questionId,
        IReadOnlyCollection<ulong> messageIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = await settingsRepository.GetAsync(cancellationToken);

            if (settings is null ||
                string.IsNullOrWhiteSpace(settings.GuildId) ||
                string.IsNullOrWhiteSpace(settings.ChannelId))
            {
                logger.LogWarning(
                    "Could not delete Discord messages for question {QuestionId}: " +
                    "the Discord question bot channel is not configured.",
                    questionId);

                return;
            }

            if (bot.Client?.ConnectionState != ConnectionState.Connected)
            {
                logger.LogWarning(
                    "Could not delete Discord messages for question {QuestionId}: " +
                    "the Discord question bot is not connected.",
                    questionId);

                return;
            }

            if (!ulong.TryParse(settings.GuildId, out var guildId) ||
                !ulong.TryParse(settings.ChannelId, out var channelId))
            {
                logger.LogWarning(
                    "Could not delete Discord messages for question {QuestionId}: " +
                    "the Discord question bot channel configuration is invalid.",
                    questionId);

                return;
            }

            var guild = bot.Client.GetGuild(guildId);
            var channel = guild?.GetTextChannel(channelId);

            if (channel is null)
            {
                logger.LogWarning(
                    "Could not delete Discord messages for question {QuestionId}: " +
                    "the configured Discord question channel is unavailable.",
                    questionId);

                return;
            }

            foreach (var messageId in messageIds)
            {
                try
                {
                    await channel.DeleteMessageAsync(messageId);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        exception,
                        "Failed to delete Discord message {DiscordMessageId} " +
                        "for question {QuestionId}.",
                        messageId,
                        questionId);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to clean up Discord messages for question {QuestionId}.",
                questionId);
        }
    }
}
