namespace BadWolfQuiz.Web.Tests;

public sealed class RapidGameplaySubmissionRegressionTests
{
    [Fact]
    public void Global_bootstrap_loads_rapid_submit_guard_before_first_soft_mounted_question()
    {
        var bootstrap = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "final-player-fallback-actions.js"));

        Assert.Contains("/js/host-gameplay-submit-guard.js?v=3", bootstrap);
        Assert.Contains("data.hostGameplaySubmitGuard", bootstrap);
    }

    [Fact]
    public void Busy_gameplay_submit_fallback_runs_in_bubble_phase()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-gameplay-submit-guard.js"));

        Assert.Contains("// Bubble phase is intentional.", script);
        Assert.Contains("document.addEventListener(\"submit\", event => {", script);
        Assert.Contains("            scheduleReplay();\n        });", script);
        Assert.Contains("if (!canNavigate(action))", script);
        Assert.Contains("event.preventDefault();", script);
        Assert.Contains("event.stopImmediatePropagation();", script);
        Assert.Contains("submitter?.hasAttribute(\"disabled\")", script);
        Assert.Contains("pendingSubmission?.key === key", script);
        Assert.Contains("if (pendingSubmission === null)", script);
        Assert.Contains("form.requestSubmit(submitter);", script);
    }

    [Fact]
    public void First_question_click_locks_other_questions_and_uses_delayed_busy_indicator()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-gameplay-submit-guard.js"));

        Assert.Contains("const lockOtherQuestionButtons = submitter =>", script);
        Assert.Contains(".filter(button => button !== submitter)", script);
        Assert.Contains("button.disabled = true;", script);
        Assert.Contains("board.setAttribute(\"aria-busy\", \"true\");", script);
        Assert.Contains("questionBusyDelayMilliseconds = 250", script);
        Assert.Contains("window.BadWolfBusy?.show?.() === true", script);
        Assert.Contains("badwolf:host-gameplay-updated", script);
        Assert.Contains("questionErrorObserver", script);
        Assert.Contains("releaseQuestionSelectionBusy", script);
    }

    [Fact]
    public void Question_selection_stays_locked_through_unrelated_gameplay_refreshes()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-gameplay-submit-guard.js"));

        Assert.Contains("const keepQuestionSelectionLocked = () =>", script);
        Assert.Contains("if (board.hidden)", script);
        Assert.Contains("board.querySelectorAll(questionButtonSelector)", script);
        Assert.Contains(".forEach(rememberAndDisableQuestionButton);", script);
        Assert.Contains("if (board?.hidden === true)", script);
        Assert.Contains("keepQuestionSelectionLocked();", script);
    }

    [Fact]
    public void Host_gameplay_disables_mouse_text_selection_but_keeps_editable_text_selectable()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-gameplay-submit-guard.js"));

        Assert.Contains("const selectionBodyClass = \"host-gameplay-no-select\"", script);
        Assert.Contains("-webkit-user-select: none;", script);
        Assert.Contains("user-select: none;", script);
        Assert.Contains("input:not([type=\"button\"]):not([type=\"submit\"]):not([type=\"reset\"])", script);
        Assert.Contains("[contenteditable=\"true\"]", script);
        Assert.Contains("user-select: text;", script);
        Assert.Contains(".host-game-board, [data-host-gameplay-view], .game-intro-page", script);
        Assert.Contains("badwolf:host-shell-mounted", script);
    }

    [Fact]
    public void Rapid_submit_scope_is_resolved_from_current_soft_mounted_game()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-gameplay-submit-guard.js"));

        Assert.Contains(".host-game-board[data-game-id]", script);
        Assert.Contains("/Admin/Games/Lobby/", script);
        Assert.Contains("/Admin/Games/RoundIntro/", script);
        Assert.Contains("/Admin/Games/RunningRoundIntro/", script);
        Assert.Contains("/Admin/Games/FinalQuestionTransition/", script);
        Assert.Contains("url.origin !== window.location.origin", script);
    }

    [Fact]
    public void Rapid_submit_key_ignores_antiforgery_token_and_includes_submitter_value()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-gameplay-submit-guard.js"));

        Assert.Contains(
            ".filter(([name]) => name !== \"__RequestVerificationToken\")",
            script);
        Assert.Contains("formData.append(submitter.name, submitter.value);", script);
        Assert.Contains("form.method.toUpperCase()", script);
        Assert.Contains("action.href", script);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var pathParts = new string[parts.Length + 3];
            pathParts[0] = directory.FullName;
            pathParts[1] = "src";
            pathParts[2] = "BadWolfQuiz.Web";
            Array.Copy(parts, 0, pathParts, 3, parts.Length);

            var candidate = Path.Combine(pathParts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find web file: {string.Join('/', parts)}.");
    }
}
