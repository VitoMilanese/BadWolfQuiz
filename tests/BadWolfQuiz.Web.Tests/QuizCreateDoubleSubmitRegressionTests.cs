namespace BadWolfQuiz.Web.Tests;

public sealed class QuizCreateDoubleSubmitRegressionTests
{
    [Fact]
    public void Create_quiz_submit_is_locked_after_the_first_submission()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "busy-indicators.js"));
        var page = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Create.cshtml"));

        Assert.Contains("quizCreate: \"/admin/quizzes/create\"", script, StringComparison.Ordinal);
        Assert.Contains("currentPath === routes.quizCreate", script, StringComparison.Ordinal);
        Assert.Contains("lockQuizCreateSubmitter(submitter)", script, StringComparison.Ordinal);
        Assert.Contains("submitter.disabled = true", script, StringComparison.Ordinal);
        Assert.Contains("form.dataset.busyLocked === \"true\"", script, StringComparison.Ordinal);
        Assert.Contains("quiz-create-submit", page, StringComparison.Ordinal);
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
