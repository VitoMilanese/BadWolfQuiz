using System.Security.Claims;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionTagSuggestionQueryTests
{
    [Fact]
    public async Task Suggestions_are_host_scoped_ranked_filtered_and_limited()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "host-a")],
                "Test"))
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        await using var db = new QuizDbContext(options, accessor);
        await db.Database.EnsureCreatedAsync();

        var hostA = CreateHost("host-a");
        AddQuizWithTags(hostA, "Host A", [
            "серіали 2020-х", "серіали 2020-х", "серіали 2020-х",
            "серіали 2020-х", "серіали 2020-х",
            "фільми 90-х", "фільми 90-х", "фільми 90-х", "фільми 90-х",
            "мультфільми 90-х", "мультфільми 90-х", "мультфільми 90-х",
            "ігри 2000-х", "ігри 2000-х",
            "тег 1", "тег 2", "тег 3", "тег 4", "тег 5", "тег 6", "тег 7"
        ]);

        var hostB = CreateHost("host-b");
        AddQuizWithTags(hostB, "Host B",
            Enumerable.Repeat("фільми 90-х", 20));

        db.Hosts.AddRange(hostA, hostB);
        await db.SaveChangesAsync();

        var top = await QuestionTagSuggestionQuery.GetAsync(db, null);
        Assert.Equal(QuestionTagSuggestionQuery.MaxResults, top.Count);
        Assert.Equal("серіали 2020-х", top[0]);
        Assert.Equal("фільми 90-х", top[1]);

        var filtered = await QuestionTagSuggestionQuery.GetAsync(db, "ФіЛьМи");
        Assert.Equal(["фільми 90-х", "мультфільми 90-х"], filtered);
    }

    private static HostAccount CreateHost(string id) => new()
    {
        Id = id,
        Email = $"{id}@example.com",
        NormalizedEmail = $"{id.ToUpperInvariant()}@EXAMPLE.COM",
        PasswordHash = "test"
    };

    private static void AddQuizWithTags(
        HostAccount host,
        string title,
        IEnumerable<string> tagNames)
    {
        var quiz = new Quiz { HostId = host.Id, Title = title };
        var round = new QuizRound { Title = "Round", SortOrder = 1 };
        var category = new QuizCategory { Title = "Category", SortOrder = 1 };
        var rowIndex = 1;

        foreach (var tagName in tagNames)
        {
            var question = new QuizQuestion { RowIndex = rowIndex };
            question.Tags.Add(new QuizQuestionTag
            {
                Name = tagName,
                NormalizedName = tagName.ToUpperInvariant()
            });
            category.Questions.Add(question);
            round.Rows.Add(new QuizRoundRow
            {
                RowIndex = rowIndex,
                Points = rowIndex * 100
            });
            rowIndex++;
        }

        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        host.Quizzes.Add(quiz);
    }
}
