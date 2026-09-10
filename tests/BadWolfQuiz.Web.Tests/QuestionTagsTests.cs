using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionTagsTests
{
    [Fact]
    public async Task Tags_are_persisted_unique_by_normalized_name_and_deleted_with_question()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = new HostAccount
        {
            Id = "tag-host",
            Email = "tag-host@example.com",
            NormalizedEmail = "TAG-HOST@EXAMPLE.COM",
            PasswordHash = "test"
        };
        var quiz = new Quiz { HostId = host.Id, Title = "Tags test" };
        var round = new QuizRound { Title = "Round", SortOrder = 1 };
        var category = new QuizCategory { Title = "Category", SortOrder = 1 };
        var question = new QuizQuestion { RowIndex = 1 };
        question.Tags.Add(new QuizQuestionTag
        {
            Name = "фільми 90-х",
            NormalizedName = "фільми 90-х".ToUpperInvariant()
        });
        category.Questions.Add(question);
        round.Categories.Add(category);
        round.Rows.Add(new QuizRoundRow { RowIndex = 1, Points = 100 });
        quiz.Rounds.Add(round);
        host.Quizzes.Add(quiz);
        db.Hosts.Add(host);
        await db.SaveChangesAsync();

        var stored = await db.QuizQuestionTags.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal("фільми 90-х", stored.Name);

        db.QuizQuestionTags.Add(new QuizQuestionTag
        {
            QuizQuestionId = question.Id,
            Name = "ФІЛЬМИ 90-Х",
            NormalizedName = "ФІЛЬМИ 90-Х"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var persistedQuestion = await db.QuizQuestions.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == question.Id);
        db.QuizQuestions.Remove(persistedQuestion);
        await db.SaveChangesAsync();
        Assert.Empty(await db.QuizQuestionTags.IgnoreQueryFilters().ToListAsync());
    }
}
