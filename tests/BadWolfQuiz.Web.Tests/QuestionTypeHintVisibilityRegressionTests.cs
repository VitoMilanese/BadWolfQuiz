namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionTypeHintVisibilityRegressionTests
{
    [Fact]
    public void Question_type_hints_share_visibility_and_font_rules()
    {
        var root = FindRepositoryRoot();
        var styles = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "editor-reset-button.css"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "all-player-question.js"));

        Assert.Contains("#four-clues-help,", styles);
        Assert.Contains(".all-player-editor-help {", styles);
        Assert.Contains("font-family: Inter, Segoe UI, Arial, sans-serif", styles);
        Assert.Contains("font-size: 0.875rem", styles);
        Assert.Contains("font-weight: 400", styles);
        Assert.Contains("line-height: 1.5", styles);
        Assert.Contains("#four-clues-help[hidden],", styles);
        Assert.Contains(".all-player-editor-help[hidden]", styles);
        Assert.Contains("display: none", styles);
        Assert.Contains("textHelp.hidden = !isText", script);
        Assert.Contains("choiceHelp.hidden = !isChoice", script);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
