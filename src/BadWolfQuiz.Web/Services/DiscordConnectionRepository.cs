using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class DiscordConnectionRepository(QuizDbContext db)
{
    public Task<HostDiscordConnection?> GetAsync(CancellationToken cancellationToken) =>
        db.HostDiscordConnections.SingleOrDefaultAsync(cancellationToken);

    public async Task SaveIdentityAsync(
        string hostId,
        DiscordOAuthUser user,
        CancellationToken cancellationToken)
    {
        var connection = await GetAsync(cancellationToken);
        if (connection is null)
        {
            connection = new HostDiscordConnection
            {
                HostId = hostId,
                DiscordUserId = user.Id,
                DiscordUserName = user.UserName
            };
            db.HostDiscordConnections.Add(connection);
        }
        else
        {
            connection.DiscordUserId = user.Id;
            connection.DiscordUserName = user.UserName;
            connection.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveSelectionAsync(
        string guildId,
        string guildName,
        string voiceChannelId,
        string voiceChannelName,
        bool automaticMute,
        CancellationToken cancellationToken)
    {
        var connection = await GetAsync(cancellationToken)
            ?? throw new InvalidOperationException("Discord is not connected.");
        connection.GuildId = guildId;
        connection.GuildName = guildName;
        connection.VoiceChannelId = voiceChannelId;
        connection.VoiceChannelName = voiceChannelName;
        connection.AutoMuteDuringMedia = automaticMute;
        connection.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var connection = await GetAsync(cancellationToken);
        if (connection is not null)
        {
            db.HostDiscordConnections.Remove(connection);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
