using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuizMediaArchiveServiceTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "badwolf-archive-tests", Guid.NewGuid().ToString("N"));
    private TestDbContextFactory<QuizDbContext> _mainFactory = null!;
    private TestDbContextFactory<ArchiveDbContext> _archiveFactory = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        var mainOptions = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_directory, "main.db")}").Options;
        var archiveOptions = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_directory, "archive.db")}").Options;
        _mainFactory = new(() => new QuizDbContext(mainOptions));
        _archiveFactory = new(() => new ArchiveDbContext(archiveOptions));
        await using var main = await _mainFactory.CreateDbContextAsync();
        await using var archive = await _archiveFactory.CreateDbContextAsync();
        await main.Database.EnsureCreatedAsync();
        await archive.Database.EnsureCreatedAsync();
        await SeedAsync(main);
    }

    [Fact]
    public async Task ArchiveAndRestore_RoundTripsAllBlobRoles()
    {
        var service = CreateService();
        var archived = await service.ArchiveAsync(1, "host-1");
        Assert.True(archived.Succeeded);
        Assert.Equal(4, archived.MediaCount);

        await using (var main = await _mainFactory.CreateDbContextAsync())
        {
            var quiz = await main.Quizzes.IgnoreQueryFilters().SingleAsync(x => x.Id == 1);
            Assert.Equal(QuizMediaState.Archived, quiz.MediaState);
            Assert.Null(await main.QuestionContentBlocks.IgnoreQueryFilters().Where(x => x.Id == 1).Select(x => x.FileData).SingleAsync());
            Assert.Null(await main.AnswerContentBlocks.IgnoreQueryFilters().Where(x => x.Id == 1).Select(x => x.FileData).SingleAsync());
        }
        await using (var archive = await _archiveFactory.CreateDbContextAsync())
        {
            var items = await archive.ArchivedQuizMedia.ToListAsync();
            Assert.Equal(4, items.Count);
            Assert.All(items, item => Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(item.Data)), item.Sha256));
        }

        var restored = await service.RestoreAsync(1, "host-1");
        Assert.True(restored.Succeeded);
        await using var restoredMain = await _mainFactory.CreateDbContextAsync();
        Assert.Equal(QuizMediaState.Active, (await restoredMain.Quizzes.IgnoreQueryFilters().SingleAsync(x => x.Id == 1)).MediaState);
        Assert.Equal([1, 2, 3], await restoredMain.QuestionContentBlocks.IgnoreQueryFilters().Where(x => x.Id == 1).Select(x => x.FileData).SingleAsync());
    }

    [Fact]
    public async Task Archive_WrongHostCannotReadOrChangeQuiz()
    {
        var result = await CreateService().ArchiveAsync(1, "host-2");
        Assert.False(result.Succeeded);
        Assert.Equal("not-found", result.Code);
        await using var main = await _mainFactory.CreateDbContextAsync();
        Assert.Equal(QuizMediaState.Active, (await main.Quizzes.IgnoreQueryFilters().SingleAsync(x => x.Id == 1)).MediaState);
    }

    [Fact]
    public async Task Restore_CorruptArchiveDoesNotPartiallyRestore()
    {
        var service = CreateService();
        Assert.True((await service.ArchiveAsync(1, "host-1")).Succeeded);
        await using (var archive = await _archiveFactory.CreateDbContextAsync())
        {
            var item = await archive.ArchivedQuizMedia.FirstAsync();
            item.Data = [99];
            await archive.SaveChangesAsync();
        }
        var result = await service.RestoreAsync(1, "host-1");
        Assert.False(result.Succeeded);
        await using var main = await _mainFactory.CreateDbContextAsync();
        Assert.All(await main.QuestionContentBlocks.IgnoreQueryFilters().Select(x => x.FileData).ToListAsync(), Assert.Null);
        Assert.Equal(QuizMediaState.Failed, (await main.Quizzes.IgnoreQueryFilters().SingleAsync(x => x.Id == 1)).MediaState);
    }

    [Fact]
    public async Task Archive_ResumedOperationIsIdempotent()
    {
        var operationId = Guid.NewGuid();
        await using (var main = await _mainFactory.CreateDbContextAsync())
        {
            await main.Quizzes.IgnoreQueryFilters().Where(x => x.Id == 1).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.MediaState, QuizMediaState.Archiving)
                .SetProperty(x => x.CurrentArchiveOperationId, operationId));
        }
        var service = CreateService();
        Assert.True((await service.ArchiveAsync(1, "host-1")).Succeeded);
        Assert.True((await service.ArchiveAsync(1, "host-1")).Succeeded);
        await using var archive = await _archiveFactory.CreateDbContextAsync();
        Assert.Equal(4, await archive.ArchivedQuizMedia.CountAsync());
    }

    [Fact]
    public async Task RepeatedArchiveRestore_ReusesOperationWithoutAccumulatingMedia()
    {
        var service = CreateService();
        Assert.True((await service.ArchiveAsync(1, "host-1")).Succeeded);

        Guid operationId;
        await using (var main = await _mainFactory.CreateDbContextAsync())
        {
            operationId = (await main.Quizzes.IgnoreQueryFilters().SingleAsync(x => x.Id == 1))
                .CurrentArchiveOperationId!.Value;
        }

        for (var cycle = 0; cycle < 3; cycle++)
        {
            Assert.True((await service.RestoreAsync(1, "host-1")).Succeeded);
            Assert.True((await service.ArchiveAsync(1, "host-1")).Succeeded);

            await using var archive = await _archiveFactory.CreateDbContextAsync();
            Assert.Equal(4, await archive.ArchivedQuizMedia.CountAsync());
            Assert.Equal(1, await archive.ArchiveOperations.CountAsync());
            Assert.All(
                await archive.ArchivedQuizMedia.ToListAsync(),
                item => Assert.Equal(operationId, item.OperationId));
        }
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        return Task.CompletedTask;
    }

    private QuizMediaArchiveService CreateService() => new(
        _mainFactory, _archiveFactory, NullLogger<QuizMediaArchiveService>.Instance);

    private static async Task SeedAsync(QuizDbContext db)
    {
        var host = new HostAccount { Id = "host-1", Email = "host@example.com", NormalizedEmail = "HOST@EXAMPLE.COM", PasswordHash = "hash" };
        var quiz = new Quiz { Id = 1, HostId = host.Id, Title = "Archive test" };
        var round = new QuizRound { Id = 1, Quiz = quiz, Title = "Round", SortOrder = 1 };
        var category = new QuizCategory { Id = 1, Round = round, Title = "Category", SortOrder = 1 };
        var question = new QuizQuestion { Id = 1, Category = category, RowIndex = 1 };
        question.QuestionBlocks.Add(new QuestionContentBlock { Id = 1, BlockType = ContentBlockType.Image, FileData = [1, 2, 3], FileContentType = "image/png", FileName = "q.png" });
        question.AnswerBlocks.Add(new AnswerContentBlock { Id = 1, BlockType = ContentBlockType.Audio, FileData = [4, 5], FileContentType = "audio/mpeg", FileName = "a.mp3" });
        quiz.FinalQuestionBlocks.Add(new FinalQuestionContentBlock { Id = 1, BlockType = ContentBlockType.Video, FileData = [6, 7, 8] });
        quiz.FinalAnswerBlocks.Add(new FinalAnswerContentBlock { Id = 1, BlockType = ContentBlockType.Image, FileData = [9, 10] });
        db.Hosts.Add(host); db.Quizzes.Add(quiz); db.QuizRounds.Add(round); db.QuizCategories.Add(category); db.QuizQuestions.Add(question);
        await db.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory<TContext>(Func<TContext> factory) : IDbContextFactory<TContext>
        where TContext : DbContext
    {
        public TContext CreateDbContext() => factory();
        public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(factory());
    }
}
