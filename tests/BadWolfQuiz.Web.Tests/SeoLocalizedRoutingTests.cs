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
    [InlineData("/Faq", "uk", "/uk/faq")]
    [InlineData("/About", "en", "/en/about")]
    [InlineData("/PublicQuizzes", "it", "/it/public-quizzes")]
    [InlineData("/Faq", "ru", "/faq")]
    [InlineData("/About", "ru", "/about")]
    [InlineData("/PublicQuizzes", "ru", "/public-quizzes")]
    public void Header_navigation_paths_are_explicit_and_culture_aware(
        string page,
        string culture,
        string expected)
    {
        Assert.Equal(expected, SeoRouteCatalog.BuildNavigationPath(page, culture));
    }

    [Fact]
    public void Header_public_navigation_links_use_the_explicit_route_tag_helper()
    {
        var imports = File.ReadAllText(FindWebFile("Pages", "_ViewImports.cshtml"));
        var tagHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "HeaderSeoNavigationTagHelper.cs"));

        Assert.Contains("HeaderSeoNavigationTagHelper", imports, StringComparison.Ordinal);
        Assert.Contains("action-menu-item", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/PublicQuizzes", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/Faq", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/About", tagHelper, StringComparison.Ordinal);
        Assert.Contains("BuildNavigationPath", tagHelper, StringComparison.Ordinal);
        Assert.Contains("SetAttribute(\n            \"href\"", tagHelper.ReplaceLineEndings("\n"), StringComparison.Ordinal);
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
