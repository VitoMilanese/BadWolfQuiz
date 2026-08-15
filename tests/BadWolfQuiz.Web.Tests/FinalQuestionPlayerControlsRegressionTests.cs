namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionPlayerControlsRegressionTests
{
    [Fact]
    public void Final_host_scoreboard_supports_removal_in_static_and_live_rendering()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var finalHostStart = markup.IndexOf(
            "host-game-board final-question-host",
            StringComparison.Ordinal);
        Assert.True(finalHostStart >= 0);
        var finalHost = markup[finalHostStart..];

        Assert.Contains("data-remove-player-label", finalHost, StringComparison.Ordinal);
        Assert.Contains("data-remove-player-confirm", finalHost, StringComparison.Ordinal);
        Assert.Contains("data-remove-player=\"@player.Id.Value\"", finalHost, StringComparison.Ordinal);
        Assert.Contains("remove.dataset.removePlayer = player.id", finalHost, StringComparison.Ordinal);
        Assert.Contains("scoreboard-remove-player", finalHost, StringComparison.Ordinal);
    }

    [Fact]
    public void Final_players_changed_refreshes_server_rendered_final_controls()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var branchStart = markup.IndexOf(
            "if (lobby.classList.contains(\"final-question-host\"))",
            StringComparison.Ordinal);
        Assert.True(branchStart >= 0);
        var branchEnd = markup.IndexOf(
            "playerList.replaceChildren();",
            branchStart + 1,
            StringComparison.Ordinal);
        Assert.True(branchEnd > branchStart);
        var branch = markup[branchStart..(branchEnd + "playerList.replaceChildren();".Length)];

        Assert.Contains("requestHostGameplayRefresh()", branch, StringComparison.Ordinal);
        Assert.Contains("PlayersChanged", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"SubmitMinimumFinalWager\"", markup, StringComparison.Ordinal);
        Assert.Contains("Model.IsPlayerInactive(submission.PlayerId)", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Removal_handler_broadcasts_status_when_final_removal_completes_game()
    {
        var pageModel = File.ReadAllText(FindPageModel());
        var handlerStart = pageModel.IndexOf(
            "OnPostRemovePlayerAsync",
            StringComparison.Ordinal);
        var handlerEnd = pageModel.IndexOf(
            "OnPostUnblockPlayer",
            handlerStart,
            StringComparison.Ordinal);
        var handler = pageModel[handlerStart..handlerEnd];

        Assert.Contains("var previousStatus = game.Session.Status", handler, StringComparison.Ordinal);
        Assert.Contains("previousStatus != game.Session.Status", handler, StringComparison.Ordinal);
        Assert.Contains("\"GameStatusChanged\"", handler, StringComparison.Ordinal);
        Assert.Contains("\"FinalQuestionProgressChanged\"", handler, StringComparison.Ordinal);
        Assert.Contains("BroadcastPlayersAsync", handler, StringComparison.Ordinal);
    }

    private static string FindLobbyView() =>
        Path.Combine(FindRepositoryRoot(), "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml");

    private static string FindPageModel() =>
        Path.Combine(FindRepositoryRoot(), "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml.cs");

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
