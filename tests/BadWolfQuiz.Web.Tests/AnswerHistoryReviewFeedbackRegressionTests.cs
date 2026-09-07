namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerHistoryReviewFeedbackRegressionTests
{
    [Fact]
    public void Answer_history_formats_question_numbers_inside_the_razor_expression()
    {
        var markup = ReadWebFile("Pages", "Admin", "Games", "AnswerHistory.cshtml");

        Assert.Contains(
            "@((questionIndex + 1).ToString(\"00\"))",
            markup,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "@(questionIndex + 1).ToString(\"00\")",
            markup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_history_session_card_does_not_expose_the_room_code_and_stays_compact()
    {
        var markup = ReadWebFile("Pages", "Admin", "Games", "AnswerHistory.cshtml");
        var styles = ReadWebFile("wwwroot", "css", "gameplay-review-fixes.css")
            .ReplaceLineEndings("\n");

        Assert.DoesNotContain("answer-history-session-code", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Model.Game.PublicCode", markup, StringComparison.Ordinal);
        Assert.Contains("Model.Game.Session.Quiz.Title", markup, StringComparison.Ordinal);
        Assert.Contains(".answer-history-session {", styles, StringComparison.Ordinal);
        Assert.Contains("min-height: 0;", styles, StringComparison.Ordinal);
        Assert.Contains(".answer-history-session > strong {", styles, StringComparison.Ordinal);
        Assert.Contains("margin-top: 0;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Player_waiting_notice_cannot_consume_the_buzzer_stage()
    {
        var styles = ReadWebFile("wwwroot", "css", "gameplay-review-fixes.css")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            ".player-lobby[data-final-status=\"lobby\"] > .dialog-warning {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("align-self: start;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-rows: none;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-auto-rows: max-content;", styles, StringComparison.Ordinal);
        Assert.Contains("align-content: start;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Player_buzzer_timer_spans_the_live_gameplay_grid_and_is_large()
    {
        var styles = ReadWebFile("wwwroot", "css", "gameplay-review-fixes.css")
            .ReplaceLineEndings("\n");
        var assets = ReadWebFile("TagHelpers", "AnonymousSharedWagerAssetsTagHelper.cs");

        Assert.Contains(
            ".player-lobby:has(.player-buzzer-panel) > #game-timer.player-game-timer {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("grid-column: 1 / -1 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("width: min(520px, calc(100% - 2rem)) !important;", styles, StringComparison.Ordinal);
        Assert.Contains("height: auto !important;", styles, StringComparison.Ordinal);
        Assert.Contains(
            ".player-lobby:has(.player-buzzer-panel) > #game-timer.player-game-timer > strong {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("font-size: clamp(4rem, 7vw, 6.25rem) !important;", styles, StringComparison.Ordinal);
        Assert.Contains("/css/gameplay-review-fixes.css?v=2", assets, StringComparison.Ordinal);
    }

    [Fact]
    public void Player_answer_timer_uses_the_answer_color_for_the_digits_too()
    {
        var styles = ReadWebFile("wwwroot", "css", "gameplay-review-fixes.css")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            ".player-lobby:has(.player-buzzer-panel) > #game-timer.player-game-timer.answer-timer {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".player-lobby:has(.player-buzzer-panel) > #game-timer.player-game-timer.answer-timer > strong {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("color: currentColor !important;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_history_dialogs_cannot_overflow_horizontally()
    {
        var styles = ReadWebFile("wwwroot", "css", "gameplay-review-fixes.css")
            .ReplaceLineEndings("\n");

        Assert.Contains(".answer-history-dialog {", styles, StringComparison.Ordinal);
        Assert.Contains("width: min(680px, calc(100vw - 32px));", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-x: hidden;", styles, StringComparison.Ordinal);
        Assert.Contains(".answer-history-dialog-card {", styles, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", styles, StringComparison.Ordinal);
        Assert.Contains("max-width: 100%;", styles, StringComparison.Ordinal);
        Assert.Contains("box-sizing: border-box;", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_history_update_and_delete_use_ajax_without_page_navigation()
    {
        var script = ReadWebFile("wwwroot", "js", "answer-history-live-edit.js")
            .ReplaceLineEndings("\n");
        var model = ReadWebFile("Pages", "Admin", "Games", "AnswerHistory.cshtml.cs")
            .ReplaceLineEndings("\n");
        var assets = ReadWebFile("TagHelpers", "HostNavigationGuardAssetsTagHelper.cs");

        Assert.Contains("form.matches(\".answer-history-entry-form\")", script, StringComparison.Ordinal);
        Assert.Contains("form.closest(\"#delete-answer-history-dialog\")", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault();", script, StringComparison.Ordinal);
        Assert.Contains("fetch(form.action", script, StringComparison.Ordinal);
        Assert.Contains("\"X-Requested-With\": \"XMLHttpRequest\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.assign", script, StringComparison.Ordinal);

        Assert.Contains("private bool IsAjaxRequest()", model, StringComparison.Ordinal);
        Assert.Contains("return new JsonResult(new", model, StringComparison.Ordinal);
        Assert.Contains("CreateUpdateAjaxData", model, StringComparison.Ordinal);
        Assert.Contains("CreateDeleteAjaxData", model, StringComparison.Ordinal);
        Assert.Contains("questionIsVisible = IsVisibleInHistory(question)", model, StringComparison.Ordinal);
        Assert.Contains("noEntriesLabel = localizer[\"AnswerHistory_NoEntries\"].Value", model, StringComparison.Ordinal);
        Assert.Contains("/js/answer-history-live-edit.js?v=1", assets, StringComparison.Ordinal);
        Assert.Contains("/css/gameplay-review-fixes.css?v=2", assets, StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_history_loads_the_review_fix_asset_through_its_existing_page_asset_guard()
    {
        var assets = ReadWebFile("TagHelpers", "HostNavigationGuardAssetsTagHelper.cs");

        Assert.Contains("model is AnswerHistoryModel", assets, StringComparison.Ordinal);
        Assert.Contains("/css/gameplay-review-fixes.css?v=2", assets, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate web file: {string.Join('/', parts)}");
    }
}
