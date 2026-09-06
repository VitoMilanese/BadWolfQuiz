using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameHintSearchQuestionModeTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"BadWolfQuiz-minigame-hint-question-search-{Guid.NewGuid():N}");

    public MinigameHintSearchQuestionModeTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Free_selection_hint_search_uses_players_remaining_pool_and_includes_unassigned_answers()
    {
        var legacy = Path.Combine(_root, "legacy");
        Directory.CreateDirectory(legacy);
        var databasePath = Path.Combine(_root, "catalog.db");
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

        for (var index = 1; index <= 6; index++)
        {
            Assert.Equal(
                MinigameCatalogMutationResult.Success,
                await catalog.CreateQuestionAsync($"Alpha clue {index:00}?"));
        }

        var game = (await catalog.GetGamesAsync()).Single(item => item.Name == "Game 01");
        var questions = await catalog.GetQuestionItemsAsync();
        var answers = questions.ToDictionary(
            question => question.Id,
            question => question.SortOrder switch
            {
                0 => (bool?)true,
                1 => false,
                2 => null,
                _ => question.SortOrder % 2 == 0
            });
        Assert.Equal(
            MinigameCatalogMutationResult.Success,
            await catalog.SaveAnswersAsync(game.Id, answers));

        var cards = await catalog.GenerateCardsAsync(10);
        var candidate = cards.Single(card => card.DisplayName == game.Name);
        var exclusions = cards
            .Where(card => card.FileName != candidate.FileName)
            .Take(2)
            .ToArray();

        var rooms = new MinigameRoomStore(TimeProvider.System);
        var player1 = rooms.CreateRoom();
        var player2 = rooms.JoinRoom(player1.RoomCode);
        rooms.StartNewGameWithQuestionSearch(
            player1.RoomCode,
            player1.PlayerToken,
            cards,
            questions.Select(question => question.Text).ToArray());
        rooms.ToggleExclusion(
            player1.RoomCode,
            player1.PlayerToken,
            exclusions[0].FileName);
        rooms.ToggleExclusion(
            player1.RoomCode,
            player2.PlayerToken,
            exclusions[1].FileName);

        var hints = new MinigameHintService(factory, 10, rooms);
        hints.SetEnabled(player1.RoomCode, enabled: true);

        var initial = await hints.SearchCardHintsAsync(
            player1.RoomCode,
            player1.PlayerToken,
            candidate.FileName,
            "ALPHA",
            page: 1);

        Assert.Equal(6, initial.TotalCount);
        Assert.Equal(6, initial.Items.Count);
        var unassigned = Assert.Single(
            initial.Items,
            item => item.Question == "Alpha clue 03?");
        Assert.Null(unassigned.AnswerYes);

        rooms.SelectQuestionByText(
            player1.RoomCode,
            player1.PlayerToken,
            "Alpha clue 01?");

        var player1AfterUse = await hints.SearchCardHintsAsync(
            player1.RoomCode,
            player1.PlayerToken,
            candidate.FileName,
            "alpha",
            page: 1);
        Assert.Equal(5, player1AfterUse.TotalCount);
        Assert.DoesNotContain(
            player1AfterUse.Items,
            item => item.Question == "Alpha clue 01?");

        var player2StillHasQuestion = await hints.SearchCardHintsAsync(
            player1.RoomCode,
            player2.PlayerToken,
            candidate.FileName,
            "clue 01",
            page: 1);
        Assert.Equal(1, player2StillHasQuestion.TotalCount);
        Assert.Equal("Alpha clue 01?", Assert.Single(player2StillHasQuestion.Items).Question);
    }

    [Fact]
    public async Task Three_card_hint_search_uses_all_enabled_questions_and_includes_unassigned_answers()
    {
        var legacy = Path.Combine(_root, $"legacy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(legacy);
        var databasePath = Path.Combine(_root, $"catalog-{Guid.NewGuid():N}.db");
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

        for (var index = 1; index <= 6; index++)
        {
            Assert.Equal(
                MinigameCatalogMutationResult.Success,
                await catalog.CreateQuestionAsync($"Alpha clue {index:00}?"));
        }

        var game = (await catalog.GetGamesAsync()).Single(item => item.Name == "Game 01");
        var questions = await catalog.GetQuestionItemsAsync();
        var answers = questions.ToDictionary(
            question => question.Id,
            question => question.SortOrder switch
            {
                0 => (bool?)true,
                1 => false,
                2 => null,
                _ => question.SortOrder % 2 == 0
            });
        Assert.Equal(
            MinigameCatalogMutationResult.Success,
            await catalog.SaveAnswersAsync(game.Id, answers));

        var availability = new MinigameQuestionAvailabilityStore(factory);
        var disabledQuestion = questions.Single(question => question.SortOrder == 5);
        Assert.True(await availability.SetEnabledAsync(disabledQuestion.Id, enabled: false));
        var enabledQuestions = await availability.GetEnabledQuestionsAsync();
        Assert.Equal(5, enabledQuestions.Count);

        var cards = await catalog.GenerateCardsAsync(10);
        var candidate = cards.Single(card => card.DisplayName == game.Name);
        var exclusions = cards
            .Where(card => card.FileName != candidate.FileName)
            .Take(2)
            .ToArray();

        var rooms = new MinigameRoomStore(TimeProvider.System);
        var player1 = rooms.CreateRoom();
        var player2 = rooms.JoinRoom(player1.RoomCode);
        rooms.StartNewGame(
            player1.RoomCode,
            player1.PlayerToken,
            cards,
            questionCardsEnabled: true,
            enabledQuestions);
        rooms.ToggleExclusion(
            player1.RoomCode,
            player1.PlayerToken,
            exclusions[0].FileName);
        rooms.ToggleExclusion(
            player1.RoomCode,
            player2.PlayerToken,
            exclusions[1].FileName);

        var hints = new MinigameHintService(factory, 10, rooms);
        hints.SetEnabled(player1.RoomCode, enabled: true);

        var currentHints = await hints.GetCardHintsAsync(
            player1.RoomCode,
            player1.PlayerToken,
            candidate.FileName);
        Assert.Equal(MinigameQuestionStore.MinimumQuestionCount, currentHints.PinnedQuestions.Count);

        var search = await hints.SearchCardHintsAsync(
            player1.RoomCode,
            player1.PlayerToken,
            candidate.FileName,
            "alpha",
            page: 1);

        Assert.Equal(5, search.TotalCount);
        Assert.True(search.TotalCount > currentHints.PinnedQuestions.Count);
        Assert.DoesNotContain(
            search.Items,
            item => item.Question == disabledQuestion.Text);
        var unassigned = Assert.Single(
            search.Items,
            item => item.Question == "Alpha clue 03?");
        Assert.Null(unassigned.AnswerYes);
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
}
