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
        Assert.Contains("asp-page-handler=\"SubmitEmptyFinalAnswer\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Final_fallback_actions_do_not_depend_on_player_presence()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var pageModel = File.ReadAllText(FindPageModel());
        var registry = File.ReadAllText(FindRegistry());

        Assert.Contains("@if (submission.Wager is null)", markup, StringComparison.Ordinal);
        Assert.Contains("@if (submission.Answer is null)", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("IsPlayerUnavailableForFinalAction", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("IsPlayerUnavailableForFinalAction", pageModel, StringComparison.Ordinal);
        Assert.Contains("SubmitMinimumFinalWagerForPlayer", pageModel, StringComparison.Ordinal);
        Assert.Contains("SubmitEmptyFinalAnswerForPlayer", pageModel, StringComparison.Ordinal);
        Assert.Contains("SubmitMinimumFinalWagerForPlayer", registry, StringComparison.Ordinal);
        Assert.Contains("SubmitEmptyFinalAnswerForPlayer", registry, StringComparison.Ordinal);
        Assert.DoesNotContain("ForInactivePlayer", pageModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ForInactivePlayer", registry, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_final_standings_hide_results_heading_but_keep_finish_action()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var guardStart = markup.IndexOf(
            "@if (Model.FinalStandings.Count > 0)",
            StringComparison.Ordinal);
        Assert.True(guardStart >= 0);
        var finishStart = markup.IndexOf(
            "<div class=\"form-actions final-finish-actions\">",
            guardStart,
            StringComparison.Ordinal);
        Assert.True(finishStart > guardStart);
        var guardedResults = markup[guardStart..finishStart];

        Assert.Contains("FinalQuestion_Results", guardedResults, StringComparison.Ordinal);
        Assert.Contains("round-podium", guardedResults, StringComparison.Ordinal);
        Assert.Contains("GameBoard_FinishGame", markup[finishStart..], StringComparison.Ordinal);
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

    private static string FindRegistry() =>
        Path.Combine(FindRepositoryRoot(), "src", "BadWolfQuiz.Web", "Services", "GameSessionRegistry.cs");

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
