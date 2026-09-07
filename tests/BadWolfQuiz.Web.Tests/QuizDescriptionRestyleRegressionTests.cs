namespace BadWolfQuiz.Web.Tests;

public sealed class QuizDescriptionRestyleRegressionTests
{
    [Fact]
    public void Shareable_description_uses_dedicated_announcement_presentation()
    {
        var markup = ReadWebFile("Pages", "QuizDescription.cshtml");

        Assert.Contains("~/css/quiz-description.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"quiz-announcement-page\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"quiz-announcement-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"quiz-announcement-card\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"quiz-announcement-copy\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("page-heading quiz-description-heading", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("form-card narrow quiz-description-card", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<style>", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Shareable_description_preserves_content_rating_and_social_metadata_contract()
    {
        var markup = ReadWebFile("Pages", "QuizDescription.cshtml");

        Assert.Contains("@page \"/quiz-description/{id:int}/{token}\"", markup, StringComparison.Ordinal);
        Assert.Contains("Model.Quiz.Title", markup, StringComparison.Ordinal);
        Assert.Contains("Model.Quiz.Description", markup, StringComparison.Ordinal);
        Assert.Contains("QuizDescriptionLocalizer[\"NoDescription\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Model.Quiz.AverageRating", markup, StringComparison.Ordinal);
        Assert.Contains("quiz-announcement-rating", markup, StringComparison.Ordinal);
        Assert.Contains("QuizRating_AccessibleSummary", markup, StringComparison.Ordinal);
        Assert.Contains("SocialTitleViewDataKey", markup, StringComparison.Ordinal);
        Assert.Contains("SocialDescriptionViewDataKey", markup, StringComparison.Ordinal);
        Assert.Contains("SocialImageViewDataKey", markup, StringComparison.Ordinal);
        Assert.Contains("SocialUrlViewDataKey", markup, StringComparison.Ordinal);
        Assert.Contains("Model.Quiz.PreviewPath", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-page=\"Editor\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Announcement_styles_cover_full_width_long_copy_and_responsive_layouts()
    {
        var styles = ReadWebFile("wwwroot", "css", "quiz-description.css")
            .ReplaceLineEndings("\n");

        Assert.Contains("body.portal-layout:has(.quiz-announcement-page) > .page-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".quiz-announcement-heading h1 {", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere;", styles, StringComparison.Ordinal);
        Assert.Contains(".quiz-description-copy,", styles, StringComparison.Ordinal);
        Assert.Contains("white-space: pre-wrap;", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 800px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate web file: {string.Join('/', parts)}");
    }
}
