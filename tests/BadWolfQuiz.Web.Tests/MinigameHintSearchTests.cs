using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameHintSearchTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"BadWolfQuiz-minigame-hint-search-{Guid.NewGuid():N}");

    public MinigameHintSearchTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Search_without_question_cards_is_case_insensitive_paged_and_excludes_unassigned_answers()
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

        for (var index = 1; index <= 25; index++)
        {
            Assert.Equal(
                MinigameCatalogMutationResult.Success,
                await catalog.CreateQuestionAsync($"Alpha clue {index:00}?"));
        }

        var game = (await catalog.GetGamesAsync()).Single(item => item.Name == "Game 01");
        var questions = await catalog.GetQuestionItemsAsync();
        var assigned = questions
            .Take(22)
            .ToDictionary(
                question => question.Id,
                question => (bool?)(question.SortOrder % 2 == 0));
        Assert.Equal(
            MinigameCatalogMutationResult.Success,
            await catalog.SaveAnswersAsync(game.Id, assigned));

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
            questionCardsEnabled: false,
            questions: []);
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

        var firstPage = await hints.SearchCardHintsAsync(
            player1.RoomCode,
            player1.PlayerToken,
            candidate.FileName,
            "ALPHA",
            page: 1);
        Assert.Equal(22, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(MinigameHintService.SearchPageSize, firstPage.Items.Count);
        Assert.All(firstPage.Items, item => Assert.NotNull(item.AnswerYes));

        var secondPage = await hints.SearchCardHintsAsync(
            player1.RoomCode,
            player1.PlayerToken,
            candidate.FileName,
            "alpha",
            page: 2);
        Assert.Equal(2, secondPage.Items.Count);
        Assert.All(secondPage.Items, item => Assert.NotNull(item.AnswerYes));

        var unassignedOnly = await hints.SearchCardHintsAsync(
            player1.RoomCode,
            player1.PlayerToken,
            candidate.FileName,
            "clue 25",
            page: 1);
        Assert.Equal(0, unassignedOnly.TotalCount);
        Assert.Empty(unassignedOnly.Items);

        var tooShort = await hints.SearchCardHintsAsync(
            player1.RoomCode,
            player1.PlayerToken,
            candidate.FileName,
            "al",
            page: 1);
        Assert.Equal(0, tooShort.TotalCount);
        Assert.Empty(tooShort.Items);
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
