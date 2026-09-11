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

public sealed class QuestionEditorSavePerformanceTests
{
    [Fact]
    public async Task Edit_metadata_queries_skip_blob_columns_and_metadata_update_preserves_media()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        var accessor = CreateAccessor("host-a");

        int questionId;
        int questionBlockId;
        byte[] media = Enumerable.Range(0, 1024 * 1024)
            .Select(index => (byte)(index % 251))
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
            round.Rows.Add(new QuizRoundRow { RowIndex = 1, Points = 200 });
            var category = new QuizCategory { Title = "Category", SortOrder = 1 };
            var question = new QuizQuestion { RowIndex = 1 };
            var questionBlock = new QuestionContentBlock
            {
                BlockType = ContentBlockType.Image,
                SortOrder = 0,
                TextContent = "Before",
                FileData = media,
                FileContentType = "image/png",
                FileName = "image.png"
            };
            question.QuestionBlocks.Add(questionBlock);
            question.AnswerBlocks.Add(new AnswerContentBlock
            {
                BlockType = ContentBlockType.Text,
                SortOrder = 0,
                TextContent = "Answer"
            });
            category.Questions.Add(question);
            round.Categories.Add(category);
            quiz.Rounds.Add(round);
            host.Quizzes.Add(quiz);
            seedDb.Hosts.Add(host);
            await seedDb.SaveChangesAsync();
            questionId = question.Id;
            questionBlockId = questionBlock.Id;
        }

        var interceptor = new ResultColumnInterceptor();
        var measuredOptions = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        await using (var db = new QuizDbContext(measuredOptions, accessor))
        {
            var questionSnapshots = await QuestionEditorModel
                .GetQuestionBlockEditMetadataQuery(db, questionId)
                .ToListAsync();
            var answerSnapshots = await QuestionEditorModel
                .GetAnswerBlockEditMetadataQuery(db, questionId)
                .ToListAsync();

            Assert.Single(questionSnapshots);
            Assert.Single(answerSnapshots);
            Assert.Equal(2, interceptor.ReaderCommandCount);
            Assert.DoesNotContain(
                interceptor.ResultColumnNames.SelectMany(columns => columns),
                column => string.Equals(
                    column,
                    nameof(ContentBlockBase.FileData),
                    StringComparison.OrdinalIgnoreCase));

            var tracked = QuestionEditorModel.AttachQuestionBlockForUpdate(
                db,
                questionId,
                questionSnapshots.Single());
            tracked.TextContent = "After";
            await db.SaveChangesAsync();
        }

        await using (var verifyDb = new QuizDbContext(options, accessor))
        {
            var saved = await verifyDb.QuestionContentBlocks
                .AsNoTracking()
                .Where(x => x.Id == questionBlockId)
                .Select(x => new
                {
                    x.TextContent,
                    x.FileData,
                    x.FileContentType,
                    x.FileName
                })
                .SingleAsync();
            Assert.Equal("After", saved.TextContent);
            Assert.Equal(media, saved.FileData);
            Assert.Equal("image/png", saved.FileContentType);
            Assert.Equal("image.png", saved.FileName);
        }
    }

    [Fact]
    public void Ajax_save_returns_tracked_ids_without_reloading_blocks_after_save()
    {
        var source = File.ReadAllText(FindWebFile(
            "Pages", "Admin", "Quizzes", "QuestionEditor.cshtml.cs"));
        var saveIndex = source.IndexOf(
            "await db.SaveChangesAsync(cancellationToken);",
            StringComparison.Ordinal);
        var successIndex = source.IndexOf(
            "TempData[\"SuccessMessage\"]",
            saveIndex,
            StringComparison.Ordinal);
        Assert.True(saveIndex >= 0 && successIndex > saveIndex);
        var ajaxTail = source[saveIndex..successIndex];
        Assert.DoesNotContain("db.QuestionContentBlocks", ajaxTail, StringComparison.Ordinal);
        Assert.DoesNotContain("db.AnswerContentBlocks", ajaxTail, StringComparison.Ordinal);
        Assert.Contains("persistedQuestionBlocks", ajaxTail, StringComparison.Ordinal);
        Assert.Contains("persistedAnswerBlocks", ajaxTail, StringComparison.Ordinal);
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

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException(Path.Combine(parts));
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
