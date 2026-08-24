using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BadWolfQuiz.Web.Tests;

public sealed class DeferredGameMediaStoreTests : IDisposable
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"bad-wolf-deferred-media-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task MaterializeAsync_restores_real_media_bytes_from_database()
    {
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite($"Data Source={databasePath};Foreign Keys=False;Pooling=False")
            .Options;
        var quiz = CreateQuiz();

        await using (var db = new QuizDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Quizzes.Add(quiz);
            await db.SaveChangesAsync();
        }

        var snapshot = new QuizSnapshotFactory().CreateFromDetachedQuiz(quiz);
        var deferredBlock = snapshot.Rounds.Single().Questions.Single()
            .AnswerBlocks.Single();
        Assert.True(DeferredGameMediaStore.IsDeferred(deferredBlock.FileData));

        var store = new DeferredGameMediaStore(
            new QuizDbContextFactory(options),
            NullLogger<DeferredGameMediaStore>.Instance);
        var materialized = await store.MaterializeAsync(
            Guid.NewGuid(),
            snapshot);

        var restoredBlock = materialized.Rounds.Single().Questions.Single()
            .AnswerBlocks.Single();
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, restoredBlock.FileData);
        Assert.Equal("image/png", restoredBlock.FileContentType);
        Assert.Equal("answer.png", restoredBlock.FileName);
        Assert.False(DeferredGameMediaStore.IsDeferred(restoredBlock.FileData));
    }

    public void Dispose()
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private static Quiz CreateQuiz()
    {
        var quiz = new Quiz
        {
            Id = 1,
            HostId = "host-1",
            Title = "Deferred media"
        };
        var round = new QuizRound
        {
            Id = 2,
            QuizId = quiz.Id,
            Quiz = quiz,
            Title = "Round",
            SortOrder = 0
        };
        round.Rows.Add(new QuizRoundRow
        {
            Id = 3,
            QuizRoundId = round.Id,
            Round = round,
            RowIndex = 0,
            Points = 100
        });
        var category = new QuizCategory
        {
            Id = 4,
            QuizRoundId = round.Id,
            Round = round,
            Title = "Category",
            SortOrder = 0
        };
        var question = new QuizQuestion
        {
            Id = 5,
            QuizCategoryId = category.Id,
            Category = category,
            RowIndex = 0
        };
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            Id = 6,
            QuizQuestionId = question.Id,
            Question = question,
            BlockType = ContentBlockType.Text,
            TextContent = "Question",
            SortOrder = 0
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            Id = 7,
            QuizQuestionId = question.Id,
            Question = question,
            BlockType = ContentBlockType.Image,
            FileData = [1, 2, 3, 4],
            FileContentType = "image/png",
            FileName = "answer.png",
            SortOrder = 0
        });

        category.Questions.Add(question);
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        return quiz;
    }
}
