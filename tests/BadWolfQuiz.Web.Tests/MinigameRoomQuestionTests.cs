using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameRoomQuestionTests
{
    private static readonly string[] Questions = Enumerable
        .Range(1, 12)
        .Select(index => $"Question {index}?")
        .ToArray();

    [Fact]
    public void Question_mode_gives_each_player_three_private_available_questions()
    {
        var (store, player1, player2, _) = CreatePlayingRoom(questionCardsEnabled: true);

        var state1 = store.GetState(player1.RoomCode, player1.PlayerToken);
        var state2 = store.GetState(player1.RoomCode, player2.PlayerToken);

        Assert.True(state1.QuestionCardsEnabled);
        Assert.True(state2.QuestionCardsEnabled);
        Assert.Equal(3, state1.MyAvailableQuestions.Count);
        Assert.Equal(3, state2.MyAvailableQuestions.Count);
        Assert.All(state1.MyAvailableQuestions, question => Assert.Contains(question, Questions));
        Assert.All(state2.MyAvailableQuestions, question => Assert.Contains(question, Questions));
        Assert.Empty(state1.QuestionHistory);
        Assert.False(state1.HasSelectedQuestionThisTurn);
    }

    [Fact]
    public void Selecting_question_records_shared_history_and_replaces_that_option()
    {
        var (store, player1, player2, _) = CreatePlayingRoom(questionCardsEnabled: true);
        var before = store.GetState(player1.RoomCode, player1.PlayerToken);
        var selected = before.MyAvailableQuestions[1];

        var after = store.SelectQuestion(player1.RoomCode, player1.PlayerToken, 1);
        var opponent = store.GetState(player1.RoomCode, player2.PlayerToken);

        Assert.True(after.HasSelectedQuestionThisTurn);
        Assert.Equal(3, after.MyAvailableQuestions.Count);
        Assert.DoesNotContain(selected, after.MyAvailableQuestions);
        var history = Assert.Single(after.QuestionHistory);
        Assert.Equal(1, history.PlayerNumber);
        Assert.Equal(selected, history.Question);
        Assert.Equal(after.QuestionHistory, opponent.QuestionHistory);
    }

    [Fact]
    public void Only_one_question_can_be_selected_per_turn()
    {
        var (store, player1, _, _) = CreatePlayingRoom(questionCardsEnabled: true);
        store.SelectQuestion(player1.RoomCode, player1.PlayerToken, 0);

        var error = Assert.Throws<MinigameRoomException>(() =>
            store.SelectQuestion(player1.RoomCode, player1.PlayerToken, 0));

        Assert.Equal(MinigameRoomError.QuestionAlreadySelected, error.Error);
    }

    [Fact]
    public void Ending_turn_or_guessing_requires_question_in_question_mode()
    {
        var (store, player1, player2, _) = CreatePlayingRoom(questionCardsEnabled: true);
        var target = store.GetState(player1.RoomCode, player2.PlayerToken).MySecretCardFileName!;

        var endTurn = Assert.Throws<MinigameRoomException>(() =>
            store.EndTurn(player1.RoomCode, player1.PlayerToken));
        Assert.Equal(MinigameRoomError.QuestionRequired, endTurn.Error);

        var guess = Assert.Throws<MinigameRoomException>(() =>
            store.SubmitGuess(player1.RoomCode, player1.PlayerToken, target));
        Assert.Equal(MinigameRoomError.QuestionRequired, guess.Error);

        store.SelectQuestion(player1.RoomCode, player1.PlayerToken, 0);
        var next = store.EndTurn(player1.RoomCode, player1.PlayerToken);
        Assert.Equal(2, next.CurrentPlayerNumber);
        Assert.False(next.HasSelectedQuestionThisTurn);
    }

    [Fact]
    public void Timeout_without_question_records_no_selection_and_passes_turn()
    {
        var (store, player1, _, time) = CreatePlayingRoom(questionCardsEnabled: true);
        time.Advance(MinigameRoomStore.FirstTurnDuration);

        var state = store.ExpireTurn(player1.RoomCode, player1.PlayerToken);

        Assert.Equal(2, state.CurrentPlayerNumber);
        var history = Assert.Single(state.QuestionHistory);
        Assert.Equal(1, history.PlayerNumber);
        Assert.Null(history.Question);
        Assert.False(state.HasSelectedQuestionThisTurn);
    }

    [Fact]
    public void Question_mode_off_preserves_current_turn_behavior()
    {
        var (store, player1, _, _) = CreatePlayingRoom(questionCardsEnabled: false);
        var state = store.GetState(player1.RoomCode, player1.PlayerToken);

        Assert.False(state.QuestionCardsEnabled);
        Assert.Empty(state.MyAvailableQuestions);
        Assert.Empty(state.QuestionHistory);
        var next = store.EndTurn(player1.RoomCode, player1.PlayerToken);
        Assert.Equal(2, next.CurrentPlayerNumber);
    }

    [Fact]
    public void Question_mode_requires_at_least_three_questions()
    {
        var time = new ManualTimeProvider();
        var store = new MinigameRoomStore(time);
        var player1 = store.CreateRoom();
        store.JoinRoom(player1.RoomCode);

        var error = Assert.Throws<MinigameRoomException>(() =>
            store.StartNewGame(
                player1.RoomCode,
                player1.PlayerToken,
                CreateCards(10),
                questionCardsEnabled: true,
                ["One?", "Two?"]));

        Assert.Equal(MinigameRoomError.QuestionsUnavailable, error.Error);
    }

    private static (
        MinigameRoomStore Store,
        MinigameRoomConnection Player1,
        MinigameRoomConnection Player2,
        ManualTimeProvider Time) CreatePlayingRoom(bool questionCardsEnabled)
    {
        var time = new ManualTimeProvider();
        var store = new MinigameRoomStore(time);
        var player1 = store.CreateRoom();
        var player2 = store.JoinRoom(player1.RoomCode);
        store.StartNewGame(
            player1.RoomCode,
            player1.PlayerToken,
            CreateCards(10),
            questionCardsEnabled,
            questionCardsEnabled ? Questions : null);
        store.ToggleExclusion(player1.RoomCode, player1.PlayerToken, "Card-1.png");
        store.ToggleExclusion(player1.RoomCode, player2.PlayerToken, "Card-2.png");
        return (store, player1, player2, time);
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
