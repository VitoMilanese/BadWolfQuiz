namespace BadWolfQuiz.Web.Tests;

public sealed class PreviousRoundIntroRoutingTests
{
    [Fact]
    public void Previous_round_uses_the_same_intro_routing_layer_as_next_round()
    {
        var script = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "wwwroot", "js", "site.js"));
        var intro = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "RunningRoundIntro.cshtml.cs"));

        Assert.Contains("handler === \"PreviousRound\"", script);
        Assert.Contains("`${runningIntroBase}?handler=Previous`", script);
        Assert.Contains("OnPostPreviousAsync", intro);
        Assert.Contains("ReturnToPreviousUnfinishedRound(game.PublicCode)", intro);
        Assert.Contains("RedirectToPage(new { id, returning = true })", intro);
    }

    [Fact]
    public void Previous_round_navigation_cannot_be_interrupted_by_host_signalr_reload()
    {
        var script = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "wwwroot", "js", "site.js"));

        Assert.Contains("handler === \"Previous\"", script);
        Assert.Contains("handler === \"PreviousRound\"", script);
        Assert.Contains("event.stopImmediatePropagation();", script);
        Assert.Contains("hostBoard?.classList.remove(\"host-game-board\")", script);
        Assert.Contains("fetch(form.action", script);
        Assert.Contains("window.location.assign(response.url || `${runningIntroBase}?returning=true`)", script);
        Assert.Contains("hostBoard?.classList.add(\"host-game-board\")", script);
    }

    [Fact]
    public void Previous_round_with_players_stages_leaderboard_before_intro()
    {
        var intro = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "RunningRoundIntro.cshtml.cs"));

        Assert.Contains("IsPreviousRoundReturnPending", intro);
        Assert.Contains("PrepareReturnToPreviousUnfinishedRound", intro);
        Assert.Contains("RedirectToPage(\"Lobby\", new { id })", intro);
        Assert.Contains("RedirectToPage(new { id, returning = true })", intro);
    }

    [Fact]
    public void Final_guard_return_with_players_stages_leaderboard_then_replays_intro()
    {
        var intro = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "RunningRoundIntro.cshtml.cs"));
        var script = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "wwwroot", "js", "site.js"));

        Assert.Contains("OnPostReturnToUnfinishedAsync", intro);
        Assert.Contains("PrepareReturnToNearestUnfinishedRoundExcludingCurrent", intro);
        Assert.Contains("IsUnfinishedRoundReturnPending", intro);
        Assert.Contains("RedirectToPage(new { id, returning = true })", intro);
        Assert.Contains("handler === \"ReturnToUnfinishedRound\"", script);
        Assert.Contains("handler=ReturnToUnfinished", script);
    }

    [Fact]
    public void Forced_next_round_filters_completed_category_intros()
    {
        var intro = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "RunningRoundIntro.cshtml.cs"));

        Assert.Contains("filterCompletedCategories || game.Session.IsForcedRoundAdvancePending", intro);
        Assert.Contains("returning = filterCompletedCategories", intro);
        Assert.Contains("RedirectToPage(new { id, returning = true })", intro);
        Assert.Contains("!returning || session.Board.Questions.Any", intro);
        var markup = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));
        Assert.Contains("name=\"filterCompletedCategories\"", markup);
        Assert.Contains("IsForcedRoundAdvancePending.ToString().ToLowerInvariant()", markup);
    }

    private static string FindFile(params string[] path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. path]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(path)} from the test output directory.");
    }
}
