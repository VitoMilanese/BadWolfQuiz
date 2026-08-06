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

    private static DiscordMuteCoordinator CreateCoordinator(RecordingGateway gateway) =>
        new(
            gateway,
            Options.Create(new DiscordIntegrationOptions()),
            TimeProvider.System,
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
            return Task.FromResult(new DiscordMuteResult(1, 1, 0, 0, []));
        }
    }
}
