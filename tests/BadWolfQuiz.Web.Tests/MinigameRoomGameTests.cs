using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameRoomGameTests
{
    [Fact]
    public void Ten_card_game_removes_one_card_per_player_and_starts_with_eight()
    {
        var store = new MinigameRoomStore(TimeProvider.System);
        var player1 = store.CreateRoom();
        var player2 = store.JoinRoom(player1.RoomCode);
        store.StartNewGame(player1.RoomCode, player1.PlayerToken, CreateCards(10));

        var afterPlayer1 = store.ToggleExclusion(
            player1.RoomCode,
            player1.PlayerToken,
            "Card-1.png");
        Assert.Equal(MinigameRoomPhase.ChoosingExclusions, afterPlayer1.Phase);

        var afterPlayer2 = store.ToggleExclusion(
            player1.RoomCode,
            player2.PlayerToken,
            "Card-2.png");

        Assert.Equal(MinigameRoomPhase.Playing, afterPlayer2.Phase);
        Assert.Equal(8, afterPlayer2.Cards.Count);
        Assert.DoesNotContain(afterPlayer2.Cards, card => card.FileName == "Card-1.png");
        Assert.DoesNotContain(afterPlayer2.Cards, card => card.FileName == "Card-2.png");
        Assert.NotNull(afterPlayer2.MySecretCardFileName);
        Assert.Contains(
            afterPlayer2.Cards,
            card => card.FileName == afterPlayer2.MySecretCardFileName);
    }

    [Fact]
    public void Twenty_card_game_requires_two_exclusions_per_player()
    {
        var store = new MinigameRoomStore(TimeProvider.System);
        var player1 = store.CreateRoom();
        var player2 = store.JoinRoom(player1.RoomCode);
        var state = store.StartNewGame(
            player1.RoomCode,
            player1.PlayerToken,
            CreateCards(20));

        Assert.Equal(2, state.RequiredExclusionsPerPlayer);

        store.ToggleExclusion(player1.RoomCode, player1.PlayerToken, "Card-1.png");
        store.ToggleExclusion(player1.RoomCode, player1.PlayerToken, "Card-2.png");
        store.ToggleExclusion(player1.RoomCode, player2.PlayerToken, "Card-3.png");
        var started = store.ToggleExclusion(
            player1.RoomCode,
            player2.PlayerToken,
            "Card-4.png");

        Assert.Equal(MinigameRoomPhase.Playing, started.Phase);
        Assert.Equal(16, started.Cards.Count);
    }

    [Fact]
    public void Players_cannot_exclude_the_same_card()
    {
        var store = new MinigameRoomStore(TimeProvider.System);
        var player1 = store.CreateRoom();
        var player2 = store.JoinRoom(player1.RoomCode);
        store.StartNewGame(player1.RoomCode, player1.PlayerToken, CreateCards(10));
        store.ToggleExclusion(player1.RoomCode, player1.PlayerToken, "Card-1.png");

        var exception = Assert.Throws<MinigameRoomException>(() =>
            store.ToggleExclusion(
                player1.RoomCode,
                player2.PlayerToken,
                "Card-1.png"));

        Assert.Equal(MinigameRoomError.CardAlreadyExcluded, exception.Error);
    }

    [Fact]
    public void A_player_cannot_exceed_the_exclusion_limit()
    {
        var store = new MinigameRoomStore(TimeProvider.System);
        var player1 = store.CreateRoom();
        store.StartNewGame(player1.RoomCode, player1.PlayerToken, CreateCards(10));
        store.ToggleExclusion(player1.RoomCode, player1.PlayerToken, "Card-1.png");

        var exception = Assert.Throws<MinigameRoomException>(() =>
            store.ToggleExclusion(
                player1.RoomCode,
                player1.PlayerToken,
                "Card-2.png"));

        Assert.Equal(MinigameRoomError.ExclusionLimitReached, exception.Error);
    }

    [Fact]
    public void New_game_resets_previous_exclusions_and_secret_cards()
    {
        var store = new MinigameRoomStore(TimeProvider.System);
        var player1 = store.CreateRoom();
        var player2 = store.JoinRoom(player1.RoomCode);
        var first = store.StartNewGame(
            player1.RoomCode,
            player1.PlayerToken,
            CreateCards(10));
        store.ToggleExclusion(player1.RoomCode, player1.PlayerToken, "Card-1.png");
        store.ToggleExclusion(player1.RoomCode, player2.PlayerToken, "Card-2.png");

        var reset = store.StartNewGame(
            player1.RoomCode,
            player2.PlayerToken,
            CreateCards(20));

        Assert.Equal(first.GameNumber + 1, reset.GameNumber);
        Assert.Equal(MinigameRoomPhase.ChoosingExclusions, reset.Phase);
        Assert.Empty(reset.MyExcludedFiles);
        Assert.Empty(reset.OpponentExcludedFiles);
        Assert.Null(reset.MySecretCardFileName);
        Assert.Equal(20, reset.Cards.Count);
    }

    [Fact]
    public void Secret_cards_are_private_and_distinct()
    {
        var store = new MinigameRoomStore(TimeProvider.System);
        var player1 = store.CreateRoom();
        var player2 = store.JoinRoom(player1.RoomCode);
        store.StartNewGame(player1.RoomCode, player1.PlayerToken, CreateCards(10));
        store.ToggleExclusion(player1.RoomCode, player1.PlayerToken, "Card-1.png");
        store.ToggleExclusion(player1.RoomCode, player2.PlayerToken, "Card-2.png");

        var player1State = store.GetState(player1.RoomCode, player1.PlayerToken);
        var player2State = store.GetState(player1.RoomCode, player2.PlayerToken);

        Assert.NotNull(player1State.MySecretCardFileName);
        Assert.NotNull(player2State.MySecretCardFileName);
        Assert.NotEqual(
            player1State.MySecretCardFileName,
            player2State.MySecretCardFileName);
    }

    private static IReadOnlyList<MinigameCardDescriptor> CreateCards(int count) =>
        Enumerable.Range(1, count)
            .Select(index => new MinigameCardDescriptor(
                $"Card-{index}.png",
                $"Card {index}"))
            .ToArray();
}
