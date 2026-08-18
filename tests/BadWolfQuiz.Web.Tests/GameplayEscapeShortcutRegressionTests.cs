namespace BadWolfQuiz.Web.Tests;

public sealed class GameplayEscapeShortcutRegressionTests
{
    [Fact]
    public void Escape_shortcuts_reuse_existing_intro_and_review_actions()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));
        var layout = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Shared",
            "_Layout.cshtml"));
        var roundIntro = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "RoundIntro.cshtml"));
        var runningRoundIntro = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "RunningRoundIntro.cshtml"));
        var lobby = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));

        Assert.Contains("gameplay-escape-shortcuts.js", layout, StringComparison.Ordinal);
        Assert.Contains("form[data-game-intro-start]", script, StringComparison.Ordinal);
        Assert.Contains("requestSubmit", script, StringComparison.Ordinal);
        Assert.Contains(
            "[data-game-intro-page] .game-intro-actions a[href]:not([data-game-intro-next])",
            script,
            StringComparison.Ordinal);
        Assert.Contains("previewQuestionId", script, StringComparison.Ordinal);
        Assert.Contains("questionReviewReturn.click()", script, StringComparison.Ordinal);
        Assert.Contains("dialog[open]", script, StringComparison.Ordinal);
        Assert.Contains("details.action-menu[open]", script, StringComparison.Ordinal);

        Assert.Contains("data-game-intro-start", roundIntro, StringComparison.Ordinal);
        Assert.Contains("data-game-intro-next", runningRoundIntro, StringComparison.Ordinal);
        Assert.Contains("asp-route-previewQuestionId", lobby, StringComparison.Ordinal);
        Assert.Contains("GameBoard_ReturnToBoard", lobby, StringComparison.Ordinal);
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
