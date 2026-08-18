using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuizCloneOperationsTests
{
    [Fact]
    public async Task CloneAsync_creates_independent_unpublished_quiz_without_history_or_ratings()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var source = CreateSourceQuiz();
        db.Quizzes.Add(source);
        await db.SaveChangesAsync();

        var session = new GameSession
        {
            QuizId = source.Id,
            PublicCode = "CLONE1",
            Status = GameSessionStatus.Finished
        };
        db.GameSessions.Add(session);
        await db.SaveChangesAsync();
        db.QuizRatings.Add(new QuizRating
        {
            QuizId = source.Id,
            GameSessionId = session.Id,
            RaterKey = "player:Rose",
            Score = 5
        });
        await db.SaveChangesAsync();

        var sourceId = source.Id;
        var sourceRoundId = source.Rounds.Single().Id;
        var sourceCategoryId = source.Rounds.Single().Categories.Single().Id;
        var sourceQuestionId = source.Rounds.Single().Categories.Single().Questions.Single().Id;
        var sourceQuestionBlockId = source.Rounds.Single().Categories.Single().Questions.Single()
            .QuestionBlocks.Single().Id;
        db.ChangeTracker.Clear();

        var clone = await QuizCloneOperations.CloneAsync(
            db,
            sourceId,
            "Independent clone",
            CancellationToken.None);

        Assert.NotNull(clone);
        Assert.NotEqual(sourceId, clone.Id);
        Assert.Equal("Independent clone", clone.Title);
        Assert.Equal("Source description", clone.Description);
        Assert.False(clone.IsPublic);
        Assert.Null(clone.PublishedAtUtc);
        Assert.Null(clone.LastPlayedAtUtc);
        Assert.False(clone.IsArchived);
        Assert.Equal(QuizMediaState.Active, clone.MediaState);
        Assert.Null(clone.CurrentArchiveOperationId);
        Assert.Equal(0, clone.ArchivedMediaCount);
        Assert.Equal(0, clone.ArchivedMediaBytes);
        Assert.Null(clone.MediaArchivedAtUtc);
        Assert.Null(clone.MediaRestoredAtUtc);
        Assert.Null(clone.MediaArchiveFailureReason);
        Assert.True(clone.PreventAutomaticArchiving);

        var cloneRound = Assert.Single(clone.Rounds);
        Assert.NotEqual(sourceRoundId, cloneRound.Id);
        Assert.Equal(45, cloneRound.DefaultTimeLimitSeconds);
        Assert.Equal(BuzzActivationMode.AfterDelay, cloneRound.DefaultBuzzMode);
        Assert.True(cloneRound.UseRandomWagerQuestions);
        Assert.Equal(2, cloneRound.RandomWagerQuestionCount);
        Assert.Equal(500, Assert.Single(cloneRound.Rows).Points);
        Assert.Equal("Round description", Assert.Single(cloneRound.DescriptionBlocks).TextContent);

        var cloneCategory = Assert.Single(cloneRound.Categories);
        Assert.NotEqual(sourceCategoryId, cloneCategory.Id);
        Assert.Equal("Category description", Assert.Single(cloneCategory.DescriptionBlocks).TextContent);

        var cloneQuestion = Assert.Single(cloneCategory.Questions);
        Assert.NotEqual(sourceQuestionId, cloneQuestion.Id);
        Assert.Equal(20, cloneQuestion.TimeLimitSecondsOverride);
        Assert.Equal(BuzzActivationMode.AfterMedia, cloneQuestion.BuzzModeOverride);
        Assert.Equal(3, cloneQuestion.BuzzDelaySeconds);
        Assert.True(cloneQuestion.IsSpecial);
        Assert.True(cloneQuestion.ExcludeFromRandomWagerSelection);

        var cloneQuestionBlock = Assert.Single(cloneQuestion.QuestionBlocks);
        Assert.NotEqual(sourceQuestionBlockId, cloneQuestionBlock.Id);
        Assert.Equal(ContentBlockType.Image, cloneQuestionBlock.BlockType);
        Assert.Equal("Question text", cloneQuestionBlock.TextContent);
        Assert.Equal("Top", cloneQuestionBlock.TopCaption);
        Assert.Equal("Bottom", cloneQuestionBlock.BottomCaption);
        Assert.Equal("legacy/path.png", cloneQuestionBlock.MediaPath);
        Assert.Equal("https://example.invalid/media", cloneQuestionBlock.ExternalUrl);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, cloneQuestionBlock.FileData);
        Assert.Equal("image/png", cloneQuestionBlock.FileContentType);
        Assert.Equal("question.png", cloneQuestionBlock.FileName);
        Assert.True(cloneQuestionBlock.Autoplay);

        Assert.Equal("Answer", Assert.Single(cloneQuestion.AnswerBlocks).TextContent);
        Assert.Equal("Final question", Assert.Single(clone.FinalQuestionBlocks).TextContent);
        Assert.Equal("Final answer", Assert.Single(clone.FinalAnswerBlocks).TextContent);

        Assert.Single(await db.GameSessions.Where(item => item.QuizId == sourceId).ToListAsync());
        Assert.Empty(await db.GameSessions.Where(item => item.QuizId == clone.Id).ToListAsync());
        Assert.Single(await db.QuizRatings.Where(item => item.QuizId == sourceId).ToListAsync());
        Assert.Empty(await db.QuizRatings.Where(item => item.QuizId == clone.Id).ToListAsync());

        cloneQuestionBlock.TextContent = "Changed clone text";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var sourceText = await db.QuestionContentBlocks
            .Where(block => block.Id == sourceQuestionBlockId)
            .Select(block => block.TextContent)
            .SingleAsync();
        Assert.Equal("Question text", sourceText);
    }

    [Fact]
    public async Task CloneAsync_rejects_sources_without_active_media()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var source = CreateSourceQuiz();
        source.MediaState = QuizMediaState.Archived;
        db.Quizzes.Add(source);
        await db.SaveChangesAsync();

        Assert.Null(await QuizCloneOperations.CloneAsync(
            db,
            source.Id,
            "Clone",
            CancellationToken.None));
    }

    [Fact]
    public void Clone_action_is_inserted_immediately_after_edit_and_posts_a_new_name()
    {
        var root = FindRepositoryRoot();
        var bootstrap = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "quiz-clone-action.js"));
        var endpoint = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Clone.cshtml.cs"));

        Assert.Contains(".quiz-list .quiz-action-menu", bootstrap, StringComparison.Ordinal);
        Assert.Contains("/js/quiz-clone-action.js", bootstrap, StringComparison.Ordinal);
        Assert.Contains("/Admin\\/Quizzes\\/Editor", script, StringComparison.Ordinal);
        Assert.Contains("insertAdjacentElement(\"afterend\", cloneButton)", script, StringComparison.Ordinal);
        Assert.Contains("cloneButton.className = \"action-menu-item\"", script, StringComparison.Ordinal);
        Assert.Contains("nameInput.name = \"title\"", script, StringComparison.Ordinal);
        Assert.Contains("nameInput.required = true", script, StringComparison.Ordinal);
        Assert.Contains("quizId.name = \"quizId\"", script, StringComparison.Ordinal);
        Assert.Contains("form.method = \"post\"", script, StringComparison.Ordinal);
        Assert.Contains("QuizCloneOperations.CloneAsync", endpoint, StringComparison.Ordinal);
        Assert.Contains("RedirectToPage(\"Editor\"", endpoint, StringComparison.Ordinal);
    }

    private static Quiz CreateSourceQuiz()
    {
        var source = new Quiz
        {
            Title = "Source quiz",
            Description = "Source description",
            IsPublic = true,
            PublishedAtUtc = DateTime.UtcNow.AddDays(-2),
            LastPlayedAtUtc = DateTime.UtcNow.AddDays(-1),
            PreventAutomaticArchiving = true,
            CurrentArchiveOperationId = Guid.NewGuid(),
            ArchivedMediaCount = 3,
            ArchivedMediaBytes = 1234,
            MediaArchivedAtUtc = DateTime.UtcNow.AddDays(-4),
            MediaRestoredAtUtc = DateTime.UtcNow.AddDays(-3),
            MediaArchiveFailureReason = "old failure"
        };
        source.FinalQuestionBlocks.Add(new FinalQuestionContentBlock
        {
            BlockType = ContentBlockType.Text,
            SortOrder = 1,
            TextContent = "Final question"
        });
        source.FinalAnswerBlocks.Add(new FinalAnswerContentBlock
        {
            BlockType = ContentBlockType.Text,
            SortOrder = 1,
            TextContent = "Final answer"
        });

        var round = new QuizRound
        {
            Title = "Round",
            SortOrder = 1,
            DefaultTimeLimitSeconds = 45,
            DefaultBuzzMode = BuzzActivationMode.AfterDelay,
            UseRandomWagerQuestions = true,
            RandomWagerQuestionCount = 2
        };
        round.Rows.Add(new QuizRoundRow { RowIndex = 1, Points = 500 });
        round.DescriptionBlocks.Add(new RoundDescriptionContentBlock
        {
            BlockType = ContentBlockType.Text,
            SortOrder = 1,
            TextContent = "Round description"
        });

        var category = new QuizCategory { Title = "Category", SortOrder = 1 };
        category.DescriptionBlocks.Add(new CategoryDescriptionContentBlock
        {
            BlockType = ContentBlockType.Text,
            SortOrder = 1,
            TextContent = "Category description"
        });
        var question = new QuizQuestion
        {
            RowIndex = 1,
            TimeLimitSecondsOverride = 20,
            BuzzModeOverride = BuzzActivationMode.AfterMedia,
            BuzzDelaySeconds = 3,
            IsSpecial = true,
            PresentationType = QuestionPresentationType.Standard,
            ExcludeFromRandomWagerSelection = true
        };
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            BlockType = ContentBlockType.Image,
            SortOrder = 1,
            TextContent = "Question text",
            TopCaption = "Top",
            BottomCaption = "Bottom",
            MediaPath = "legacy/path.png",
            ExternalUrl = "https://example.invalid/media",
            FileData = new byte[] { 1, 2, 3, 4 },
            FileContentType = "image/png",
            FileName = "question.png",
            Autoplay = true
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = ContentBlockType.YouTube,
            SortOrder = 1,
            TextContent = "Answer",
            ExternalUrl = "https://youtu.be/example"
        });
        category.Questions.Add(question);
        round.Categories.Add(category);
        source.Rounds.Add(round);
        return source;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
