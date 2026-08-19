namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionTypeHintVisibilityRegressionTests
{
    [Fact]
    public void Hidden_all_player_editor_hints_are_not_rendered()
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
