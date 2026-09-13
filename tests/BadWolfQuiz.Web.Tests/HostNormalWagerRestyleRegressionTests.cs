namespace BadWolfQuiz.Web.Tests;

public sealed class HostNormalWagerRestyleRegressionTests
{
    [Fact]
    public void Host_normal_wager_uses_player_wager_visual_language_without_replacing_runtime()
    {
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "AnonymousSharedWagerAssetsTagHelper.cs"));
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-normal-wager-restyle.css"))
            .ReplaceLineEndings("\n");
        var lobby = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));

        Assert.Contains(
            "/css/host-normal-wager-restyle.css?v=3",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/css/host-normal-wager-viewport-center.css?v=3",
            helper,
            StringComparison.Ordinal);

        Assert.Contains(
            ".current-question-summary.wager-mode:not(.anonymous-shared-wager-mode):has(> .wager-entry-panel)",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "grid-template-columns: minmax(220px, 300px) minmax(340px, 520px) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("display: contents !important;", styles, StringComparison.Ordinal);
        Assert.Contains("width: min(520px, 100%) !important;", styles, StringComparison.Ordinal);
        Assert.Contains("border-radius: 24px !important;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(3, minmax(0, 1fr)) !important;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-column: 1 / -1 !important;", styles, StringComparison.Ordinal);
        Assert.Contains(".wager-validation.wager-valid", styles, StringComparison.Ordinal);

        Assert.Contains("class=\"wager-entry-panel\"", lobby, StringComparison.Ordinal);
        Assert.Contains("data-wager-form", lobby, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"SubmitQuestionWager\"", lobby, StringComparison.Ordinal);
        Assert.Contains("data-minimum=\"@wagerLimits.Minimum\"", lobby, StringComparison.Ordinal);
        Assert.Contains("data-maximum=\"@wagerLimits.Maximum\"", lobby, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_normal_wager_keeps_context_beside_keypad_and_compacts_for_zoom()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-normal-wager-restyle.css"))
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "> .wager-entry-panel > .wager-player-summary {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("grid-column: 1 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-row: 2 !important;", styles, StringComparison.Ordinal);
        Assert.Contains(
            "> .wager-entry-panel > .question-wager-form {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("grid-column: 2 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-row: 1 / span 2 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("position: static !important;", styles, StringComparison.Ordinal);
        Assert.Contains("content: none !important;", styles, StringComparison.Ordinal);
        Assert.Contains(
            "@media (max-height: 760px) and (min-width: 821px)",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("min-height: 38px !important;", styles, StringComparison.Ordinal);
        Assert.Contains(
            "@media (max-width: 820px)",
            styles,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Host_normal_wager_centers_a_compact_two_card_left_rail_without_stretching_cards_to_viewport()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-normal-wager-viewport-center.css"))
            .ReplaceLineEndings("\n");

        Assert.Contains("@media (min-width: 821px)", styles, StringComparison.Ordinal);
        Assert.Contains(
            ".current-question-summary.wager-mode:not(.anonymous-shared-wager-mode):has(> .wager-entry-panel)",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("height: calc(", styles, StringComparison.Ordinal);
        Assert.Contains("100dvh -", styles, StringComparison.Ordinal);
        Assert.Contains("var(--topbar-height, 60px) -", styles, StringComparison.Ordinal);
        Assert.Contains("var(--game-scoreboard-space, 130px) -", styles, StringComparison.Ordinal);
        Assert.Contains(
            "grid-template-rows: max-content auto !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("align-content: safe center !important;", styles, StringComparison.Ordinal);
        Assert.Contains("align-items: stretch !important;", styles, StringComparison.Ordinal);
        Assert.Contains("row-gap: 32px !important;", styles, StringComparison.Ordinal);
        Assert.Contains("height: auto !important;", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("height: 100% !important;", styles, StringComparison.Ordinal);
        Assert.Contains("row-gap: 18px !important;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_normal_wager_restyle_does_not_target_all_player_or_anonymous_shared_wagers()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-normal-wager-restyle.css"));
        var centerStyles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "host-normal-wager-viewport-center.css"));

        Assert.Contains(
            ":not(.anonymous-shared-wager-mode):has(> .wager-entry-panel)",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ":not(.anonymous-shared-wager-mode):has(> .wager-entry-panel)",
            centerStyles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".all-player-wager-waiting {",
            styles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".anonymous-shared-wager-host-panel {",
            styles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".all-player-wager-waiting {",
            centerStyles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".anonymous-shared-wager-host-panel {",
            centerStyles,
            StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[]
                {
                    directory.FullName,
                    "src",
                    "BadWolfQuiz.Web"
                }.Concat(parts).ToArray());

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {string.Join('/', parts)}");
    }
}
