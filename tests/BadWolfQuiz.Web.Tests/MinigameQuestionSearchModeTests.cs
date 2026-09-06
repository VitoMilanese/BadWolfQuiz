using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameQuestionSearchModeTests
{
    private static readonly string[] Questions = Enumerable
        .Range(1, 25)
        .Select(index => $"Question {index:00}?")
        .ToArray();

    [Fact]
    public void Search_mode_replaces_three_card_hand_with_paged_personal_question_pool()
    {
        var (store, player1, player2) = CreatePlayingRoom();
        var state1 = store.GetState(player1.RoomCode, player1.PlayerToken);
        var state2 = store.GetState(player1.RoomCode, player2.PlayerToken);

        Assert.True(state1.QuestionCardsEnabled);
        Assert.Empty(state1.MyAvailableQuestions);
        Assert.Empty(state2.MyAvailableQuestions);
        Assert.Equal(
            MinigameQuestionSelectionMode.Search,
            store.GetQuestionSelectionMode(player1.RoomCode, player1.PlayerToken));

        var firstPage = store.SearchAvailableQuestions(
            player1.RoomCode,
            player1.PlayerToken,
            "Question",
            page: 1);
        var thirdPage = store.SearchAvailableQuestions(
            player1.RoomCode,
            player1.PlayerToken,
            "Question",
            page: 3);

        Assert.Equal(10, firstPage.Questions.Count);
        Assert.Equal(25, firstPage.TotalCount);
        Assert.Equal(3, firstPage.TotalPages);
        Assert.Equal(5, thirdPage.Questions.Count);
        Assert.Equal(3, thirdPage.Page);
    }

    [Fact]
    public void Search_requires_at_least_three_characters()
    {
        var (store, player1, _) = CreatePlayingRoom();

        var error = Assert.Throws<MinigameRoomException>(() =>
            store.SearchAvailableQuestions(
                player1.RoomCode,
                player1.PlayerToken,
                "Qu",
                page: 1));

        Assert.Equal(MinigameRoomError.InvalidQuestion, error.Error);
    }

    [Fact]
    public void Selected_question_is_consumed_only_for_the_player_who_asked_it()
    {
        var (store, player1, player2) = CreatePlayingRoom();
        const string question = "Question 01?";

        var selected = store.SelectQuestionByText(
            player1.RoomCode,
            player1.PlayerToken,
            question);

        Assert.Equal(question, selected.PendingQuestion);
        Assert.Equal(2, selected.PendingQuestionResponsePlayerNumber);
        Assert.DoesNotContain(
            question,
            store.SearchAvailableQuestions(
                player1.RoomCode,
                player1.PlayerToken,
                "Question 01",
                page: 1).Questions);
        Assert.Contains(
            question,
            store.SearchAvailableQuestions(
                player1.RoomCode,
                player2.PlayerToken,
                "Question 01",
                page: 1).Questions);

        store.SubmitQuestionResponse(
            player1.RoomCode,
            player2.PlayerToken,
            answerYes: true);
        store.EndTurn(player1.RoomCode, player1.PlayerToken);

        var player2Selection = store.SelectQuestionByText(
            player1.RoomCode,
            player2.PlayerToken,
            question);
        Assert.Equal(question, player2Selection.PendingQuestion);
        Assert.Equal(1, player2Selection.PendingQuestionResponsePlayerNumber);
    }

    [Fact]
    public void Restart_preserves_search_mode_and_restores_question_pool()
    {
        var (store, player1, player2) = CreatePlayingRoom();
        const string question = "Question 01?";
        store.SelectQuestionByText(player1.RoomCode, player1.PlayerToken, question);
        store.SubmitQuestionResponse(
            player1.RoomCode,
            player2.PlayerToken,
            answerYes: true);

        store.RestartGame(player1.RoomCode, player1.PlayerToken);

        Assert.Equal(
            MinigameQuestionSelectionMode.Search,
            store.GetQuestionSelectionMode(player1.RoomCode, player1.PlayerToken));
        Assert.Contains(
            question,
            store.SearchAvailableQuestions(
                player1.RoomCode,
                player1.PlayerToken,
                "Question 01",
                page: 1).Questions);
    }

    [Fact]
    public void Ai_question_score_prefers_the_question_with_the_best_worst_case_split()
    {
        var balanced = MinigameSoloAiService.GetQuestionEliminationScore(5, 5);
        var lopsided = MinigameSoloAiService.GetQuestionEliminationScore(9, 1);
        var allSame = MinigameSoloAiService.GetQuestionEliminationScore(10, 0);

        Assert.Equal(5, balanced);
        Assert.Equal(1, lopsided);
        Assert.Equal(0, allSame);
        Assert.True(balanced > lopsided);
    }

    private static (
        MinigameRoomStore Store,
        MinigameRoomConnection Player1,
        MinigameRoomConnection Player2) CreatePlayingRoom()
    {
        var store = new MinigameRoomStore(TimeProvider.System);
        var player1 = store.CreateRoom();
        var player2 = store.JoinRoom(player1.RoomCode);
        var cards = Enumerable.Range(1, 10)
            .Select(index => new MinigameCardDescriptor(
                $"Card-{index}.png",
                $"Card {index}"))
            .ToArray();

        store.StartNewGameWithQuestionSearch(
            player1.RoomCode,
            player1.PlayerToken,
            cards,
            Questions);
        store.ToggleExclusion(player1.RoomCode, player1.PlayerToken, "Card-1.png");
        store.ToggleExclusion(player1.RoomCode, player2.PlayerToken, "Card-2.png");
        return (store, player1, player2);
    }
}
