using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Pages;
using BadWolfQuiz.Web.Services;
using BadWolfQuiz.Web.TagHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuizDescriptionLinkTests
{
    [Fact]
    public void Share_token_is_stable_opaque_and_identity_bound()
    {
        var createdAt = new DateTime(2026, 8, 28, 1, 2, 3, DateTimeKind.Utc).AddTicks(4567);

        var token = QuizDescriptionLink.CreateToken(42, "host-a", createdAt);

        Assert.Equal(32, token.Length);
        Assert.Equal(token, QuizDescriptionLink.CreateToken(42, "host-a", createdAt));
        Assert.NotEqual(token, QuizDescriptionLink.CreateToken(43, "host-a", createdAt));
        Assert.NotEqual(token, QuizDescriptionLink.CreateToken(42, "host-b", createdAt));
        Assert.NotEqual(token, QuizDescriptionLink.CreateToken(42, "host-a", createdAt.AddTicks(1)));
        Assert.True(QuizDescriptionLink.IsValidToken(42, "host-a", createdAt, token));
        Assert.False(QuizDescriptionLink.IsValidToken(42, "host-a", createdAt, "not-a-token"));
        Assert.False(QuizDescriptionLink.IsValidToken(42, "host-a", createdAt, new string('0', 32)));
    }

    [Fact]
    public async Task Public_by_link_lookup_allows_non_public_quiz_without_publishing_it()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        var createdAt = new DateTime(2026, 8, 28, 1, 2, 3, DateTimeKind.Utc);
        var quiz = new Quiz
        {
            Title = "Private announcement",
            Description = "Only people with the announcement link should see this.",
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
            IsPublic = false
        };
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        var token = QuizDescriptionLink.CreateToken(quiz.Id, quiz.HostId, quiz.CreatedAtUtc);
        var result = await QuizDescriptionLink.LoadAsync(db, quiz.Id, token);

        Assert.NotNull(result);
        Assert.Equal(quiz.Title, result.Title);
        Assert.Equal(quiz.Description, result.Description);
        Assert.Null(result.AverageRating);
        Assert.Equal(0, result.RatingCount);
        Assert.False(await db.Quizzes.IgnoreQueryFilters()
            .Where(item => item.Id == quiz.Id)
            .Select(item => item.IsPublic)
            .SingleAsync());
    }

    [Fact]
    public async Task Anonymous_page_handler_serves_non_public_quiz_and_sets_noindex_header()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        var quiz = new Quiz
        {
            Title = "Unlisted quiz",
            Description = "Announcement copy",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsPublic = false
        };
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        var token = QuizDescriptionLink.CreateToken(quiz.Id, quiz.HostId, quiz.CreatedAtUtc);
        var model = new QuizDescriptionModel(db)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await model.OnGetAsync(quiz.Id, token, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Unlisted quiz", model.Quiz.Title);
        Assert.Equal(
            "noindex, nofollow",
            model.Response.Headers["X-Robots-Tag"].ToString());
        Assert.False(quiz.IsPublic);
    }

    [Fact]
    public async Task Public_by_link_lookup_rejects_wrong_token_deleted_quiz_and_missing_quiz()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        var quiz = new Quiz
        {
            Title = "Announcement",
            Description = "Description",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsPublic = true
        };
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        var token = QuizDescriptionLink.CreateToken(quiz.Id, quiz.HostId, quiz.CreatedAtUtc);
        Assert.Null(await QuizDescriptionLink.LoadAsync(db, quiz.Id, new string('0', 32)));
        Assert.Null(await QuizDescriptionLink.LoadAsync(db, quiz.Id + 999, token));

        quiz.IsArchived = true;
        await db.SaveChangesAsync();
        Assert.Null(await QuizDescriptionLink.LoadAsync(db, quiz.Id, token));
    }

    [Fact]
    public void Quizzes_action_menu_places_description_link_before_unfinished_game_delete()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "quiz-description-link.js"));

        var descriptionAction = markup.IndexOf("data-copy-quiz-description-link", StringComparison.Ordinal);
        var unfinishedDelete = markup.IndexOf("data-open-delete-unfinished-dialog", StringComparison.Ordinal);
        var quizDelete = markup.IndexOf("data-open-delete-dialog", descriptionAction, StringComparison.Ordinal);

        Assert.True(descriptionAction >= 0);
        Assert.True(unfinishedDelete > descriptionAction);
        Assert.True(quizDelete > descriptionAction);
        Assert.Contains("data-description-url=\"@Model.DescriptionLinks[quiz.Id]\"", markup);
        Assert.Contains("QuizDescriptionLocalizer[\"Action\"]", markup);
        Assert.Contains("~/js/quiz-description-link.js", markup);
        Assert.Contains("navigator.clipboard?.writeText", script);
        Assert.Contains("document.execCommand('copy')", script);
        Assert.Contains("data-quiz-description-copy-status", script);
        Assert.Contains("message-success", script);
        Assert.Contains("message-error", script);
        Assert.Contains("window.clearTimeout(dismissHandle)", script);
        Assert.Contains("window.setTimeout(hideStatus, 4000)", script);
        Assert.Contains("message-hidden", script);
        Assert.Contains("transitionend", script);
    }

    [Fact]
    public void Description_page_is_read_only_noindex_and_uses_quiz_specific_social_metadata()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "QuizDescription.cshtml"));
        var model = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "QuizDescription.cshtml.cs"));
        var linkService = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Services", "QuizDescriptionLink.cs"));
        var publicCatalog = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "PublicQuizzes.cshtml.cs"));

        Assert.Contains("@page \"/quiz-description/{id:int}/{token}\"", markup);
        Assert.Contains("SocialTitleViewDataKey", markup);
        Assert.Contains("SocialDescriptionViewDataKey", markup);
        Assert.Contains("SocialImageViewDataKey", markup);
        Assert.Contains("SocialUrlViewDataKey", markup);
        Assert.Contains("Model.Quiz.PreviewPath", markup);
        Assert.Contains("Model.Quiz.AverageRating", markup);
        Assert.Contains("quiz-rating-summary", markup);
        Assert.Contains("X-Robots-Tag", model);
        Assert.Contains("noindex, nofollow", model);
        Assert.Contains(".IgnoreQueryFilters()", linkService);
        Assert.Contains("!quiz.IsArchived", linkService);
        Assert.DoesNotContain("quiz.IsPublic", linkService);
        Assert.Contains("quiz.IsPublic && !quiz.IsArchived", publicCatalog);
        Assert.DoesNotContain("asp-page=\"Editor\"", markup);
        Assert.DoesNotContain("QuizQuestion", markup);
        Assert.False(SeoMetadataCatalog.IsIndexableRequest(
            "/QuizDescription",
            routeCulture: null,
            uiCulture: "en"));
    }

    [Fact]
    public void Quiz_specific_social_preview_uses_standard_dimensions()
    {
        var bytes = SocialPreviewImageRenderer.RenderQuizDescription(
            "Cinema night",
            "A quiz about films, series and animation.",
            4.7,
            23);

        using var bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);
        Assert.Equal(SocialPreviewImageRenderer.Width, bitmap.Width);
        Assert.Equal(SocialPreviewImageRenderer.Height, bitmap.Height);
    }

    private static QuizDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        return new QuizDbContext(options);
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
