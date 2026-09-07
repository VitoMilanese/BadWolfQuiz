namespace BadWolfQuiz.Web.Tests;

public sealed class LegalPageRestyleRegressionTests
{
    [Theory]
    [InlineData("License.cshtml", "license-page")]
    [InlineData("Privacy.cshtml", "privacy-page")]
    public void Legal_pages_use_shared_portal_presentation(string fileName, string pageClass)
    {
        var markup = ReadPage(fileName);

        Assert.Contains("~/css/public-account-pages.css", markup, StringComparison.Ordinal);
        Assert.Contains("~/css/legal-pages.css", markup, StringComparison.Ordinal);
        Assert.Contains($"class=\"portal-page {pageClass}\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-content\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<style>", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void License_page_keeps_runtime_license_source_and_surfaces_russia_related_prohibition()
    {
        var markup = ReadPage("License.cshtml");

        Assert.Contains("@inject IWebHostEnvironment HostEnvironment", markup, StringComparison.Ordinal);
        Assert.Contains("System.IO.File.ReadAllText(licensePath)", markup, StringComparison.Ordinal);
        Assert.Contains("<pre class=\"license-text\">@licenseText</pre>", markup, StringComparison.Ordinal);
        Assert.Contains("license-summary-card-critical", markup, StringComparison.Ordinal);
        Assert.Contains("Russia-related use / Використання, пов’язане з РФ", markup, StringComparison.Ordinal);
        Assert.Contains("no exception mechanism under this license", markup, StringComparison.Ordinal);
        Assert.Contains("https://github.com/VitoMilanese/BadWolfQuiz/blob/main/LICENSE", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Privacy_page_preserves_existing_localized_content_contract()
    {
        var markup = ReadPage("Privacy.cshtml");
        var requiredKeys = new[]
        {
            "Privacy_Title",
            "Privacy_Intro",
            "Privacy_CookiesTitle",
            "Privacy_CookiesIntro",
            "Privacy_QuestionHistoryTitle",
            "Privacy_QuestionHistoryText",
            "Privacy_LanguageTitle",
            "Privacy_LanguageText",
            "Privacy_SecurityTitle",
            "Privacy_SecurityText",
            "Privacy_YouTubeTitle",
            "Privacy_YouTubeText",
            "Privacy_NoAdvertisingTitle",
            "Privacy_NoAdvertisingText",
            "Privacy_LocalStorageTitle",
            "Privacy_LocalStorageText",
            "Privacy_ControlTitle",
            "Privacy_ControlText"
        };

        Assert.All(requiredKeys, key =>
            Assert.Contains($"Localizer[\"{key}\"]", markup, StringComparison.Ordinal));
        Assert.Contains("class=\"privacy-detail-grid\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-card privacy-panel privacy-panel-wide\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Legal_page_styles_cover_summary_cards_license_text_and_responsive_privacy_layout()
    {
        var styles = File.ReadAllText(FindWebFile("wwwroot", "css", "legal-pages.css"));

        Assert.Contains(".license-summary-grid", styles, StringComparison.Ordinal);
        Assert.Contains(".license-summary-card-critical", styles, StringComparison.Ordinal);
        Assert.Contains(".license-text", styles, StringComparison.Ordinal);
        Assert.Contains(".privacy-grid", styles, StringComparison.Ordinal);
        Assert.Contains(".privacy-detail-grid", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 900px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", styles, StringComparison.Ordinal);
    }

    private static string ReadPage(string fileName)
        => File.ReadAllText(FindWebFile("Pages", fileName));

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

        throw new FileNotFoundException($"Could not locate web file: {string.Join('/', parts)}");
    }
}
