namespace BadWolfQuiz.Web.Tests;

public sealed class GameRestartRegressionTests
{
    [Fact]
    public void Restart_preserves_registration_and_rebuilds_clean_running_state()
    {
        var root = FindRepositoryRoot();
        var registration = Read(root,
            "src", "BadWolfQuiz.Web", "Services", "GameSessionRegistration.cs");

        Assert.Contains("public GameSession Session { get; private set; }", registration, StringComparison.Ordinal);
        Assert.Contains("public void RestartSession", registration, StringComparison.Ordinal);
        Assert.Contains("var fresh = GameSession.Create(", registration, StringComparison.Ordinal);
        Assert.Contains("Id = currentState.Id", registration, StringComparison.Ordinal);
        Assert.Contains("Status = GameSessionStatus.Running", registration, StringComparison.Ordinal);
        Assert.Contains("CurrentRoundIndex = 0", registration, StringComparison.Ordinal);
        Assert.Contains("player with { Score = 0 }", registration, StringComparison.Ordinal);
        Assert.Contains("CurrentRoundStartScores = players", registration, StringComparison.Ordinal);
        Assert.Contains("FurthestVisitedRoundIndex = 0", registration, StringComparison.Ordinal);
        Assert.Contains("Session = GameSession.Restore(", registration, StringComparison.Ordinal);
        Assert.Contains("BuzzerRace = null;", registration, StringComparison.Ordinal);
        Assert.Contains("MarkPersistenceChanged();", registration, StringComparison.Ordinal);
    }

    [Fact]
    public void Tools_restart_is_confirmed_last_menu_content_and_posts_to_owned_restart_page()
    {
        var root = FindRepositoryRoot();
        var tagHelper = Read(root,
            "src", "BadWolfQuiz.Web", "TagHelpers", "RestartGameToolsTagHelper.cs");
        var restartPage = Read(root,
            "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Restart.cshtml.cs");
        var imports = Read(root,
            "src", "BadWolfQuiz.Web", "Pages", "_ViewImports.cshtml");

        Assert.Contains("Contains(\"action-menu-popover\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("output.PostContent.AppendHtml", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/Admin/Games/Restart/", tagHelper, StringComparison.Ordinal);
        Assert.Contains("return confirm(", tagHelper, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", tagHelper, StringComparison.Ordinal);
        Assert.Contains("RestartGameToolsTagHelper, BadWolfQuiz.Web", imports, StringComparison.Ordinal);

        Assert.Contains("sessionRegistry.FindOwned(", restartPage, StringComparison.Ordinal);
        Assert.Contains("currentHost.RequiredId", restartPage, StringComparison.Ordinal);
        Assert.Contains("game.Session.Status != GameSessionStatus.Running", restartPage, StringComparison.Ordinal);
        Assert.Contains("game.RestartSession();", restartPage, StringComparison.Ordinal);
        Assert.Contains("\"PlayersChanged\"", restartPage, StringComparison.Ordinal);
        Assert.Contains("\"TimerStateChanged\"", restartPage, StringComparison.Ordinal);
        Assert.Contains("RedirectToPage(\"RunningRoundIntro\"", restartPage, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

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
