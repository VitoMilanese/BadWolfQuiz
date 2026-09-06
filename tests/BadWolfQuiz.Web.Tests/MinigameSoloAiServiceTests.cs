using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameSoloAiServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"BadWolfQuiz-minigame-solo-ai-{Guid.NewGuid():N}");

    public MinigameSoloAiServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Availability_requires_at_least_eighty_percent_of_answers()
    {
        var setup = await CreateSetupAsync();

        var availability = await setup.SoloAi.GetAvailabilityAsync();

        Assert.Equal(10, availability.EligibleGameCount);
        Assert.Equal(80, availability.MinimumAnswerCoveragePercent);
        Assert.True(availability.Available);
    }

    [Fact]
    public async Task Solo_game_uses_question_cards_and_ai_plays_as_player_two()
    {
        var setup = await CreateSetupAsync();
        var player1 = setup.Rooms.CreateRoom();
        var questions = await setup.Catalog.GetQuestionsAsync();

        var state = await setup.SoloAi.StartSoloGameAsync(
            player1.RoomCode,
            player1.PlayerToken,
            cardCount: 10,
            questions);

        Assert.True(state.QuestionCardsEnabled);
        Assert.Equal(2, state.PlayerCount);
        Assert.Equal(MinigameRoomPhase.ChoosingExclusions, state.Phase);
        Assert.Single(state.OpponentExcludedFiles);
        Assert.Throws<MinigameRoomException>(() =>
            setup.Rooms.JoinRoom(player1.RoomCode));

        var status = setup.SoloAi.GetStatus(
            player1.RoomCode,
            player1.PlayerToken);
        Assert.True(status.IsSoloGame);
        Assert.True(status.CanStartSoloGame);
        Assert.False(status.HasHumanOpponent);

        var humanExclusion = state.Cards.First(card =>
            !state.OpponentExcludedFiles.Contains(card.FileName));
        state = setup.Rooms.ToggleExclusion(
            player1.RoomCode,
            player1.PlayerToken,
            humanExclusion.FileName);
        Assert.Equal(MinigameRoomPhase.Playing, state.Phase);
        Assert.Equal(1, state.CurrentPlayerNumber);

        var aiMembership = setup.Rooms.EnsureSoloOpponent(
            player1.RoomCode,
            player1.PlayerToken);
        var aiState = setup.Rooms.GetState(
            player1.RoomCode,
            aiMembership.PlayerToken,
            touchActivity: false);
        var aiSecretId = int.Parse(aiState.MySecretCardFileName!);
        var aiAnswers = await setup.Catalog.GetAnswerItemsAsync(aiSecretId);

        state = setup.Rooms.GetState(
            player1.RoomCode,
            player1.PlayerToken,
            touchActivity: false);
        var definedChoice = state.MyAvailableQuestions
            .Select((question, index) => new { question, index })
            .First(choice => aiAnswers
                .Single(row => row.QuestionText == choice.question)
                .AnswerYes.HasValue);
        var expectedAnswer = aiAnswers
            .Single(row => row.QuestionText == definedChoice.question)
            .AnswerYes;

        setup.Rooms.SelectQuestion(
            player1.RoomCode,
            player1.PlayerToken,
            definedChoice.index);
        state = await setup.SoloAi.AdvanceAsync(
            player1.RoomCode,
            player1.PlayerToken);

        Assert.Null(state.PendingQuestionResponsePlayerNumber);
        var aiAnswer = state.QuestionHistory.Last();
        Assert.Equal(MinigameQuestionHistoryKind.Answer, aiAnswer.Kind);
        Assert.Equal(2, aiAnswer.PlayerNumber);
        Assert.Equal(expectedAnswer, aiAnswer.AnswerYes);

        setup.Rooms.EndTurn(player1.RoomCode, player1.PlayerToken);
        state = await setup.SoloAi.AdvanceAsync(
            player1.RoomCode,
            player1.PlayerToken);

        Assert.Equal(2, state.CurrentPlayerNumber);
        Assert.Equal(1, state.PendingQuestionResponsePlayerNumber);
        Assert.False(string.IsNullOrWhiteSpace(state.PendingQuestion));

        setup.Rooms.SubmitQuestionResponse(
            player1.RoomCode,
            player1.PlayerToken,
            answerYes: true);
        state = await setup.SoloAi.AdvanceAsync(
            player1.RoomCode,
            player1.PlayerToken);

        Assert.Equal(MinigameRoomPhase.Playing, state.Phase);
        Assert.Equal(1, state.CurrentPlayerNumber);
    }

    [Fact]
    public async Task Missing_ai_answer_is_reported_as_unknown_in_solo_status()
    {
        var setup = await CreateSetupAsync();
        var player1 = setup.Rooms.CreateRoom();
        var questions = await setup.Catalog.GetQuestionsAsync();
        var state = await setup.SoloAi.StartSoloGameAsync(
            player1.RoomCode,
            player1.PlayerToken,
            cardCount: 10,
            questions);

        var humanExclusion = state.Cards.First(card =>
            !state.OpponentExcludedFiles.Contains(card.FileName));
        setup.Rooms.ToggleExclusion(
            player1.RoomCode,
            player1.PlayerToken,
            humanExclusion.FileName);

        for (var turn = 0; turn < 5; turn++)
        {
            state = setup.Rooms.GetState(
                player1.RoomCode,
                player1.PlayerToken,
                touchActivity: false);
            var missingIndex = state.MyAvailableQuestions
                .Select((question, index) => new { question, index })
                .FirstOrDefault(choice => choice.question == "Question 5?")?.index;
            if (missingIndex.HasValue)
            {
                setup.Rooms.SelectQuestion(
                    player1.RoomCode,
                    player1.PlayerToken,
                    missingIndex.Value);
                state = await setup.SoloAi.AdvanceAsync(
                    player1.RoomCode,
                    player1.PlayerToken);

                var status = setup.SoloAi.GetStatus(
                    player1.RoomCode,
                    player1.PlayerToken);
                Assert.Contains(
                    state.QuestionHistory.Count - 1,
                    status.UnknownAnswerHistoryIndexes);
                Assert.Equal(2, state.QuestionHistory.Last().PlayerNumber);
                Assert.Equal(MinigameQuestionHistoryKind.Answer, state.QuestionHistory.Last().Kind);
                return;
            }

            setup.Rooms.SelectQuestion(
                player1.RoomCode,
                player1.PlayerToken,
                optionIndex: 0);
            await setup.SoloAi.AdvanceAsync(
                player1.RoomCode,
                player1.PlayerToken);
            setup.Rooms.EndTurn(player1.RoomCode, player1.PlayerToken);
            state = await setup.SoloAi.AdvanceAsync(
                player1.RoomCode,
                player1.PlayerToken);
            Assert.Equal(1, state.PendingQuestionResponsePlayerNumber);
            setup.Rooms.SubmitQuestionResponse(
                player1.RoomCode,
                player1.PlayerToken,
                answerYes: true);
            await setup.SoloAi.AdvanceAsync(
                player1.RoomCode,
                player1.PlayerToken);
        }

        throw new Xunit.Sdk.XunitException("The missing-answer question never reached Player 1's three-card hand.");
    }

    private async Task<SoloSetup> CreateSetupAsync()
    {
        var legacy = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(legacy);
        var databasePath = Path.Combine(_root, $"{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        var factory = new QuizDbContextFactory(options);
        var catalog = new MinigameCatalogStore(factory, defaultCardCount: 10, legacy);

        for (var index = 1; index <= 11; index++)
        {
            Assert.Equal(
                MinigameCatalogMutationResult.Success,
                await catalog.CreateGameAsync(
                    $"Game {index:00}",
                    new byte[] { (byte)index },
                    "image/png"));
        }

        for (var index = 1; index <= 5; index++)
        {
            Assert.Equal(
                MinigameCatalogMutationResult.Success,
                await catalog.CreateQuestionAsync($"Question {index}?"));
        }

        var questionItems = await catalog.GetQuestionItemsAsync();
        var games = await catalog.GetGamesAsync();
        foreach (var game in games)
        {
            var assignedCount = game.Name == "Game 11" ? 3 : 4;
            var values = questionItems
                .Take(assignedCount)
                .ToDictionary(
                    question => question.Id,
                    _ => (bool?)true);
            Assert.Equal(
                MinigameCatalogMutationResult.Success,
                await catalog.SaveAnswersAsync(game.Id, values));
        }

        var rooms = new MinigameRoomStore(TimeProvider.System);
        var soloAi = new MinigameSoloAiService(factory, 10, rooms);
        return new SoloSetup(catalog, rooms, soloAi);
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

    private sealed record SoloSetup(
        MinigameCatalogStore Catalog,
        MinigameRoomStore Rooms,
        MinigameSoloAiService SoloAi);
}
