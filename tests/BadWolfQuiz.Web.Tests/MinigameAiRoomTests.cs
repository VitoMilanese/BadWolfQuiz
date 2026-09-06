using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameAiRoomTests
{
    private static readonly string[] Questions = ["One?", "Two?", "Three?"];

    [Fact]
    public void Ai_room_has_virtual_player_two_and_required_question_cards()
    {
        var store = new MinigameAiRoomStore(TimeProvider.System);
        var baseState = CreateBaseState();
        var aiGame = CreateAiGame(_ => true);

        var state = store.Start("ABC123", "token", baseState, aiGame);

        Assert.Equal(2, state.PlayerCount);
        Assert.True(state.QuestionCardsEnabled);
        Assert.Single(state.OpponentExcludedFiles);
        Assert.True(store.GetStatus("ABC123", "token").IsAiOpponent);
    }

    [Fact]
    public void Ai_uses_dont_know_for_unassigned_secret_answer()
    {
        var store = new MinigameAiRoomStore(TimeProvider.System);
        var choosing = store.Start(
            "ABC123",
            "token",
            CreateBaseState(),
            CreateAiGame(_ => null));
        var started = FinishPlayerExclusions(store, choosing);

        var selected = store.SelectQuestion("ABC123", "token", 0);

        Assert.Equal(MinigameRoomPhase.Playing, started.Phase);
        Assert.Null(selected.PendingQuestion);
        Assert.Null(selected.PendingQuestionResponsePlayerNumber);
        Assert.Collection(
            selected.QuestionHistory,
            question => Assert.Equal(MinigameQuestionHistoryKind.Question, question.Kind),
            answer =>
            {
                Assert.Equal(2, answer.PlayerNumber);
                Assert.Equal(MinigameQuestionHistoryKind.Answer, answer.Kind);
                Assert.Null(answer.AnswerYes);
            });
    }

    [Fact]
    public void Contradictory_answer_to_ai_question_finishes_as_draw()
    {
        var store = new MinigameAiRoomStore(TimeProvider.System);
        var choosing = store.Start(
            "ABC123",
            "token",
            CreateBaseState(),
            CreateAiGame(_ => true));
        FinishPlayerExclusions(store, choosing);

        var aiTurn = store.EndTurn("ABC123", "token");
        Assert.Equal(2, aiTurn.CurrentPlayerNumber);
        Assert.Equal(1, aiTurn.PendingQuestionResponsePlayerNumber);
        Assert.NotNull(aiTurn.PendingQuestion);

        var result = store.SubmitQuestionResponse("ABC123", "token", answerYes: false);

        Assert.Equal(MinigameRoomPhase.Finished, result.Phase);
        Assert.True(store.GetStatus("ABC123", "token").IsDraw);
        Assert.Null(result.WinnerPlayerNumber);
        Assert.Null(result.TurnDeadlineUtc);
    }

    [Fact]
    public void Human_dont_know_answer_eliminates_nothing_and_returns_turn()
    {
        var store = new MinigameAiRoomStore(TimeProvider.System);
        var choosing = store.Start(
            "ABC123",
            "token",
            CreateBaseState(),
            CreateAiGame(index => index % 2 == 0));
        FinishPlayerExclusions(store, choosing);
        var aiTurn = store.EndTurn("ABC123", "token");
        Assert.NotNull(aiTurn.PendingQuestion);

        var result = store.SubmitQuestionResponse("ABC123", "token", answerYes: null);

        Assert.Equal(MinigameRoomPhase.Playing, result.Phase);
        Assert.Equal(1, result.CurrentPlayerNumber);
        Assert.Null(result.QuestionHistory[^1].AnswerYes);
    }

    [Fact]
    public void Ai_room_rejects_the_wrong_player_token()
    {
        var store = new MinigameAiRoomStore(TimeProvider.System);
        store.Start("ABC123", "token", CreateBaseState(), CreateAiGame(_ => true));

        var error = Assert.Throws<MinigameRoomException>(() =>
            store.GetState("ABC123", "wrong-token"));

        Assert.Equal(MinigameRoomError.InvalidPlayer, error.Error);
    }

    private static MinigameRoomSnapshot FinishPlayerExclusions(
        MinigameAiRoomStore store,
        MinigameRoomSnapshot choosing)
    {
        var blocked = choosing.OpponentExcludedFiles.ToHashSet(StringComparer.Ordinal);
        var file = choosing.Cards.First(card => !blocked.Contains(card.FileName)).FileName;
        return store.ToggleExclusion("ABC123", "token", file);
    }

    private static MinigameRoomSnapshot CreateBaseState() =>
        new(
            "ABC123",
            1,
            1,
            5,
            0,
            MinigameRoomPhase.WaitingForGame,
            [],
            0,
            [],
            [],
            null,
            null,
            null,
            null,
            false,
            [],
            false,
            null,
            null,
            [],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1),
            new MinigameThemeSnapshot("", new Dictionary<string, string>()));

    private static MinigameAiGameData CreateAiGame(Func<int, bool?> answerFactory)
    {
        var games = Enumerable.Range(1, 10)
            .Select(index => new MinigameAiGameKnowledge(
                new MinigameCardDescriptor(index.ToString(), $"Game {index}"),
                Questions.ToDictionary(
                    question => question,
                    _ => answerFactory(index),
                    StringComparer.Ordinal)))
            .ToArray();
        return new MinigameAiGameData(games, Questions);
    }
}
