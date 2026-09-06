using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameQuestionAvailabilityStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"BadWolfQuiz-minigame-question-availability-{Guid.NewGuid():N}");

    public MinigameQuestionAvailabilityStoreTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Disabled_questions_stay_in_catalog_but_are_excluded_from_runtime_deck()
    {
        var legacy = Path.Combine(_root, "empty-legacy");
        Directory.CreateDirectory(legacy);
        var factory = CreateFactory();
        var catalog = new MinigameCatalogStore(factory, defaultCardCount: 10, legacy);
        var availability = new MinigameQuestionAvailabilityStore(factory);

        await catalog.CreateQuestionAsync("Question one?");
        await catalog.CreateQuestionAsync("Question two?");
        await catalog.CreateQuestionAsync("Question three?");
        await catalog.CreateQuestionAsync("Question four?");

        var questions = await catalog.GetQuestionItemsAsync();
        var disabled = questions.Single(question => question.Text == "Question two?");

        Assert.True(await availability.SetEnabledAsync(disabled.Id, enabled: false));

        Assert.Contains(disabled.Id, await availability.GetDisabledQuestionIdsAsync());
        Assert.Equal(3, await availability.GetEnabledQuestionCountAsync());
        Assert.Equal(
            new[] { "Question one?", "Question three?", "Question four?" },
            await availability.GetEnabledQuestionsAsync());
        Assert.Equal(4, (await catalog.GetQuestionItemsAsync()).Count);

        Assert.True(await availability.SetEnabledAsync(disabled.Id, enabled: true));
        Assert.Empty(await availability.GetDisabledQuestionIdsAsync());
        Assert.Equal(4, await availability.GetEnabledQuestionCountAsync());
    }

    [Fact]
    public async Task Deleting_question_removes_disabled_marker_via_foreign_key()
    {
        var legacy = Path.Combine(_root, "delete-legacy");
        Directory.CreateDirectory(legacy);
        var factory = CreateFactory();
        var catalog = new MinigameCatalogStore(factory, defaultCardCount: 10, legacy);
        var availability = new MinigameQuestionAvailabilityStore(factory);

        await catalog.CreateQuestionAsync("Delete me?");
        var question = Assert.Single(await catalog.GetQuestionItemsAsync());
        Assert.True(await availability.SetEnabledAsync(question.Id, enabled: false));
        Assert.True(await catalog.DeleteQuestionAsync(question.Id));

        Assert.Empty(await availability.GetDisabledQuestionIdsAsync());
    }

    private QuizDbContextFactory CreateFactory()
    {
        var databasePath = Path.Combine(_root, "catalog.db");
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        return new QuizDbContextFactory(options);
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
