using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameResourceSyncTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"BadWolfQuiz-minigame-resource-sync-{Guid.NewGuid():N}");

    public MinigameResourceSyncTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Synchronize_adds_new_games_updates_existing_games_and_returns_database_only_games()
    {
        var resources = CreateDirectory("resources");
        var factory = CreateFactory();
        var store = CreateStore(factory);
        await store.CreateQuestionAsync("First?");
        await store.CreateQuestionAsync("Second?");
        await store.CreateQuestionAsync("Third?");
        await store.CreateGameAsync("Game A", new byte[] { 1 }, "image/png");
        await store.CreateGameAsync("Database only", new byte[] { 2 }, "image/png");

        var questions = await store.GetQuestionItemsAsync();
        var gameA = (await store.GetGamesAsync()).Single(game => game.Name == "Game A");
        await store.SaveAnswersAsync(
            gameA.Id,
            questions.ToDictionary(question => question.Id, _ => (bool?)true));

        await File.WriteAllBytesAsync(
            Path.Combine(resources, "Game A.png"),
            new byte[] { 10, 20, 30 });
        await File.WriteAllTextAsync(
            Path.Combine(resources, "Game A.txt"),
            "0\n\n1\n");
        await File.WriteAllBytesAsync(
            Path.Combine(resources, "Game B.jpg"),
            new byte[] { 40, 50 });
        await File.WriteAllTextAsync(
            Path.Combine(resources, "Game B.txt"),
            "1\n0\n1\n");

        var service = new MinigameResourceSyncService(factory, resources);
        var result = await service.SynchronizeAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.UpdatedCount);
        var missing = Assert.Single(result.MissingGames);
        Assert.Equal("Database only", missing.Name);
        Assert.Equal(
            new[] { "First?", "Second?", "Third?" },
            await store.GetQuestionsAsync());

        var games = await store.GetGamesAsync();
        var updatedGameA = games.Single(game => game.Name == "Game A");
        var gameB = games.Single(game => game.Name == "Game B");
        var imageA = await store.GetGameImageAsync(updatedGameA.Id);
        var imageB = await store.GetGameImageAsync(gameB.Id);
        Assert.Equal(new byte[] { 10, 20, 30 }, imageA!.Data);
        Assert.Equal("image/png", imageA.ContentType);
        Assert.Equal(new byte[] { 40, 50 }, imageB!.Data);
        Assert.Equal("image/jpeg", imageB.ContentType);
        Assert.Equal(
            new bool?[] { false, null, true },
            (await store.GetAnswerItemsAsync(updatedGameA.Id))
                .Select(answer => answer.AnswerYes)
                .ToArray());
        Assert.Equal(
            new bool?[] { true, false, true },
            (await store.GetAnswerItemsAsync(gameB.Id))
                .Select(answer => answer.AnswerYes)
                .ToArray());
    }

    [Fact]
    public async Task Synchronize_without_txt_keeps_existing_answers()
    {
        var resources = CreateDirectory("resources-no-txt");
        var factory = CreateFactory();
        var store = CreateStore(factory);
        await store.CreateQuestionAsync("First?");
        await store.CreateQuestionAsync("Second?");
        await store.CreateGameAsync("Game A", new byte[] { 1 }, "image/png");
        var questions = await store.GetQuestionItemsAsync();
        var game = Assert.Single(await store.GetGamesAsync());
        await store.SaveAnswersAsync(
            game.Id,
            new Dictionary<int, bool?>
            {
                [questions[0].Id] = true,
                [questions[1].Id] = false
            });
        await File.WriteAllBytesAsync(
            Path.Combine(resources, "Game A.webp"),
            new byte[] { 9, 8, 7 });

        var result = await new MinigameResourceSyncService(factory, resources)
            .SynchronizeAsync();

        Assert.True(result.Success);
        Assert.Equal(
            new bool?[] { true, false },
            (await store.GetAnswerItemsAsync(game.Id))
                .Select(answer => answer.AnswerYes)
                .ToArray());
        Assert.Equal(
            new byte[] { 9, 8, 7 },
            (await store.GetGameImageAsync(game.Id))!.Data);
    }

    [Fact]
    public async Task Invalid_txt_fails_preflight_without_partial_database_changes()
    {
        var resources = CreateDirectory("resources-invalid");
        var factory = CreateFactory();
        var store = CreateStore(factory);
        await store.CreateQuestionAsync("First?");
        await store.CreateQuestionAsync("Second?");
        await store.CreateGameAsync("Game A", new byte[] { 1, 2 }, "image/png");
        var game = Assert.Single(await store.GetGamesAsync());

        await File.WriteAllBytesAsync(
            Path.Combine(resources, "Game A.png"),
            new byte[] { 99 });
        await File.WriteAllTextAsync(
            Path.Combine(resources, "Game A.txt"),
            "1\nwat\n");
        await File.WriteAllBytesAsync(
            Path.Combine(resources, "Game B.png"),
            new byte[] { 5 });

        var result = await new MinigameResourceSyncService(factory, resources)
            .SynchronizeAsync();

        Assert.False(result.Success);
        Assert.Equal(MinigameResourceSyncError.InvalidAnswerFile, result.Error);
        Assert.Equal(2, result.InvalidLineNumber);
        Assert.Equal(
            new byte[] { 1, 2 },
            (await store.GetGameImageAsync(game.Id))!.Data);
        Assert.DoesNotContain(
            await store.GetGamesAsync(),
            item => item.Name == "Game B");
    }

    [Fact]
    public async Task Delete_missing_games_never_deletes_a_game_that_exists_in_resources()
    {
        var resources = CreateDirectory("resources-delete");
        var factory = CreateFactory();
        var store = CreateStore(factory);
        await store.CreateQuestionAsync("First?");
        await store.CreateGameAsync("Resource game", new byte[] { 1 }, "image/png");
        await store.CreateGameAsync("Database only", new byte[] { 2 }, "image/png");
        var games = await store.GetGamesAsync();
        var resourceGame = games.Single(game => game.Name == "Resource game");
        var databaseOnly = games.Single(game => game.Name == "Database only");
        await File.WriteAllBytesAsync(
            Path.Combine(resources, "Resource game.png"),
            new byte[] { 3 });

        var service = new MinigameResourceSyncService(factory, resources);
        var result = await service.DeleteMissingGamesAsync(
            new[] { resourceGame.Id, databaseOnly.Id });

        Assert.True(result.Success);
        Assert.Equal(1, result.DeletedCount);
        var remaining = await store.GetGamesAsync();
        Assert.Contains(remaining, game => game.Id == resourceGame.Id);
        Assert.DoesNotContain(remaining, game => game.Id == databaseOnly.Id);
    }

    private QuizDbContextFactory CreateFactory()
    {
        var databasePath = Path.Combine(_root, "catalog.db");
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        return new QuizDbContextFactory(options);
    }

    private MinigameCatalogStore CreateStore(QuizDbContextFactory factory)
    {
        var legacy = CreateDirectory("empty-legacy");
        return new MinigameCatalogStore(factory, defaultCardCount: 10, legacy);
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
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
