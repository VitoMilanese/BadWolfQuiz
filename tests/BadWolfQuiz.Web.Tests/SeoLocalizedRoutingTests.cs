using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class SeoLocalizedRoutingTests
{
    [Fact]
    public void Search_facing_cultures_exclude_novelty_culture()
    {
        Assert.Equal(["uk", "en", "it"], SeoRouteCatalog.Cultures);
        Assert.DoesNotContain("ru", SeoRouteCatalog.Cultures);
        Assert.DoesNotContain("ru", SeoRouteCatalog.CultureRouteParameter);
    }

    [Fact]
    public void Initial_indexable_page_catalog_is_explicit()
    {
        Assert.Equal(
            [
                new SeoPageRoute("/Index", string.Empty),
                new SeoPageRoute("/Faq", "faq"),
                new SeoPageRoute("/About", "about"),
                new SeoPageRoute("/PublicQuizzes", "public-quizzes")
            ],
            SeoRouteCatalog.IndexablePages);
    }

    [Theory]
    [InlineData("/uk", "en", "/en")]
    [InlineData("/uk/faq", "it", "/it/faq")]
    [InlineData("/it/public-quizzes?Search=test", "uk", "/uk/public-quizzes?Search=test")]
    [InlineData("/en/about", "ru", "/about")]
    [InlineData("/Faq", "it", "/Faq")]
    public void Language_switch_rewrites_only_search_facing_culture_prefixes(
        string returnUrl,
        string culture,
        string expected)
    {
        Assert.Equal(
            expected,
            SeoRouteCatalog.RewriteLocalizedReturnUrl(returnUrl, culture));
    }

    [Fact]
    public void Program_uses_route_culture_before_cookie_culture()
    {
        var program = File.ReadAllText(FindWebFile("Program.cs"));

        Assert.Contains("new RouteDataRequestCultureProvider()", program);
        Assert.True(
            program.IndexOf("new RouteDataRequestCultureProvider()", StringComparison.Ordinal) <
            program.IndexOf("new CookieRequestCultureProvider()", StringComparison.Ordinal));
        Assert.True(
            program.IndexOf("app.UseRouting();", StringComparison.Ordinal) <
            program.IndexOf("app.UseRequestLocalization(localizationOptions);", StringComparison.Ordinal));
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
