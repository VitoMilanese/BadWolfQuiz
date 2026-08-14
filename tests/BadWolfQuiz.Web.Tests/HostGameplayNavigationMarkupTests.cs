namespace BadWolfQuiz.Web.Tests;

public sealed class HostGameplayNavigationMarkupTests
{
    [Fact]
    public void Running_host_keeps_the_board_mounted_while_gameplay_view_changes()
    {
        var markup = File.ReadAllText(FindLobbyView());

        Assert.Contains("id=\"host-gameplay-view\" data-host-gameplay-view", markup);
        Assert.Contains("data-host-gameplay-board", markup);
        Assert.Contains("Model.CurrentQuestion is not null ||", markup);
        Assert.Contains("Model.PreviewQuestion is not null ||", markup);
        Assert.Contains("Model.IsRoundSummaryVisible ? \"hidden\" : null", markup);
        Assert.Contains("data-host-gameplay-transient", markup);
    }

    [Fact]
    public void Resolved_question_preview_navigation_replaces_only_the_gameplay_view()
    {
        var markup = File.ReadAllText(FindLobbyView());

        Assert.Contains("window.BadWolfHostGameplay = (() =>", markup);
        Assert.Contains("new DOMParser().parseFromString(markup, \"text/html\")", markup);
        Assert.Contains("currentView.replaceChildren(", markup);
        Assert.Contains("currentBoard.hidden = nextBoard.hidden;", markup);
        Assert.Contains("a.host-board-question.status-resolved, .question-review-actions a", markup);
        Assert.Contains("history.pushState({ hostGameplay: true }", markup);
        Assert.Contains("window.addEventListener(\"popstate\"", markup);
        Assert.Contains("navigate(window.location.href, \"none\")", markup);
    }

    [Fact]
    public void Selecting_a_question_refreshes_gameplay_without_reloading_the_page()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var selectionStart = markup.IndexOf(
            "for (const form of questionSelectionForms)",
            StringComparison.Ordinal);
        var selectionEnd = markup.IndexOf(
            "document.addEventListener(\"submit\", async event =>",
            selectionStart,
            StringComparison.Ordinal);
        var selectionHandler = markup[selectionStart..selectionEnd];

        Assert.Contains("await requestHostGameplayRefresh();", selectionHandler);
        Assert.DoesNotContain("requestHostReload();", selectionHandler);
    }

    [Fact]
    public void Dynamically_mounted_question_controls_are_reinitialized()
    {
        var markup = File.ReadAllText(FindLobbyView());

        Assert.Contains("badwolf:host-gameplay-updated", markup);
        Assert.Contains("timerPanel = document.getElementById(\"game-timer\")", markup);
        Assert.Contains("questionHeading = document.querySelector(\"[data-question-heading]\")", markup);
        Assert.Contains("initializeWagerForm", markup);
        Assert.Contains("event.target.matches(\"[data-reveal-clue-form]\")", markup);
        Assert.Contains("event.target.matches(\".game-timer-pause\")", markup);
        Assert.Contains("event.target.matches(\".question-judge-actions\")", markup);
    }

    [Fact]
    public void Persistent_board_state_is_reused_for_round_summary_and_host_card_layout()
    {
        var markup = File.ReadAllText(FindLobbyView());

        Assert.Contains("await window.BadWolfHostGameplay.refresh();", markup);
        Assert.DoesNotContain(
            "currentLayout.replaceWith(document.importNode(nextSummary, true))",
            markup);
        Assert.Contains(
            "document.querySelector(\"[data-host-gameplay-board]\")?.hidden === false",
            markup);
        Assert.Contains("relocateAndRestoreHostCard();", markup);
        Assert.Contains("refreshScrollingNames();", markup);
    }

    [Fact]
    public void Partial_gameplay_layout_preserves_full_bleed_and_hides_the_board()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        Assert.Contains("[data-host-gameplay-view]", css);
        Assert.Contains("display: contents;", css);
        Assert.Contains(".host-board-layout[hidden]", css);
        Assert.Contains("display: none;", css);
        Assert.Contains(
            ".host-game-board:has(> [data-host-gameplay-view] > .current-question-summary:not(.wager-mode))",
            css);
        Assert.Contains("width: calc(100vw - 32px);", css);
    }

    [Fact]
    public void Gameplay_forms_use_partial_navigation_after_dynamic_replacement()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));

        Assert.Contains("configureHostGameplayFormNavigation", script);
        Assert.Contains("form.matches(\".question-selection-form\")", script);
        Assert.Contains("form.closest(viewSelector)", script);
        Assert.Contains("event.stopImmediatePropagation();", script);
        Assert.Contains("expectsJson", script);
        Assert.Contains("Accept: \"application/json\"", script);
        Assert.Contains("\"X-Requested-With\": \"XMLHttpRequest\"", script);
        Assert.Contains("showHostGameplayError", script);
        Assert.Contains("await window.BadWolfHostGameplay.refresh();", script);
        Assert.Contains("await applyMarkup(await response.text(), responseUrl);", script);
        Assert.Contains("if (!canNavigate(action))", script);
    }

    [Fact]
    public void Partial_refresh_synchronizes_resolved_question_tiles_from_server_markup()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));

        Assert.Contains("const syncBoardQuestions = nextBoard =>", script);
        Assert.Contains("getQuestionContainer(currentQuestion).replaceWith(", script);
        Assert.Contains("syncBoardQuestions(nextBoard);", script);
        Assert.Contains("currentGrid.replaceChildren(", script);
        Assert.Contains("const sameRound =", script);
        Assert.Contains("previewQuestionId", script);
    }

    [Fact]
    public void Partial_refresh_uses_round_identity_when_category_ids_repeat_between_rounds()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));

        Assert.Contains(
            "data-source-round-id=",
            markup);
        Assert.Contains("const currentRoundId = currentGrid.dataset.sourceRoundId;", script);
        Assert.Contains("const nextRoundId = nextGrid.dataset.sourceRoundId;", script);
        Assert.Contains("? currentRoundId === nextRoundId", script);
        Assert.Contains("currentGrid.dataset.sourceRoundId = nextRoundId;", script);
        Assert.Contains("const syncPersistentRoundBoard = (currentBoard, nextBoard) =>", markup);
        Assert.Contains("syncPersistentRoundBoard(currentBoard, nextBoard);", markup);
        Assert.Contains("currentRoundHeading.textContent = nextRoundHeading.textContent;", markup);
    }

    [Fact]
    public void Player_blocking_uses_partial_form_submission()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));

        Assert.Contains("form.id === \"remove-player-form\"", script);
        Assert.Contains("[data-confirm-block-player]", script);
        Assert.Contains("blockInput.value = blockPlayer ? \"true\" : \"false\";", script);
        Assert.Contains("form.requestSubmit();", script);
    }

    [Fact]
    public void Running_round_and_final_transition_pages_can_render_inside_the_gameplay_region()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));

        Assert.Contains("/Admin/Games/RunningRoundIntro/", script);
        Assert.Contains("/Admin/Games/FinalQuestionTransition/", script);
        Assert.Contains("const renderExternalFlow = parsed =>", script);
        Assert.Contains("[data-game-intro-page], [data-final-question-transition]", script);
        Assert.Contains("window.BadWolfHostFlowNavigation.navigate(targetUrl)", script);
        Assert.Contains("await window.BadWolfHostFlowNavigation.navigate(responseUrl);", script);
    }

    [Fact]
    public void Resolved_question_preview_uses_the_full_host_viewport()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        Assert.Contains(
            ".host-game-board:has(> [data-host-gameplay-view] > .question-review-preview)",
            css);
        Assert.Contains(
            "[data-host-gameplay-view] > .question-review-preview",
            css);
        Assert.Contains("height: 100%;", css);
        Assert.Contains("margin-bottom: 0;", css);
    }

    [Fact]
    public void Embedded_category_intro_is_constrained_to_the_host_viewport()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-admission-menu.css"));
        var selector = "[data-host-gameplay-view] > [data-game-intro-page]";
        var blockStart = css.LastIndexOf(selector, StringComparison.Ordinal);
        var blockEnd = css.IndexOf('}', blockStart);
        var block = css[blockStart..blockEnd];

        Assert.Contains("flex: 1 1 auto;", block);
        Assert.Contains("min-height: 0;", block);
        Assert.Contains("height: 100%;", block);
        Assert.Contains("max-height: 100%;", block);
    }

    [Fact]
    public void Presentation_views_hide_persistent_player_cards_and_join_code_panel()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        Assert.Contains("const syncPersistentHostChrome = () =>", script);
        Assert.Contains("host-gameplay-presentation-mode", script);
        Assert.Contains(".question-review-preview, [data-game-intro-page], [data-final-question-transition]", script);
        Assert.Contains("--game-scoreboard-space: 0px !important;", css);
        Assert.Contains("host-gameplay-presentation-mode > .game-scoreboard", css);
        Assert.Contains("host-gameplay-presentation-mode > .game-side-controls", css);
        Assert.Contains("const hidesPlayerPanel = view !== null &&", script);
        Assert.Contains("const exclusiveGameplayView = gameplayView !== null &&", markup);
        Assert.Contains("exclusiveGameplayView;", markup);
    }

    [Fact]
    public void Blocked_player_dialog_is_synchronized_after_partial_player_commands()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));

        Assert.Contains("const syncBlockedPlayers = parsed =>", script);
        Assert.Contains("parsed.getElementById(\"blocked-players-dialog\")", script);
        Assert.Contains("syncBlockedPlayers(parsed);", script);
        Assert.Contains("form.closest(\"#blocked-players-dialog\") !== null", script);
    }

    [Fact]
    public void Resolved_preview_renders_content_directly_inside_the_outer_preview_panel()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-admission-menu.css"));
        var previewMarkup = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "_GameContentPreview.cshtml"));

        Assert.DoesNotContain("<div class=\"question-review-content\">", markup);
        Assert.Contains(
            "@previewQuestion.CategoryTitle — @previewQuestion.Points —",
            markup);
        Assert.Contains("Localizer[\"GameBoard_Question\"]", markup);
        Assert.Contains("Localizer[\"GameBoard_Answer\"]", markup);
        Assert.DoesNotContain("<p class=\"eyebrow\">", previewMarkup);
        Assert.Contains("<section class=\"content-panel question-review-preview\">", markup);
        Assert.Contains("<partial name=\"_GameContentPreview\"", markup);
        Assert.Contains(
            "[data-host-gameplay-view] > .question-review-preview > .game-content-presentation",
            css);
        Assert.Contains("max-width: none;", css);
    }

    [Fact]
    public void Final_question_uses_partial_gameplay_refresh_and_keeps_player_cards_visible()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        Assert.True(
            markup.Split("id=\"host-gameplay-view\" data-host-gameplay-view", StringSplitOptions.None).Length >= 3);
        Assert.Contains("<div data-host-gameplay-board hidden></div>", markup);
        Assert.Contains("currentHostBoard.classList.toggle(", markup);
        Assert.Contains("nextHostBoard.classList.contains(\"final-question-host\")", markup);
        Assert.Contains("currentHostBoard.dataset.gameStatus", markup);
        Assert.Contains("event.defaultPrevented ||", script);
        Assert.Contains(".host-game-board.final-question-host > .game-side-controls", css);

        var progressStart = markup.IndexOf(
            "connection.on(\"FinalQuestionProgressChanged\"",
            StringComparison.Ordinal);
        var progressEnd = markup.IndexOf(
            "const joinSession",
            progressStart,
            StringComparison.Ordinal);
        var progressHandler = markup[progressStart..progressEnd];
        Assert.Contains("void requestHostGameplayRefresh();", progressHandler);
        Assert.DoesNotContain("requestHostReload();", progressHandler);
    }

    [Fact]
    public void Partial_round_refresh_synchronizes_tools_navigation_visibility()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "site.css"));

        Assert.Contains("!Model.Game.Session.HasPreviousUnfinishedRound", markup);
        Assert.Contains("!Model.Game.Session.HasNextUnfinishedRound", markup);
        Assert.Contains("const syncRoundNavigationActions = parsed =>", markup);
        Assert.Contains("[data-open-previous-round-dialog]", markup);
        Assert.Contains("#force-advance-round-form", markup);
        Assert.Contains("currentAction.hidden = nextAction.hidden;", markup);
        Assert.Contains("syncRoundNavigationActions(parsed);", markup);
        Assert.Contains(".action-menu-popover form[hidden]", css);
        Assert.Contains(".action-menu-popover .action-menu-item[hidden]", css);
        Assert.Contains("display: none !important;", css);
    }

    [Fact]
    public void Forced_final_confirmation_uses_partial_host_flow_navigation()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));

        Assert.Contains("forceAdvanceFinalForm?.requestSubmit();", markup);
        Assert.DoesNotContain("forceAdvanceFinalForm?.submit();", markup);
        Assert.Contains("handler === \"PrepareFinalQuestionLeaderboard\"", script);
        Assert.Contains("[data-confirm-force-advance-final]", script);
        Assert.Contains("fetch(form.action", script);
        Assert.Contains("await window.BadWolfHostFlowNavigation.navigate(responseUrl);", script);
    }

    [Fact]
    public void Repeated_leaderboard_refresh_does_not_restart_podium_animation()
    {
        var markup = File.ReadAllText(FindLobbyView());

        Assert.Contains("const animatedLeaderboardSignature = view =>", markup);
        Assert.Contains("const preserveAnimatedLeaderboard =", markup);
        Assert.Contains("currentLeaderboardSignature === nextLeaderboardSignature", markup);
        Assert.Contains("if (!preserveAnimatedLeaderboard)", markup);
    }

    [Fact]
    public void Natural_final_warning_actions_close_and_return_without_a_second_click()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));
        var runningIntro = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "RunningRoundIntro.cshtml.cs"));

        var naturalDialogStart = markup.IndexOf(
            "<dialog id=\"natural-final-warning-dialog\"",
            StringComparison.Ordinal);
        var naturalDialogEnd = markup.IndexOf(
            "</dialog>",
            naturalDialogStart,
            StringComparison.Ordinal);
        var naturalDialog = markup[naturalDialogStart..naturalDialogEnd];
        Assert.Contains(
            "name=\"skipLeaderboard\" value=\"true\"",
            naturalDialog);

        var finishDialogStart = markup.IndexOf(
            "<dialog id=\"finish-game-warning-dialog\"",
            StringComparison.Ordinal);
        var finishDialogEnd = markup.IndexOf(
            "</dialog>",
            finishDialogStart,
            StringComparison.Ordinal);
        var finishDialog = markup[finishDialogStart..finishDialogEnd];
        Assert.DoesNotContain("name=\"skipLeaderboard\"", finishDialog);
        var finalHandlerStart = script.IndexOf(
            "if ((handler === \"StartFinalQuestion\" ||",
            StringComparison.Ordinal);
        var finalHandlerEnd = script.IndexOf(
            "if (handler === \"Previous\" ||",
            finalHandlerStart,
            StringComparison.Ordinal);
        var finalHandler = script[finalHandlerStart..finalHandlerEnd];
        Assert.Contains("form.closest(\"dialog\")?.close();", finalHandler);
        Assert.Contains("bool skipLeaderboard", runningIntro);
        Assert.Contains("!skipLeaderboard &&", runningIntro);
    }

    [Fact]
    public void First_round_soft_mount_reinitializes_host_navigation_and_repairs_incomplete_board_dom()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));
        var discordScript = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-settings-dialog.js"));

        Assert.Contains("badwolf:host-shell-mounted", script);
        Assert.Contains("configureDynamicHostShell", script);
        Assert.Contains("gameRoundIntroRoutesConfigured", script);
        Assert.Contains("hostGameplayFormNavigationConfigured", script);
        Assert.Contains("const nextById = new Map(", script);
        Assert.Contains("currentById.size !== nextById.size", script);
        Assert.Contains("replaceGrid();", script);
        Assert.Contains("badwolf:host-shell-mounted", discordScript);
        Assert.Contains("discordSettingsDialogInitialized", discordScript);
    }

    [Fact]
    public void Previous_round_intro_is_protected_from_stale_buzzer_refreshes()
    {
        var markup = File.ReadAllText(FindLobbyView());
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));

        Assert.Contains("const isExternalHostFlowActive = () =>", markup);
        Assert.Contains("!isExternalHostFlowActive()", markup);
        Assert.Contains("const cancelPending = () =>", markup);
        Assert.Contains("return { cancelPending, refresh, navigate, updatedEventName };", markup);
        Assert.Contains("window.BadWolfHostGameplay?.cancelPending?.();", script);
    }

    private static string FindLobbyView() => FindWebFile(
        "Pages",
        "Admin",
        "Games",
        "Lobby.cshtml");

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[]
            {
                directory.FullName,
                "src",
                "BadWolfQuiz.Web"
            }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
