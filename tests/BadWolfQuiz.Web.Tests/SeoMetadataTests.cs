using System.Buffers.Binary;
using System.Text.Encodings.Web;
using System.Text.Json;
using BadWolfQuiz.Web.Services;
using BadWolfQuiz.Web.TagHelpers;
using Microsoft.AspNetCore.Http;

namespace BadWolfQuiz.Web.Tests;

public sealed class SeoMetadataTests
{
    [Fact]
    public void Metadata_exists_for_every_indexable_page_and_search_culture()
    {
        foreach (var page in SeoRouteCatalog.IndexablePages)
        {
            foreach (var culture in SeoRouteCatalog.Cultures)
            {
                Assert.True(
                    SeoMetadataCatalog.TryGet(page.Page, culture, out var metadata),
                    $"Missing SEO metadata for {page.Page} ({culture}).");
                Assert.False(string.IsNullOrWhiteSpace(metadata.Title));
                Assert.False(string.IsNullOrWhiteSpace(metadata.Description));
            }
        }
    }

    [Fact]
    public void Search_metadata_has_no_novelty_language_surface()
    {
        foreach (var page in SeoRouteCatalog.IndexablePages)
        {
            Assert.False(SeoMetadataCatalog.TryGet(page.Page, "ru", out _));
        }
    }

    [Theory]
    [InlineData("/Faq", "en", "en", true)]
    [InlineData("/Faq", null, "en", false)]
    [InlineData("/Faq", "ru", "ru", false)]
    [InlineData("/Join/Index", "en", "en", false)]
    [InlineData("/Faq", "uk", "en", false)]
    public void Only_explicit_search_routes_are_indexable(
        string page,
        string? routeCulture,
        string uiCulture,
        bool expected)
    {
        Assert.Equal(
            expected,
            SeoMetadataCatalog.IsIndexableRequest(page, routeCulture, uiCulture));
    }

    [Fact]
    public void Head_markup_uses_self_canonical_and_only_uk_en_it_alternates()
    {
        Assert.True(SeoMetadataCatalog.TryGet("/Faq", "en", out var metadata));
        var helper = new SeoHeadTagHelper(HtmlEncoder.Default);
        var markup = helper.BuildSeoMarkup("/Faq", "en", metadata);

        Assert.Contains(
            "<link rel=\"canonical\" href=\"https://badwolf.buzz/en/faq\" />",
            markup);
        Assert.Contains("hreflang=\"uk\"", markup);
        Assert.Contains("hreflang=\"en\"", markup);
        Assert.Contains("hreflang=\"it\"", markup);
        Assert.Contains(
            "hreflang=\"x-default\" href=\"https://badwolf.buzz/uk/faq\"",
            markup);
        Assert.DoesNotContain("hreflang=\"ru\"", markup);
        Assert.DoesNotContain("/ru/", markup);
    }

    [Fact]
    public void Social_markup_contains_large_image_open_graph_and_twitter_metadata()
    {
        var helper = new SeoHeadTagHelper(HtmlEncoder.Default);
        var metadata = SocialPreviewMetadataCatalog.GetDefault("en");
        var markup = helper.BuildSocialMarkup(
            metadata,
            "https://badwolf.buzz/en",
            "https://badwolf.buzz/social-preview.png?variant=site",
            "website");

        Assert.Contains("property=\"og:title\"", markup);
        Assert.Contains("property=\"og:description\"", markup);
        Assert.Contains("property=\"og:url\"", markup);
        Assert.Contains("property=\"og:locale\"", markup);
        Assert.Contains("property=\"og:image\"", markup);
        Assert.Contains("property=\"og:image:width\" content=\"1200\"", markup);
        Assert.Contains("property=\"og:image:height\" content=\"630\"", markup);
        Assert.Contains("name=\"twitter:card\" content=\"summary_large_image\"", markup);
        Assert.Contains("name=\"twitter:image\"", markup);
    }

    [Fact]
    public void Join_social_preview_is_specific_to_joining_without_exposing_a_game_code()
    {
        var preview = SocialPreviewMetadataCatalog.Resolve("/Join/Index", "uk-UA");
        const string gameCode = "ABC123";

        Assert.Equal("join", preview.ImageVariant);
        Assert.Contains("Приєднуйся до гри", preview.Title, StringComparison.Ordinal);
        Assert.DoesNotContain(gameCode, preview.Title, StringComparison.Ordinal);
        Assert.DoesNotContain(gameCode, preview.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_join_pages_receive_the_default_site_social_preview()
    {
        var preview = SocialPreviewMetadataCatalog.Resolve("/Index", "it-IT");

        Assert.Equal("site", preview.ImageVariant);
        Assert.Contains("Bad Wolf Quiz", preview.Title, StringComparison.Ordinal);
        Assert.Equal("it_IT", preview.OpenGraphLocale);
    }

    [Fact]
    public void Social_urls_use_forwarded_https_host_for_public_requests()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("127.0.0.1:5080");
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-Host"] = "badwolf.buzz";
        context.Request.Path = "/Join/ABC123";

        Assert.Equal(
            "https://badwolf.buzz/Join/ABC123",
            SeoHeadTagHelper.BuildAbsoluteRequestUrl(context.Request));
        Assert.Equal(
            "https://badwolf.buzz/social-preview.png?variant=join",
            SeoHeadTagHelper.BuildAbsolutePathUrl(
                context.Request,
                "/social-preview.png?variant=join"));
    }

    [Fact]
    public void Social_preview_renderer_returns_a_1200_by_630_png()
    {
        var png = SocialPreviewImageRenderer.Render("join");
        ReadOnlySpan<byte> signature =
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

        Assert.True(png.Length > 24);
        Assert.True(png.AsSpan(0, 8).SequenceEqual(signature));
        Assert.Equal(
            SocialPreviewImageRenderer.Width,
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(
            SocialPreviewImageRenderer.Height,
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
    }

    [Fact]
    public void Indexable_head_markup_contains_website_and_organization_structured_data()
    {
        Assert.True(SeoMetadataCatalog.TryGet("/About", "uk", out var metadata));
        var helper = new SeoHeadTagHelper(HtmlEncoder.Default);
        var markup = helper.BuildSeoMarkup("/About", "uk", metadata);
        var scriptStart = markup.IndexOf("<script type=\"application/ld+json\">", StringComparison.Ordinal);
        var jsonStart = scriptStart + "<script type=\"application/ld+json\">".Length;
        var jsonEnd = markup.IndexOf("</script>", jsonStart, StringComparison.Ordinal);
        var json = markup[jsonStart..jsonEnd];

        using var document = JsonDocument.Parse(json);
        var graph = document.RootElement.GetProperty("@graph").EnumerateArray().ToArray();

        Assert.Contains(graph, item => item.GetProperty("@type").GetString() == "Organization");
        Assert.Contains(graph, item => item.GetProperty("@type").GetString() == "WebSite");
        Assert.Equal("https://schema.org", document.RootElement.GetProperty("@context").GetString());
        Assert.DoesNotContain("ru", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Non_search_pages_get_noindex_nofollow()
    {
        Assert.Equal(
            "<meta name=\"robots\" content=\"noindex, nofollow\" />",
            SeoHeadTagHelper.BuildNoIndexMarkup());
    }

    [Fact]
    public void Seo_head_tag_helper_is_registered_for_all_razor_pages()
    {
        var imports = File.ReadAllText(FindWebFile("Pages", "_ViewImports.cshtml"));

        Assert.Contains(
            "@addTagHelper BadWolfQuiz.Web.TagHelpers.SeoHeadTagHelper, BadWolfQuiz.Web",
            imports);
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
