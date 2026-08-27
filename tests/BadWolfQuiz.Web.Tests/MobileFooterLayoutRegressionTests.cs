namespace BadWolfQuiz.Web.Tests;

public sealed class MobileFooterLayoutRegressionTests
{
    [Fact]
    public void Mobile_portal_layout_uses_document_scroll_instead_of_a_persistent_footer_viewport()
    {
        var css = File.ReadAllText(FindStickyFooterStyles());

        const string mobileRule = """
@media (max-width: 700px) {
    body.portal-layout {
        height: auto;
        min-height: 100vh;
        min-height: 100dvh;
        overflow: visible;
    }

    .portal-layout > .page-shell {
        flex: 1 0 auto;
        min-height: auto;
        overflow-y: visible;
        overscroll-behavior-y: auto;
    }
}
""";

        Assert.Contains(mobileRule, css, StringComparison.Ordinal);
    }

    [Fact]
    public void Desktop_portal_layout_keeps_the_existing_sticky_footer_scroll_container()
    {
        var css = File.ReadAllText(FindStickyFooterStyles());

        Assert.Contains("height: 100dvh;", css, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden;", css, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto;", css, StringComparison.Ordinal);
        Assert.Contains("overscroll-behavior-y: contain;", css, StringComparison.Ordinal);
    }

    private static string FindStickyFooterStyles()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "BadWolfQuiz.Web",
                "wwwroot",
                "css",
                "sticky-footer.css");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate sticky-footer.css from the test output directory.");
    }
}
