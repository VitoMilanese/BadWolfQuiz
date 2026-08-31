using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameCatalogStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"BadWolfQuiz-minigame-catalog-{Guid.NewGuid():N}");

    public MinigameCatalogStoreTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Database_catalog_serves_cards_questions_and_image_bytes()
    {
        var legacy = Path.Combine(_root, "empty-legacy");
        Directory.CreateDirectory(legacy);
        var store = CreateStore(legacy);

        Assert.Equal(
            MinigameCatalogMutationResult.Success,
            await store.CreateGameAsync("Game A", new byte[] { 1, 2, 3 }, "image/png"));
        Assert.Equal(
            MinigameCatalogMutationResult.Success,
            await store.CreateQuestionAsync("Question A?"));

        var counts = await store.GetCountsAsync();
        var cards = await store.GenerateCardsAsync(1);
        var questions = await store.GetQuestionsAsync();
        var image = await store.GetGameImageAsync(cards[0].FileName);

        Assert.Equal(1, counts.GameCount);
        Assert.Equal(1, counts.QuestionCount);
        Assert.Single(cards);
        Assert.Equal("Game A", cards[0].DisplayName);
        Assert.Equal(new[] { "Question A?" }, questions);
        Assert.NotNull(image);
        Assert.Equal("image/png", image!.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.Data);
    }

    [Fact]
    public async Task Legacy_catalog_imports_questions_images_and_matching_answer_file_once()
    {
        var legacy = Path.Combine(_root, "legacy");
        Directory.CreateDirectory(legacy);
        await File.WriteAllTextAsync(
            Path.Combine(legacy, "questions.txt"),
            "First?\nSecond?\nThird?\n");
        await File.WriteAllBytesAsync(
            Path.Combine(legacy, "Game A.png"),
            new byte[] { 10, 20, 30 });
        await File.WriteAllTextAsync(Path.Combine(legacy, "Game A.txt"), "1\n0\n1\n");

        var store = CreateStore(legacy);
        var counts = await store.GetCountsAsync();
        var game = Assert.Single(await store.GetGamesAsync());
        var answers = await store.GetAnswerItemsAsync(game.Id);

        Assert.Equal(1, counts.GameCount);
        Assert.Equal(3, counts.QuestionCount);
        Assert.Equal(
            new bool?[] { true, false, true },
            answers.Select(answer => answer.AnswerYes).ToArray());

        await File.WriteAllTextAsync(
            Path.Combine(legacy, "questions.txt"),
            "Changed?\nSecond?\nThird?\n");
        Assert.Equal(
            new[] { "First?", "Second?", "Third?" },
            await store.GetQuestionsAsync());
    }

    [Theory]
    [InlineData("1\n0\n1\n", 3, true, 0)]
    [InlineData("1\n2\n0\n", 3, false, 2)]
    [InlineData("1\n0\n", 3, false, 0)]
    [InlineData("1\n\n0\n", 3, false, 2)]
    public void Answer_import_requires_exactly_one_zero_or_one_per_question(
        string content,
        int questionCount,
        bool expectedSuccess,
        int expectedInvalidLine)
    {
        var result = MinigameAnswerImportParser.Parse(content, questionCount);

        Assert.Equal(expectedSuccess, result.Success);
        Assert.Equal(expectedInvalidLine, result.InvalidLineNumber);
    }

    [Fact]
    public async Task Bulk_answer_replacement_is_all_or_nothing_after_validation()
    {
        var legacy = Path.Combine(_root, "bulk-empty");
        Directory.CreateDirectory(legacy);
        var store = CreateStore(legacy);
        await store.CreateGameAsync("Game", new byte[] { 1 }, "image/png");
        await store.CreateQuestionAsync("One?");
        await store.CreateQuestionAsync("Two?");
        var game = Assert.Single(await store.GetGamesAsync());

        Assert.Equal(
            MinigameCatalogMutationResult.Success,
            await store.ReplaceAnswersAsync(game.Id, new[] { true, false }));
        Assert.Equal(
            MinigameCatalogMutationResult.Invalid,
            await store.ReplaceAnswersAsync(game.Id, new[] { false }));

        var answers = await store.GetAnswerItemsAsync(game.Id);
        Assert.Equal(
            new bool?[] { true, false },
            answers.Select(answer => answer.AnswerYes).ToArray());
    }

    private MinigameCatalogStore CreateStore(string legacyRoot)
    {
        var databasePath = Path.Combine(_root, "catalog.db");
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        return new MinigameCatalogStore(
            new QuizDbContextFactory(options),
            defaultCardCount: 10,
            legacyRoot);
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
