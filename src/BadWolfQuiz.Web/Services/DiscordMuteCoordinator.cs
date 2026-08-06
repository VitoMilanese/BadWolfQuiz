using System.Collections.Concurrent;
using System.Diagnostics;
using BadWolfQuiz.Web.Models;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Services;

public sealed class DiscordMuteCoordinator(
    IDiscordVoiceGateway gateway,
    IOptions<DiscordIntegrationOptions> options,
    TimeProvider timeProvider,
    ILogger<DiscordMuteCoordinator> logger)
{
    private readonly ConcurrentDictionary<Guid, GameMuteState> states = new();
    private readonly DiscordIntegrationOptions settings = options.Value;

    public Task<DiscordMuteResult> SetManualAsync(
        Guid gameId,
        string hostId,
        HostDiscordConnection connection,
        bool requested,
        CancellationToken cancellationToken) =>
        UpdateAsync(gameId, hostId, connection, DiscordMuteReason.Manual,
            state => state.ManualMuteRequested = requested, cancellationToken);

    public Task<DiscordMuteResult> SetAutomaticAsync(
        Guid gameId,
        string hostId,
        HostDiscordConnection connection,
        bool requested,
        CancellationToken cancellationToken)
    {
        if (!connection.AutoMuteDuringMedia && requested)
        {
            return Task.FromResult(EmptyResult());
        }

        return UpdateAsync(gameId, hostId, connection, DiscordMuteReason.AutomaticMedia,
            state =>
            {
                state.AutomaticMediaMuteRequested = requested;
                state.AutomaticRequestedAtUtc = requested ? timeProvider.GetUtcNow() : null;
            }, cancellationToken);
    }

    public async Task<DiscordMuteResult> CleanupAsync(
        Guid gameId,
        string hostId,
        HostDiscordConnection connection,
        CancellationToken cancellationToken)
    {
        if (!states.TryRemove(gameId, out var state))
        {
            return EmptyResult();
        }

        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            return state.AppliedMuted
                ? await ApplyAsync(gameId, hostId, connection, false,
                    DiscordMuteReason.GameCleanup, cancellationToken)
                : EmptyResult();
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public async Task ReleaseExpiredAutomaticMutesAsync(
        Func<Guid, CancellationToken, Task<(string HostId, HostDiscordConnection Connection)?>> resolve,
        CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().AddMinutes(-settings.AutomaticMuteTimeoutMinutes);
        foreach (var item in states.Where(item =>
                     item.Value.AutomaticMediaMuteRequested &&
                     item.Value.AutomaticRequestedAtUtc <= cutoff).ToArray())
        {
            var context = await resolve(item.Key, cancellationToken);
            if (context is not { } resolved)
            {
                states.TryRemove(item.Key, out _);
                continue;
            }

            await UpdateAsync(item.Key, resolved.HostId, resolved.Connection,
                DiscordMuteReason.AutomaticTimeout,
                state =>
                {
                    state.AutomaticMediaMuteRequested = false;
                    state.AutomaticRequestedAtUtc = null;
                }, cancellationToken);
        }
    }

    private async Task<DiscordMuteResult> UpdateAsync(
        Guid gameId,
        string hostId,
        HostDiscordConnection connection,
        DiscordMuteReason reason,
        Action<GameMuteState> update,
        CancellationToken cancellationToken)
    {
        var state = states.GetOrAdd(gameId, _ => new());
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            update(state);
            var shouldBeMuted = state.ManualMuteRequested || state.AutomaticMediaMuteRequested;
            if (shouldBeMuted == state.AppliedMuted)
            {
                return EmptyResult();
            }

            var result = await ApplyAsync(gameId, hostId, connection, shouldBeMuted,
                reason, cancellationToken);
            if (result.FailedCount == 0)
            {
                state.AppliedMuted = shouldBeMuted;
            }
            return result;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private async Task<DiscordMuteResult> ApplyAsync(
        Guid gameId,
        string hostId,
        HostDiscordConnection connection,
        bool muted,
        DiscordMuteReason reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connection.GuildId) ||
            string.IsNullOrWhiteSpace(connection.VoiceChannelId))
        {
            return new(0, 0, 1, 0, [new("", "Discord connection is not configured.")]);
        }

        var stopwatch = Stopwatch.StartNew();
        var result = await gateway.SetMutedAsync(
            connection.GuildId,
            connection.VoiceChannelId,
            connection.DiscordUserId,
            muted,
            settings.MaximumParallelOperations,
            cancellationToken);
        logger.LogInformation(
            "Discord voice moderation HostId={HostId} GameSessionId={GameSessionId} GuildId={GuildId} ChannelId={ChannelId} Operation={Operation} Reason={Reason} Targets={Targets} Succeeded={Succeeded} Failed={Failed} Skipped={Skipped} ElapsedMs={ElapsedMs}",
            hostId, gameId, connection.GuildId, connection.VoiceChannelId,
            muted ? "mute" : "unmute", reason, result.TargetCount,
            result.SucceededCount, result.FailedCount, result.SkippedCount,
            stopwatch.ElapsedMilliseconds);
        return result;
    }

    private static DiscordMuteResult EmptyResult() => new(0, 0, 0, 0, []);

    private sealed class GameMuteState
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public bool ManualMuteRequested { get; set; }
        public bool AutomaticMediaMuteRequested { get; set; }
        public DateTimeOffset? AutomaticRequestedAtUtc { get; set; }
        public bool AppliedMuted { get; set; }
    }
}
