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
        Assert.Contains("fetch(form.action", script);
        Assert.Contains("await window.BadWolfHostFlowNavigation.navigate(responseUrl);", script);
        Assert.DoesNotContain("hostBoard?.classList.remove(\"host-game-board\")", script);
        Assert.DoesNotContain("hostBoard?.classList.add(\"host-game-board\")", script);
    }

    [Fact]
    public void Previous_round_confirmation_closes_and_disables_repeat_submission()
    {
        var script = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "wwwroot", "js", "site.js"));
        var handlerStart = script.IndexOf(
            "if (handler === \"Previous\" ||",
            StringComparison.Ordinal);
        var handlerEnd = script.IndexOf(
            "routeRoundForm(form);",
            handlerStart,
            StringComparison.Ordinal);
        var handler = script[handlerStart..handlerEnd];

        Assert.Contains("submitter?.setAttribute(\"disabled\", \"disabled\")", handler);
        Assert.Contains("form.closest(\"dialog\")?.close();", handler);
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
    public void Forced_next_round_closes_confirmation_and_submits_once_through_partial_flow()
    {
        var script = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "wwwroot", "js", "site.js"));
        var handlerStart = script.IndexOf(
            "const forceAdvanceButton = target?.closest(",
            StringComparison.Ordinal);
        var handlerEnd = script.IndexOf(
            "const submitter = target?.closest",
            handlerStart,
            StringComparison.Ordinal);
        var handler = script[handlerStart..handlerEnd];

        Assert.Contains("forceAdvanceButton.disabled = true;", handler);
        Assert.Contains("force-advance-round-dialog", handler);
        Assert.Contains("routeRoundForm(form);", handler);
        Assert.Contains("fetch(form.action", handler);
        Assert.Contains("await window.BadWolfHostFlowNavigation.navigate(responseUrl);", handler);
        Assert.Contains("forceAdvanceButton.disabled = false;", handler);
        Assert.DoesNotContain("if (!hasPlayers)", handler);
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
    public void Final_question_header_qr_uses_secondary_button_background_for_theme_contrast()
    {
        var layout = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Shared", "_Layout.cshtml"));

        Assert.Contains("gameHeaderQrDarkDataUri", layout);
        Assert.Contains("gameHeaderQrLightDataUri", layout);
        Assert.Contains("qrCode.GetGraphic(4, new byte[] { 0, 0, 0, 255 }, new byte[] { 0, 0, 0, 0 })", layout);
        Assert.Contains("qrCode.GetGraphic(4, new byte[] { 255, 255, 255, 255 }, new byte[] { 0, 0, 0, 0 })", layout);
        Assert.Contains("const buttonBackground = getComputedStyle(discordButton).backgroundColor;", layout);
        Assert.Contains("qrButton.style.backgroundColor = buttonBackground;", layout);
        Assert.Contains("const channels = buttonBackground", layout);
        Assert.Contains("const qrImage = qrButton?.querySelector('img');", layout);
        Assert.Contains("data-dark-src=\"@gameHeaderQrDarkDataUri\"", layout);
        Assert.Contains("data-light-src=\"@gameHeaderQrLightDataUri\"", layout);
        Assert.Contains("qrImage.src = backgroundIsLight", layout);
        Assert.Contains("? qrImage.dataset.darkSrc", layout);
        Assert.Contains(": qrImage.dataset.lightSrc", layout);
        Assert.DoesNotContain("qrImage.style.filter", layout);
        Assert.DoesNotContain("qrButton.classList.add(", layout);
        Assert.DoesNotContain("mix-blend-mode: multiply", layout);
        Assert.DoesNotContain("mix-blend-mode: screen", layout);
        Assert.Contains("GameSessionStatus.FinalWagering", layout);
        Assert.Contains("GameSessionStatus.FinalAnswering", layout);
        Assert.Contains("GameSessionStatus.FinalJudging", layout);
    }

    [Fact]
    public void Final_question_uses_the_same_game_header_context_as_running_gameplay()
    {
        var lobby = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));
        var layout = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Shared", "_Layout.cshtml"));

        Assert.Contains("GameSessionStatus.FinalWagering or", lobby);
        Assert.Contains("GameSessionStatus.FinalAnswering or", lobby);
        Assert.Contains("GameSessionStatus.FinalJudging", lobby);
        Assert.Contains("<div class=\"game-header-context\">", lobby);
        Assert.Contains("data-open-discord-settings", lobby);
        Assert.Contains("@if (Model.Game.Session.Status == BadWolfQuiz.Game.Runtime.GameSessionStatus.Running)", lobby);
        Assert.Contains(".game-header-square-button[hidden]", layout);
        Assert.Contains("const gameHeader = document.querySelector('.game-header-context');", layout);
        Assert.Contains("const discordButton = gameHeader?.querySelector('[data-open-discord-settings]');", layout);
    }

    [Fact]
    public void Header_join_code_button_toggles_persisted_visibility_and_labels()
    {
        var page = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));
        var layout = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "Pages", "Shared", "_Layout.cshtml"));
        var script = File.ReadAllText(FindFile("src", "BadWolfQuiz.Web", "wwwroot", "js", "site.js"));

        Assert.Contains("setVisible(localStorage.getItem(visibleKey) !== \"true\")", page);
        Assert.Contains("syncOpenButtonLabels(visible)", page);
        Assert.Contains("data-show-join-code-label", layout);
        Assert.Contains("data-hide-join-code-label", layout);
        Assert.DoesNotContain("panel.hidden = !panel.hidden", script);
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
