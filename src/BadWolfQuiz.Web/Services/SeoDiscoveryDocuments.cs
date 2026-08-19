using System.Xml.Linq;

namespace BadWolfQuiz.Web.Services;

public static class SeoDiscoveryDocuments
{
    public const string PublicBaseUrl = "https://badwolf.buzz";

    private static IReadOnlyList<string> DisallowedPaths { get; } =
    [
        "/Admin/",
        "/Account/",
        "/Player/",
        "/Api/",
        "/hubs/",
        "/Join/",
        "/SetLanguage",
        "/AllPlayerQuestion",
        "/ContributorFrame",
        "/ContributorFrames",
        "/MyQuestions",
        "/Error"
    ];

    public static string BuildRobotsTxt()
    {
        var lines = new List<string>
        {
            "User-agent: *",
            "Allow: /"
        };

        lines.AddRange(DisallowedPaths.Select(path => $"Disallow: {path}"));
        lines.Add(string.Empty);
        lines.Add($"Sitemap: {PublicBaseUrl}/sitemap.xml");

        return string.Join('\n', lines) + '\n';
    }

    public static string BuildSitemapXml()
    {
        XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var urls =
            from page in SeoRouteCatalog.IndexablePages
            from culture in SeoRouteCatalog.Cultures
            select new XElement(
                sitemap + "url",
                new XElement(
                    sitemap + "loc",
                    BuildAbsoluteUrl(page, culture)));

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(sitemap + "urlset", urls))
            .ToString(SaveOptions.DisableFormatting);
    }

    private static string BuildAbsoluteUrl(SeoPageRoute page, string culture)
    {
        var suffix = string.IsNullOrWhiteSpace(page.Path)
            ? string.Empty
            : $"/{page.Path}";

        return $"{PublicBaseUrl}/{culture}{suffix}";
    }
}
