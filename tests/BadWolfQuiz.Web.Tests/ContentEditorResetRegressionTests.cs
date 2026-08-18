namespace BadWolfQuiz.Web.Tests;

public sealed class ContentEditorResetRegressionTests
{
    [Fact]
    public void Reset_control_is_shared_across_content_editors_and_only_reloads_persisted_state()
    {
        var root = FindRepositoryRoot();
        var questionEditor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "QuestionEditor.cshtml"));
        var finalQuestionEditor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "FinalQuestionEditor.cshtml"));
        var descriptionEditor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "DescriptionEditor.cshtml"));
        var bootstrap = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "editor-reset-button.js"));
        var stylesheet = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "editor-reset-button.css"));

        Assert.Contains("question-editor-back-link", questionEditor, StringComparison.Ordinal);
        Assert.Contains("final-question-editor-back-link", finalQuestionEditor, StringComparison.Ordinal);
        Assert.Contains("description-editor-back", descriptionEditor, StringComparison.Ordinal);

        Assert.Contains("#question-editor-back-link", bootstrap, StringComparison.Ordinal);
        Assert.Contains("#final-question-editor-back-link", bootstrap, StringComparison.Ordinal);
        Assert.Contains("#description-editor-back", bootstrap, StringComparison.Ordinal);
        Assert.Contains("/css/editor-reset-button.css", bootstrap, StringComparison.Ordinal);
        Assert.Contains("/js/editor-reset-button.js", bootstrap, StringComparison.Ordinal);

        Assert.Contains("button.type = \"button\"", script, StringComparison.Ordinal);
        Assert.Contains("data-editor-reset", script, StringComparison.Ordinal);
        Assert.Contains("insertAdjacentElement(\"afterend\", button)", script, StringComparison.Ordinal);
        Assert.Contains("targetUrl.searchParams.delete(\"saved\")", script, StringComparison.Ordinal);
        Assert.Contains("targetUrl.searchParams.set(", script, StringComparison.Ordinal);
        Assert.Contains("resetTokenParameter", script, StringComparison.Ordinal);
        Assert.Contains("Date.now().toString()", script, StringComparison.Ordinal);
        Assert.Contains("window.location.replace(targetUrl.href)", script, StringComparison.Ordinal);
        Assert.Contains("window.history.replaceState(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("requestSubmit", script, StringComparison.Ordinal);

        Assert.Contains(".editor-actions .editor-reset-button", stylesheet, StringComparison.Ordinal);
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
