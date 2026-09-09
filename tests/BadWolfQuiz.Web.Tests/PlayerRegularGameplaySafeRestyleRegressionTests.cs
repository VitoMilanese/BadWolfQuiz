namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerRegularGameplaySafeRestyleRegressionTests
{
    [Fact]
    public void Player_regular_gameplay_polish_does_not_replace_or_gate_existing_runtime_state()
    {
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "AnonymousSharedWagerAssetsTagHelper.cs"));
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-regular-gameplay-safe.css"));
        var anonymous = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "anonymous-shared-wager-player.js"));

        Assert.Contains(
            "/css/player-regular-gameplay-safe.css?v=3",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/anonymous-shared-wager-player.js?v=1",
            helper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "player-regular-gameplay-stage.js",
            helper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "regular-question-wager-player.js",
            helper,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            ".player-lobby:is(",
            styles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".player-lobby.all-player-runtime-active {",
            styles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "position: fixed;\n    inset: var(--topbar-height",
            styles.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);

        Assert.Contains(
            ".player-lobby > .player-all-player-panel:not([hidden])",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "grid-column: 1 / -1 !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".player-lobby .player-all-player-panel .all-player-question-timer",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "display: none !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".player-lobby .player-all-player-panel .all-player-choice-grid",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "grid-template-columns: repeat(2, minmax(0, 1fr)) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".player-lobby .player-all-player-panel .question-wager-form .wager-keypad",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "grid-template-columns: repeat(3, minmax(0, 1fr));",
            styles,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "anonymous-shared-wager-active",
            anonymous,
            StringComparison.Ordinal);
        Assert.Contains(
            "setInterval(refresh, 1000);",
            anonymous,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Host_timer_keeps_absolute_layout_and_answer_timer_is_red()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-regular-gameplay-safe.css"))
            .ReplaceLineEndings("\n");

        Assert.Contains(
            ".host-game-board .host-game-timer {\n    z-index: 1000 !important;\n}",
            styles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".host-game-board .game-timer,\n.host-game-board .quick-timer-controls {\n    position: relative;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".host-game-board .host-game-timer.answer-timer {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "color: color-mix(in srgb, #d1242f 78%, var(--text)) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".host-game-board .host-game-timer.answer-timer > strong,",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("color: currentColor !important;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_all_player_judging_buttons_have_no_extra_background_panel()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-regular-gameplay-safe.css"))
            .ReplaceLineEndings("\n");

        Assert.Contains(
            ".host-game-board .all-player-host-judge-actions {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("position: fixed !important;", styles, StringComparison.Ordinal);
        Assert.Contains("background: transparent !important;", styles, StringComparison.Ordinal);
        Assert.Contains("border: 0 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("box-shadow: none !important;", styles, StringComparison.Ordinal);
        Assert.Contains("backdrop-filter: none;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void All_player_runtime_still_owns_wager_visibility_and_submission()
    {
        var allPlayer = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "all-player-question.js"));

        Assert.Contains(
            "if (state.phase === \"wagering\" && !state.hasWager)",
            allPlayer,
            StringComparison.Ordinal);
        Assert.Contains("buildWagerControls(state);", allPlayer, StringComparison.Ordinal);
        Assert.Contains("panel.hidden = false;", allPlayer, StringComparison.Ordinal);
        Assert.Contains(
            "const submitWager = amount => postPlayerAction(",
            allPlayer,
            StringComparison.Ordinal);
        Assert.Contains("\"Wager\"", allPlayer, StringComparison.Ordinal);
        Assert.Contains(
            "document.addEventListener(\"badwolf:player-session-ready\"",
            allPlayer,
            StringComparison.Ordinal);
        Assert.Contains("playerSessionPending = false;", allPlayer, StringComparison.Ordinal);
        Assert.Contains("playerPollNow?.();", allPlayer, StringComparison.Ordinal);
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
