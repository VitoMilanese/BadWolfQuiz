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
    public void Next_round_filters_completed_category_intros_for_natural_and_forced_progression()
    {
        var intro = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "RunningRoundIntro.cshtml.cs"));

        Assert.Contains("filterCompletedCategories = true;", intro);
        Assert.Contains("returning = filterCompletedCategories", intro);
        Assert.Contains("RedirectToPage(new { id, returning = true })", intro);
        Assert.Contains("!returning || session.Board.Questions.Any", intro);
        Assert.Contains("question.Status == RuntimeQuestionStatus.Available", intro);
    }

    [Fact]
    public void Forced_next_without_players_keeps_filtered_intro_redirect()
    {
        var script = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "wwwroot", "js", "site.js"));
        var noPlayerBranchStart = script.IndexOf("if (!hasPlayers)", StringComparison.Ordinal);
        var noPlayerBranchEnd = script.IndexOf("const submitter", noPlayerBranchStart, StringComparison.Ordinal);
        var noPlayerBranch = script[noPlayerBranchStart..noPlayerBranchEnd];

        Assert.Contains("fetch(`${runningIntroBase}?handler=ForceAdvance`", noPlayerBranch);
        Assert.Contains("window.location.assign(response.url || `${runningIntroBase}?returning=true`)", noPlayerBranch);
        Assert.DoesNotContain("window.location.assign(runningIntroBase);", noPlayerBranch);
    }

    [Fact]
    public void Category_header_opens_single_category_intro_and_returns_to_board()
    {
        var markup = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));
        var intro = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "RunningRoundIntro.cshtml.cs"));
        var introMarkup = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "RunningRoundIntro.cshtml"));
        var script = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "wwwroot", "js", "site.js"));
        var styles = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "wwwroot", "css", "site.css"));

        Assert.Contains("data-category-preview-url", markup);
        Assert.Contains("sourceCategoryId = category.SourceCategoryId", markup);
        Assert.Contains("role=\"link\"", markup);
        Assert.Contains("tabindex=\"0\"", markup);
        Assert.Contains("LoadBoardCategoryPreview", intro);
        Assert.Contains("IsBoardCategoryPreview", introMarkup);
        Assert.Contains("ReturnToBoardLabel", introMarkup);
        Assert.Contains("target?.closest(\"[data-category-preview-url]\")", script);
        Assert.Contains("event.key !== \"Enter\" && event.key !== \" \"", script);
        Assert.Contains("h3[data-category-preview-url]:hover", styles);
        Assert.Contains("h3[data-category-preview-url] {", styles);
        Assert.Contains("border: 1px solid var(--line)", styles);
        Assert.Contains("border-radius: 8px 8px 0 0", styles);
        Assert.Contains("border-color: var(--red-bright)", styles);
    }

    [Fact]
    public void Header_join_code_button_toggles_the_floating_panel()
    {
        var script = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "wwwroot", "js", "site.js"));

        Assert.Contains("[data-open-join-code]", script);
        Assert.Contains("[data-join-code-panel]", script);
        Assert.Contains("event.stopImmediatePropagation()", script);
        Assert.Contains("panel.hidden = !panel.hidden", script);
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
