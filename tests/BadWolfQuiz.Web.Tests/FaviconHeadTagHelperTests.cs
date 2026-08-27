using System.Buffers.Binary;
using System.Text.Json;
using BadWolfQuiz.Web.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.Tests;

public sealed class FaviconHeadTagHelperTests
{
    [Fact]
    public void Head_markup_references_standard_favicon_and_web_app_assets()
    {
        var helper = new FaviconHeadTagHelper();
        var context = new TagHelperContext(
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            "favicon-test");
        var output = new TagHelperOutput(
            "head",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        helper.Process(context, output);

        var markup = output.PostContent.GetContent();
        Assert.Equal(1000, helper.Order);
        Assert.Contains("rel=\"icon\" type=\"image/png\" sizes=\"32x32\" href=\"/favicon-32x32.png\"", markup, StringComparison.Ordinal);
        Assert.Contains("rel=\"icon\" type=\"image/png\" sizes=\"16x16\" href=\"/favicon-16x16.png\"", markup, StringComparison.Ordinal);
        Assert.Contains("rel=\"icon\" href=\"/favicon.ico\"", markup, StringComparison.Ordinal);
        Assert.Contains("rel=\"apple-touch-icon\" sizes=\"180x180\" href=\"/apple-touch-icon.png?v=3\"", markup, StringComparison.Ordinal);
        Assert.Contains("rel=\"manifest\" href=\"/site-manifest.json?v=2\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"mobile-web-app-capable\" content=\"yes\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"apple-mobile-web-app-capable\" content=\"yes\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"apple-mobile-web-app-title\" content=\"Bad Wolf Quiz\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Favicon_tag_helper_is_registered_for_razor_pages()
    {
        var viewImports = File.ReadAllText(FindRepoFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));

        Assert.Contains(
            "@addTagHelper BadWolfQuiz.Web.TagHelpers.FaviconHeadTagHelper, BadWolfQuiz.Web",
            viewImports,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("favicon-16x16.png", 16, 16)]
    [InlineData("favicon-32x32.png", 32, 32)]
    [InlineData("apple-touch-icon.png", 180, 180)]
    [InlineData("android-chrome-192x192.png", 192, 192)]
    [InlineData("android-chrome-512x512.png", 512, 512)]
    public void Png_assets_have_expected_dimensions(string fileName, int expectedWidth, int expectedHeight)
    {
        var path = FindRepoFile("src", "BadWolfQuiz.Web", "wwwroot", fileName);
        var bytes = File.ReadAllBytes(path);
        byte[] pngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

        Assert.True(bytes.Length >= 24);
        Assert.True(bytes.AsSpan(0, pngSignature.Length).SequenceEqual(pngSignature));
        Assert.Equal(expectedWidth, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(expectedHeight, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }

    [Fact]
    public void Apple_touch_icon_is_a_standard_truecolor_rgb_png()
    {
        var bytes = File.ReadAllBytes(FindRepoFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "apple-touch-icon.png"));

        Assert.True(bytes.Length >= 26);
        Assert.Equal((byte)8, bytes[24]);
        Assert.Equal((byte)2, bytes[25]);
    }

    [Fact]
    public void Web_app_manifest_configures_installable_home_screen_app()
    {
        var path = FindRepoFile("src", "BadWolfQuiz.Web", "wwwroot", "site-manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        var root = manifest.RootElement;

        Assert.Equal("Bad Wolf Quiz", root.GetProperty("name").GetString());
        Assert.Equal("Bad Wolf Quiz", root.GetProperty("short_name").GetString());
        Assert.Equal("/", root.GetProperty("start_url").GetString());
        Assert.Equal("/", root.GetProperty("scope").GetString());
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.Equal("#000000", root.GetProperty("theme_color").GetString());
        Assert.Equal("#000000", root.GetProperty("background_color").GetString());

        var icons = root.GetProperty("icons").EnumerateArray().ToArray();
        Assert.Contains(icons, icon =>
            icon.GetProperty("src").GetString() == "/android-chrome-192x192.png?v=2" &&
            icon.GetProperty("sizes").GetString() == "192x192" &&
            icon.GetProperty("type").GetString() == "image/png" &&
            icon.GetProperty("purpose").GetString() == "any");
        Assert.Contains(icons, icon =>
            icon.GetProperty("src").GetString() == "/android-chrome-512x512.png?v=2" &&
            icon.GetProperty("sizes").GetString() == "512x512" &&
            icon.GetProperty("type").GetString() == "image/png" &&
            icon.GetProperty("purpose").GetString() == "maskable");
    }

    [Fact]
    public void Ico_asset_contains_multiple_icon_sizes()
    {
        var bytes = File.ReadAllBytes(FindRepoFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "favicon.ico"));

        Assert.True(bytes.Length >= 6);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        Assert.True(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2)) >= 3);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var parts = new string[relativeParts.Length + 1];
            parts[0] = directory.FullName;
            Array.Copy(relativeParts, 0, parts, 1, relativeParts.Length);
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {Path.Combine(relativeParts)}");
    }
}
