using System.Security.Claims;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionTagsTests
{
    [Fact]
    public async Task Tags_are_unique_and_deleted_with_quiz()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = CreateHost("final-tags-host");
        var quiz = new Quiz { HostId = host.Id, Title = "Final tags" };
        quiz.FinalQuestionTags.Add(new FinalQuestionTag
        {
            Name = "історія",
            NormalizedName = "ІСТОРІЯ"
        });
        host.Quizzes.Add(quiz);
        db.Hosts.Add(host);
        await db.SaveChangesAsync();

        var stored = await db.FinalQuestionTags
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("історія", stored.Name);

        db.FinalQuestionTags.Add(new FinalQuestionTag
        {
            QuizId = quiz.Id,
            Name = "ІСТОРІЯ",
            NormalizedName = "ІСТОРІЯ"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var persistedQuiz = await db.Quizzes
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == quiz.Id);
        db.Quizzes.Remove(persistedQuiz);
        await db.SaveChangesAsync();
        Assert.Empty(await db.FinalQuestionTags.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Suggestions_include_final_question_tags_and_remain_host_scoped()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "host-a")],
                "Test"))
        };
        await using var db = new QuizDbContext(
            options,
            new HttpContextAccessor { HttpContext = context });
        await db.Database.EnsureCreatedAsync();

        var hostA = CreateHost("host-a");
        var quizA = new Quiz { HostId = hostA.Id, Title = "Host A" };
        quizA.FinalQuestionTags.Add(new FinalQuestionTag
        {
            Name = "фінальний тег",
            NormalizedName = "ФІНАЛЬНИЙ ТЕГ"
        });
        hostA.Quizzes.Add(quizA);

        var hostB = CreateHost("host-b");
        var quizB = new Quiz { HostId = hostB.Id, Title = "Host B" };
        quizB.FinalQuestionTags.Add(new FinalQuestionTag
        {
            Name = "фінальний секрет",
            NormalizedName = "ФІНАЛЬНИЙ СЕКРЕТ"
        });
        hostB.Quizzes.Add(quizB);

        db.Hosts.AddRange(hostA, hostB);
        await db.SaveChangesAsync();

        var suggestions = await QuestionTagSuggestionQuery.GetAsync(db, "фінальний");
        Assert.Equal(["фінальний тег"], suggestions);
    }

    [Fact]
    public async Task Clone_preserves_final_question_tags()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "clone-final-tags-host")],
                "Test"))
        };
        await using var db = new QuizDbContext(
            options,
            new HttpContextAccessor { HttpContext = context });
        await db.Database.EnsureCreatedAsync();

        var host = CreateHost("clone-final-tags-host");
        var quiz = new Quiz { HostId = host.Id, Title = "Source" };
        quiz.FinalQuestionTags.Add(new FinalQuestionTag
        {
            Name = "географія",
            NormalizedName = "ГЕОГРАФІЯ"
        });
        host.Quizzes.Add(quiz);
        db.Hosts.Add(host);
        await db.SaveChangesAsync();

        var clone = await QuizCloneOperations.CloneAsync(
            db,
            quiz.Id,
            "Clone",
            CancellationToken.None);
        Assert.NotNull(clone);

        db.ChangeTracker.Clear();
        var persisted = await db.Quizzes
            .IgnoreQueryFilters()
            .Include(item => item.FinalQuestionTags)
            .SingleAsync(item => item.Id == clone!.Id);
        Assert.Equal("географія", Assert.Single(persisted.FinalQuestionTags).Name);
    }

    [Fact]
    public void Final_editor_uses_the_same_tag_combobox_contract_as_question_editor()
    {
        var root = FindRepositoryRoot();
        var questionEditor = File.ReadAllText(Path.Combine(
            root,
            "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "QuestionEditor.cshtml"));
        var finalEditor = File.ReadAllText(Path.Combine(
            root,
            "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "FinalQuestionEditor.cshtml"));

        foreach (var marker in new[]
        {
            "data-question-tags-editor",
            "data-question-tag-list",
            "data-question-tag-fields",
            "data-question-tag-combobox",
            "data-question-tag-suggestions",
            "data-add-question-tag",
            "role=\"combobox\"",
            "aria-autocomplete=\"list\""
        })
        {
            Assert.Contains(marker, questionEditor);
            Assert.Contains(marker, finalEditor);
        }

        Assert.Contains(
            "FinalQuestionEditor\", \"TagSuggestions",
            finalEditor,
            StringComparison.Ordinal);
    }

    private static HostAccount CreateHost(string id) => new()
    {
        Id = id,
        Email = $"{id}@example.com",
        NormalizedEmail = $"{id.ToUpperInvariant()}@EXAMPLE.COM",
        PasswordHash = "test"
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
