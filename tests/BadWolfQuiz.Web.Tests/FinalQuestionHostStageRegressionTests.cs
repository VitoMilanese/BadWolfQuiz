namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionHostStageRegressionTests
{
    [Fact]
    public void Final_question_host_stages_load_scoped_stage_styles_during_soft_navigation()
    {
        var markup = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));
        var imports = File.ReadAllText(FindWebFile(
            "Pages",
            "_ViewImports.cshtml"));
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "FinalQuestionHostStageAssetsTagHelper.cs"));
        var judgingProgressHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "FinalQuestionJudgingProgressTagHelper.cs"));
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "final-question-host-stage.css"));
        var answerSpaceStyles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "final-question-host-answer-space.css"));
        var responsiveness = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "final-question-host-responsiveness.js"));

        Assert.Contains(
            "FinalQuestionHostStageAssetsTagHelper",
            imports,
            StringComparison.Ordinal);
        Assert.Contains(
            "FinalQuestionJudgingProgressTagHelper",
            imports,
            StringComparison.Ordinal);
        Assert.Contains(
            "[HtmlTargetElement(\"div\", Attributes = \"data-host-gameplay-view\")]",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "output.PreContent.AppendHtml",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "final-question-host-stage.css?v=3",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "final-question-host-answer-space.css?v=2",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "final-question-host-responsiveness.js?v=2",
            helper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "IHttpContextAccessor",
            helper,
            StringComparison.Ordinal);

        Assert.Contains("data-host-gameplay-view", markup, StringComparison.Ordinal);
        Assert.Contains("FinalWagering", markup, StringComparison.Ordinal);
        Assert.Contains("FinalAnswering", markup, StringComparison.Ordinal);
        Assert.Contains("FinalJudging", markup, StringComparison.Ordinal);

        Assert.Contains(
            "body.gameplay-layout:has(.host-game-board.final-question-host)",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "repeating-linear-gradient(",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-question-host .final-question-panel",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-game-status=\"finalwagering\"]",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-game-status=\"finalanswering\"] .question-presentation",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-game-status=\"finaljudging\"] .final-player-answer-presentation",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-game-status=\"finaljudging\"] .answer-presentation",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("max-width: none;", styles, StringComparison.Ordinal);
        Assert.Contains("border: 0 !important;", styles, StringComparison.Ordinal);
        Assert.Contains("background: transparent !important;", styles, StringComparison.Ordinal);
        Assert.Contains("box-shadow: none !important;", styles, StringComparison.Ordinal);

        Assert.Contains(
            ".final-question-panel:has(> .answer-presentation) > .eyebrow",
            answerSpaceStyles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-question-host .answer-presentation > .eyebrow",
            answerSpaceStyles,
            StringComparison.Ordinal);
        Assert.Contains("display: none;", answerSpaceStyles, StringComparison.Ordinal);
        Assert.Contains("display: block;", answerSpaceStyles, StringComparison.Ordinal);
        Assert.Contains("text-transform: uppercase;", answerSpaceStyles, StringComparison.Ordinal);

        Assert.Contains(
            "final-judging-list",
            judgingProgressHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "localizer[\"GameBoard_Answer\"]",
            judgingProgressHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "WebUtility.HtmlDecode",
            judgingProgressHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            " / ",
            judgingProgressHelper,
            StringComparison.Ordinal);

        Assert.Contains(
            "PrepareFinalQuestionLeaderboard",
            responsiveness,
            StringComparison.Ordinal);
        Assert.Contains(
            "StartFinalQuestion",
            responsiveness,
            StringComparison.Ordinal);
        Assert.Contains(
            "LockFinalWagers",
            responsiveness,
            StringComparison.Ordinal);
        Assert.Contains(
            "LockFinalAnswers",
            responsiveness,
            StringComparison.Ordinal);
        Assert.Contains(
            "JudgeFinalAnswer",
            responsiveness,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"X-Requested-With\": \"XMLHttpRequest\"",
            responsiveness,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.BadWolfHostGameplay.refresh()",
            responsiveness,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.BadWolfBusy?.show?.()",
            responsiveness,
            StringComparison.Ordinal);
        Assert.Contains(
            "form.matches(\"[data-final-question-transition-form]\")",
            responsiveness,
            StringComparison.Ordinal);
        Assert.Contains(
            "submitter === null",
            responsiveness,
            StringComparison.Ordinal);
        Assert.Contains(
            "Keep the proven browser navigation path for the actual transition",
            responsiveness,
            StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[]
                {
                    directory.FullName,
                    "src",
                    "BadWolfQuiz.Web"
                }.Concat(parts).ToArray());

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {string.Join('/', parts)}");
    }
}
