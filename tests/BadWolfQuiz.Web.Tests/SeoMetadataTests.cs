using System.Text.Encodings.Web;
using BadWolfQuiz.Web.Services;
using BadWolfQuiz.Web.TagHelpers;

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
        Assert.Contains("property=\"og:title\"", markup);
        Assert.Contains("property=\"og:description\"", markup);
        Assert.Contains("property=\"og:url\"", markup);
        Assert.Contains("property=\"og:locale\"", markup);
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
