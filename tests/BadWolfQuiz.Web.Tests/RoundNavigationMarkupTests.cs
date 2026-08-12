namespace BadWolfQuiz.Web.Tests;

public sealed class RoundNavigationMarkupTests
{
    [Fact]
    public void Host_gameplay_contains_all_unfinished_round_navigation_controls()
    {
        var markup = File.ReadAllText(FindLobbyView());
        Assert.Contains("asp-page-handler=\"PreviousRound\"", markup);
        Assert.Contains("HasPreviousUnfinishedRound", markup);
        Assert.Contains("HasNextUnfinishedRound", markup);
        Assert.Contains("natural-final-warning-dialog", markup);
        Assert.Contains("finish-game-warning-dialog", markup);
        Assert.Contains("GameBoard_StayInCurrentRound", markup);
        Assert.Contains("asp-page-handler=\"ReturnToUnfinishedRound\"", markup);
    }

    [Fact]
    public void Manual_final_warning_is_only_opened_when_other_rounds_are_unfinished()
    {
        var markup = File.ReadAllText(FindLobbyView());
        Assert.Contains("HasUnfinishedRegularRoundExcludingCurrent", markup);
        Assert.Contains("data-open-force-advance-final-dialog", markup);
    }

    private static string FindLobbyView()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate Lobby.cshtml from the test output directory.");
    }
}
