using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsCompetitiveRoomRegressionTests
{
    [Fact]
    public void Partial_score_is_personal_and_partial_word_moves_to_correct_region()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-partial-competitive-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var store = WordRingsRoomStore.Get(new TestWebHostEnvironment(root));
            var host = store.CreateRoom("Host", 10, partialScoreEnabled: true);
            var guest = store.JoinRoom(host.RoomCode, "Guest");
            var state = store.StartGame(host.RoomCode, host.PlayerToken);
            var word = state.BankWords
                .Concat(state.QueuedWords)
                .First(candidate => GetDefaultMembership(candidate).Length >= 2);
            var expected = GetDefaultMembership(word);
            var submittedMembership = expected[0].ToString();

            var result = store.SubmitPlacement(
                host.RoomCode,
                host.PlayerToken,
                word,
                submittedMembership,
                25,
                25);

            Assert.False(result.IsCorrect);
            Assert.True(result.IsPartial);
            Assert.Equal(0.5, result.PointsAwarded);
            Assert.Equal(0.5, result.State.PlayerScore);
            Assert.Null(result.State.WinnerPlayerId);
            Assert.Equal(0.5, Assert.Single(result.State.Players, player => player.Id == host.State.PlayerId).Score);
            Assert.Equal(0, Assert.Single(result.State.Players, player => player.Id == guest.State.PlayerId).Score);

            var placement = Assert.Single(result.State.Placements, item => item.Word == word);
            Assert.Equal(expected, placement.Membership);
            Assert.NotEqual(25, placement.X);
            Assert.NotEqual(25, placement.Y);

            var moved = store.MovePlacement(
                host.RoomCode,
                host.PlayerToken,
                placement.Id,
                expected,
                52.5,
                44.5);
            var movedPlacement = Assert.Single(moved.Placements, item => item.Id == placement.Id);
            Assert.Equal(52.5, movedPlacement.X);
            Assert.Equal(44.5, movedPlacement.Y);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Incorrect_word_moves_to_correct_region_even_without_partial_scoring()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-wrong-autocorrect-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var store = WordRingsRoomStore.Get(new TestWebHostEnvironment(root));
            var host = store.CreateRoom("Host", 10, partialScoreEnabled: false);
            _ = store.JoinRoom(host.RoomCode, "Guest");
            var state = store.StartGame(host.RoomCode, host.PlayerToken);
            var word = state.BankWords.Concat(state.QueuedWords).First(candidate => GetDefaultMembership(candidate).Length > 0);
            var expected = GetDefaultMembership(word);

            var result = store.SubmitPlacement(host.RoomCode, host.PlayerToken, word, string.Empty, 91, 91);

            Assert.False(result.IsCorrect);
            Assert.False(result.IsPartial);
            Assert.Equal(0, result.PointsAwarded);
            var placement = Assert.Single(result.State.Placements, item => item.Word == word);
            Assert.Equal(expected, placement.Membership);
            Assert.NotEqual(91, placement.X);
            Assert.NotEqual(91, placement.Y);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void First_player_to_target_is_only_winner_and_host_can_restart_same_room()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-winner-restart-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var store = WordRingsRoomStore.Get(new TestWebHostEnvironment(root));
            var host = store.CreateRoom("Host", 5, partialScoreEnabled: false);
            var guest = store.JoinRoom(host.RoomCode, "Guest");
            var state = store.StartGame(host.RoomCode, host.PlayerToken);
            var scoringWords = state.BankWords
                .Concat(state.QueuedWords)
                .Where(word => GetDefaultMembership(word).Length > 0)
                .Take(5)
                .ToArray();
            Assert.Equal(5, scoringWords.Length);

            WordRingsRoomPlacementResult? last = null;
            foreach (var word in scoringWords)
            {
                last = store.SubmitPlacement(
                    host.RoomCode,
                    host.PlayerToken,
                    word,
                    GetDefaultMembership(word),
                    50,
                    43);
            }

            Assert.NotNull(last);
            Assert.Equal("finished", last!.State.Phase);
            Assert.Equal("won", last.State.Outcome);
            Assert.Equal(last.State.PlayerId, last.State.WinnerPlayerId);
            Assert.Equal(5, last.State.PlayerScore);
            Assert.Equal(5, Assert.Single(last.State.Players, player => player.Id == host.State.PlayerId).Score);
            Assert.Equal(0, Assert.Single(last.State.Players, player => player.Id == guest.State.PlayerId).Score);

            var guestFinished = store.GetState(host.RoomCode, guest.PlayerToken);
            Assert.Equal("finished", guestFinished.Phase);
            Assert.Equal("lost", guestFinished.Outcome);
            Assert.Equal(0, guestFinished.PlayerScore);
            Assert.Equal(host.State.PlayerId, guestFinished.WinnerPlayerId);

            var checkedPlacement = last.State.Placements.Last();
            var movedAfterFinish = store.MovePlacement(
                host.RoomCode,
                host.PlayerToken,
                checkedPlacement.Id,
                checkedPlacement.Membership,
                55,
                45);
            Assert.Equal(55, Assert.Single(movedAfterFinish.Placements, item => item.Id == checkedPlacement.Id).X);

            var restarted = store.StartGame(host.RoomCode, host.PlayerToken);
            Assert.Equal("playing", restarted.Phase);
            Assert.Equal("none", restarted.Outcome);
            Assert.Null(restarted.WinnerPlayerId);
            Assert.Equal(0, restarted.PlayerScore);
            Assert.All(restarted.Players, player => Assert.Equal(0, player.Score));
            Assert.Empty(restarted.Placements);

            var guestRestarted = store.GetState(host.RoomCode, guest.PlayerToken);
            Assert.Equal("playing", guestRestarted.Phase);
            Assert.Null(guestRestarted.WinnerPlayerId);
            Assert.Equal(0, guestRestarted.PlayerScore);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Multiplayer_client_uses_personal_score_and_keeps_finished_room_restartable()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-coop.js");
        var roomText = ReadWebFile("Localization", "WordRingsRoomText.cs");
        var page = ReadWebFile("Pages", "WordRings.cshtml");
        var pointerDrag = ReadWebFile("wwwroot", "js", "word-rings-pointer-drag.js");
        var refinements = ReadWebFile("wwwroot", "css", "word-rings-refinements.css");

        Assert.Contains("nextState.playerScore", script, StringComparison.Ordinal);
        Assert.Contains("state?.phase === 'finished'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("startButton.hidden = state?.isHost !== true || state?.phase === 'playing';", script, StringComparison.Ordinal);
        Assert.DoesNotContain("nextState.teamScore", script, StringComparison.Ordinal);
        Assert.DoesNotContain("state?.phase === 'finished' ||\n            root.querySelector", script, StringComparison.Ordinal);
        Assert.Contains("Мультиплеєр", roomText, StringComparison.Ordinal);
        Assert.Contains("Ви першим набрали {0} балів. Ціль: {1}.", roomText, StringComparison.Ordinal);
        Assert.DoesNotContain("Команда набрала", roomText, StringComparison.Ordinal);
        Assert.Contains("token.dataset.word = placement.word;", script, StringComparison.Ordinal);
        Assert.Contains("livePlacementIds", script, StringComparison.Ordinal);
        Assert.Contains("findBestAutomaticPlacement", script, StringComparison.Ordinal);
        Assert.Contains("repositionIncorrectPlacementIfNeeded", script, StringComparison.Ordinal);
        Assert.DoesNotContain("words.textContent = `${player.remainingWords}`", script, StringComparison.Ordinal);
        Assert.Contains("navigator.clipboard.writeText(roomCode)", script, StringComparison.Ordinal);
        Assert.Contains("navigator.clipboard.writeText(url.toString())", script, StringComparison.Ordinal);
        Assert.Contains("data-copy-room-link", page, StringComparison.Ordinal);
        Assert.Contains("data-toggle-room-code", page, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: minmax(220px, 1fr) minmax(0, 980px) minmax(230px, 1fr);", refinements, StringComparison.Ordinal);
        Assert.Contains("width: min(100%, 280px);", refinements, StringComparison.Ordinal);
        Assert.Contains("width: min(100%, 300px);", refinements, StringComparison.Ordinal);
        Assert.Contains("border: 1px solid var(--line);", refinements, StringComparison.Ordinal);
        Assert.Contains("box-shadow: none;", refinements, StringComparison.Ordinal);
        Assert.Contains("••••••", page, StringComparison.Ordinal);
        Assert.True(
            pointerDrag.IndexOf("bringToFront(word);", StringComparison.Ordinal) <
            pointerDrag.IndexOf("if (!canBegin(value, word)) return;", StringComparison.Ordinal));
    }

    private static string GetDefaultMembership(string word)
    {
        var blue = new HashSet<string>(
            ["кіт", "вовк", "жаба", "собака", "песик", "олень", "панда", "коала"],
            StringComparer.OrdinalIgnoreCase);
        var yellow = new HashSet<string>(
            ["ракета", "машина", "жаба", "собака", "лампа", "банка", "панда", "коала"],
            StringComparer.OrdinalIgnoreCase);
        var red = new HashSet<string>(
            ["лісок", "човен", "песик", "олень", "лампа", "банка", "панда", "коала"],
            StringComparer.OrdinalIgnoreCase);
        return string.Concat(
            blue.Contains(word) ? "A" : string.Empty,
            yellow.Contains(word) ? "B" : string.Empty,
            red.Contains(word) ? "C" : string.Empty);
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
