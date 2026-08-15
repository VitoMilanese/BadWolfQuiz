namespace BadWolfQuiz.Web.Tests;

public sealed class WagerBalanceRegressionTests
{
    [Fact]
    public void HostWagerEntryShowsSelectedPlayerCurrentScore()
    {
        var root = FindRepositoryRoot();
        var markup = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));
        Assert.Contains("var wagerPlayer = Model.Players.First(", markup, StringComparison.Ordinal);
        Assert.Contains("@wagerPlayer.Name</strong>", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"wager-player-balance\"", markup, StringComparison.Ordinal);
        Assert.Contains("@wagerPlayer.Score", markup, StringComparison.Ordinal);
        Assert.Contains("GameBoard_WagerLimits", markup, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) && Directory.Exists(Path.Combine(directory.FullName, "tests"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
