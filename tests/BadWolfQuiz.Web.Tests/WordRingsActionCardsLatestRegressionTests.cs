using System.Collections;
using System.Reflection;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsActionCardsLatestRegressionTests
{
    [Fact]
    public void Swap_keeps_a_stolen_blocked_word_blocked_for_the_new_owner()
    {
        var root = CreateRoot();
        try
        {
            var environment = new LatestTestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var cards = WordRingsActionCardCoordinator.Get(environment);
            var patch = WordRingsActionCardPatchCoordinator.Get(environment);
            var runtime = WordRingsActionCardRuntimePatch.Get(environment);
            var store = WordRingsRoomStore.Get(environment);

            var host = rooms.CreateRoom("Host", 15, partialScoreEnabled: false, hostChoosesRules: false);
            var guest = rooms.JoinRoom(host.RoomCode, "Guest");
            cards.RegisterRoom(host, enabled: true, correctWordsPerCard: 2, maximumCards: 4);
            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);
            cards.BeginRound(host.RoomCode, host.PlayerToken);
            TrimPlayerWords(store, host.RoomCode, guest.State.PlayerId, 2);

            patch.GrantDebugCard(host.RoomCode, host.PlayerToken, host.State.PlayerId, (int)WordRingsActionCardKind.Block);
            patch.GrantDebugCard(host.RoomCode, host.PlayerToken, host.State.PlayerId, (int)WordRingsActionCardKind.Swap);
            _ = cards.UseCard(
                host.RoomCode,
                host.PlayerToken,
                (int)WordRingsActionCardKind.Block,
                guest.State.PlayerId,
                null);
            Assert.Equal(2, cards.GetState(host.RoomCode, guest.PlayerToken).BlockedWords.Count);

            var beforeRoom = rooms.GetRoomState(host.RoomCode, host.PlayerToken);
            var actorBefore = cards.GetState(host.RoomCode, host.PlayerToken);
            var targetBefore = cards.GetState(host.RoomCode, guest.PlayerToken);
            var actorWord = actorBefore.Words.First(word =>
                !targetBefore.Words.Contains(word, StringComparer.OrdinalIgnoreCase));
            var capture = runtime.CaptureSwapBlockTransfer(beforeRoom, (int)WordRingsActionCardKind.Swap);
            var result = cards.UseCard(
                host.RoomCode,
                host.PlayerToken,
                (int)WordRingsActionCardKind.Swap,
                guest.State.PlayerId,
                actorWord);

            Assert.True(runtime.CompleteSwapBlockTransfer(
                host.RoomCode,
                guest.State.PlayerId,
                result.TargetName,
                capture));

            var after = cards.GetState(host.RoomCode, host.PlayerToken);
            var incoming = after.Words.First(word =>
                !actorBefore.Words.Contains(word, StringComparer.OrdinalIgnoreCase));
            Assert.Contains(incoming, after.BlockedWords, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Swap_auto_unblocks_a_transferred_block_if_it_is_the_receivers_only_word()
    {
        var root = CreateRoot();
        try
        {
            var environment = new LatestTestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var cards = WordRingsActionCardCoordinator.Get(environment);
            var patch = WordRingsActionCardPatchCoordinator.Get(environment);
            var runtime = WordRingsActionCardRuntimePatch.Get(environment);
            var store = WordRingsRoomStore.Get(environment);

            var host = rooms.CreateRoom("Host", 15, partialScoreEnabled: false, hostChoosesRules: false);
            var guest = rooms.JoinRoom(host.RoomCode, "Guest");
            cards.RegisterRoom(host, enabled: true, correctWordsPerCard: 2, maximumCards: 4);
            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);
            cards.BeginRound(host.RoomCode, host.PlayerToken);
            TrimPlayerWords(store, host.RoomCode, host.State.PlayerId, 1);
            TrimPlayerWords(store, host.RoomCode, guest.State.PlayerId, 2);

            patch.GrantDebugCard(host.RoomCode, host.PlayerToken, host.State.PlayerId, (int)WordRingsActionCardKind.Block);
            patch.GrantDebugCard(host.RoomCode, host.PlayerToken, host.State.PlayerId, (int)WordRingsActionCardKind.Swap);
            _ = cards.UseCard(
                host.RoomCode,
                host.PlayerToken,
                (int)WordRingsActionCardKind.Block,
                guest.State.PlayerId,
                null);

            var beforeRoom = rooms.GetRoomState(host.RoomCode, host.PlayerToken);
            var actorBefore = cards.GetState(host.RoomCode, host.PlayerToken);
            var capture = runtime.CaptureSwapBlockTransfer(beforeRoom, (int)WordRingsActionCardKind.Swap);
            var result = cards.UseCard(
                host.RoomCode,
                host.PlayerToken,
                (int)WordRingsActionCardKind.Swap,
                guest.State.PlayerId,
                actorBefore.Words[0]);

            _ = runtime.CompleteSwapBlockTransfer(
                host.RoomCode,
                guest.State.PlayerId,
                result.TargetName,
                capture);

            var after = cards.GetState(host.RoomCode, host.PlayerToken);
            Assert.Single(after.Words);
            Assert.Empty(after.BlockedWords);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Immunity_no_keeps_the_host_moved_word_but_awards_zero_points_even_for_partial_overlap()
    {
        var root = CreateRoot();
        try
        {
            var environment = new LatestTestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var cards = WordRingsActionCardCoordinator.Get(environment);
            var patch = WordRingsActionCardPatchCoordinator.Get(environment);
            var runtime = WordRingsActionCardRuntimePatch.Get(environment);
            var store = WordRingsRoomStore.Get(environment);

            var host = rooms.CreateRoom("Host", 15, partialScoreEnabled: true, hostChoosesRules: false);
            var guest = rooms.JoinRoom(host.RoomCode, "Guest");
            cards.RegisterRoom(host, enabled: true, correctWordsPerCard: 2, maximumCards: 4);
            var started = rooms.StartGame(host.RoomCode, host.PlayerToken);
            cards.BeginRound(host.RoomCode, host.PlayerToken);

            var word = started.BankWords[0];
            var submitted = store.SubmitHostedPlacement(host.RoomCode, host.PlayerToken, word, "AB", 50, 50);
            var pending = Assert.Single(submitted.State.Placements, item => item.IsPending);
            _ = store.MoveHostedPlacement(host.RoomCode, host.PlayerToken, pending.Id, "A", 40, 40);
            const long decisionId = 77;
            InjectPendingImmunityDecision(
                patch,
                host.RoomCode,
                host.State.PlayerId,
                decisionId,
                pending.Id,
                host.State.PlayerId,
                "Host",
                word,
                host.PlayerToken);

            var resolution = runtime.ResolveImmunityDecisionWithoutScore(
                patch,
                host.RoomCode,
                host.PlayerToken,
                decisionId);

            Assert.NotNull(resolution.PlacementResult);
            Assert.False(resolution.PlacementResult!.IsCorrect);
            Assert.True(resolution.PlacementResult.IsPartial);
            Assert.Equal(0, resolution.PlacementResult.PointsAwarded);

            var state = rooms.GetRoomState(host.RoomCode, host.PlayerToken);
            Assert.Equal(0, state.PlayerScore);
            var resolved = Assert.Single(state.Placements, item => item.Id == pending.Id);
            Assert.False(resolved.IsPending);
            Assert.Equal(0, resolved.PointsAwarded);
            Assert.Equal(guest.State.PlayerId, state.CurrentPlayerId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Latest_layout_patch_centers_the_stage_instead_of_bottom_aligning_it()
    {
        var css = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "word-rings-action-cards-patch.css"));

        Assert.Contains(".word-rings-stage {\n    align-self: center;\n}", css, StringComparison.Ordinal);
    }

    private static void InjectPendingImmunityDecision(
        WordRingsActionCardPatchCoordinator patch,
        string roomCode,
        Guid ownerPlayerId,
        long decisionId,
        long placementId,
        Guid placementPlayerId,
        string playerName,
        string word,
        string hostToken)
    {
        _ = patch.GetState(roomCode, ownerPlayerId);
        var syncField = typeof(WordRingsActionCardPatchCoordinator)
            .GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var roomsField = typeof(WordRingsActionCardPatchCoordinator)
            .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pendingType = typeof(WordRingsActionCardPatchCoordinator)
            .GetNestedType("PendingImmunityDecision", BindingFlags.NonPublic)!;
        var pending = Activator.CreateInstance(
            pendingType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [decisionId, placementId, placementPlayerId, playerName, word, hostToken],
            culture: null)!;

        lock (syncField.GetValue(patch)!)
        {
            var rooms = (IDictionary)roomsField.GetValue(patch)!;
            var meta = rooms[roomCode]!;
            var decisions = (IDictionary)Get(meta, "PendingImmunityDecisions")!;
            decisions[ownerPlayerId] = pending;
        }
    }

    private static void TrimPlayerWords(WordRingsRoomStore store, string roomCode, Guid playerId, int count)
    {
        var syncField = typeof(WordRingsRoomStore).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var roomsField = typeof(WordRingsRoomStore).GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;
        lock (syncField.GetValue(store)!)
        {
            var rooms = (IDictionary)roomsField.GetValue(store)!;
            var room = rooms[roomCode]!;
            var players = (IList)Get(room, "Players")!;
            var player = players.Cast<object>().Single(item => (Guid)Get(item, "Id")! == playerId);
            var words = (IList)Get(player, "RemainingWords")!;
            while (words.Count > count) words.RemoveAt(words.Count - 1);
        }
    }

    private static object? Get(object instance, string property) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(instance);

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-latest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

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

    private sealed class LatestTestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
