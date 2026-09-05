using System.Data.Common;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionCopyDestinationLoadingTests
{
    [Fact]
    public async Task GetDestinationsAsync_uses_bounded_metadata_queries_without_returning_media_payload_columns()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var seedOptions = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var seedDb = new QuizDbContext(seedOptions))
        {
            await seedDb.Database.EnsureCreatedAsync();
            seedDb.Hosts.Add(CreateHost("host-a"));

            var source = CreateQuiz(
                "host-a",
                "Source",
                CreateFilledQuestion("Source question"));
            var blank = CreateQuiz(
                "host-a",
                "Blank target",
                CreateBlankQuestion());
            var mediaHeavy = CreateQuiz(
                "host-a",
                "Media-heavy target",
                CreateMediaQuestion());
            seedDb.Quizzes.AddRange(source, blank, mediaHeavy);
            await seedDb.SaveChangesAsync();

            var sourceQuestionId = source.Rounds.Single()
                .Categories.Single()
                .Questions.Single().Id;

            var interceptor = new DestinationQueryInterceptor();
            var measuredOptions = new DbContextOptionsBuilder<QuizDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptor)
                .Options;
            await using var measuredDb = new QuizDbContext(measuredOptions);

            var destinations = await QuestionCopyOperations.GetDestinationsAsync(
                measuredDb,
                "host-a",
                sourceQuestionId,
                1,
                CancellationToken.None);

            Assert.NotNull(destinations);
            Assert.False(Assert.Single(
                destinations,
                destination => destination.QuizTitle == "Source").HasCapacity);
            Assert.True(Assert.Single(
                destinations,
                destination => destination.QuizTitle == "Blank target").HasCapacity);
            Assert.False(Assert.Single(
                destinations,
                destination => destination.QuizTitle == "Media-heavy target").HasCapacity);

            Assert.Equal(4, interceptor.ReaderCommandCount);
            Assert.DoesNotContain(
                interceptor.ResultColumnNames.SelectMany(columns => columns),
                column => string.Equals(
                    column,
                    nameof(ContentBlockBase.FileData),
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Copy_dialog_destination_loading_has_timeout_retry_and_abort_support()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "question-copy-action.js"));

        Assert.Contains("destinationLoadTimeoutMs = 15000", script, StringComparison.Ordinal);
        Assert.Contains("new AbortController()", script, StringComparison.Ordinal);
        Assert.Contains("signal: controller.signal", script, StringComparison.Ordinal);
        Assert.Contains("retryButton.hidden = !canRetry", script, StringComparison.Ordinal);
        Assert.Contains("setStatus(labels.loadError, \"error\", true)", script, StringComparison.Ordinal);
        Assert.Contains("destinationAbortController?.abort()", script, StringComparison.Ordinal);
    }

    private static HostAccount CreateHost(string id) => new()
    {
        Id = id,
        Email = $"{id}@example.invalid",
        NormalizedEmail = $"{id}@example.invalid".ToUpperInvariant(),
        PasswordHash = "hash"
    };

    private static Quiz CreateQuiz(
        string hostId,
        string title,
        QuizQuestion question)
    {
        var quiz = new Quiz
        {
            HostId = hostId,
            Title = title
        };
        var round = new QuizRound
        {
            Title = $"{title} round",
            SortOrder = 1
        };
        round.Rows.Add(new QuizRoundRow
        {
            RowIndex = 1,
            Points = 200
        });
        var category = new QuizCategory
        {
            Title = $"{title} category",
            SortOrder = 1
        };
        category.Questions.Add(question);
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        return quiz;
    }

    private static QuizQuestion CreateFilledQuestion(string text)
    {
        var question = new QuizQuestion { RowIndex = 1 };
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            BlockType = ContentBlockType.Text,
            SortOrder = 1,
            TextContent = text
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = ContentBlockType.Text,
            SortOrder = 1
        });
        return question;
    }

    private static QuizQuestion CreateBlankQuestion()
    {
        var question = new QuizQuestion { RowIndex = 1 };
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            BlockType = ContentBlockType.Text,
            SortOrder = 1,
            TextContent = "   ",
            FileData = Array.Empty<byte>()
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = ContentBlockType.Text,
            SortOrder = 1,
            TopCaption = "  "
        });
        return question;
    }

    private static QuizQuestion CreateMediaQuestion()
    {
        var question = new QuizQuestion { RowIndex = 1 };
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            BlockType = ContentBlockType.Image,
            SortOrder = 1,
            FileData = new byte[512 * 1024],
            FileContentType = "image/png",
            FileName = "large.png"
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = ContentBlockType.Text,
            SortOrder = 1
        });
        return question;
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var parts = new string[relativeParts.Length + 1];
            parts[0] = directory.FullName;
            Array.Copy(relativeParts, 0, parts, 1, relativeParts.Length);
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find repository file: {Path.Combine(relativeParts)}");
    }

    private sealed class DestinationQueryInterceptor : DbCommandInterceptor
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
            return base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
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
            return base.ReaderExecutedAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }
    }
}
