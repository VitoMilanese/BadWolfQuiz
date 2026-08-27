using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class SeoSearchSemanticsTests
{
    [Theory]
    [InlineData("uk", "квіз", "гра", "вікторина")]
    [InlineData("en", "quiz", "trivia", "game")]
    [InlineData("it", "gioco quiz", "quiz online", "gioca")]
    public void Home_metadata_contains_natural_non_brand_search_terms(
        string culture,
        string firstTerm,
        string secondTerm,
        string thirdTerm)
    {
        Assert.True(SeoMetadataCatalog.TryGet("/Index", culture, out var metadata));
        var text = $"{metadata.Title} {metadata.Description}";

        Assert.Contains(firstTerm, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(secondTerm, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(thirdTerm, text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("uk", "онлайн-квіз", "вікторин", "квіз-гру")]
    [InlineData("en", "quiz game", "trivia game", "public quizzes")]
    [InlineData("it", "gioco quiz", "giochi a quiz", "quiz pubblici")]
    public void Home_visible_copy_explains_the_product_with_search_semantics(
        string culture,
        string firstTerm,
        string secondTerm,
        string thirdTerm)
    {
        Assert.True(HomeSeoContentCatalog.TryGet(culture, out var content));
        var text = string.Join(
            ' ',
            content.Title,
            content.Introduction,
            content.CreateTitle,
            content.CreateText,
            content.PlayTitle,
            content.PlayText);

        Assert.Contains(firstTerm, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(secondTerm, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(thirdTerm, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Novelty_language_is_not_exposed_as_search_copy()
    {
        Assert.False(HomeSeoContentCatalog.TryGet("ru", out _));
    }

    [Fact]
    public void Home_renders_visible_semantic_copy_without_meta_keywords()
    {
        var page = File.ReadAllText(FindWebFile("Pages", "Index.cshtml"));

        Assert.Contains("HomeSeoContentCatalog.TryGet", page, StringComparison.Ordinal);
        Assert.Contains("home-search-title", page, StringComparison.Ordinal);
        Assert.DoesNotContain("meta name=\"keywords\"", page, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindWebFile(params string[] parts)
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
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
