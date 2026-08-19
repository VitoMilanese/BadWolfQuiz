namespace BadWolfQuiz.Web.Tests;

public sealed class AllPlayerHostWagerLayoutRegressionTests
{
    [Fact]
    public void Wager_host_width_selector_accounts_for_gameplay_view_wrapper()
    {
        var root = FindRepositoryRoot();
        var styles = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        Assert.Contains("[data-host-gameplay-view] { display: contents; }", styles);
        Assert.Contains(
            ".host-game-board:has(> [data-host-gameplay-view] > .current-question-summary.wager-mode:has(.all-player-wager-waiting))",
            styles);

        var siteStyles = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "site.css"));
        var host = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "all-player-question.js"));

        Assert.Contains("all-player-question-wagering", host);
        Assert.Contains("all-player-question-wagering", script);
        Assert.Contains(
            ".host-game-board.all-player-question-wagering",
            siteStyles);
        Assert.Contains("width: calc(100vw - 32px) !important", siteStyles);
        Assert.Contains("width: 100% !important", siteStyles);
        Assert.Contains("justify-self: stretch !important", siteStyles);
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
