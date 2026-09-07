namespace BadWolfQuiz.Web.Tests;

public sealed class GameIntroStageRestyleRegressionTests
{
    [Fact]
    public void Both_round_intro_pages_keep_the_shared_navigation_contract()
    {
        var firstRound = ReadWebFile("Pages", "Admin", "Games", "RoundIntro.cshtml");
        var runningRound = ReadWebFile("Pages", "Admin", "Games", "RunningRoundIntro.cshtml");

        Assert.Contains("data-game-intro-page", firstRound, StringComparison.Ordinal);
        Assert.Contains("data-game-intro-group", firstRound, StringComparison.Ordinal);
        Assert.Contains("data-game-intro-actions", firstRound, StringComparison.Ordinal);
        Assert.Contains("data-game-intro-next", firstRound, StringComparison.Ordinal);
        Assert.Contains("data-game-intro-start", firstRound, StringComparison.Ordinal);
        Assert.Contains("badWolfInitialRoundIntroNavigationInitialized", firstRound, StringComparison.Ordinal);

        Assert.Contains("data-game-intro-page", runningRound, StringComparison.Ordinal);
        Assert.Contains("data-game-intro-group", runningRound, StringComparison.Ordinal);
        Assert.Contains("data-game-intro-actions", runningRound, StringComparison.Ordinal);
        Assert.Contains("data-game-intro-next", runningRound, StringComparison.Ordinal);
        Assert.Contains("badWolfStandaloneRoundIntroNavigationInitialized", runningRound, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"Lobby\"", runningRound, StringComparison.Ordinal);
    }

    [Fact]
    public void Intro_stage_asset_is_registered_and_attached_to_the_replaceable_frame()
    {
        var imports = ReadWebFile("Pages", "_ViewImports.cshtml");
        var helper = ReadWebFile("TagHelpers", "GameIntroStageAssetsTagHelper.cs");

        Assert.Contains("GameIntroStageAssetsTagHelper", imports, StringComparison.Ordinal);
        Assert.Contains("[HtmlTargetElement(\"div\", Attributes = \"data-game-intro-page\")]", helper, StringComparison.Ordinal);
        Assert.Contains("/css/game-intro-stage.css?v=8", helper, StringComparison.Ordinal);
        Assert.Contains("output.PreContent.AppendHtml", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Intro_stage_styles_keep_content_centered_spaced_and_frameless_without_inner_scrolling()
    {
        var styles = ReadWebFile("wwwroot", "css", "game-intro-stage.css")
            .ReplaceLineEndings("\n");

        Assert.Contains("body.gameplay-layout:has(.game-intro-page)", styles, StringComparison.Ordinal);
        Assert.Contains("body.gameplay-layout:has(.game-intro-page) > main.page-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".game-intro-page > .game-intro-group", styles, StringComparison.Ordinal);
        Assert.Contains("align-self: center;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-rows: auto auto;", styles, StringComparison.Ordinal);
        Assert.Contains("gap: clamp(16px, 1.9vh, 24px);", styles, StringComparison.Ordinal);
        Assert.Contains("max-width: 24ch;", styles, StringComparison.Ordinal);
        Assert.Contains(".game-intro-heading::before", styles, StringComparison.Ordinal);
        Assert.Contains(".game-intro-blocks", styles, StringComparison.Ordinal);
        Assert.Contains("gap: clamp(18px, 2vh, 28px);", styles, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden;", styles, StringComparison.Ordinal);
        Assert.Contains(".game-intro-image-block", styles, StringComparison.Ordinal);
        Assert.Contains("padding: 0 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("border: 0 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("background: transparent !important;", styles, StringComparison.Ordinal);
        Assert.Contains(".game-intro-image-block::before", styles, StringComparison.Ordinal);
        Assert.Contains("content: none !important;", styles, StringComparison.Ordinal);
        Assert.Contains("width: auto;", styles, StringComparison.Ordinal);
        Assert.Contains("height: min(54vh, 620px);", styles, StringComparison.Ordinal);
        Assert.Contains("box-shadow: none;", styles, StringComparison.Ordinal);
        Assert.Contains("filter: none;", styles, StringComparison.Ordinal);
        Assert.Contains(".game-intro-image-block:has(> .game-intro-caption:first-child):has(> .game-intro-caption:last-child)", styles, StringComparison.Ordinal);
        Assert.Contains("height: min(43vh, 500px);", styles, StringComparison.Ordinal);
        Assert.Contains("font-size: clamp(1.64rem, 2.28vw, 2.15rem);", styles, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain;", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("main.page-shell > .game-intro-page::before", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow-y: auto;", styles, StringComparison.Ordinal);
        Assert.Contains(".game-intro-page > .game-intro-actions", styles, StringComparison.Ordinal);
        Assert.Contains(":focus-visible", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 560px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-height: 620px)", styles, StringComparison.Ordinal);
        Assert.Contains("height: 18vh;", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
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
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate web file: {string.Join('/', parts)}");
    }
}
