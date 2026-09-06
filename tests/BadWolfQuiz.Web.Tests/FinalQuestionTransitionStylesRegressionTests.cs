namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionTransitionStylesRegressionTests
{
    [Fact]
    public void Transition_keeps_stage_styles_available_during_soft_navigation()
    {
        var markup = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "FinalQuestionTransition.cshtml"));
        var navigation = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "site.js"));

        Assert.Contains("<style data-final-question-stage-styles>", markup);
        Assert.Contains("@@import url(\"/css/final-question-stage.css\");", markup);
        Assert.Contains("main.page-shell > style", navigation);
        Assert.Contains("styles.map(style => document.importNode(style, true))", navigation);
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
