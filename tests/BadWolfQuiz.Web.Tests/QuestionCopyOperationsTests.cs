using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionCopyOperationsTests
{
    [Fact]
    public async Task CopyAsync_creates_independent_copy_and_extends_target_round()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        db.Hosts.Add(CreateHost("host-a"));
        var source = CreateSourceQuiz("host-a");
        var target = CreateTargetQuiz("host-a", "Target", 1, 2);
        FillQuestion(
            target.Rounds.Single().Categories
                .OrderBy(category => category.SortOrder)
                .First().Questions.Single(),
            "Occupied target slot");
        db.Quizzes.AddRange(source, target);
        await db.SaveChangesAsync();

        var sourceQuestion = source.Rounds.Single().Categories.Single().Questions.Single();
        var sourceQuestionId = sourceQuestion.Id;
        var sourceQuestionBlockId = sourceQuestion.QuestionBlocks.Single().Id;
        var targetRound = target.Rounds.Single();
        var categories = targetRound.Categories.OrderBy(category => category.SortOrder).ToArray();
        var targetCategoryId = categories[0].Id;
        var siblingCategoryId = categories[1].Id;
        var targetRoundId = targetRound.Id;
        var targetQuizId = target.Id;
        db.ChangeTracker.Clear();

        var result = await QuestionCopyOperations.CopyAsync(
            db,
            "host-a",
            sourceQuestionId,
            targetCategoryId,
            3,
            CancellationToken.None);

        Assert.Equal(QuestionCopyStatus.Success, result.Status);
        Assert.Equal(targetQuizId, result.QuizId);
        Assert.Equal(targetRoundId, result.RoundId);
        Assert.Equal(targetCategoryId, result.CategoryId);

        var copied = await db.QuizQuestions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(question => question.QuestionBlocks)
            .Include(question => question.AnswerBlocks)
            .SingleAsync(question => question.Id == result.QuestionId);
        Assert.NotEqual(sourceQuestionId, copied.Id);
        Assert.Equal(2, copied.RowIndex);
        Assert.Equal(20, copied.TimeLimitSecondsOverride);
        Assert.Equal(BuzzActivationMode.AfterMedia, copied.BuzzModeOverride);
        Assert.Equal(3, copied.BuzzDelaySeconds);
        Assert.True(copied.IsSpecial);
        Assert.Equal(sourceQuestion.PresentationType, copied.PresentationType);
        Assert.True(copied.ExcludeFromRandomWagerSelection);

        var copiedBlock = Assert.Single(copied.QuestionBlocks);
        Assert.NotEqual(sourceQuestionBlockId, copiedBlock.Id);
        Assert.Equal(ContentBlockType.Image, copiedBlock.BlockType);
        Assert.Equal("Question text", copiedBlock.TextContent);
        Assert.Equal("Top", copiedBlock.TopCaption);
        Assert.Equal("Bottom", copiedBlock.BottomCaption);
        Assert.Equal("legacy/path.png", copiedBlock.MediaPath);
        Assert.Equal("https://example.invalid/media", copiedBlock.ExternalUrl);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, copiedBlock.FileData);
        Assert.Equal("image/png", copiedBlock.FileContentType);
        Assert.Equal("question.png", copiedBlock.FileName);
        Assert.True(copiedBlock.AudioOnly);
        Assert.True(copiedBlock.Autoplay);
        Assert.Equal("Answer", Assert.Single(copied.AnswerBlocks).TextContent);

        var addedRow = await db.QuizRoundRows
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(row => row.QuizRoundId == targetRoundId && row.RowIndex == 2);
        Assert.Equal(400, addedRow.Points);

        var sibling = await db.QuizQuestions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(question => question.QuestionBlocks)
            .Include(question => question.AnswerBlocks)
            .SingleAsync(question =>
                question.QuizCategoryId == siblingCategoryId &&
                question.RowIndex == 2);
        Assert.Equal(ContentBlockType.Text, Assert.Single(sibling.QuestionBlocks).BlockType);
        Assert.Equal(ContentBlockType.Text, Assert.Single(sibling.AnswerBlocks).BlockType);

        var original = await db.QuizQuestions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(question => question.QuestionBlocks)
            .SingleAsync(question => question.Id == sourceQuestionId);
        Assert.Equal(1, original.RowIndex);
        Assert.Equal("Question text", Assert.Single(original.QuestionBlocks).TextContent);
    }

    [Fact]
    public async Task CopyAsync_reuses_blank_placeholder_allows_same_quiz_clone_and_rejects_content_filled()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        db.Hosts.Add(CreateHost("host-a"));
        var source = CreateSourceQuiz("host-a");
        var target = CreateTargetQuiz("host-a", "Target", 2, 1);
        var targetCategory = target.Rounds.Single().Categories.Single();
        FillQuestion(
            targetCategory.Questions.Single(question => question.RowIndex == 1),
            "Occupied target slot");
        var full = CreateTargetQuiz("host-a", "Full", 2, 1);
        FillAllQuestions(full);
        db.Quizzes.AddRange(source, target, full);
        await db.SaveChangesAsync();

        var sourceQuestion = source.Rounds.Single().Categories.Single().Questions.Single();
        var sourceQuestionId = sourceQuestion.Id;
        var sourceCategoryId = sourceQuestion.QuizCategoryId;
        var sourceQuizId = source.Id;
        var targetCategoryId = targetCategory.Id;
        var targetRoundId = target.Rounds.Single().Id;
        var targetPlaceholderId = targetCategory.Questions
            .Single(question => question.RowIndex == 2).Id;
        var fullCategoryId = full.Rounds.Single().Categories.Single().Id;
        db.ChangeTracker.Clear();

        var filledSlot = await QuestionCopyOperations.CopyAsync(
            db, "host-a", sourceQuestionId, targetCategoryId, 2, CancellationToken.None);
        Assert.Equal(QuestionCopyStatus.Success, filledSlot.Status);
        Assert.Equal(targetPlaceholderId, filledSlot.QuestionId);
        Assert.Equal(2, await db.QuizRoundRows
            .IgnoreQueryFilters()
            .CountAsync(row => row.QuizRoundId == targetRoundId));
        Assert.Equal(2, await db.QuizQuestions
            .IgnoreQueryFilters()
            .CountAsync(question => question.QuizCategoryId == targetCategoryId));

        var reusedPlaceholder = await db.QuizQuestions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(question => question.QuestionBlocks)
            .Include(question => question.AnswerBlocks)
            .SingleAsync(question => question.Id == filledSlot.QuestionId);
        Assert.Equal(2, reusedPlaceholder.RowIndex);
        Assert.Equal(BuzzActivationMode.AfterMedia, reusedPlaceholder.BuzzModeOverride);
        Assert.Equal(ContentBlockType.Image, Assert.Single(reusedPlaceholder.QuestionBlocks).BlockType);
        Assert.Equal("Question text", reusedPlaceholder.QuestionBlocks.Single().TextContent);
        Assert.Equal("Answer", Assert.Single(reusedPlaceholder.AnswerBlocks).TextContent);

        db.ChangeTracker.Clear();
        var sameQuizClone = await QuestionCopyOperations.CopyAsync(
            db, "host-a", sourceQuestionId, sourceCategoryId, 3, CancellationToken.None);
        Assert.Equal(QuestionCopyStatus.Success, sameQuizClone.Status);
        Assert.Equal(sourceQuizId, sameQuizClone.QuizId);
        Assert.Equal(sourceCategoryId, sameQuizClone.CategoryId);
        var clonedQuestion = await db.QuizQuestions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(question => question.Id == sameQuizClone.QuestionId);
        Assert.Equal(sourceCategoryId, clonedQuestion.QuizCategoryId);
        Assert.Equal(2, clonedQuestion.RowIndex);

        db.ChangeTracker.Clear();
        var noCapacity = await QuestionCopyOperations.CopyAsync(
            db, "host-a", sourceQuestionId, fullCategoryId, 2, CancellationToken.None);
        Assert.Equal(QuestionCopyStatus.NoCapacity, noCapacity.Status);
    }

    [Fact]
    public async Task GetDestinationsAsync_treats_blank_placeholders_as_capacity_and_marks_only_content_filled_categories_full()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        db.Hosts.AddRange(CreateHost("host-a"), CreateHost("host-b"));
        var source = CreateSourceQuiz("host-a");
        var available = CreateTargetQuiz("host-a", "Available", 1, 1);
        var partial = CreateTargetQuiz("host-a", "Partial", 2, 1);
        FillQuestion(
            partial.Rounds.Single().Categories.Single().Questions
                .Single(question => question.RowIndex == 1),
            "Occupied partial slot");
        var full = CreateTargetQuiz("host-a", "Full", 2, 1);
        FillAllQuestions(full);
        var archived = CreateTargetQuiz("host-a", "Archived", 1, 1);
        archived.IsArchived = true;
        var otherHost = CreateTargetQuiz("host-b", "Other", 1, 1);
        db.Quizzes.AddRange(source, available, partial, full, archived, otherHost);
        await db.SaveChangesAsync();

        var sourceQuestionId = source.Rounds.Single().Categories.Single().Questions.Single().Id;
        var sourceQuizId = source.Id;
        var archivedId = archived.Id;
        var otherHostId = otherHost.Id;
        var availableId = available.Id;
        var partialId = partial.Id;
        var fullId = full.Id;
        db.ChangeTracker.Clear();

        var destinations = await QuestionCopyOperations.GetDestinationsAsync(
            db, "host-a", sourceQuestionId, 2, CancellationToken.None);

        Assert.NotNull(destinations);
        Assert.True(Assert.Single(destinations.Where(destination =>
            destination.QuizId == sourceQuizId)).HasCapacity);
        Assert.DoesNotContain(destinations, destination => destination.QuizId == archivedId);
        Assert.DoesNotContain(destinations, destination => destination.QuizId == otherHostId);
        Assert.True(Assert.Single(destinations.Where(destination =>
            destination.QuizId == availableId)).HasCapacity);
        Assert.True(Assert.Single(destinations.Where(destination =>
            destination.QuizId == partialId)).HasCapacity);
        Assert.False(Assert.Single(destinations.Where(destination =>
            destination.QuizId == fullId)).HasCapacity);
    }

    [Fact]
    public void Quiz_editor_copy_dialog_closes_uses_save_overlay_and_spaces_three_actions()
    {
        var root = FindRepositoryRoot();
        var bootstrap = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "editor-save-overlay.js"));
        var script = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "question-copy-action.js"));
        var endpoint = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "QuestionCopy.cshtml.cs"));

        Assert.Contains("form.quiz-board-form", bootstrap, StringComparison.Ordinal);
        Assert.Contains("/js/question-copy-action.js", bootstrap, StringComparison.Ordinal);
        Assert.Contains(".question-cell-slot[data-question-id]", script, StringComparison.Ordinal);
        Assert.Contains(".js-question-delete", script, StringComparison.Ordinal);
        Assert.Contains("js-question-copy button button-secondary icon-button", script, StringComparison.Ordinal);
        Assert.Contains("deleteButton.insertAdjacentElement(\"beforebegin\", copyButton)", script, StringComparison.Ordinal);
        Assert.Contains("copyButton.textContent = \"⧉\"", script, StringComparison.Ordinal);
        Assert.Contains("has-question-copy", script, StringComparison.Ordinal);
        Assert.Contains("justify-content: space-evenly", script, StringComparison.Ordinal);
        Assert.Contains("height: 30px", script, StringComparison.Ordinal);
        Assert.Contains("[data-quiz-save-status]", script, StringComparison.Ordinal);
        Assert.Contains("dialog.close();", script, StringComparison.Ordinal);
        Assert.Contains("showQuizOverlay(labels.success)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("setStatus(labels.success", script, StringComparison.Ordinal);
        Assert.Contains("Targets", script, StringComparison.Ordinal);
        Assert.Contains("targetCategoryId", script, StringComparison.Ordinal);
        Assert.Contains("option.disabled = !category.hasCapacity", script, StringComparison.Ordinal);
        Assert.Contains("language === \"ru\"", script, StringComparison.Ordinal);
        Assert.Contains("labelsByLanguage.uk", script, StringComparison.Ordinal);
        Assert.DoesNotContain("form.question-editor", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Копировать", script, StringComparison.Ordinal);
        Assert.Contains("QuestionCopyOperations.GetDestinationsAsync", endpoint, StringComparison.Ordinal);
        Assert.Contains("QuestionCopyOperations.CopyAsync", endpoint, StringComparison.Ordinal);
        Assert.Contains("currentHost.RequiredId", endpoint, StringComparison.Ordinal);
        Assert.Contains("MaximumQuestionCount", endpoint, StringComparison.Ordinal);
    }

    private static async Task<QuizDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static HostAccount CreateHost(string id) => new()
    {
        Id = id,
        Email = $"{id}@example.invalid",
        NormalizedEmail = $"{id}@example.invalid".ToUpperInvariant(),
        PasswordHash = "hash"
    };

    private static Quiz CreateSourceQuiz(string hostId)
    {
        var quiz = new Quiz { HostId = hostId, Title = "Source" };
        var round = new QuizRound { Title = "Source round", SortOrder = 1 };
        round.Rows.Add(new QuizRoundRow { RowIndex = 1, Points = 600 });
        var category = new QuizCategory { Title = "Source category", SortOrder = 1 };
        var question = new QuizQuestion
        {
            RowIndex = 1,
            TimeLimitSecondsOverride = 20,
            BuzzModeOverride = BuzzActivationMode.AfterMedia,
            BuzzDelaySeconds = 3,
            IsSpecial = true,
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
            AudioOnly = true,
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
        quiz.Rounds.Add(round);
        return quiz;
    }

    private static Quiz CreateTargetQuiz(
        string hostId,
        string title,
        int rowCount,
        int categoryCount)
    {
        var quiz = new Quiz { HostId = hostId, Title = title };
        var round = new QuizRound { Title = $"{title} round", SortOrder = 1 };
        for (var rowIndex = 1; rowIndex <= rowCount; rowIndex++)
        {
            round.Rows.Add(new QuizRoundRow
            {
                RowIndex = rowIndex,
                Points = rowIndex * 200
            });
        }

        for (var categoryIndex = 1; categoryIndex <= categoryCount; categoryIndex++)
        {
            var category = new QuizCategory
            {
                Title = $"Category {categoryIndex}",
                SortOrder = categoryIndex
            };
            for (var rowIndex = 1; rowIndex <= rowCount; rowIndex++)
            {
                var question = new QuizQuestion { RowIndex = rowIndex };
                question.QuestionBlocks.Add(new QuestionContentBlock
                {
                    BlockType = ContentBlockType.Text,
                    SortOrder = 1
                });
                question.AnswerBlocks.Add(new AnswerContentBlock
                {
                    BlockType = ContentBlockType.Text,
                    SortOrder = 1
                });
                category.Questions.Add(question);
            }
            round.Categories.Add(category);
        }

        quiz.Rounds.Add(round);
        return quiz;
    }

    private static void FillAllQuestions(Quiz quiz)
    {
        foreach (var question in quiz.Rounds
            .SelectMany(round => round.Categories)
            .SelectMany(category => category.Questions))
        {
            FillQuestion(question, $"Occupied {question.RowIndex}");
        }
    }

    private static void FillQuestion(QuizQuestion question, string text)
    {
        var block = question.QuestionBlocks.Single();
        block.BlockType = ContentBlockType.Text;
        block.TextContent = text;
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
