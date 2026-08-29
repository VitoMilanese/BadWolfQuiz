namespace BadWolfQuiz.Web.Tests;

public sealed class FinishGameRatingRegressionTests
{
    [Fact]
    public void Finish_game_posts_finalization_before_refreshing_player_clients()
    {
        var imports = ReadWebFile("Pages", "_ViewImports.cshtml");
        var tagHelper = ReadWebFile("TagHelpers", "FinishGameLinkTagHelper.cs")
            .ReplaceLineEndings("\n");
        var finishPage = ReadWebFile("Pages", "Admin", "Games", "Finish.cshtml.cs")
            .ReplaceLineEndings("\n");
        var playerLobby = ReadWebFile("Pages", "Player", "Lobby.cshtml")
            .ReplaceLineEndings("\n");

        Assert.Contains("FinishGameLinkTagHelper", imports, StringComparison.Ordinal);
        Assert.Contains("ViewContext.ViewData.Model is not LobbyModel", tagHelper, StringComparison.Ordinal);
        Assert.Contains("\"/Admin/Quizzes/Index\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("\"button-primary\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("\"button-danger\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("page: \"/Admin/Games/Finish\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("GetAndStoreTokens", tagHelper, StringComparison.Ordinal);
        Assert.Contains("data-finish-game-form", tagHelper, StringComparison.Ordinal);
        Assert.Contains("SetAttribute(\"method\", \"post\")", tagHelper, StringComparison.Ordinal);

        Assert.Contains("sessionRegistry.FindOwned(", finishPage, StringComparison.Ordinal);
        Assert.Contains("currentHost.RequiredId", finishPage, StringComparison.Ordinal);
        Assert.Contains(
            ".Group(GameHub.GroupName(game.PublicCode))",
            finishPage,
            StringComparison.Ordinal);

        var finalizeIndex = finishPage.IndexOf(
            "FinalizeRatingPhaseAsync(",
            StringComparison.Ordinal);
        var broadcastIndex = finishPage.IndexOf(
            ".SendAsync(\"QuizCompleted\"",
            StringComparison.Ordinal);
        Assert.True(finalizeIndex >= 0);
        Assert.True(broadcastIndex > finalizeIndex);

        Assert.Contains(
            "connection.on(\"QuizCompleted\"",
            playerLobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "reloadForGameTransition().catch(console.error);",
            playerLobby,
            StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            new[] { root, "src", "BadWolfQuiz.Web" }.Concat(parts).ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
