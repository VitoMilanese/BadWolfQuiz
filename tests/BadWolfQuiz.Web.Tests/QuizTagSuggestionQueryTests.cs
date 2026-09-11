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
    public async Task Suggestions_are_host_scoped_ranked_filtered_and_limited()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>().UseSqlite(connection).Options;
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "host-a")], "Test"))
        };
        await using var db = new QuizDbContext(
            options,
            new HttpContextAccessor { HttpContext = httpContext });
        await db.Database.EnsureCreatedAsync();

        var hostA = CreateHost("host-a");
        AddQuiz(hostA, "A1", "movies", "comedy", "tag 1");
        AddQuiz(hostA, "A2", "movies", "comedy", "tag 2");
        AddQuiz(hostA, "A3", "movies", "tag 3");
        AddQuiz(hostA, "A4", "movies", "tag 4");
        AddQuiz(hostA, "A5", "series", "tag 5");
        AddQuiz(hostA, "A6", "tag 6");
        AddQuiz(hostA, "A7", "tag 7");
        AddQuiz(hostA, "A8", "tag 8");
        AddQuiz(hostA, "A9", "tag 9");

        var hostB = CreateHost("host-b");
        for (var index = 0; index < 12; index++) AddQuiz(hostB, $"B{index}", "movies");

        db.Hosts.AddRange(hostA, hostB);
        await db.SaveChangesAsync();

        var top = await QuizTagSuggestionQuery.GetAsync(db, null);
        Assert.Equal(QuizTagSuggestionQuery.MaxResults, top.Count);
        Assert.Equal("movies", top[0]);
        Assert.Equal("comedy", top[1]);

        var filtered = await QuizTagSuggestionQuery.GetAsync(db, "MOV");
        Assert.Equal(["movies"], filtered);
    }

    private static HostAccount CreateHost(string id) => new()
    {
        Id = id,
        Email = $"{id}@example.com",
        NormalizedEmail = $"{id.ToUpperInvariant()}@EXAMPLE.COM",
        PasswordHash = "test"
    };

    private static void AddQuiz(HostAccount host, string title, params string[] tags)
    {
        var quiz = new Quiz { HostId = host.Id, Title = title };
        foreach (var tag in tags)
        {
            quiz.Tags.Add(new QuizTag { Name = tag, NormalizedName = tag.ToUpperInvariant() });
        }
        host.Quizzes.Add(quiz);
    }
}
