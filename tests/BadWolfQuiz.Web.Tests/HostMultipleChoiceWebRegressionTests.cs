namespace BadWolfQuiz.Web.Tests;

public sealed class HostMultipleChoiceWebRegressionTests
{
    [Fact]
    public void Editor_and_host_gameplay_wire_host_multiple_choice_rules()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "QuestionEditor.cshtml.cs"));
        var endpoint = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "HostMultipleChoice.cshtml.cs"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-multiple-choice.js"));
        var viewImports = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));

        Assert.Contains(
            "QuestionPresentationType.HostMultipleChoice",
            editor);
        Assert.Contains("options.Count is < 4 or > 10", editor);
        Assert.Contains("option.TextContent.Trim().Length > 20", editor);
        Assert.Contains("Distinct(StringComparer.OrdinalIgnoreCase)", editor);
        Assert.Contains("isHostMultipleChoice || Input.ExcludeFromRandomWagerSelection", editor);

        Assert.Contains("SelectHostMultipleChoiceOption", endpoint);
        Assert.Contains("HostMultipleChoiceRewardPercentage", endpoint);
        Assert.Contains("RemainingHostMultipleChoiceOptions", endpoint);
        Assert.Contains("BuzzerStateChanged", endpoint);
        Assert.Contains("TimerStateChanged", endpoint);

        Assert.Contains("value = \"4\"", script);
        Assert.Contains("textarea.maxLength = 20", script);
        Assert.Contains("cards.length < 4 || cards.length > 10", script);
        Assert.Contains("host-multiple-choice-correct-badge", script);
        Assert.Contains("host-multiple-choice-panel", script);
        Assert.Contains("data-question-heading", script);
        Assert.Contains("handler=ResolveQuestion", script);
        Assert.Contains("handler=Select", script);
        Assert.Contains("window.setInterval(poll, 750)", script);

        Assert.Contains(
            "HostMultipleChoiceAssetsTagHelper",
            viewImports);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
