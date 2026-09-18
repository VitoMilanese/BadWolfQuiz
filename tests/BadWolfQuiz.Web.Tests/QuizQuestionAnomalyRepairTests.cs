using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuizQuestionAnomalyRepairTests
{
    [Fact]
    public async Task Broken_all_player_choice_is_flagged_and_reset_to_safe_standard_question()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var quiz = new Quiz { Title = "Broken quiz" };
        var round = new QuizRound { Title = "Round 1", SortOrder = 1 };
        round.Rows.Add(new QuizRoundRow { RowIndex = 1, Points = 200 });
        var category = new QuizCategory { Title = "Ghosts", SortOrder = 1 };
        var question = new QuizQuestion
        {
            RowIndex = 1,
            PresentationType = QuestionPresentationType.AllPlayerMultipleChoice,
            BuzzModeOverride = BuzzActivationMode.Disabled,
            ExcludeFromRandomWagerSelection = true
        };
        question.QuestionBlocks.Add(new QuestionContentBlock
        {
            BlockType = ContentBlockType.Text,
            TextContent = "Preserve me",
            SortOrder = 1
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = ContentBlockType.Text,
            SortOrder = 1
        });
        category.Questions.Add(question);
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        Assert.NotNull(QuizQuestionAnomalyDetector.Detect(question, round.Rows));

        var result = await QuizQuestionRepairOperations.RepairAsync(
            db,
            question,
            CancellationToken.None);

        Assert.True(result.Changed);
        Assert.True(result.ResetToStandard);
        Assert.Equal(QuestionPresentationType.Standard, question.PresentationType);
        Assert.Equal(BuzzActivationMode.UseRoundDefault, question.BuzzModeOverride);
        Assert.False(question.ExcludeFromRandomWagerSelection);
        Assert.Equal("Preserve me", Assert.Single(question.QuestionBlocks).TextContent);
        Assert.Single(question.AnswerBlocks);
        Assert.Equal(ContentBlockType.Text, question.AnswerBlocks.Single().BlockType);
        Assert.Null(QuizQuestionAnomalyDetector.Detect(question, round.Rows));
    }

    [Fact]
    public void Valid_multiple_choice_is_not_flagged()
    {
        var round = new QuizRound { Title = "Round", SortOrder = 1 };
        round.Rows.Add(new QuizRoundRow { RowIndex = 1, Points = 200 });
        var category = new QuizCategory { Title = "Category", SortOrder = 1 };
        var question = new QuizQuestion
        {
            Id = 10,
            RowIndex = 1,
            PresentationType = QuestionPresentationType.AllPlayerMultipleChoice
        };
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = ContentBlockType.AnswerOptions,
            TextContent = "2",
            SortOrder = 0
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = ContentBlockType.Text,
            TextContent = "One",
            SortOrder = 1
        });
        question.AnswerBlocks.Add(new AnswerContentBlock
        {
            BlockType = ContentBlockType.Text,
            TextContent = "Two",
            SortOrder = 2
        });
        category.Questions.Add(question);
        round.Categories.Add(category);

        Assert.Null(QuizQuestionAnomalyDetector.Detect(question, round.Rows));
    }

    [Fact]
    public void Quiz_editor_renders_anomaly_badge_and_fix_action()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "Editor.cshtml"));
        var model = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "Editor.cshtml.cs"));

        Assert.Contains("question-anomaly-badge", page);
        Assert.Contains("js-question-fix", page);
        Assert.Contains("data-fix-url", page);
        Assert.Contains("fetch(button.dataset.fixUrl", page);
        Assert.Contains("window.BadWolfBusy?.show?.()", page);
        Assert.Contains("window.BadWolfBusy?.hide?.()", page);
        Assert.Contains("question-anomaly-badge", page);
        Assert.Contains("question-anomalous", page);
        Assert.Contains("button.remove()", page);
        Assert.Contains("QuestionAnomalies", model);
        Assert.Contains("QuizQuestionRepairOperations.RepairAsync", model);
        Assert.Contains(".AsSplitQuery()", model);
        Assert.Contains("resetToStandard = result.ResetToStandard", model);
    }

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
