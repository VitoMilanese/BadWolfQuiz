using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
public sealed partial class SeoHeadTagHelper : TagHelper
{
    private readonly HtmlEncoder _htmlEncoder;

    public SeoHeadTagHelper(HtmlEncoder htmlEncoder)
    {
        _htmlEncoder = htmlEncoder;
    }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var page = ViewContext.RouteData.Values["page"]?.ToString();
        var culture = CultureInfo.CurrentUICulture.Name;
        if (!SeoRouteCatalog.IsSeoCulture(culture) ||
            !SeoMetadataCatalog.TryGet(page, culture, out var metadata) ||
            page is null)
        {
            return;
        }

        var childContent = await output.GetChildContentAsync();
        var headHtml = childContent.GetContent();
        var encodedTitle = _htmlEncoder.Encode(metadata.Title);
        headHtml = TitleRegex().Replace(
            headHtml,
            $"<title>{encodedTitle}</title>",
            count: 1);

        output.Content.SetHtmlContent(headHtml + BuildSeoMarkup(page, culture, metadata));
    }

    public string BuildSeoMarkup(string page, string culture, SeoMetadata metadata)
    {
        var canonical = SeoMetadataCatalog.BuildAbsoluteUrl(page, culture);
        var lines = new List<string>
        {
            $"<meta name=\"description\" content=\"{_htmlEncoder.Encode(metadata.Description)}\" />",
            $"<link rel=\"canonical\" href=\"{_htmlEncoder.Encode(canonical)}\" />"
        };

        foreach (var alternateCulture in SeoRouteCatalog.Cultures)
        {
            var alternateUrl = SeoMetadataCatalog.BuildAbsoluteUrl(page, alternateCulture);
            lines.Add(
                $"<link rel=\"alternate\" hreflang=\"{alternateCulture}\" href=\"{_htmlEncoder.Encode(alternateUrl)}\" />");
        }

        var xDefaultUrl = SeoMetadataCatalog.BuildAbsoluteUrl(page, "uk");
        lines.Add(
            $"<link rel=\"alternate\" hreflang=\"x-default\" href=\"{_htmlEncoder.Encode(xDefaultUrl)}\" />");
        lines.Add("<meta property=\"og:type\" content=\"website\" />");
        lines.Add("<meta property=\"og:site_name\" content=\"Bad Wolf Quiz\" />");
        lines.Add($"<meta property=\"og:title\" content=\"{_htmlEncoder.Encode(metadata.Title)}\" />");
        lines.Add($"<meta property=\"og:description\" content=\"{_htmlEncoder.Encode(metadata.Description)}\" />");
        lines.Add($"<meta property=\"og:url\" content=\"{_htmlEncoder.Encode(canonical)}\" />");
        lines.Add($"<meta property=\"og:locale\" content=\"{metadata.OpenGraphLocale}\" />");

        return Environment.NewLine + string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    [GeneratedRegex("<title>.*?</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();
}
