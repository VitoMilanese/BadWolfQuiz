using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
public sealed partial class SeoHeadTagHelper : TagHelper
{
    public const string SocialTitleViewDataKey = "SocialTitle";
    public const string SocialDescriptionViewDataKey = "SocialDescription";
    public const string SocialImageViewDataKey = "SocialImage";
    public const string SocialImageVariantViewDataKey = "SocialImageVariant";
    public const string SocialUrlViewDataKey = "SocialUrl";
    public const string SocialTypeViewDataKey = "SocialType";
    public const string SocialThemeIdViewDataKey = "SocialThemeId";
    public const string SocialThemeColorsViewDataKey = "SocialThemeColors";

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
        var routeCulture = ViewContext.RouteData.Values["culture"]?.ToString();
        var uiCulture = CultureInfo.CurrentUICulture.Name;
        var request = ViewContext.HttpContext.Request;
        var childContent = await output.GetChildContentAsync();
        var headHtml = childContent.GetContent();
        var metadataMarkup = string.Empty;
        SeoMetadata? seoMetadata = null;

        if (page is not null &&
            routeCulture is not null &&
            SeoMetadataCatalog.IsIndexableRequest(page, routeCulture, uiCulture) &&
            SeoMetadataCatalog.TryGet(page, routeCulture, out var resolvedMetadata))
        {
            seoMetadata = resolvedMetadata;
            var encodedTitle = _htmlEncoder.Encode(resolvedMetadata.Title);
            headHtml = TitleRegex().Replace(
                headHtml,
                $"<title>{encodedTitle}</title>",
                count: 1);
            metadataMarkup = BuildSeoMarkup(page, routeCulture, resolvedMetadata);
        }
        else
        {
            metadataMarkup =
                Environment.NewLine + BuildNoIndexMarkup() + Environment.NewLine;
        }

        var defaultPreview = seoMetadata is { } metadata
            ? new SocialPreviewMetadata(
                metadata.Title,
                metadata.Description,
                metadata.OpenGraphLocale,
                "site")
            : SocialPreviewMetadataCatalog.Resolve(page, uiCulture);

        var socialTitle = GetViewDataString(SocialTitleViewDataKey) ?? defaultPreview.Title;
        var socialDescription = GetViewDataString(SocialDescriptionViewDataKey) ??
            defaultPreview.Description;
        var socialType = GetViewDataString(SocialTypeViewDataKey) ?? "website";
        var imageVariant = GetViewDataString(SocialImageVariantViewDataKey) ??
            defaultPreview.ImageVariant;
        var socialThemeId = GetViewDataString(SocialThemeIdViewDataKey);
        var socialThemeColors = GetViewDataThemeColors(SocialThemeColorsViewDataKey);

        var defaultSocialUrl = seoMetadata is not null &&
            page is not null &&
            routeCulture is not null
                ? SeoMetadataCatalog.BuildAbsoluteUrl(page, routeCulture)
                : BuildAbsoluteRequestUrl(request);
        var socialUrl = ResolveAbsoluteUrl(
            request,
            GetViewDataString(SocialUrlViewDataKey),
            defaultSocialUrl);

        var defaultImagePath = BuildSocialPreviewImagePath(
            imageVariant,
            socialThemeId,
            socialThemeColors);
        var defaultImageUrl = BuildAbsolutePathUrl(request, defaultImagePath);
        var socialImageUrl = ResolveAbsoluteUrl(
            request,
            GetViewDataString(SocialImageViewDataKey),
            defaultImageUrl);

        var socialPreview = new SocialPreviewMetadata(
            socialTitle,
            socialDescription,
            defaultPreview.OpenGraphLocale,
            imageVariant);

        output.Content.SetHtmlContent(
            headHtml +
            metadataMarkup +
            BuildSocialMarkup(
                socialPreview,
                socialUrl,
                socialImageUrl,
                socialType));
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
        lines.Add(
            $"<script type=\"application/ld+json\">{SeoStructuredData.Build(page, culture)}</script>");

        return Environment.NewLine + string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    public string BuildSocialMarkup(
        SocialPreviewMetadata metadata,
        string url,
        string imageUrl,
        string type)
    {
        var encodedTitle = _htmlEncoder.Encode(metadata.Title);
        var encodedDescription = _htmlEncoder.Encode(metadata.Description);
        var encodedUrl = _htmlEncoder.Encode(url);
        var encodedImageUrl = _htmlEncoder.Encode(imageUrl);
        var encodedType = _htmlEncoder.Encode(type);
        var encodedLocale = _htmlEncoder.Encode(metadata.OpenGraphLocale);

        var lines = new[]
        {
            $"<meta property=\"og:type\" content=\"{encodedType}\" />",
            "<meta property=\"og:site_name\" content=\"Bad Wolf Quiz\" />",
            $"<meta property=\"og:title\" content=\"{encodedTitle}\" />",
            $"<meta property=\"og:description\" content=\"{encodedDescription}\" />",
            $"<meta property=\"og:url\" content=\"{encodedUrl}\" />",
            $"<meta property=\"og:locale\" content=\"{encodedLocale}\" />",
            $"<meta property=\"og:image\" content=\"{encodedImageUrl}\" />",
            "<meta property=\"og:image:type\" content=\"image/png\" />",
            $"<meta property=\"og:image:width\" content=\"{SocialPreviewImageRenderer.Width}\" />",
            $"<meta property=\"og:image:height\" content=\"{SocialPreviewImageRenderer.Height}\" />",
            "<meta name=\"twitter:card\" content=\"summary_large_image\" />",
            $"<meta name=\"twitter:title\" content=\"{encodedTitle}\" />",
            $"<meta name=\"twitter:description\" content=\"{encodedDescription}\" />",
            $"<meta name=\"twitter:image\" content=\"{encodedImageUrl}\" />"
        };

        return Environment.NewLine + string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    public static string BuildNoIndexMarkup() =>
        "<meta name=\"robots\" content=\"noindex, nofollow\" />";

    public static string BuildAbsoluteRequestUrl(HttpRequest request) =>
        $"{BuildOrigin(request)}{request.PathBase}{request.Path}{request.QueryString}";

    public static string BuildAbsolutePathUrl(HttpRequest request, string pathAndQuery)
    {
        var normalizedPath = pathAndQuery.StartsWith("/", StringComparison.Ordinal)
            ? pathAndQuery
            : $"/{pathAndQuery}";
        return $"{BuildOrigin(request)}{request.PathBase}{normalizedPath}";
    }

    public static string BuildSocialPreviewImagePath(
        string imageVariant,
        string? themeId,
        SiteThemeColors? customThemeColors)
    {
        var query = new List<string>
        {
            $"variant={Uri.EscapeDataString(imageVariant)}"
        };

        if (!string.Equals(imageVariant, "join", StringComparison.OrdinalIgnoreCase))
        {
            return $"/social-preview.png?{string.Join("&", query)}";
        }

        var normalizedThemeId = SiteThemeCatalog.Normalize(themeId);
        query.Add($"theme={Uri.EscapeDataString(normalizedThemeId)}");

        if (string.Equals(normalizedThemeId, "custom", StringComparison.Ordinal))
        {
            var colors = SiteThemeCatalog.Normalize(customThemeColors);
            AddColor(query, "bg", colors.Background);
            AddColor(query, "panel", colors.Panel);
            AddColor(query, "panel2", colors.PanelSecondary);
            AddColor(query, "text", colors.Text);
            AddColor(query, "muted", colors.MutedText);
            AddColor(query, "accent", colors.Accent);
            AddColor(query, "accentBright", colors.AccentBright);
            AddColor(query, "highlight", colors.Highlight);
        }

        return $"/social-preview.png?{string.Join("&", query)}";
    }

    public static string ResolveAbsoluteUrl(
        HttpRequest request,
        string? value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            (string.Equals(absolute.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(absolute.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            return absolute.ToString();
        }

        return value.StartsWith("/", StringComparison.Ordinal)
            ? BuildAbsolutePathUrl(request, value)
            : fallback;
    }

    private static void AddColor(List<string> query, string key, string value) =>
        query.Add($"{key}={Uri.EscapeDataString(value)}");

    private string? GetViewDataString(string key)
    {
        if (!ViewContext.ViewData.TryGetValue(key, out var value))
        {
            return null;
        }

        var text = value?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private SiteThemeColors? GetViewDataThemeColors(string key)
    {
        if (!ViewContext.ViewData.TryGetValue(key, out var value) ||
            value is not SiteThemeColors colors)
        {
            return null;
        }

        return SiteThemeCatalog.AreValid(colors) ? colors : null;
    }

    private static string BuildOrigin(HttpRequest request)
    {
        var forwardedProto = FirstForwardedValue(
            request.Headers["X-Forwarded-Proto"].ToString());
        var forwardedHost = FirstForwardedValue(
            request.Headers["X-Forwarded-Host"].ToString());
        var scheme = forwardedProto ?? request.Scheme;
        var host = forwardedHost ?? request.Host.Value;

        if (string.IsNullOrWhiteSpace(host))
        {
            return SeoDiscoveryDocuments.PublicBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(scheme))
        {
            scheme = Uri.UriSchemeHttps;
        }

        var hostName = host.Split(':', 2)[0];
        if (string.Equals(hostName, "badwolf.buzz", StringComparison.OrdinalIgnoreCase) ||
            hostName.EndsWith(".badwolf.buzz", StringComparison.OrdinalIgnoreCase))
        {
            scheme = Uri.UriSchemeHttps;
        }

        return $"{scheme}://{host}";
    }

    private static string? FirstForwardedValue(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        return headerValue.Split(',', 2)[0].Trim();
    }

    [GeneratedRegex("<title>.*?</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();
}
