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
        Assert.Contains("/css/gameplay-review-fixes.css?v=1", assets, StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_history_loads_the_review_fix_asset_through_its_existing_page_asset_guard()
    {
        var assets = ReadWebFile("TagHelpers", "HostNavigationGuardAssetsTagHelper.cs");

        Assert.Contains("model is AnswerHistoryModel", assets, StringComparison.Ordinal);
        Assert.Contains("/css/gameplay-review-fixes.css?v=1", assets, StringComparison.Ordinal);
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
