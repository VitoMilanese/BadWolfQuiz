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
        Assert.Equal(MinigameQuestionHistoryKind.Question, history.Kind);
        Assert.Equal(selected, history.Value);
        Assert.Null(history.IsCorrect);
        Assert.Equal(selected, after.PendingQuestion);
        Assert.Equal(2, after.PendingQuestionResponsePlayerNumber);
        Assert.Equal(after.QuestionHistory, opponent.QuestionHistory);
        Assert.Equal(selected, opponent.PendingQuestion);
        Assert.Equal(2, opponent.PendingQuestionResponsePlayerNumber);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Asked_player_answer_is_recorded_in_shared_history(bool answerYes)
    {
        var (store, player1, player2, _) = CreatePlayingRoom(questionCardsEnabled: true);
        var selected = store
            .GetState(player1.RoomCode, player1.PlayerToken)
            .MyAvailableQuestions[0];
        store.SelectQuestion(player1.RoomCode, player1.PlayerToken, 0);

        var answered = store.SubmitQuestionResponse(
            player1.RoomCode,
            player2.PlayerToken,
            answerYes);
        var asker = store.GetState(player1.RoomCode, player1.PlayerToken);

        Assert.Null(answered.PendingQuestion);
        Assert.Null(answered.PendingQuestionResponsePlayerNumber);
        Assert.Collection(
            answered.QuestionHistory,
            entry =>
            {
                Assert.Equal(1, entry.PlayerNumber);
                Assert.Equal(MinigameQuestionHistoryKind.Question, entry.Kind);
                Assert.Equal(selected, entry.Value);
            },
            entry =>
            {
                Assert.Equal(2, entry.PlayerNumber);
                Assert.Equal(MinigameQuestionHistoryKind.Answer, entry.Kind);
                Assert.Equal(answerYes, entry.AnswerYes);
                Assert.Null(entry.Value);
                Assert.Null(entry.IsCorrect);
            });
        Assert.Equal(answered.QuestionHistory, asker.QuestionHistory);
    }

    [Fact]
    public void Question_answer_can_only_be_submitted_by_the_asked_player()
    {
        var (store, player1, _, _) = CreatePlayingRoom(questionCardsEnabled: true);
        store.SelectQuestion(player1.RoomCode, player1.PlayerToken, 0);

        var error = Assert.Throws<MinigameRoomException>(() =>
            store.SubmitQuestionResponse(
                player1.RoomCode,
                player1.PlayerToken,
                answerYes: true));

        Assert.Equal(MinigameRoomError.QuestionResponseNotPending, error.Error);
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
    public void Ending_turn_is_available_before_question_and_records_history()
    {
        var (store, player1, player2, _) = CreatePlayingRoom(questionCardsEnabled: true);

        var next = store.EndTurn(player1.RoomCode, player1.PlayerToken);
        var opponent = store.GetState(player1.RoomCode, player2.PlayerToken);

        Assert.Equal(2, next.CurrentPlayerNumber);
        var history = Assert.Single(next.QuestionHistory);
        Assert.Equal(1, history.PlayerNumber);
        Assert.Equal(MinigameQuestionHistoryKind.TurnEnded, history.Kind);
        Assert.Null(history.Value);
        Assert.Null(history.IsCorrect);
        Assert.Equal(next.QuestionHistory, opponent.QuestionHistory);
    }

    [Fact]
    public void Wrong_guess_is_available_before_question_and_records_game_name()
    {
        var (store, player1, player2, _) = CreatePlayingRoom(questionCardsEnabled: true);
        var player1State = store.GetState(player1.RoomCode, player1.PlayerToken);
        var opponentSecret = store
            .GetState(player1.RoomCode, player2.PlayerToken)
            .MySecretCardFileName!;
        var wrongCard = player1State.Cards.First(card => card.FileName != opponentSecret);

        var next = store.SubmitGuess(
            player1.RoomCode,
            player1.PlayerToken,
            wrongCard.FileName);

        Assert.Equal(2, next.CurrentPlayerNumber);
        var history = Assert.Single(next.QuestionHistory);
        Assert.Equal(1, history.PlayerNumber);
        Assert.Equal(MinigameQuestionHistoryKind.Guess, history.Kind);
        Assert.Equal(wrongCard.DisplayName, history.Value);
        Assert.False(history.IsCorrect);
    }

    [Fact]
    public void Correct_guess_is_available_before_question_and_records_game_name()
    {
        var (store, player1, player2, _) = CreatePlayingRoom(questionCardsEnabled: true);
        var opponent = store.GetState(player1.RoomCode, player2.PlayerToken);
        var targetFile = opponent.MySecretCardFileName!;
        var targetCard = opponent.Cards.Single(card => card.FileName == targetFile);

        var finished = store.SubmitGuess(
            player1.RoomCode,
            player1.PlayerToken,
            targetFile);

        Assert.Equal(MinigameRoomPhase.Finished, finished.Phase);
        Assert.Equal(1, finished.WinnerPlayerNumber);
        var history = Assert.Single(finished.QuestionHistory);
        Assert.Equal(1, history.PlayerNumber);
        Assert.Equal(MinigameQuestionHistoryKind.Guess, history.Kind);
        Assert.Equal(targetCard.DisplayName, history.Value);
        Assert.True(history.IsCorrect);
    }

    [Fact]
    public void Timeout_records_timed_out_turn_and_passes_turn()
    {
        var (store, player1, _, time) = CreatePlayingRoom(questionCardsEnabled: true);
        time.Advance(MinigameRoomStore.FirstTurnDuration);

        var state = store.ExpireTurn(player1.RoomCode, player1.PlayerToken);

        Assert.Equal(2, state.CurrentPlayerNumber);
        var history = Assert.Single(state.QuestionHistory);
        Assert.Equal(1, history.PlayerNumber);
        Assert.Equal(MinigameQuestionHistoryKind.TurnTimedOut, history.Kind);
        Assert.Null(history.Value);
        Assert.False(state.HasSelectedQuestionThisTurn);
    }

    [Fact]
    public void Question_history_can_include_question_then_manual_turn_end()
    {
        var (store, player1, _, _) = CreatePlayingRoom(questionCardsEnabled: true);
        var before = store.GetState(player1.RoomCode, player1.PlayerToken);
        var selected = before.MyAvailableQuestions[0];

        store.SelectQuestion(player1.RoomCode, player1.PlayerToken, 0);
        var next = store.EndTurn(player1.RoomCode, player1.PlayerToken);

        Assert.Collection(
            next.QuestionHistory,
            entry =>
            {
                Assert.Equal(MinigameQuestionHistoryKind.Question, entry.Kind);
                Assert.Equal(selected, entry.Value);
            },
            entry => Assert.Equal(MinigameQuestionHistoryKind.TurnEnded, entry.Kind));
    }

    [Fact]
    public void Game_continues_after_question_deck_is_exhausted()
    {
        string[] shortDeck = ["One?", "Two?", "Three?"];
        var (store, player1, player2, _) = CreatePlayingRoom(
            questionCardsEnabled: true,
            questions: shortDeck);

        for (var index = 0; index < shortDeck.Length; index++)
        {
            var current = store.GetState(player1.RoomCode, player1.PlayerToken);
            Assert.NotEmpty(current.MyAvailableQuestions);
            store.SelectQuestion(player1.RoomCode, player1.PlayerToken, 0);
            store.SubmitQuestionResponse(
                player1.RoomCode,
                player2.PlayerToken,
                answerYes: true);
            store.EndTurn(player1.RoomCode, player1.PlayerToken);
            store.EndTurn(player1.RoomCode, player2.PlayerToken);
        }

        var exhausted = store.GetState(player1.RoomCode, player1.PlayerToken);
        Assert.Empty(exhausted.MyAvailableQuestions);
        Assert.False(exhausted.HasSelectedQuestionThisTurn);

        var next = store.EndTurn(player1.RoomCode, player1.PlayerToken);
        Assert.Equal(2, next.CurrentPlayerNumber);
        Assert.Equal(
            MinigameQuestionHistoryKind.TurnEnded,
            next.QuestionHistory[^1].Kind);
    }

    [Fact]
    public void Restart_keeps_active_cards_clears_history_and_changes_only_requesters_secret()
    {
        var (store, player1, player2, time) = CreatePlayingRoom(questionCardsEnabled: true);
        var before1 = store.GetState(player1.RoomCode, player1.PlayerToken);
        var before2 = store.GetState(player1.RoomCode, player2.PlayerToken);
        var visibleFiles = before1.Cards.Select(card => card.FileName).ToArray();
        store.SelectQuestion(player1.RoomCode, player1.PlayerToken, 0);
        store.SubmitQuestionResponse(player1.RoomCode, player2.PlayerToken, answerYes: true);

        var restarted = store.RestartGame(player1.RoomCode, player1.PlayerToken);
        var opponent = store.GetState(player1.RoomCode, player2.PlayerToken);

        Assert.Equal(MinigameRoomPhase.Playing, restarted.Phase);
        Assert.Equal(before1.GameNumber + 1, restarted.GameNumber);
        Assert.Equal(visibleFiles, restarted.Cards.Select(card => card.FileName));
        Assert.DoesNotContain("Card-1.png", restarted.Cards.Select(card => card.FileName));
        Assert.DoesNotContain("Card-2.png", restarted.Cards.Select(card => card.FileName));
        Assert.NotEqual(before1.MySecretCardFileName, restarted.MySecretCardFileName);
        Assert.Equal(before2.MySecretCardFileName, opponent.MySecretCardFileName);
        Assert.Empty(restarted.QuestionHistory);
        Assert.Null(restarted.PendingQuestion);
        Assert.Null(restarted.PendingQuestionResponsePlayerNumber);
        Assert.Equal(3, restarted.MyAvailableQuestions.Count);
        Assert.Equal(1, restarted.CurrentPlayerNumber);
        Assert.Equal(
            time.GetUtcNow() + MinigameRoomStore.FirstTurnDuration,
            restarted.TurnDeadlineUtc);
        Assert.Null(restarted.WinnerPlayerNumber);
    }

    [Fact]
    public void Restart_by_player_two_changes_only_player_two_secret()
    {
        var (store, player1, player2, _) = CreatePlayingRoom(questionCardsEnabled: false);
        var before1 = store.GetState(player1.RoomCode, player1.PlayerToken);
        var before2 = store.GetState(player1.RoomCode, player2.PlayerToken);

        var restarted = store.RestartGame(player1.RoomCode, player2.PlayerToken);
        var player1After = store.GetState(player1.RoomCode, player1.PlayerToken);

        Assert.NotEqual(before2.MySecretCardFileName, restarted.MySecretCardFileName);
        Assert.Equal(before1.MySecretCardFileName, player1After.MySecretCardFileName);
        Assert.Equal(1, restarted.CurrentPlayerNumber);
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
        Assert.Empty(next.QuestionHistory);
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
        ManualTimeProvider Time) CreatePlayingRoom(
            bool questionCardsEnabled,
            IReadOnlyList<string>? questions = null)
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
            questionCardsEnabled ? questions ?? Questions : null);
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
