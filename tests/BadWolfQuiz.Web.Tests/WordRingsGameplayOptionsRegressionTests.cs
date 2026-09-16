using System.Collections;
using System.Reflection;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsGameplayOptionsRegressionTests
{
    [Fact]
    public void Hand_size_shrinks_with_remaining_score_and_empty_hand_wins()
    {
        var root = CreateRoot();
        try
        {
            var environment = new TestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var gameplay = WordRingsGameplayOptionsCoordinator.Get(environment);
            var store = WordRingsRoomStore.Get(environment);

            var host = rooms.CreateRoom("Host", 5, partialScoreEnabled: true, hostChoosesRules: false);
            _ = rooms.JoinRoom(host.RoomCode, "Guest");
            _ = gameplay.ConfigureRoom(host.RoomCode, host.PlayerToken, targetScore: 5, handSize: 5);
            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);
            _ = gameplay.PrepareRound(host.RoomCode, host.PlayerToken);

            SetPlayerScore(store, host.RoomCode, host.State.PlayerId, 3.5);
            Assert.Equal(2, gameplay.GetEffectiveHandSize(host.RoomCode, host.State.PlayerId));
            Assert.Equal(2, gameplay.GetDecoratedRoomState(host.RoomCode, host.PlayerToken).BankWords.Count);
            SetPlayerScore(store, host.RoomCode, host.State.PlayerId, 4.5);
            Assert.Equal(1, gameplay.GetEffectiveHandSize(host.RoomCode, host.State.PlayerId));
            Assert.Single(gameplay.GetDecoratedRoomState(host.RoomCode, host.PlayerToken).BankWords);

            ClearPlayerWords(store, host.RoomCode, host.State.PlayerId);
            var finished = gameplay.FinalizePlayerExhaustion(host.RoomCode, host.PlayerToken, host.State.PlayerId);
            Assert.Equal("finished", finished.Phase);
            Assert.Equal("won", finished.Outcome);
            Assert.Equal(host.State.PlayerId, finished.WinnerPlayerId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Exhausted_hand_overrides_the_core_terminal_loss()
    {
        var root = CreateRoot();
        try
        {
            var environment = new TestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var gameplay = WordRingsGameplayOptionsCoordinator.Get(environment);
            var store = WordRingsRoomStore.Get(environment);
            var exhaustion = new WordRingsExhaustionWinOverride(environment);

            var host = rooms.CreateRoom("Host", 5, partialScoreEnabled: false, hostChoosesRules: false);
            _ = rooms.JoinRoom(host.RoomCode, "Guest");
            _ = gameplay.ConfigureRoom(host.RoomCode, host.PlayerToken, 5, 5);
            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);

            ClearPlayerWords(store, host.RoomCode, host.State.PlayerId);
            SetRoomTerminalLoss(store, host.RoomCode);
            exhaustion.ApplyForPlayer(host.RoomCode, host.PlayerToken, host.State.PlayerId);

            var state = rooms.GetRoomState(host.RoomCode, host.PlayerToken);
            Assert.Equal("finished", state.Phase);
            Assert.Equal("won", state.Outcome);
            Assert.Equal(host.State.PlayerId, state.WinnerPlayerId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Gameplay_patch_exposes_five_word_defaults_settings_and_first_turn_roulette()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-gameplay-options.js");
        var css = ReadWebFile("wwwroot", "css", "word-rings-gameplay-options.css");
        var service = ReadWebFile("Services", "WordRingsGameplayOptionsCoordinator.cs");
        var exhaustion = ReadWebFile("Services", "WordRingsExhaustionWinOverride.cs");
        var api = ReadWebFile("Pages", "WordRingsGameplayOptionsApi.cshtml.cs");
        var tagHelper = ReadWebFile("TagHelpers", "WordRingsActionCardsAssetsTagHelper.cs");

        Assert.Contains("targetInput.value = '5';", script, StringComparison.Ordinal);
        Assert.Contains("handInput.value = '5';", script, StringComparison.Ordinal);
        Assert.Contains("data-create-room-hand-size", script, StringComparison.Ordinal);
        Assert.Contains("badwolf.wordrings.gameplay-options", script, StringComparison.Ordinal);
        Assert.Contains("Math.ceil(remaining)", script, StringComparison.Ordinal);
        Assert.Contains("FinalizePlayerExhaustion", script, StringComparison.Ordinal);
        Assert.Contains("FinalizePlacementExhaustion", script, StringComparison.Ordinal);
        Assert.Contains("is-first-turn-roulette", script, StringComparison.Ordinal);
        Assert.Contains("rouletteTick", script, StringComparison.Ordinal);
        Assert.Contains("190 * Math.pow", script, StringComparison.Ordinal);
        Assert.Contains("dedicatedHostMode === true && item.player.isHost === true", script, StringComparison.Ordinal);
        Assert.Contains("patchHandler === 'EnsureTargetWords'", script, StringComparison.Ordinal);
        Assert.Contains("CaptureWordCounts", script, StringComparison.Ordinal);
        Assert.Contains("RestoreWordCounts", script, StringComparison.Ordinal);

        Assert.Contains("MinimumHandSize = 5", service, StringComparison.Ordinal);
        Assert.Contains("MaximumHandSize = 10", service, StringComparison.Ordinal);
        Assert.Contains("Math.Ceiling(remaining)", service, StringComparison.Ordinal);
        Assert.Contains("dedicatedHostMode && (bool)Get(player, \"IsHost\")!", service, StringComparison.Ordinal);
        Assert.Contains("WinnerPlayerId", service, StringComparison.Ordinal);
        Assert.Contains("string.Equals(phase, \"Finished\"", exhaustion, StringComparison.Ordinal);
        Assert.Contains("string.Equals(outcome, \"Lost\"", exhaustion, StringComparison.Ordinal);
        Assert.Contains("Exhaustion.ApplyForPlayer", api, StringComparison.Ordinal);
        Assert.Contains("Exhaustion.ApplyForPlacement", api, StringComparison.Ordinal);

        Assert.Contains("word-rings-first-turn-winner", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(3", css, StringComparison.Ordinal);
        Assert.Contains("word-rings-gameplay-options.js?v=1", tagHelper, StringComparison.Ordinal);
        Assert.True(
            tagHelper.IndexOf("word-rings-gameplay-options.js?v=1", StringComparison.Ordinal) <
            tagHelper.IndexOf("word-rings-action-cards-patch.js?v=2", StringComparison.Ordinal));
    }

    private static void SetPlayerScore(WordRingsRoomStore store, string roomCode, Guid playerId, double score)
    {
        lock (StoreSync(store))
        {
            var player = FindPlayer(store, roomCode, playerId);
            Set(player, "Score", score);
        }
    }

    private static void ClearPlayerWords(WordRingsRoomStore store, string roomCode, Guid playerId)
    {
        lock (StoreSync(store))
        {
            var player = FindPlayer(store, roomCode, playerId);
            ((IList)Get(player, "RemainingWords")!).Clear();
        }
    }

    private static void SetRoomTerminalLoss(WordRingsRoomStore store, string roomCode)
    {
        lock (StoreSync(store))
        {
            var room = FindRoom(store, roomCode);
            SetEnum(room, "Phase", "Finished");
            SetEnum(room, "Outcome", "Lost");
            Set(room, "WinnerPlayerId", null);
        }
    }

    private static object FindPlayer(WordRingsRoomStore store, string roomCode, Guid playerId)
    {
        var room = FindRoom(store, roomCode);
        var players = (IList)Get(room, "Players")!;
        return players.Cast<object>().Single(player => (Guid)Get(player, "Id")! == playerId);
    }

    private static object FindRoom(WordRingsRoomStore store, string roomCode)
    {
        var roomsField = typeof(WordRingsRoomStore).GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rooms = (IDictionary)roomsField.GetValue(store)!;
        return rooms[roomCode]!;
    }

    private static object StoreSync(WordRingsRoomStore store) =>
        typeof(WordRingsRoomStore).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;

    private static object? Get(object instance, string property) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(instance);

    private static void Set(object instance, string property, object? value) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(instance, value);

    private static void SetEnum(object instance, string property, string value)
    {
        var propertyInfo = instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        propertyInfo.SetValue(instance, Enum.Parse(propertyInfo.PropertyType, value));
    }

    private static string ReadWebFile(params string[] pathParts) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "BadWolfQuiz.Web", Path.Combine(pathParts)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("BadWolfQuiz repository root was not found.");
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-gameplay-options-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
