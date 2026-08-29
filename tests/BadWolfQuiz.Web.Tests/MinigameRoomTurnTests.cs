using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameRoomTurnTests
{
    [Fact]
    public void Each_players_first_turn_is_three_minutes_then_turns_are_ninety_seconds()
    {
        var time = new ManualTimeProvider();
        var (store, player1, player2) = CreatePlayingRoom(time);

        var first = store.GetState(player1.RoomCode, player1.PlayerToken);
        Assert.Equal(1, first.CurrentPlayerNumber);
        Assert.Equal(time.GetUtcNow() + TimeSpan.FromMinutes(3), first.TurnDeadlineUtc);

        var player2Turn = store.EndTurn(player1.RoomCode, player1.PlayerToken);
        Assert.Equal(2, player2Turn.CurrentPlayerNumber);
        Assert.Equal(time.GetUtcNow() + TimeSpan.FromMinutes(3), player2Turn.TurnDeadlineUtc);

        var player1SecondTurn = store.EndTurn(player1.RoomCode, player2.PlayerToken);
        Assert.Equal(1, player1SecondTurn.CurrentPlayerNumber);
        Assert.Equal(time.GetUtcNow() + TimeSpan.FromSeconds(90), player1SecondTurn.TurnDeadlineUtc);
    }

    [Fact]
    public void Expired_turn_is_passed_to_the_other_player()
    {
        var time = new ManualTimeProvider();
        var (store, player1, player2) = CreatePlayingRoom(time);

        time.Advance(TimeSpan.FromMinutes(3));
        var state = store.ExpireTurn(player1.RoomCode, player2.PlayerToken);

        Assert.Equal(2, state.CurrentPlayerNumber);
        Assert.Equal(time.GetUtcNow() + TimeSpan.FromMinutes(3), state.TurnDeadlineUtc);
    }

    [Fact]
    public void Wrong_guess_ends_the_turn_immediately()
    {
        var time = new ManualTimeProvider();
        var (store, player1, player2) = CreatePlayingRoom(time);
        var player1State = store.GetState(player1.RoomCode, player1.PlayerToken);
        var player2State = store.GetState(player1.RoomCode, player2.PlayerToken);
        var wrong = player1State.Cards.First(card =>
            card.FileName != player2State.MySecretCardFileName);

        var result = store.SubmitGuess(
            player1.RoomCode,
            player1.PlayerToken,
            wrong.FileName);

        Assert.Equal(MinigameRoomPhase.Playing, result.Phase);
        Assert.Equal(2, result.CurrentPlayerNumber);
        Assert.Null(result.WinnerPlayerNumber);
    }

    [Fact]
    public void Correct_guess_finishes_the_game_with_the_current_player_as_winner()
    {
        var time = new ManualTimeProvider();
        var (store, player1, player2) = CreatePlayingRoom(time);
        var target = store
            .GetState(player1.RoomCode, player2.PlayerToken)
            .MySecretCardFileName;
        Assert.NotNull(target);

        var result = store.SubmitGuess(
            player1.RoomCode,
            player1.PlayerToken,
            target);

        Assert.Equal(MinigameRoomPhase.Finished, result.Phase);
        Assert.Equal(1, result.WinnerPlayerNumber);
        Assert.Null(result.TurnDeadlineUtc);
    }

    [Fact]
    public void Only_current_player_can_end_turn_or_submit_a_guess()
    {
        var time = new ManualTimeProvider();
        var (store, player1, player2) = CreatePlayingRoom(time);
        var card = store.GetState(player1.RoomCode, player2.PlayerToken).Cards[0];

        var endTurn = Assert.Throws<MinigameRoomException>(() =>
            store.EndTurn(player1.RoomCode, player2.PlayerToken));
        Assert.Equal(MinigameRoomError.NotYourTurn, endTurn.Error);

        var guess = Assert.Throws<MinigameRoomException>(() =>
            store.SubmitGuess(
                player1.RoomCode,
                player2.PlayerToken,
                card.FileName));
        Assert.Equal(MinigameRoomError.NotYourTurn, guess.Error);
    }

    [Fact]
    public void New_game_resets_turn_and_winner_state()
    {
        var time = new ManualTimeProvider();
        var (store, player1, player2) = CreatePlayingRoom(time);
        var target = store
            .GetState(player1.RoomCode, player2.PlayerToken)
            .MySecretCardFileName!;
        store.SubmitGuess(player1.RoomCode, player1.PlayerToken, target);

        var reset = store.StartNewGame(
            player1.RoomCode,
            player2.PlayerToken,
            CreateCards(20));

        Assert.Equal(MinigameRoomPhase.ChoosingExclusions, reset.Phase);
        Assert.Null(reset.CurrentPlayerNumber);
        Assert.Null(reset.TurnDeadlineUtc);
        Assert.Null(reset.WinnerPlayerNumber);
    }

    private static (
        MinigameRoomStore Store,
        MinigameRoomConnection Player1,
        MinigameRoomConnection Player2) CreatePlayingRoom(ManualTimeProvider time)
    {
        var store = new MinigameRoomStore(time);
        var player1 = store.CreateRoom();
        var player2 = store.JoinRoom(player1.RoomCode);
        store.StartNewGame(player1.RoomCode, player1.PlayerToken, CreateCards(10));
        store.ToggleExclusion(player1.RoomCode, player1.PlayerToken, "Card-1.png");
        store.ToggleExclusion(player1.RoomCode, player2.PlayerToken, "Card-2.png");
        return (store, player1, player2);
    }

    private static IReadOnlyList<MinigameCardDescriptor> CreateCards(int count) =>
        Enumerable.Range(1, count)
            .Select(index => new MinigameCardDescriptor(
                $"Card-{index}.png",
                $"Card {index}"))
            .ToArray();

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow =
            new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
