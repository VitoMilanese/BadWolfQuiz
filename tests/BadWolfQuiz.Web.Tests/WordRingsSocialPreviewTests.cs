using System.Buffers.Binary;
using BadWolfQuiz.Web.Pages;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsSocialPreviewTests
{
    [Theory]
    [InlineData("uk-UA", "Слівце в кільце — Bad Wolf Quiz", "word-rings-uk", "uk_UA")]
    [InlineData("it-IT", "Parola nel cerchio — Bad Wolf Quiz", "word-rings-it", "it_IT")]
    [InlineData("en-US", "Word into the ring — Bad Wolf Quiz", "word-rings-en", "en_US")]
    [InlineData("ru-RU", "Україна", "word-rings-ru", "ru_RU")]
    public void Word_rings_page_uses_specific_localized_social_metadata(
        string culture,
        string expectedTitle,
        string expectedVariant,
        string expectedLocale)
    {
        var preview = SocialPreviewMetadataCatalog.Resolve("/WordRings", culture);

        Assert.Equal(expectedTitle, preview.Title);
        Assert.Equal(expectedVariant, preview.ImageVariant);
        Assert.Equal(expectedLocale, preview.OpenGraphLocale);
        Assert.False(string.IsNullOrWhiteSpace(preview.Description));
    }

    [Theory]
    [InlineData("word-rings-uk")]
    [InlineData("word-rings-it")]
    [InlineData("word-rings-en")]
    [InlineData("word-rings-ru")]
    public void Dedicated_word_rings_renderer_returns_a_1200_by_630_png(string variant)
    {
        var png = WordRingsSocialPreviewRenderer.Render(variant);
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
    public void Word_rings_preview_is_visually_distinct_from_the_generic_site_card()
    {
        var wordRings = WordRingsSocialPreviewRenderer.Render("word-rings-uk");
        var generic = SocialPreviewImageRenderer.Render("site");

        Assert.False(wordRings.SequenceEqual(generic));
    }

    [Fact]
    public void Social_preview_endpoint_routes_word_rings_variant_to_the_dedicated_renderer()
    {
        var httpContext = new DefaultHttpContext();
        var model = new SocialPreviewModel
        {
            PageContext = new PageContext
            {
                HttpContext = httpContext
            }
        };

        var result = Assert.IsType<FileContentResult>(model.OnGet("word-rings-uk"));
        var expected = WordRingsSocialPreviewRenderer.Render("word-rings-uk");

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(expected, result.FileContents);
        Assert.Equal("public, max-age=86400", httpContext.Response.Headers.CacheControl.ToString());
    }
}
