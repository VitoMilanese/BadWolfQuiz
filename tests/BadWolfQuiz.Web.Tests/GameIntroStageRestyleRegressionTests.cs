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
    public void Intro_stage_assets_are_registered_and_attached_to_the_replaceable_frame()
    {
        var imports = ReadWebFile("Pages", "_ViewImports.cshtml");
        var helper = ReadWebFile("TagHelpers", "GameIntroStageAssetsTagHelper.cs");
        var sharedStyles = ReadWebFile("wwwroot", "css", "busy-indicators.css");

        Assert.Contains("GameIntroStageAssetsTagHelper", imports, StringComparison.Ordinal);
        Assert.Contains("[HtmlTargetElement(\"div\", Attributes = \"data-game-intro-page\")]", helper, StringComparison.Ordinal);
        Assert.Contains("/css/game-intro-stage.css?v=9", helper, StringComparison.Ordinal);
        Assert.Contains("/css/game-intro-stage-refinements.css?v=4", helper, StringComparison.Ordinal);
        Assert.Contains("output.PreContent.AppendHtml", helper, StringComparison.Ordinal);
        Assert.Contains("@import url(\"./game-intro-stage.css?v=9\");", sharedStyles, StringComparison.Ordinal);
        Assert.Contains("@import url(\"./game-intro-stage-refinements.css?v=4\");", sharedStyles, StringComparison.Ordinal);
    }

    [Fact]
    public void Intro_stage_styles_keep_standalone_and_embedded_round_intros_consistent()
    {
        var styles = ReadWebFile("wwwroot", "css", "game-intro-stage.css")
            .ReplaceLineEndings("\n");
        var refinements = ReadWebFile("wwwroot", "css", "game-intro-stage-refinements.css")
            .ReplaceLineEndings("\n");

        Assert.Contains("body.gameplay-layout:has(.game-intro-page)", styles, StringComparison.Ordinal);
        Assert.Contains("body.gameplay-layout:has(.game-intro-page) > main.page-shell", styles, StringComparison.Ordinal);
        Assert.Contains(":is(main.page-shell, [data-host-gameplay-view]) > .game-intro-page", styles, StringComparison.Ordinal);
        Assert.Contains(".host-game-board.host-gameplay-presentation-mode:has(> [data-host-gameplay-view] > .game-intro-page)", styles, StringComparison.Ordinal);
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
        Assert.Contains("border: 0;", styles, StringComparison.Ordinal);
        Assert.Contains("background: transparent;", styles, StringComparison.Ordinal);
        Assert.Contains(".game-intro-actions::before", styles, StringComparison.Ordinal);
        Assert.Contains("content: none;", styles, StringComparison.Ordinal);
        Assert.Contains(":focus-visible", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 560px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-height: 620px)", styles, StringComparison.Ordinal);
        Assert.Contains("height: 18vh;", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);

        Assert.Contains("body.gameplay-layout:has([data-host-gameplay-view] > .game-intro-page)", refinements, StringComparison.Ordinal);
        Assert.Contains("> main.page-shell", refinements, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", refinements, StringComparison.Ordinal);
        Assert.Contains("overflow-x: hidden;", refinements, StringComparison.Ordinal);
        Assert.DoesNotContain("width: 100vw;", refinements, StringComparison.Ordinal);
        Assert.DoesNotContain("margin-left: -50vw;", refinements, StringComparison.Ordinal);
        Assert.Contains("position: fixed;", refinements, StringComparison.Ordinal);
        Assert.Contains("inset: var(--topbar-height, 60px) 0 0;", refinements, StringComparison.Ordinal);
        Assert.Contains("body.gameplay-layout:has([data-host-gameplay-view] > .game-intro-page)::after", refinements, StringComparison.Ordinal);
        Assert.Contains("top: calc(var(--topbar-height, 60px) + clamp(-180px, -10vw, -90px));", refinements, StringComparison.Ordinal);
        Assert.Contains("[data-host-gameplay-view] > .game-intro-page::after", refinements, StringComparison.Ordinal);
        Assert.Contains("display: none;", refinements, StringComparison.Ordinal);
        Assert.Contains("repeating-linear-gradient", refinements, StringComparison.Ordinal);
        Assert.Contains(".game-intro-page .game-intro-text", refinements, StringComparison.Ordinal);
        Assert.Contains("border: 0 !important;", refinements, StringComparison.Ordinal);
        Assert.Contains("background: transparent !important;", refinements, StringComparison.Ordinal);
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
