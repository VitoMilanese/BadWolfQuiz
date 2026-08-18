namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerNameMarqueeRegressionTests
{
    [Fact]
    public void Host_bootstrap_loads_cache_coherent_player_name_marquee_assets()
    {
        var bootstrap = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));

        Assert.Contains("const hostGameplayTarget = document.querySelector(\".host-game-board\")", bootstrap, StringComparison.Ordinal);
        Assert.Contains("if (!assetUrl.search && bootstrapUrl?.search)", bootstrap, StringComparison.Ordinal);
        Assert.Contains("playerNameMarqueeVersion = \"3\"", bootstrap, StringComparison.Ordinal);
        Assert.Contains("/css/player-name-marquee.css?v=${playerNameMarqueeVersion}", bootstrap, StringComparison.Ordinal);
        Assert.Contains("/js/player-name-marquee.js?v=${playerNameMarqueeVersion}", bootstrap, StringComparison.Ordinal);
    }

    [Fact]
    public void Marquee_targets_final_submission_cards_and_board_sidebar_names()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "player-name-marquee.js"));

        Assert.Contains(".final-submission-list li > strong", script, StringComparison.Ordinal);
        Assert.Contains(".game-scoreboard .scoreboard-player[data-player-id] > strong", script, StringComparison.Ordinal);
        Assert.Contains(".board-player-name", script, StringComparison.Ordinal);
        Assert.Contains("const trackWidth = track.getBoundingClientRect().width", script, StringComparison.Ordinal);
        Assert.Contains("trackWidth - name.clientWidth", script, StringComparison.Ordinal);
        Assert.Contains("--player-name-marquee-end", script, StringComparison.Ordinal);
        Assert.Contains("new MutationObserver(scheduleRefresh)", script, StringComparison.Ordinal);
        Assert.Contains("new ResizeObserver", script, StringComparison.Ordinal);
        Assert.Contains("badwolf:host-gameplay-updated", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Marquee_bounces_between_edges_and_overrides_legacy_carousel_spacing()
    {
        var styles = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-name-marquee.css"));

        Assert.Contains(".player-name-marquee.is-overflowing > .player-name-marquee-track", styles, StringComparison.Ordinal);
        Assert.Contains("width: max-content", styles, StringComparison.Ordinal);
        Assert.Contains("padding-left: 0", styles, StringComparison.Ordinal);
        Assert.Contains("animation: player-name-marquee-bounce 8s linear infinite alternate", styles, StringComparison.Ordinal);
        Assert.Contains("translateX(var(--player-name-marquee-end, 0px))", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
        Assert.Contains("animation: none", styles, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
