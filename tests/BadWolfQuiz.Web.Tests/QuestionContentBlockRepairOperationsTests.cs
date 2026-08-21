using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionContentBlockRepairOperationsTests
{
    [Fact]
    public async Task Repair_removes_empty_invalid_question_and_answer_blocks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        var question = CreateQuestion();
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            BlockType = ContentBlockType.YouTube,
            ExternalUrl = "https://youtu.be/example",
            SortOrder = 0
        });
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            BlockType = (ContentBlockType)0,
            SortOrder = 1
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = ContentBlockType.Text,
            TextContent = "Answer",
            SortOrder = 0
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = (ContentBlockType)0,
            SortOrder = 1
        });

        db.Quizzes.Add(GetQuiz(question));
        await db.SaveChangesAsync();
        var questionId = question.Id;
        var invalidQuestionBlockId = question.QuestionBlocks
            .Single(block => !Enum.IsDefined(block.BlockType)).Id;
        var invalidAnswerBlockId = question.AnswerBlocks
            .Single(block => !Enum.IsDefined(block.BlockType)).Id;
        db.ChangeTracker.Clear();

        var loaded = await LoadQuestionAsync(db, questionId);
        var result = await QuestionContentBlockRepairOperations
            .RepairEmptyInvalidBlocksAsync(db, loaded, CancellationToken.None);

        Assert.Equal(1, result.RemovedQuestionBlocks);
        Assert.Equal(1, result.RemovedAnswerBlocks);
        Assert.Equal(0, result.PreservedInvalidQuestionBlocks);
        Assert.Equal(0, result.PreservedInvalidAnswerBlocks);
        Assert.False(result.AddedQuestionPlaceholder);
        Assert.False(result.AddedAnswerPlaceholder);
        Assert.DoesNotContain(loaded.QuestionBlocks, block => block.Id == invalidQuestionBlockId);
        Assert.DoesNotContain(loaded.AnswerBlocks, block => block.Id == invalidAnswerBlockId);
        Assert.False(await db.QuestionContentBlocks.IgnoreQueryFilters()
            .AnyAsync(block => block.Id == invalidQuestionBlockId));
        Assert.False(await db.AnswerContentBlocks.IgnoreQueryFilters()
            .AnyAsync(block => block.Id == invalidAnswerBlockId));
        Assert.Equal(ContentBlockType.YouTube, Assert.Single(loaded.QuestionBlocks).BlockType);
        Assert.Equal(ContentBlockType.Text, Assert.Single(loaded.AnswerBlocks).BlockType);
    }

    [Fact]
    public async Task Repair_preserves_invalid_blocks_that_contain_meaningful_content()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        var question = CreateQuestion();
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            BlockType = (ContentBlockType)0,
            TextContent = "Legacy question data",
            SortOrder = 1
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = (ContentBlockType)0,
            FileName = "legacy-answer.bin",
            SortOrder = 1
        });

        db.Quizzes.Add(GetQuiz(question));
        await db.SaveChangesAsync();
        var questionId = question.Id;
        var questionBlockId = question.QuestionBlocks.Single().Id;
        var answerBlockId = question.AnswerBlocks.Single().Id;
        db.ChangeTracker.Clear();

        var loaded = await LoadQuestionAsync(db, questionId);
        var result = await QuestionContentBlockRepairOperations
            .RepairEmptyInvalidBlocksAsync(db, loaded, CancellationToken.None);

        Assert.False(result.Changed);
        Assert.Equal(1, result.PreservedInvalidQuestionBlocks);
        Assert.Equal(1, result.PreservedInvalidAnswerBlocks);
        Assert.True(await db.QuestionContentBlocks.IgnoreQueryFilters()
            .AnyAsync(block => block.Id == questionBlockId));
        Assert.True(await db.AnswerContentBlocks.IgnoreQueryFilters()
            .AnyAsync(block => block.Id == answerBlockId));
    }

    [Fact]
    public async Task Repair_replaces_an_only_empty_invalid_block_with_text_placeholder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);

        var question = CreateQuestion();
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            BlockType = (ContentBlockType)0,
            SortOrder = 1
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = (ContentBlockType)0,
            SortOrder = 1
        });

        db.Quizzes.Add(GetQuiz(question));
        await db.SaveChangesAsync();
        var questionId = question.Id;
        db.ChangeTracker.Clear();

        var loaded = await LoadQuestionAsync(db, questionId);
        var result = await QuestionContentBlockRepairOperations
            .RepairEmptyInvalidBlocksAsync(db, loaded, CancellationToken.None);

        Assert.True(result.AddedQuestionPlaceholder);
        Assert.True(result.AddedAnswerPlaceholder);
        Assert.Equal(ContentBlockType.Text, Assert.Single(loaded.QuestionBlocks).BlockType);
        Assert.Equal(ContentBlockType.Text, Assert.Single(loaded.AnswerBlocks).BlockType);
        Assert.All(loaded.QuestionBlocks, block => Assert.True(Enum.IsDefined(block.BlockType)));
        Assert.All(loaded.AnswerBlocks, block => Assert.True(Enum.IsDefined(block.BlockType)));
    }

    [Fact]
    public void Question_editor_registers_repair_tag_helper_for_ajax_form()
    {
        var root = FindRepositoryRoot();
        var imports = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "_ViewImports.cshtml"));
        var page = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes",
            "QuestionEditor.cshtml"));
        var helper = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "TagHelpers",
            "QuestionEditorContentBlockRepairTagHelper.cs"));

        Assert.Contains(
            "QuestionEditorContentBlockRepairTagHelper",
            imports,
            StringComparison.Ordinal);
        Assert.Contains("data-ajax-question-editor", page, StringComparison.Ordinal);
        Assert.Contains(
            "Attributes = \"data-ajax-question-editor\"",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "RepairEmptyInvalidBlocksAsync",
            helper,
            StringComparison.Ordinal);
        Assert.Contains("target.Clear();", helper, StringComparison.Ordinal);
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

    private static QuizQuestion CreateQuestion() => new()
    {
        RowIndex = 1
    };

    private static Quiz GetQuiz(QuizQuestion question)
    {
        var quiz = new Quiz { Title = "Repair test" };
        var round = new QuizRound { Title = "Round", SortOrder = 1 };
        round.Rows.Add(new QuizRoundRow { RowIndex = 1, Points = 200 });
        var category = new QuizCategory { Title = "Category", SortOrder = 1 };
        category.Questions.Add(question);
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        return quiz;
    }

    private static Task<QuizQuestion> LoadQuestionAsync(
        QuizDbContext db,
        int questionId) =>
        db.QuizQuestions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.QuestionBlocks)
            .Include(item => item.AnswerBlocks)
            .SingleAsync(item => item.Id == questionId);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
