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
        Assert.Contains("form.closest(\"[data-host-gameplay-view]\")", script);
        Assert.Contains("event.stopImmediatePropagation();", script);
        Assert.Contains("headers: { Accept: \"text/html\" }", script);
        Assert.Contains("window.BadWolfHostGameplay.navigate(responseUrl.href, \"none\")", script);
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
