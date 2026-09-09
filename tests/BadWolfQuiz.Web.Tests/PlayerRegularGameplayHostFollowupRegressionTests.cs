namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerRegularGameplayHostFollowupRegressionTests
{
    [Fact]
    public void Host_followup_keeps_judging_controls_inside_stage_and_answer_progress_out_of_grid_flow()
    {
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "AnonymousSharedWagerAssetsTagHelper.cs"));
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-regular-gameplay-host-followup.css"))
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "/css/player-regular-gameplay-host-followup.css?v=6",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/host-gameplay-submit-guard.js?v=4",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-host-gameplay-submit-guard",
            helper,
            StringComparison.Ordinal);

        Assert.Contains(
            ".host-game-board .all-player-host-judge-actions {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "var(--game-scoreboard-space, 0px) +\n        clamp(28px, 3vh, 40px)) !important;",
            styles,
            StringComparison.Ordinal);

        Assert.Contains(
            ".current-question-summary:not(.wager-mode):has(> .answer-presentation)",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "> .all-player-host-progress {\n        position: absolute !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "width: min(24rem, calc(100% - 1rem)) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("max-height: none !important;", styles, StringComparison.Ordinal);
        Assert.Contains(
            "transform: translateX(calc(100% - 2.75rem)) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "transition: transform 180ms ease, box-shadow 180ms ease !important;",
            styles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "transition: width 180ms",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "> .all-player-host-progress:hover,",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "transform: translateX(0) !important;",
            styles,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_progress_drawer_keeps_player_rows_top_aligned_and_scrollable()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-regular-gameplay-host-followup.css"))
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "grid-template-rows: auto minmax(0, 1fr);",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "> .all-player-host-progress .all-player-answer-progress {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("grid-auto-rows: max-content;", styles, StringComparison.Ordinal);
        Assert.Contains("align-content: start !important;", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto !important;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Question_selection_busy_state_keeps_visible_board_geometry_stable()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-regular-gameplay-host-followup.css"))
            .ReplaceLineEndings("\n");
        var submitGuard = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-gameplay-submit-guard.js"));

        Assert.Contains(
            ".host-game-board:has(> [data-host-gameplay-board]:not([hidden])):has(",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "> [data-host-gameplay-view] > .current-question-summary",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("width: auto !important;", styles, StringComparison.Ordinal);
        Assert.Contains("height: auto !important;", styles, StringComparison.Ordinal);
        Assert.Contains("margin: 0 !important;", styles, StringComparison.Ordinal);
        Assert.Contains(
            ".host-board-layout[aria-busy=\"true\"]:not([hidden])",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "box-sizing: border-box !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "const freezeQuestionSelectionLayout = board =>",
            submitGuard,
            StringComparison.Ordinal);
        Assert.Contains(
            "const restoreQuestionSelectionLayout = () =>",
            submitGuard,
            StringComparison.Ordinal);
        Assert.Contains(
            "const computedStyle = window.getComputedStyle(hostBoard);",
            submitGuard,
            StringComparison.Ordinal);
        Assert.Contains(
            "hostBoard.style.setProperty(property, value, \"important\");",
            submitGuard,
            StringComparison.Ordinal);
        Assert.Contains(
            "freezeQuestionSelectionLayout(board);",
            submitGuard,
            StringComparison.Ordinal);
        Assert.Contains(
            "restoreQuestionSelectionLayout();",
            submitGuard,
            StringComparison.Ordinal);
        Assert.Contains(
            "board.setAttribute(\"aria-busy\", \"true\");",
            submitGuard,
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
