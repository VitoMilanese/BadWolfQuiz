namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerCardOverlayRegressionTests
{
    [Fact]
    public void Gameplay_score_is_overlaid_on_media_without_adding_a_layout_row()
    {
        var css = ReadFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "game-player-card-overlays.css");

        Assert.Contains(
            ".game-scoreboard .scoreboard-player:not(.host-card) > .scoreboard-details",
            css);
        Assert.Contains("display: contents;", css);
        Assert.Contains(
            ".game-scoreboard .scoreboard-player:not(.host-card) .scoreboard-score",
            css);
        Assert.Contains("grid-row: 1;", css);
        Assert.Contains("align-self: end;", css);
        Assert.Contains("justify-self: center;", css);
        Assert.Contains("pointer-events: none;", css);
        Assert.Contains("padding: 4px 14px;", css);
    }

    [Fact]
    public void Hidden_board_sidebar_does_not_hide_scores_during_question_or_answer()
    {
        var siteCss = ReadFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "site.css");
        var overlayCss = ReadFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "game-player-card-overlays.css");
        var page = ReadFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml");

        Assert.Contains("@media (min-width: 1300px)", siteCss);
        Assert.Contains(
            ".host-game-board:has(.board-player-sidebar) .game-scoreboard .scoreboard-score",
            siteCss);
        Assert.Contains("class=\"host-board-layout\"", page);
        Assert.Contains("data-host-gameplay-board", page);
        Assert.Contains(
            "hidden=\"@(Model.CurrentQuestion is not null ||",
            page);
        Assert.Contains(
            ".host-game-board:not(:has(.host-board-layout:not([hidden]) .board-player-sidebar))",
            overlayCss);
        Assert.Contains("display: block;", overlayCss);
    }

    [Fact]
    public void Visible_wide_board_sidebar_still_suppresses_duplicate_bottom_scores()
    {
        var siteCss = ReadFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "site.css");
        var overlayCss = ReadFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "game-player-card-overlays.css");

        Assert.Contains(
            ".host-game-board:has(.board-player-sidebar) .game-scoreboard .scoreboard-score",
            siteCss);
        Assert.Contains(
            ":not(:has(.host-board-layout:not([hidden]) .board-player-sidebar))",
            overlayCss);
        Assert.DoesNotContain("display: block !important", overlayCss);
        Assert.DoesNotContain("display: grid !important", overlayCss);
    }

    [Fact]
    public void Gameplay_score_is_layered_above_contributor_frame()
    {
        var overlayCss = ReadFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "game-player-card-overlays.css");
        var contributorCss = ReadFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "contributor-frames.css");

        Assert.Contains("z-index: 24;", overlayCss);
        Assert.Contains(".contributor-avatar-frame-overlay", contributorCss);
        Assert.Contains("z-index: 20;", contributorCss);
    }

    [Fact]
    public void Gameplay_presence_uses_centered_non_flow_top_right_icons()
    {
        var css = ReadFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "game-player-card-overlays.css");

        Assert.Contains(
            ".game-scoreboard .scoreboard-player:not(.host-card) .presence-badge",
            css);
        Assert.Contains("position: absolute;", css);
        Assert.Contains("top: 8px;", css);
        Assert.Contains("right: 8px;", css);
        Assert.Contains("display: flex;", css);
        Assert.Contains("align-items: center;", css);
        Assert.Contains("justify-content: center;", css);
        Assert.Contains("width: 16px;", css);
        Assert.Contains("height: 16px;", css);
        Assert.Contains("-webkit-mask-position: center;", css);
        Assert.Contains("mask-position: center;", css);
        Assert.Contains(".presence-inactive::before", css);
        Assert.Contains(".presence-disconnected::before", css);
        Assert.Contains(".presence-rejoinpending::before", css);
        Assert.DoesNotContain("content: \"⏸\";", css);
        Assert.DoesNotContain("content: \"🔌\";", css);
        Assert.DoesNotContain("content: \"⏳\";", css);
    }

    [Fact]
    public void Gameplay_presence_icons_keep_localized_accessible_labels()
    {
        var script = ReadFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "player-admission-menu.js");

        Assert.Contains("/css/game-player-card-overlays.css", script);
        Assert.Contains("badge.textContent?.trim()", script);
        Assert.Contains("badge.title = label", script);
        Assert.Contains("badge.setAttribute(\"aria-label\", label)", script);
        Assert.Contains("new MutationObserver", script);
    }

    [Fact]
    public void Existing_gameplay_render_paths_keep_score_presence_and_media_elements()
    {
        var page = ReadFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml");

        Assert.Contains("scoreboard-score", page);
        Assert.Contains("presence-badge", page);
        Assert.Contains("player-card-avatar player-webcam-url-frame", page);
        Assert.Contains("video.className = isRunning", page);
    }

    private static string ReadFile(params string[] pathParts) =>
        File.ReadAllText(FindFile(pathParts));

    private static string FindFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName }.Concat(pathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(pathParts)} from the test output directory.");
    }
}
