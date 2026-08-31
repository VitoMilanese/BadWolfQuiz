using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameHintServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"BadWolfQuiz-minigame-hints-{Guid.NewGuid():N}");

    public MinigameHintServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Card_hints_use_current_questions_and_questions_asked_to_opponent_newest_first()
    {
        var setup = await CreatePlayingRoomAsync();
        setup.Hints.SetEnabled(setup.Player1.RoomCode, enabled: true);

        var missingAnswersCard = setup.Cards.Single(card => card.DisplayName == "Game 10");
        var before = await setup.Hints.GetCardHintsAsync(
            setup.Player1.RoomCode,
            setup.Player2.PlayerToken,
            missingAnswersCard.FileName);

        Assert.Equal(MinigameQuestionStore.MinimumQuestionCount, before.PinnedQuestions.Count);
        Assert.All(before.PinnedQuestions, row => Assert.Null(row.AnswerYes));
        Assert.Empty(before.AskedQuestions);

        var player1State = setup.Rooms.GetState(
            setup.Player1.RoomCode,
            setup.Player1.PlayerToken,
            touchActivity: false);
        setup.Rooms.SelectQuestion(
            setup.Player1.RoomCode,
            setup.Player1.PlayerToken,
            optionIndex: 0);

        var player2HintsAfterOpponentQuestion = await setup.Hints.GetCardHintsAsync(
            setup.Player1.RoomCode,
            setup.Player2.PlayerToken,
            missingAnswersCard.FileName);
        Assert.Empty(player2HintsAfterOpponentQuestion.AskedQuestions);

        setup.Rooms.SubmitQuestionResponse(
            setup.Player1.RoomCode,
            setup.Player2.PlayerToken,
            answerYes: true);
        setup.Rooms.EndTurn(setup.Player1.RoomCode, setup.Player1.PlayerToken);

        var player2State = setup.Rooms.GetState(
            setup.Player1.RoomCode,
            setup.Player2.PlayerToken,
            touchActivity: false);
        var firstQuestion = player2State.MyAvailableQuestions[0];
        setup.Rooms.SelectQuestion(
            setup.Player1.RoomCode,
            setup.Player2.PlayerToken,
            optionIndex: 0);

        var afterFirst = await setup.Hints.GetCardHintsAsync(
            setup.Player1.RoomCode,
            setup.Player2.PlayerToken,
            missingAnswersCard.FileName);
        Assert.Single(afterFirst.AskedQuestions);
        Assert.Equal(firstQuestion, afterFirst.AskedQuestions[0].Question);
        Assert.Null(afterFirst.AskedQuestions[0].AnswerYes);

        setup.Rooms.SubmitQuestionResponse(
            setup.Player1.RoomCode,
            setup.Player1.PlayerToken,
            answerYes: true);
        setup.Rooms.EndTurn(setup.Player1.RoomCode, setup.Player2.PlayerToken);
        setup.Rooms.EndTurn(setup.Player1.RoomCode, setup.Player1.PlayerToken);

        player2State = setup.Rooms.GetState(
            setup.Player1.RoomCode,
            setup.Player2.PlayerToken,
            touchActivity: false);
        var newestQuestion = player2State.MyAvailableQuestions[0];
        setup.Rooms.SelectQuestion(
            setup.Player1.RoomCode,
            setup.Player2.PlayerToken,
            optionIndex: 0);

        var player2Hints = await setup.Hints.GetCardHintsAsync(
            setup.Player1.RoomCode,
            setup.Player2.PlayerToken,
            missingAnswersCard.FileName);
        Assert.Equal(2, player2Hints.AskedQuestions.Count);
        Assert.Equal(newestQuestion, player2Hints.AskedQuestions[0].Question);
        Assert.Equal(firstQuestion, player2Hints.AskedQuestions[1].Question);
    }

    [Fact]
    public async Task Response_hint_uses_only_responding_players_secret_game()
    {
        var setup = await CreatePlayingRoomAsync();
        setup.Hints.SetEnabled(setup.Player1.RoomCode, enabled: true);

        var player1State = setup.Rooms.GetState(
            setup.Player1.RoomCode,
            setup.Player1.PlayerToken,
            touchActivity: false);
        var selectedQuestion = player1State.MyAvailableQuestions[0];
        setup.Rooms.SelectQuestion(
            setup.Player1.RoomCode,
            setup.Player1.PlayerToken,
            optionIndex: 0);

        var player2State = setup.Rooms.GetState(
            setup.Player1.RoomCode,
            setup.Player2.PlayerToken,
            touchActivity: false);
        Assert.NotNull(player2State.MySecretCardFileName);
        var secretGameId = int.Parse(player2State.MySecretCardFileName!);
        var expected = (await setup.Catalog.GetAnswerItemsAsync(secretGameId))
            .Single(row => row.QuestionText == selectedQuestion)
            .AnswerYes;

        var hint = await setup.Hints.GetQuestionResponseHintAsync(
            setup.Player1.RoomCode,
            setup.Player2.PlayerToken);

        Assert.Equal(selectedQuestion, hint.Question);
        Assert.Equal(expected, hint.AnswerYes);
    }

    [Fact]
    public async Task Hints_are_rejected_when_disabled_or_for_a_card_outside_the_active_room()
    {
        var setup = await CreatePlayingRoomAsync();

        await Assert.ThrowsAsync<MinigameRoomException>(() =>
            setup.Hints.GetCardHintsAsync(
                setup.Player1.RoomCode,
                setup.Player1.PlayerToken,
                setup.Cards[0].FileName));

        setup.Hints.SetEnabled(setup.Player1.RoomCode, enabled: true);
        var exception = await Assert.ThrowsAsync<MinigameRoomException>(() =>
            setup.Hints.GetCardHintsAsync(
                setup.Player1.RoomCode,
                setup.Player1.PlayerToken,
                "999999"));
        Assert.Equal(MinigameRoomError.CardNotFound, exception.Error);
    }

    private async Task<HintSetup> CreatePlayingRoomAsync()
    {
        var legacy = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(legacy);
        var databasePath = Path.Combine(_root, $"{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        var factory = new QuizDbContextFactory(options);
        var catalog = new MinigameCatalogStore(factory, defaultCardCount: 10, legacy);

        for (var index = 1; index <= 10; index++)
        {
            Assert.Equal(
                MinigameCatalogMutationResult.Success,
                await catalog.CreateGameAsync(
                    $"Game {index:00}",
                    new byte[] { (byte)index },
                    "image/png"));
        }

        for (var index = 1; index <= MinigameQuestionStore.MinimumQuestionCount; index++)
        {
            Assert.Equal(
                MinigameCatalogMutationResult.Success,
                await catalog.CreateQuestionAsync($"Question {index}?"));
        }

        var games = await catalog.GetGamesAsync();
        foreach (var game in games.Where(game => game.Name != "Game 10"))
        {
            Assert.Equal(
                MinigameCatalogMutationResult.Success,
                await catalog.ReplaceAnswersAsync(
                    game.Id,
                    Enumerable.Repeat(
                        true,
                        MinigameQuestionStore.MinimumQuestionCount).ToArray()));
        }

        var cards = await catalog.GenerateCardsAsync(10);
        var questions = await catalog.GetQuestionsAsync();
        var rooms = new MinigameRoomStore(TimeProvider.System);
        var player1 = rooms.CreateRoom();
        var player2 = rooms.JoinRoom(player1.RoomCode);
        rooms.StartNewGame(
            player1.RoomCode,
            player1.PlayerToken,
            cards,
            questionCardsEnabled: true,
            questions: questions);

        var missingAnswersKey = cards.Single(card => card.DisplayName == "Game 10").FileName;
        var exclusions = cards
            .Where(card => card.FileName != missingAnswersKey)
            .Take(2)
            .ToArray();
        rooms.ToggleExclusion(
            player1.RoomCode,
            player1.PlayerToken,
            exclusions[0].FileName);
        rooms.ToggleExclusion(
            player1.RoomCode,
            player2.PlayerToken,
            exclusions[1].FileName);

        var hints = new MinigameHintService(factory, 10, rooms);
        return new HintSetup(catalog, rooms, hints, player1, player2, cards);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record HintSetup(
        MinigameCatalogStore Catalog,
        MinigameRoomStore Rooms,
        MinigameHintService Hints,
        MinigameRoomConnection Player1,
        MinigameRoomConnection Player2,
        IReadOnlyList<MinigameCardDescriptor> Cards);
}
