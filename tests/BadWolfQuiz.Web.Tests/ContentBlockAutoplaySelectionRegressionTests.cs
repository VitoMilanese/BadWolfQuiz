namespace BadWolfQuiz.Web.Tests;

public sealed class ContentBlockAutoplaySelectionRegressionTests
{
    [Fact]
    public void Autoplay_selects_only_the_first_visible_marked_media_in_dom_order()
    {
        var source = File.ReadAllText(FindMediaAutoplayScript());

        Assert.Contains("const youtubeSelector =", source, StringComparison.Ordinal);
        Assert.Contains("const autoplaySelector =", source, StringComparison.Ordinal);
        Assert.Contains("const findFirstAutoplayTarget = root =>", source, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfYouTubeAutoExpand?.scan?.(root);", source, StringComparison.Ordinal);
        Assert.Contains("return candidates.find(candidate =>", source, StringComparison.Ordinal);
        Assert.Contains("!candidate.closest(\".question-clue-hidden\")", source, StringComparison.Ordinal);
        Assert.Contains("const target = findFirstAutoplayTarget(root);", source, StringComparison.Ordinal);
        Assert.Contains("tryPlayNative(target);", source, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfYouTubeAutoExpand?.autoplay?.(target);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("root.querySelectorAll(nativeSelector).forEach(tryPlayNative);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("window.BadWolfYouTubeAutoExpand?.autoplay?.(root);", source, StringComparison.Ordinal);
    }

    private static string FindMediaAutoplayScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "BadWolfQuiz.Web",
                "wwwroot",
                "js",
                "media-autoplay.js");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate media-autoplay.js from the test output directory.");
    }
}
