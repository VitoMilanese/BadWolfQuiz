using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Tests;

public sealed class DiscordMuteCoordinatorTests
{
    [Fact]
    public async Task ManualUnmuteKeepsParticipantsMutedWhileMediaIsActive()
    {
        var gateway = new RecordingGateway();
        var coordinator = CreateCoordinator(gateway);
        var gameId = Guid.NewGuid();
        var connection = CreateConnection();

        await coordinator.SetAutomaticAsync(gameId, "host", connection, true, default);
        await coordinator.SetManualAsync(gameId, "host", connection, true, default);
        await coordinator.SetManualAsync(gameId, "host", connection, false, default);

        Assert.Equal([true], gateway.Operations);
    }

    [Fact]
    public async Task MediaEndKeepsParticipantsMutedWhileManualMuteIsActive()
    {
        var gateway = new RecordingGateway();
        var coordinator = CreateCoordinator(gateway);
        var gameId = Guid.NewGuid();
        var connection = CreateConnection();

        await coordinator.SetManualAsync(gameId, "host", connection, true, default);
        await coordinator.SetAutomaticAsync(gameId, "host", connection, true, default);
        await coordinator.SetAutomaticAsync(gameId, "host", connection, false, default);

        Assert.Equal([true], gateway.Operations);
    }

    [Fact]
    public async Task LastReasonReleasedUnmutesParticipants()
    {
        var gateway = new RecordingGateway();
        var coordinator = CreateCoordinator(gateway);
        var gameId = Guid.NewGuid();
        var connection = CreateConnection();

        await coordinator.SetManualAsync(gameId, "host", connection, true, default);
        await coordinator.SetAutomaticAsync(gameId, "host", connection, true, default);
        await coordinator.SetManualAsync(gameId, "host", connection, false, default);
        await coordinator.SetAutomaticAsync(gameId, "host", connection, false, default);

        Assert.Equal([true, false], gateway.Operations);
    }

    [Fact]
    public async Task DisabledAutomaticMuteDoesNotCallDiscord()
    {
        var gateway = new RecordingGateway();
        var coordinator = CreateCoordinator(gateway);
        var connection = CreateConnection();
        connection.AutoMuteDuringMedia = false;

        await coordinator.SetAutomaticAsync(
            Guid.NewGuid(), "host", connection, true, default);

        Assert.Empty(gateway.Operations);
    }

    [Fact]
    public async Task DisablingAutomaticPreferenceReleasesAnExistingAutomaticMute()
    {
        var gateway = new RecordingGateway();
        var coordinator = CreateCoordinator(gateway);
        var gameId = Guid.NewGuid();
        var connection = CreateConnection();

        await coordinator.SetAutomaticAsync(gameId, "host", connection, true, default);
        connection.AutoMuteDuringMedia = false;
        await coordinator.SetAutomaticAsync(gameId, "host", connection, false, default);

        Assert.Equal([true, false], gateway.Operations);
    }

    [Fact]
    public async Task RepeatedRequestsAreIdempotent()
    {
        var gateway = new RecordingGateway();
        var coordinator = CreateCoordinator(gateway);
        var gameId = Guid.NewGuid();
        var connection = CreateConnection();

        await coordinator.SetManualAsync(gameId, "host", connection, true, default);
        await coordinator.SetManualAsync(gameId, "host", connection, true, default);
        await coordinator.SetManualAsync(gameId, "host", connection, false, default);
        await coordinator.SetManualAsync(gameId, "host", connection, false, default);

        Assert.Equal([true, false], gateway.Operations);
    }

    [Fact]
    public async Task FailedOperationIsRetriedOnTheNextRequest()
    {
        var gateway = new RecordingGateway { FailNextOperation = true };
        var coordinator = CreateCoordinator(gateway);
        var gameId = Guid.NewGuid();
        var connection = CreateConnection();

        var failed = await coordinator.SetManualAsync(
            gameId, "host", connection, true, default);
        var retried = await coordinator.SetManualAsync(
            gameId, "host", connection, true, default);

        Assert.Equal(1, failed.FailedCount);
        Assert.Equal(1, retried.SucceededCount);
        Assert.Equal([true, true], gateway.Operations);
    }

    [Fact]
    public async Task CleanupUnmutesAndRemovesTheGameState()
    {
        var gateway = new RecordingGateway();
        var coordinator = CreateCoordinator(gateway);
        var gameId = Guid.NewGuid();
        var connection = CreateConnection();

        await coordinator.SetAutomaticAsync(gameId, "host", connection, true, default);
        await coordinator.CleanupAsync(gameId, "host", connection, default);
        await coordinator.CleanupAsync(gameId, "host", connection, default);

        Assert.Equal([true, false], gateway.Operations);
    }

    [Fact]
    public async Task ClearingAHostRemovesStaleMuteStateAfterDisconnect()
    {
        var gateway = new RecordingGateway();
        var coordinator = CreateCoordinator(gateway);
        var gameId = Guid.NewGuid();
        var connection = CreateConnection();

        await coordinator.SetManualAsync(gameId, "host", connection, true, default);
        coordinator.ClearHost("another-host");
        await coordinator.SetManualAsync(gameId, "host", connection, true, default);
        coordinator.ClearHost("host");
        await coordinator.SetManualAsync(gameId, "host", connection, true, default);

        Assert.Equal([true, true], gateway.Operations);
    }

    [Fact]
    public async Task AutomaticTimeoutDoesNotCancelManualMute()
    {
        var gateway = new RecordingGateway();
        var timeProvider = new AdjustableTimeProvider();
        var coordinator = CreateCoordinator(gateway, timeProvider);
        var gameId = Guid.NewGuid();
        var connection = CreateConnection();

        await coordinator.SetManualAsync(gameId, "host", connection, true, default);
        await coordinator.SetAutomaticAsync(gameId, "host", connection, true, default);
        timeProvider.Advance(TimeSpan.FromMinutes(16));
        await coordinator.ReleaseExpiredAutomaticMutesAsync(
            (_, _) => Task.FromResult<(string, HostDiscordConnection)?>(
                ("host", connection)), default);

        Assert.Equal([true], gateway.Operations);
    }

    [Fact]
    public async Task AutomaticTimeoutUnmutesWhenItIsTheOnlyReason()
    {
        var gateway = new RecordingGateway();
        var timeProvider = new AdjustableTimeProvider();
        var coordinator = CreateCoordinator(gateway, timeProvider);
        var gameId = Guid.NewGuid();
        var connection = CreateConnection();

        await coordinator.SetAutomaticAsync(gameId, "host", connection, true, default);
        timeProvider.Advance(TimeSpan.FromMinutes(16));
        await coordinator.ReleaseExpiredAutomaticMutesAsync(
            (_, _) => Task.FromResult<(string, HostDiscordConnection)?>(
                ("host", connection)), default);

        Assert.Equal([true, false], gateway.Operations);
    }

    private static DiscordMuteCoordinator CreateCoordinator(
        RecordingGateway gateway,
        TimeProvider? timeProvider = null) =>
        new(
            gateway,
            Options.Create(new DiscordIntegrationOptions()),
            timeProvider ?? TimeProvider.System,
            NullLogger<DiscordMuteCoordinator>.Instance);

    private static HostDiscordConnection CreateConnection() => new()
    {
        HostId = "host",
        DiscordUserId = "1",
        GuildId = "2",
        VoiceChannelId = "3",
        AutoMuteDuringMedia = true
    };

    private sealed class RecordingGateway : IDiscordVoiceGateway
    {
        public List<bool> Operations { get; } = [];
        public bool FailNextOperation { get; set; }
        public bool IsReady => true;
        public IReadOnlyList<DiscordGuildOption> GetGuilds(IReadOnlySet<string> allowedGuildIds) => [];
        public IReadOnlyList<DiscordVoiceChannelOption> GetVoiceChannels(string guildId) => [];
        public DiscordConnectionHealth GetHealth(string? guildId, string? channelId) =>
            new(true, true, true, true, true, true);

        public Task<DiscordMuteResult> SetMutedAsync(
            string guildId,
            string channelId,
            string hostDiscordUserId,
            bool muted,
            int maximumParallelOperations,
            CancellationToken cancellationToken)
        {
            Operations.Add(muted);
            if (FailNextOperation)
            {
                FailNextOperation = false;
                return Task.FromResult(new DiscordMuteResult(
                    1, 0, 1, 0, [new("4", "Test failure")]));
            }
            return Task.FromResult(new DiscordMuteResult(1, 1, 0, 0, []));
        }
    }

    private sealed class AdjustableTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration) => utcNow += duration;
    }
}
