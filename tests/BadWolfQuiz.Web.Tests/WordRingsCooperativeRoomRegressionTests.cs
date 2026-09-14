using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsCooperativeRoomRegressionTests
{
    [Fact]
    public void Cooperative_room_ui_uses_custom_result_dialog_and_room_controls()
    {
        var page = ReadWebFile("Pages", "WordRings.cshtml");
        var roomScript = ReadWebFile("wwwroot", "js", "word-rings-coop.js");
        var entryScript = ReadWebFile("wwwroot", "js", "word-rings-room-entry.js");
        var resultScript = ReadWebFile("wwwroot", "js", "word-rings-end-dialog.js");
        var styles = ReadWebFile("wwwroot", "css", "word-rings-room.css");

        Assert.Contains("data-open-coop-room", page, StringComparison.Ordinal);
        Assert.Contains("data-create-room-dialog", page, StringComparison.Ordinal);
        Assert.Contains("min=\"5\" max=\"15\"", page, StringComparison.Ordinal);
        Assert.Contains("data-create-room-partial", page, StringComparison.Ordinal);
        Assert.Contains("data-word-rings-result-dialog", page, StringComparison.Ordinal);
        Assert.Contains("data-word-rings-player-list", page, StringComparison.Ordinal);
        Assert.Contains("word-rings-coop.js", page, StringComparison.Ordinal);
        Assert.Contains("word-rings-room-entry.js", page, StringComparison.Ordinal);

        Assert.Contains("SubmitRoomWord", roomScript, StringComparison.Ordinal);
        Assert.Contains("StartRoom", roomScript, StringComparison.Ordinal);
        Assert.Contains("currentPlayerId", roomScript, StringComparison.Ordinal);
        Assert.Contains("setInterval", roomScript, StringComparison.Ordinal);
        Assert.Contains("900", roomScript, StringComparison.Ordinal);
        Assert.Contains("wordrings:game-ended", roomScript, StringComparison.Ordinal);
        Assert.Contains("localStorage", entryScript, StringComparison.Ordinal);
        Assert.Contains("CreateRoom", entryScript, StringComparison.Ordinal);
        Assert.Contains("JoinRoom", entryScript, StringComparison.Ordinal);

        Assert.Contains("showModal()", resultScript, StringComparison.Ordinal);
        Assert.Contains("word-rings-result-particle", resultScript, StringComparison.Ordinal);
        Assert.Contains("wordrings:game-ended", resultScript, StringComparison.Ordinal);
        Assert.DoesNotContain("alert(", resultScript, StringComparison.Ordinal);
        Assert.Contains("@keyframes word-rings-result-card-in", styles, StringComparison.Ordinal);
        Assert.Contains("@keyframes word-rings-result-particle", styles, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Cooperative_room_store_enforces_target_turns_and_outside_scoring()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-room-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var store = WordRingsRoomStore.Get(new TestWebHostEnvironment(root));

            Assert.Equal(
                WordRingsRoomError.InvalidTargetScore,
                Assert.Throws<WordRingsRoomException>(() =>
                    store.CreateRoom("Host", 4, partialScoreEnabled: false)).Error);
            Assert.Equal(
                WordRingsRoomError.InvalidTargetScore,
                Assert.Throws<WordRingsRoomException>(() =>
                    store.CreateRoom("Host", 16, partialScoreEnabled: false)).Error);

            var host = store.CreateRoom("Host", 10, partialScoreEnabled: false);
            var guest = store.JoinRoom(host.RoomCode, "Guest");
            var state = store.StartGame(host.RoomCode, host.PlayerToken);

            Assert.Equal("playing", state.Phase);
            Assert.Equal(2, state.Players.Count);
            Assert.Equal(state.PlayerId, state.CurrentPlayerId);
            Assert.InRange(state.BankWords.Count, 1, WordRingsRoomStore.MaximumBankWords);
            Assert.InRange(
                state.BankWords.Count + state.QueuedWords.Count,
                1,
                WordRingsRoomStore.MaximumPlayerWords);

            var hostWords = state.BankWords.Concat(state.QueuedWords).ToArray();
            Assert.Contains("дім", hostWords, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("сир", hostWords, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("панда", hostWords, StringComparer.OrdinalIgnoreCase);

            var firstOutside = store.SubmitPlacement(
                host.RoomCode,
                host.PlayerToken,
                "дім",
                string.Empty,
                5,
                90);
            Assert.True(firstOutside.IsCorrect);
            Assert.Equal(1, firstOutside.PointsAwarded);
            Assert.True(firstOutside.TurnContinues);
            Assert.Equal(firstOutside.State.PlayerId, firstOutside.State.CurrentPlayerId);

            var secondOutside = store.SubmitPlacement(
                host.RoomCode,
                host.PlayerToken,
                "сир",
                string.Empty,
                7,
                88);
            Assert.True(secondOutside.IsCorrect);
            Assert.Equal(0, secondOutside.PointsAwarded);
            Assert.True(secondOutside.TurnContinues);
            Assert.Equal(secondOutside.State.PlayerId, secondOutside.State.CurrentPlayerId);

            var wrong = store.SubmitPlacement(
                host.RoomCode,
                host.PlayerToken,
                "панда",
                string.Empty,
                9,
                86);
            Assert.False(wrong.IsCorrect);
            Assert.False(wrong.TurnContinues);
            Assert.Equal(
                guest.State.PlayerId,
                wrong.State.CurrentPlayerId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Cooperative_room_can_award_half_point_and_pass_turn()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-half-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var store = WordRingsRoomStore.Get(new TestWebHostEnvironment(root));
            var host = store.CreateRoom("Host", 10, partialScoreEnabled: true);
            var guest = store.JoinRoom(host.RoomCode, "Guest");
            var state = store.StartGame(host.RoomCode, host.PlayerToken);
            Assert.Contains("панда", state.BankWords.Concat(state.QueuedWords), StringComparer.OrdinalIgnoreCase);

            var partial = store.SubmitPlacement(
                host.RoomCode,
                host.PlayerToken,
                "панда",
                "A",
                25,
                25);

            Assert.False(partial.IsCorrect);
            Assert.True(partial.IsPartial);
            Assert.Equal(0.5, partial.PointsAwarded);
            Assert.Equal(0.5, partial.State.TeamScore);
            Assert.False(partial.TurnContinues);
            Assert.Equal(guest.State.PlayerId, partial.State.CurrentPlayerId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
