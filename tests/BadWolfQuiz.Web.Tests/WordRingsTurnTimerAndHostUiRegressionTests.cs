using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using System.Reflection;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsTurnTimerAndHostUiRegressionTests
{
    [Fact]
    public void Result_reopen_button_and_dedicated_host_panels_have_explicit_hidden_contracts()
    {
        var css = ReadWebFile("wwwroot", "css", "word-rings-room.css");
        var coop = ReadWebFile("wwwroot", "js", "word-rings-coop.js");
        var page = ReadWebFile("Pages", "WordRings.cshtml");

        Assert.Contains("[data-reopen-result][hidden]", css, StringComparison.Ordinal);
        Assert.Contains("display: none !important", css, StringComparison.Ordinal);
        Assert.Contains("playing && !ownTurn && !isHostSeedSetup()", coop, StringComparison.Ordinal);
        Assert.Contains("data-word-bank-heading", page, StringComparison.Ordinal);
        Assert.Contains("data-room-host-seed-title", page, StringComparison.Ordinal);
        Assert.Contains("data-room-host-judgement-title", page, StringComparison.Ordinal);
        Assert.Contains("data-room-stage-message", page, StringComparison.Ordinal);
        Assert.Contains("roomWaitingHostSeeds", coop, StringComparison.Ordinal);
        Assert.Contains("wordBank.hidden = dedicatedHost && !seedSetup && !judging && !hasWords", coop, StringComparison.Ordinal);
    }

    [Fact]
    public void Room_creation_exposes_supported_turn_timer_choices_to_both_creator_modes()
    {
        var page = ReadWebFile("Pages", "WordRings.cshtml");
        var entry = ReadWebFile("wwwroot", "js", "word-rings-room-entry.js");
        var host = ReadWebFile("wwwroot", "js", "word-rings-room-create-options.js");
        var api = ReadWebFile("Pages", "WordRingsRoomApi.cshtml.cs");

        Assert.Contains("data-create-room-turn-timer", page, StringComparison.Ordinal);
        Assert.Contains("value=\"0\" selected", page, StringComparison.Ordinal);
        Assert.Contains("value=\"60\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"90\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"120\"", page, StringComparison.Ordinal);
        Assert.Contains("turnDurationSeconds", entry, StringComparison.Ordinal);
        Assert.Contains("turnDurationSeconds", host, StringComparison.Ordinal);
        Assert.Contains("int turnDurationSeconds", api, StringComparison.Ordinal);
    }

    [Fact]
    public void Expired_turn_forces_zero_point_word_and_passes_turn()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-timer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-09-15T10:00:00Z"));
            var store = CreateStore(new TestEnvironment(root), clock);
            var host = store.CreateRoom("Host", 5, false, turnDurationSeconds: 60);
            var guest = store.JoinRoom(host.RoomCode, "Guest");
            var started = store.StartGame(host.RoomCode, host.PlayerToken);

            Assert.Equal(60, started.TurnDurationSeconds);
            Assert.NotNull(started.TurnDeadlineUtc);
            var beforeCurrent = started.CurrentPlayerId;
            var beforeScore = started.PlayerScore;
            var beforePlayablePlacements = started.Placements.Count(item => !item.IsSeed);

            clock.Advance(TimeSpan.FromSeconds(61));
            var after = store.GetState(host.RoomCode, host.PlayerToken);
            var forced = Assert.Single(after.Placements.Where(item => !item.IsSeed));

            Assert.Equal(beforePlayablePlacements + 1, after.Placements.Count(item => !item.IsSeed));
            Assert.False(forced.IsCorrect);
            Assert.False(forced.IsPartial);
            Assert.Equal(0, forced.PointsAwarded);
            Assert.Equal(beforeScore, after.PlayerScore);
            Assert.NotEqual(beforeCurrent, after.CurrentPlayerId);
            Assert.NotNull(after.TurnDeadlineUtc);
            _ = guest;
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Unsupported_turn_timer_is_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-timer-invalid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = CreateStore(new TestEnvironment(root), new ManualTimeProvider(DateTimeOffset.UtcNow));
            var error = Assert.Throws<WordRingsRoomException>(() =>
                store.CreateRoom("Host", 5, false, turnDurationSeconds: 30));
            Assert.Equal(WordRingsRoomError.InvalidTurnDuration, error.Error);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Dedicated_host_is_not_counted_as_playing_timer_participant()
    {
        var coordinator = ReadWebFile("Services", "WordRingsRoomHostCoordinator.cs");
        var store = ReadWebFile("Services", "WordRingsRoomStore.cs");

        Assert.Contains("Set(player!, \"IsPlayingParticipant\", !IsHost(player!))", coordinator, StringComparison.Ordinal);
        Assert.Contains("Count(item => item.IsPlayingParticipant) >= 2", store, StringComparison.Ordinal);
        Assert.Contains("Set(room, \"TurnDeadlineUtc\", null)", coordinator, StringComparison.Ordinal);
        Assert.Contains("EnsureTurnDeadline(room, now)", store, StringComparison.Ordinal);
    }

    private static WordRingsRoomStore CreateStore(IWebHostEnvironment environment, TimeProvider timeProvider)
    {
        var constructor = typeof(WordRingsRoomStore).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(IWebHostEnvironment), typeof(TimeProvider)],
            modifiers: null);
        Assert.NotNull(constructor);
        return Assert.IsType<WordRingsRoomStore>(constructor!.Invoke([environment, timeProvider]));
    }

    private static string ReadWebFile(params string[] parts) => File.ReadAllText(FindWebFile(parts));

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan value) => _now += value;
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}
