using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class DiscordQuestionBotSettingsRepository(
    QuizDbContext db)
{
    private const int SettingsId = 1;

    public Task<DiscordQuestionBotSettings?> GetAsync(
    CancellationToken cancellationToken = default)
    {
        return db.DiscordQuestionBotSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == SettingsId,
                cancellationToken);
    }

    public async Task SaveAsync(
        string guildId,
        string guildName,
        string channelId,
        string channelName,
        CancellationToken cancellationToken = default)
    {
        var settings = await db.DiscordQuestionBotSettings
            .SingleOrDefaultAsync(
                x => x.Id == SettingsId,
                cancellationToken);

        if (settings is null)
        {
            settings = new DiscordQuestionBotSettings
            {
                Id = SettingsId
            };

            db.DiscordQuestionBotSettings.Add(settings);
        }

        settings.GuildId = guildId;
        settings.GuildName = guildName;
        settings.ChannelId = channelId;
        settings.ChannelName = channelName;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
