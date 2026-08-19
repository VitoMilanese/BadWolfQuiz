using System.Xml.Linq;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class SeoDiscoveryDocumentTests
{
    [Fact]
    public void Robots_allows_public_crawling_and_blocks_application_routes()
    {
        var robots = SeoDiscoveryDocuments.BuildRobotsTxt();

        Assert.Contains("User-agent: *", robots);
        Assert.Contains("Allow: /", robots);
        Assert.Contains("Disallow: /Admin/", robots);
        Assert.Contains("Disallow: /Account/", robots);
        Assert.Contains("Disallow: /Player/", robots);
        Assert.Contains("Disallow: /Api/", robots);
        Assert.Contains("Disallow: /hubs/", robots);
        Assert.Contains("Disallow: /Join/", robots);
        Assert.Contains(
            "Sitemap: https://badwolf.buzz/sitemap.xml",
            robots);
        Assert.DoesNotContain("/ru", robots, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sitemap_contains_only_indexable_uk_en_it_urls()
    {
        XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var document = XDocument.Parse(SeoDiscoveryDocuments.BuildSitemapXml());
        var urls = document
            .Descendants(sitemap + "loc")
            .Select(element => element.Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var expected = new[]
        {
            "https://badwolf.buzz/en",
            "https://badwolf.buzz/en/about",
            "https://badwolf.buzz/en/faq",
            "https://badwolf.buzz/en/public-quizzes",
            "https://badwolf.buzz/it",
            "https://badwolf.buzz/it/about",
            "https://badwolf.buzz/it/faq",
            "https://badwolf.buzz/it/public-quizzes",
            "https://badwolf.buzz/uk",
            "https://badwolf.buzz/uk/about",
            "https://badwolf.buzz/uk/faq",
            "https://badwolf.buzz/uk/public-quizzes"
        };

        Assert.Equal(expected, urls);
        Assert.All(urls, url => Assert.StartsWith("https://badwolf.buzz/", url));
        Assert.DoesNotContain(urls, url => url.Contains("/ru", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(urls, url => url.Contains("/Admin", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(urls, url => url.Contains("/Join", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Discovery_pages_expose_expected_public_routes()
    {
        Assert.Contains(
            "@page \"/robots.txt\"",
            File.ReadAllText(FindWebFile("Pages", "Robots.cshtml")));
        Assert.Contains(
            "@page \"/sitemap.xml\"",
            File.ReadAllText(FindWebFile("Pages", "Sitemap.cshtml")));
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
