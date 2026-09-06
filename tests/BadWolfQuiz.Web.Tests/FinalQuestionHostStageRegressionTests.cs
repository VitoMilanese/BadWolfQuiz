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
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "final-question-host-stage.css"));

        Assert.Contains(
            "FinalQuestionHostStageAssetsTagHelper",
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
            "final-question-host-stage.css?v=2",
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
            "background: transparent;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-game-status=\"finalwagering\"]",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-game-status=\"finalanswering\"]",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-game-status=\"finaljudging\"]",
            styles,
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
