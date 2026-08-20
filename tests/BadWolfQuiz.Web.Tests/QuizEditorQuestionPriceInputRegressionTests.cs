namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorQuestionPriceInputRegressionTests
{
    [Fact]
    public void Custom_question_prices_keep_native_100_step_but_validate_as_positive_integers_on_submit()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Editor.cshtml"));
        var pageModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Editor.cshtml.cs"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "quiz-editor-question-price-input.js"));
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "QuizEditorQuestionRoundExchangeAssetsTagHelper.cs"));

        Assert.Contains("class=\"question-points-input\"", editor, StringComparison.Ordinal);
        Assert.Contains("step=\"100\"", editor, StringComparison.Ordinal);
        Assert.Contains("input.min = \"1\"", script, StringComparison.Ordinal);
        Assert.Contains("input.step = \"100\"", script, StringComparison.Ordinal);
        Assert.Contains("input.required = true", script, StringComparison.Ordinal);
        Assert.Contains("form.noValidate = true", script, StringComparison.Ordinal);
        Assert.Contains("input.step = \"1\"", script, StringComparison.Ordinal);
        Assert.Contains("form.checkValidity()", script, StringComparison.Ordinal);
        Assert.Contains("form.reportValidity()", script, StringComparison.Ordinal);
        Assert.Contains("input.step = originalSteps[index]", script, StringComparison.Ordinal);
        Assert.Contains("x.Points <= 0", pageModel, StringComparison.Ordinal);
        Assert.Contains(
            "quiz-editor-question-price-input.js?v=273.1",
            tagHelper,
            StringComparison.Ordinal);
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
