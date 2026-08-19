using System.Text.Json;

namespace BadWolfQuiz.Web.Services;

public static class SeoStructuredData
{
    public static string Build(string page, string culture)
    {
        var canonical = SeoMetadataCatalog.BuildAbsoluteUrl(page, culture);
        var organizationId = $"{SeoDiscoveryDocuments.PublicBaseUrl}/#organization";
        var websiteId = $"{SeoDiscoveryDocuments.PublicBaseUrl}/#website";

        var graph = new object[]
        {
            new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["@id"] = organizationId,
                ["name"] = "Bad Wolf Quiz",
                ["url"] = SeoDiscoveryDocuments.PublicBaseUrl
            },
            new Dictionary<string, object?>
            {
                ["@type"] = "WebSite",
                ["@id"] = websiteId,
                ["name"] = "Bad Wolf Quiz",
                ["url"] = SeoDiscoveryDocuments.PublicBaseUrl,
                ["publisher"] = new Dictionary<string, object?>
                {
                    ["@id"] = organizationId
                },
                ["inLanguage"] = culture,
                ["mainEntityOfPage"] = canonical
            }
        };

        return JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@graph"] = graph
            });
    }
}
