namespace BadWolfQuiz.Web.Tests;

public sealed class ThemeScrollbarRegressionTests
{
    [Fact]
    public void Shared_styles_load_theme_aware_scrollbar_styles()
    {
        var sharedStyles = File.ReadAllText(FindWebFile(
            "wwwroot", "css", "busy-indicators.css"));

        Assert.Contains(
            "@import url(\"./theme-scrollbars.css?v=1\");",
            sharedStyles);
    }

    [Fact]
    public void Scrollbar_colors_derive_from_existing_theme_variables()
    {
        var source = ReadScrollbarStyles();

        Assert.Contains("var(--panel)", source);
        Assert.Contains("var(--bg)", source);
        Assert.Contains("var(--red-bright)", source);
        Assert.Contains("var(--text)", source);
        Assert.Contains(
            "scrollbar-color: var(--theme-scrollbar-thumb) var(--theme-scrollbar-track);",
            source);
    }

    [Fact]
    public void Scrollbar_styles_cover_webkit_hover_active_and_nested_scrollers_without_resizing()
    {
        var source = ReadScrollbarStyles();

        Assert.Contains("*::-webkit-scrollbar-track", source);
        Assert.Contains("*::-webkit-scrollbar-thumb:hover", source);
        Assert.Contains("*::-webkit-scrollbar-thumb:active", source);
        Assert.Contains("*::-webkit-scrollbar-corner", source);
        Assert.DoesNotContain("scrollbar-width: thin", source);
        Assert.DoesNotContain("*::-webkit-scrollbar {", source);
    }

    [Fact]
    public void Forced_colors_can_fall_back_to_native_scrollbars()
    {
        var source = ReadScrollbarStyles();

        Assert.Contains("@media (forced-colors: active)", source);
        Assert.Contains("scrollbar-color: auto;", source);
    }

    private static string ReadScrollbarStyles() =>
        File.ReadAllText(FindWebFile("wwwroot", "css", "theme-scrollbars.css"));

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
