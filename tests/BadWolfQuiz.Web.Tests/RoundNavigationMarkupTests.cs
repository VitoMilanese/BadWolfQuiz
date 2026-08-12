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
    public void Previous_round_requires_confirmation_before_posting()
    {
        var markup = File.ReadAllText(FindLobbyView());
        Assert.Contains("data-open-previous-round-dialog", markup);
        Assert.Contains("id=\"previous-round-dialog\"", markup);
        Assert.Contains("data-close-previous-round-dialog", markup);
        Assert.Contains("GameBoard_PreviousRoundConfirm", markup);
        Assert.Contains("<form method=\"post\" asp-page-handler=\"PreviousRound\">", markup);
    }

    [Fact]
    public void Manual_final_always_requires_confirmation_and_only_offers_return_when_available()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var finalFormStart = markup.IndexOf("id=\"force-advance-final-form\"", StringComparison.Ordinal);
        var finalFormEnd = markup.IndexOf("</form>", finalFormStart, StringComparison.Ordinal);
        var finalForm = markup[finalFormStart..finalFormEnd];

        Assert.Contains("type=\"button\"", finalForm);
        Assert.Contains("data-open-force-advance-final-dialog", finalForm);
        Assert.DoesNotContain("type=\"submit\"", finalForm);

        var dialogStart = markup.IndexOf("id=\"force-advance-final-dialog\"", StringComparison.Ordinal);
        var dialogEnd = markup.IndexOf("</dialog>", dialogStart, StringComparison.Ordinal);
        var dialog = markup[dialogStart..dialogEnd];
        Assert.Contains("HasUnfinishedRegularRoundExcludingCurrent", dialog);
        Assert.Contains("asp-page-handler=\"ReturnToUnfinishedRound\"", dialog);
        Assert.Contains("data-confirm-force-advance-final", dialog);
        Assert.Contains("GameBoard_StayInCurrentRound", dialog);
        Assert.Contains("asp-page-handler=\"ReturnToUnfinishedRound\"", dialog);
    }

    [Fact]
    public void Previous_and_forced_final_transitions_use_round_leaderboard()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var model = File.ReadAllText(FindLobbyModel());
        var script = File.ReadAllText(FindSiteScript());

        Assert.Contains("IsPreviousRoundReturnPending", markup);
        Assert.Contains("IsFinalQuestionAdvancePending", markup);
        Assert.Contains("asp-page-handler=\"PrepareFinalQuestionLeaderboard\"", markup);
        Assert.Contains("asp-page-handler=\"ForceAdvanceToFinalQuestion\"", markup);
        Assert.Contains("OnPostPrepareFinalQuestionLeaderboardAsync", model);
        Assert.Contains("PrepareFinalQuestionAdvance", model);
        Assert.DoesNotContain("Object.defineProperty(forceAdvanceFinalForm", script);
    }

    [Fact]
    public void Last_completed_round_posts_to_the_natural_final_transition_handler()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var model = File.ReadAllText(FindLobbyModel());

        Assert.Contains("asp-page-handler=\"StartNaturalFinalTransition\"", markup);
        Assert.Contains("OnPostStartNaturalFinalTransition", model);
        Assert.Contains("RedirectToPage(\"FinalQuestionTransition\", new { id, force = false })", model);
    }

    [Fact]
    public void Forced_next_handler_preserves_the_existing_leaderboard_flow()
    {
        var model = File.ReadAllText(FindLobbyModel());
        Assert.Contains("sessionRegistry.ForceCompleteCurrentRound(game.PublicCode);", model);
        Assert.Contains("if (game.Session.Players.Count == 0)", model);
        Assert.Contains("sessionRegistry.AdvanceToNextRound(game.PublicCode);", model);
        Assert.DoesNotContain("sessionRegistry.ForceAdvanceToNextRound(game.PublicCode);", model);
    }

    [Fact]
    public void Previous_round_confirmation_uses_danger_action_and_restarts_filtered_intro()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var model = File.ReadAllText(FindLobbyModel());
        var intro = File.ReadAllText(FindRunningRoundIntroModel());

        Assert.Contains("<button class=\"button button-danger\" type=\"submit\">", markup);
        Assert.Contains("LocalRedirect($\"/Admin/Games/RunningRoundIntro/{id:D}?returning=true\")", model);
        Assert.Contains("GetIntroCategories(game.Session, round, returning)", intro);
        Assert.Contains("!returning || session.Board.Questions.Any", intro);
        Assert.Contains("question.Status == RuntimeQuestionStatus.Available", intro);
    }

    private static string FindSiteScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "BadWolfQuiz.Web", "wwwroot", "js", "site.js");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate site.js from the test output directory.");
    }

    private static string FindRunningRoundIntroModel()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "RunningRoundIntro.cshtml.cs");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate RunningRoundIntro.cshtml.cs from the test output directory.");
    }

    private static string FindLobbyModel()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml.cs");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate Lobby.cshtml.cs from the test output directory.");
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
