namespace BadWolfQuiz.Web.Services;

public enum DiscordMuteReason
{
    Manual,
    AutomaticMedia,
    AutomaticTimeout,
    GameCleanup,
    Test
}

public sealed record DiscordGuildOption(string Id, string Name);
public sealed record DiscordVoiceChannelOption(string Id, string Name);
public sealed record DiscordTextChannelOption(string Id, string Name);
public sealed record DiscordConnectionHealth(
    bool BotOnline,
    bool GuildAvailable,
    bool ChannelAvailable,
    bool HasViewPermission,
    bool HasConnectPermission,
    bool HasMutePermission)
{
    public bool IsReady => BotOnline && GuildAvailable && ChannelAvailable &&
        HasViewPermission && HasConnectPermission && HasMutePermission;
}

public sealed record DiscordMuteFailure(string UserId, string Error);

public sealed record DiscordMuteResult(
    int TargetCount,
    int SucceededCount,
    int FailedCount,
    int SkippedCount,
    IReadOnlyList<DiscordMuteFailure> Failures)
{
    public bool IsSuccess => FailedCount == 0;
}

public interface IDiscordVoiceGateway
{
    bool IsReady { get; }
    IReadOnlyList<DiscordGuildOption> GetGuilds(IReadOnlySet<string> allowedGuildIds);
    IReadOnlyList<DiscordVoiceChannelOption> GetVoiceChannels(string guildId);
    DiscordConnectionHealth GetHealth(string? guildId, string? channelId);
    Task<DiscordMuteResult> SetMutedAsync(
        string guildId,
        string channelId,
        string hostDiscordUserId,
        bool muted,
        int maximumParallelOperations,
        CancellationToken cancellationToken);
}
