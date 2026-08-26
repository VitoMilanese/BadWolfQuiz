namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionEditorPreviewOrderRegressionTests
{
    [Fact]
    public void Final_question_editor_orders_preview_buttons_as_question_answer_description()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "FinalQuestionEditor.cshtml"));

        var questionIndex = page.IndexOf(
            "data-open-question-preview=\"question\"",
            StringComparison.Ordinal);
        var answerIndex = page.IndexOf(
            "data-open-question-preview=\"answer\"",
            StringComparison.Ordinal);
        var descriptionIndex = page.IndexOf(
            "data-open-question-preview=\"description\"",
            StringComparison.Ordinal);

        Assert.True(questionIndex >= 0);
        Assert.True(answerIndex > questionIndex);
        Assert.True(descriptionIndex > answerIndex);
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
