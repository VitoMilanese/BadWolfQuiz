using System.Security.Claims;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuizPackageServiceTests
{
    [Fact]
    public async Task Export_import_round_trip_preserves_round_and_category_descriptions()
    {
        const string hostId = "host-365";
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, hostId)], "test"))
        };
        await using var db = new QuizDbContext(
            options,
            new HttpContextAccessor { HttpContext = httpContext });
        await db.Database.EnsureCreatedAsync();

        db.Hosts.Add(new HostAccount
        {
            Id = hostId,
            Email = "package@example.com",
            NormalizedEmail = "PACKAGE@EXAMPLE.COM",
            PasswordHash = "test"
        });

        var quiz = new Quiz
        {
            HostId = hostId,
            Title = "Package descriptions",
            Description = "Quiz description"
        };
        var round = new QuizRound
        {
            Title = "Round 1",
            SortOrder = 0,
            DefaultTimeLimitSeconds = 30,
            DefaultBuzzMode = BuzzActivationMode.Manual
        };
        round.Rows.Add(new QuizRoundRow { RowIndex = 0, Points = 100 });
        round.DescriptionBlocks.Add(new RoundDescriptionContentBlock
        {
            BlockType = ContentBlockType.Image,
            TextContent = "Round intro",
            TopCaption = "Round top",
            BottomCaption = "Round bottom",
            MediaPath = "legacy/round.png",
            ExternalUrl = "https://example.invalid/round",
            SortOrder = 2,
            FileData = [1, 2, 3, 4],
            FileContentType = "image/png",
            FileName = "round.png",
            Autoplay = true
        });

        var category = new QuizCategory
        {
            Title = "Category 1",
            SortOrder = 0
        };
        category.DescriptionBlocks.Add(new CategoryDescriptionContentBlock
        {
            BlockType = ContentBlockType.Audio,
            TextContent = "Category intro",
            TopCaption = "Category top",
            BottomCaption = "Category bottom",
            SortOrder = 1,
            AudioOnly = true,
            FileData = [5, 6, 7, 8],
            FileContentType = "audio/mpeg",
            FileName = "category.mp3",
            Autoplay = true
        });

        var question = new QuizQuestion
        {
            RowIndex = 0,
            BuzzModeOverride = BuzzActivationMode.UseRoundDefault,
            PresentationType = QuestionPresentationType.Standard
        };
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            BlockType = ContentBlockType.Text,
            TextContent = "Question",
            SortOrder = 0
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = ContentBlockType.Text,
            TextContent = "Answer",
            SortOrder = 0
        });
        category.Questions.Add(question);
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        var service = new QuizPackageService(db);
        var package = await service.ExportAsync(quiz.Id, CancellationToken.None);
        Assert.NotNull(package);

        await using (package)
        {
            var imported = await service.ImportAsync(
                package,
                package.Length,
                hostId,
                CancellationToken.None);

            var importedId = imported.Id;
            db.ChangeTracker.Clear();
            var persisted = await db.Quizzes
                .AsSplitQuery()
                .Include(item => item.Rounds).ThenInclude(item => item.DescriptionBlocks)
                .Include(item => item.Rounds).ThenInclude(item => item.Categories)
                    .ThenInclude(item => item.DescriptionBlocks)
                .Include(item => item.Rounds).ThenInclude(item => item.Categories)
                    .ThenInclude(item => item.Questions)
                        .ThenInclude(item => item.QuestionBlocks)
                .Include(item => item.Rounds).ThenInclude(item => item.Categories)
                    .ThenInclude(item => item.Questions)
                        .ThenInclude(item => item.AnswerBlocks)
                .SingleAsync(item => item.Id == importedId);

            var importedRound = Assert.Single(persisted.Rounds);
            var roundDescription = Assert.Single(importedRound.DescriptionBlocks);
            Assert.Equal(ContentBlockType.Image, roundDescription.BlockType);
            Assert.Equal("Round intro", roundDescription.TextContent);
            Assert.Equal("Round top", roundDescription.TopCaption);
            Assert.Equal("Round bottom", roundDescription.BottomCaption);
            Assert.Equal("legacy/round.png", roundDescription.MediaPath);
            Assert.Equal("https://example.invalid/round", roundDescription.ExternalUrl);
            Assert.Equal(2, roundDescription.SortOrder);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, roundDescription.FileData);
            Assert.Equal("image/png", roundDescription.FileContentType);
            Assert.Equal("round.png", roundDescription.FileName);
            Assert.True(roundDescription.Autoplay);

            var importedCategory = Assert.Single(importedRound.Categories);
            var categoryDescription = Assert.Single(importedCategory.DescriptionBlocks);
            Assert.Equal(ContentBlockType.Audio, categoryDescription.BlockType);
            Assert.Equal("Category intro", categoryDescription.TextContent);
            Assert.Equal("Category top", categoryDescription.TopCaption);
            Assert.Equal("Category bottom", categoryDescription.BottomCaption);
            Assert.Equal(1, categoryDescription.SortOrder);
            Assert.True(categoryDescription.AudioOnly);
            Assert.Equal(new byte[] { 5, 6, 7, 8 }, categoryDescription.FileData);
            Assert.Equal("audio/mpeg", categoryDescription.FileContentType);
            Assert.Equal("category.mp3", categoryDescription.FileName);
            Assert.True(categoryDescription.Autoplay);

            var importedQuestion = Assert.Single(importedCategory.Questions);
            Assert.Equal("Question", Assert.Single(importedQuestion.QuestionBlocks).TextContent);
            Assert.Equal("Answer", Assert.Single(importedQuestion.AnswerBlocks).TextContent);
        }
    }
}
