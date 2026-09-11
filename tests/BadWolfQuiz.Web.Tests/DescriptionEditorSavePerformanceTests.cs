using System.Data.Common;
using System.Security.Claims;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BadWolfQuiz.Web.Tests;

public sealed class DescriptionEditorSavePerformanceTests
{
    [Fact]
    public async Task Description_edit_metadata_queries_skip_blobs_and_category_update_preserves_media()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        var accessor = CreateAccessor("host-a");

        int categoryId;
        int categoryBlockId;
        int roundId;
        byte[] categoryMedia = Enumerable.Range(0, 1024 * 1024)
            .Select(index => (byte)(index % 251))
            .ToArray();
        byte[] roundMedia = Enumerable.Range(0, 512 * 1024)
            .Select(index => (byte)(index % 239))
            .ToArray();

        await using (var seedDb = new QuizDbContext(options, accessor))
        {
            await seedDb.Database.EnsureCreatedAsync();
            var host = new HostAccount
            {
                Id = "host-a",
                Email = "host-a@example.invalid",
                NormalizedEmail = "HOST-A@EXAMPLE.INVALID",
                PasswordHash = "hash"
            };
            var quiz = new Quiz { HostId = host.Id, Title = "Quiz" };
            var round = new QuizRound { Title = "Round", SortOrder = 1 };
            round.DescriptionBlocks.Add(new RoundDescriptionContentBlock
            {
                BlockType = ContentBlockType.Audio,
                SortOrder = 1,
                FileData = roundMedia,
                FileContentType = "audio/mpeg",
                FileName = "round.mp3"
            });
            var category = new QuizCategory
            {
                Title = "Category",
                SortOrder = 1,
                ColorMode = QuizCategoryColorMode.Automatic
            };
            var categoryBlock = new CategoryDescriptionContentBlock
            {
                BlockType = ContentBlockType.Image,
                SortOrder = 1,
                TextContent = "Before",
                FileData = categoryMedia,
                FileContentType = "image/png",
                FileName = "category.png"
            };
            category.DescriptionBlocks.Add(categoryBlock);
            round.Categories.Add(category);
            quiz.Rounds.Add(round);
            host.Quizzes.Add(quiz);
            seedDb.Hosts.Add(host);
            await seedDb.SaveChangesAsync();
            roundId = round.Id;
            categoryId = category.Id;
            categoryBlockId = categoryBlock.Id;
        }

        var interceptor = new ResultColumnInterceptor();
        var measuredOptions = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        await using (var db = new QuizDbContext(measuredOptions, accessor))
        {
            var categorySnapshots = await DescriptionEditorModel
                .GetCategoryDescriptionBlockEditMetadataQuery(db, categoryId)
                .ToListAsync();
            var roundSnapshots = await DescriptionEditorModel
                .GetRoundDescriptionBlockEditMetadataQuery(db, roundId)
                .ToListAsync();

            Assert.Single(categorySnapshots);
            Assert.Single(roundSnapshots);
            Assert.Equal(2, interceptor.ReaderCommandCount);
            Assert.DoesNotContain(
                interceptor.ResultColumnNames.SelectMany(columns => columns),
                column => string.Equals(
                    column,
                    nameof(ContentBlockBase.FileData),
                    StringComparison.OrdinalIgnoreCase));

            var trackedBlock = DescriptionEditorModel
                .AttachCategoryDescriptionBlockForUpdate(
                    db,
                    categoryId,
                    categorySnapshots.Single());
            trackedBlock.TextContent = "After";

            var category = await db.QuizCategories.SingleAsync(x => x.Id == categoryId);
            category.ColorMode = QuizCategoryColorMode.Custom;
            category.CustomColor = "#EB2424";
            await db.SaveChangesAsync();
        }

        await using (var verifyDb = new QuizDbContext(options, accessor))
        {
            var savedBlock = await verifyDb.CategoryDescriptionContentBlocks
                .AsNoTracking()
                .Where(x => x.Id == categoryBlockId)
                .Select(x => new
                {
                    x.TextContent,
                    x.FileData,
                    x.FileContentType,
                    x.FileName
                })
                .SingleAsync();
            Assert.Equal("After", savedBlock.TextContent);
            Assert.Equal(categoryMedia, savedBlock.FileData);
            Assert.Equal("image/png", savedBlock.FileContentType);
            Assert.Equal("category.png", savedBlock.FileName);

            var savedCategory = await verifyDb.QuizCategories
                .AsNoTracking()
                .SingleAsync(x => x.Id == categoryId);
            Assert.Equal(QuizCategoryColorMode.Custom, savedCategory.ColorMode);
            Assert.Equal("#EB2424", savedCategory.CustomColor);
        }
    }

    private static HttpContextAccessor CreateAccessor(string hostId)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, hostId)],
                "Test"))
        };
        return new HttpContextAccessor { HttpContext = context };
    }

    private sealed class ResultColumnInterceptor : DbCommandInterceptor
    {
        public int ReaderCommandCount { get; private set; }
        public List<string[]> ResultColumnNames { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderCommandCount++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            ResultColumnNames.Add(Enumerable.Range(0, result.FieldCount)
                .Select(result.GetName)
                .ToArray());
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}
