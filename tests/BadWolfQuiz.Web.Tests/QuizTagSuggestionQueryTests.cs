using System.Security.Claims;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuizTagSuggestionQueryTests
{
    [Fact]
    public async Task Suggestions_prefer_quiz_tags_fill_from_question_tags_and_stay_host_scoped()
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
        var quizTags = Enumerable.Range(1, 10)
            .Select(index => $"quiz-{index:00}")
            .ToArray();
        AddQuiz(hostA, "Host A", quizTags, [
            "question-popular", "question-popular", "question-popular",
            "question-second", "question-third"
        ]);

        var hostB = CreateHost("host-b");
        AddQuiz(hostB, "Host B", ["other-host-quiz"], ["other-host-question"]);

        db.Hosts.AddRange(hostA, hostB);
        await db.SaveChangesAsync();

        var suggestions = await QuizTagSuggestionQuery.GetAsync(db, null);

        Assert.True(suggestions.Count > QuizTagSuggestionQuery.MaxResults);
        Assert.Equal("quiz-01", suggestions[0]);
        Assert.Contains("quiz-10", suggestions);
        Assert.Contains("question-popular", suggestions);
        Assert.Contains("question-second", suggestions);
        Assert.DoesNotContain("other-host-quiz", suggestions);
        Assert.DoesNotContain("other-host-question", suggestions);

        var filtered = await QuizTagSuggestionQuery.GetAsync(db, "QUESTION");
        Assert.Equal("question-popular", filtered[0]);
        Assert.Contains("question-second", filtered);
    }

    private static HostAccount CreateHost(string id) => new()
    {
        Id = id,
        Email = $"{id}@example.com",
        NormalizedEmail = $"{id.ToUpperInvariant()}@EXAMPLE.COM",
        PasswordHash = "test"
    };

    private static void AddQuiz(
        HostAccount host,
        string title,
        IEnumerable<string> quizTags,
        IEnumerable<string> questionTags)
    {
        var quiz = new Quiz { HostId = host.Id, Title = title };
        foreach (var tagName in quizTags)
        {
            quiz.Tags.Add(new QuizTag
            {
                Name = tagName,
                NormalizedName = tagName.ToUpperInvariant()
            });
        }

        var round = new QuizRound { Title = "Round", SortOrder = 1 };
        var category = new QuizCategory { Title = "Category", SortOrder = 1 };
        var rowIndex = 1;
        foreach (var tagName in questionTags)
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
