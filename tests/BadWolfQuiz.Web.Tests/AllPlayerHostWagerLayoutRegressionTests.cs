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
        Assert.Contains(
            ".current-question-summary.wager-mode:has(.all-player-wager-waiting)",
            siteStyles);
        Assert.Contains("width: 100%;", siteStyles);
        Assert.Contains("max-width: none;", siteStyles);
        Assert.Contains("box-sizing: border-box;", siteStyles);
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
