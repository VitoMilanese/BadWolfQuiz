namespace BadWolfQuiz.Web.Tests;

public sealed class EditorSaveOverlayRegressionTests
{
    [Fact]
    public void Editor_save_results_use_shared_bottom_overlay()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "image-clipboard-upload.js"));
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

        Assert.Contains("editorSaveOverlayDurationMs = 1000", script, StringComparison.Ordinal);
        Assert.Contains(".editor-save-overlay", script, StringComparison.Ordinal);
        Assert.Contains("bottom: max(24px, env(safe-area-inset-bottom))", script, StringComparison.Ordinal);
        Assert.Contains("pointer-events: none", script, StringComparison.Ordinal);
        Assert.Contains("document.body.appendChild(status)", script, StringComparison.Ordinal);
        Assert.Contains("MutationObserver", script, StringComparison.Ordinal);
        Assert.Contains("#success-message, [data-question-save-status]", script, StringComparison.Ordinal);
        Assert.Contains(
            ".validation-summary li, .field-validation-error, .text-danger",
            script,
            StringComparison.Ordinal);

        Assert.Contains("data-question-save-status", questionEditor, StringComparison.Ordinal);
        Assert.Contains("image-clipboard-upload.js", questionEditor, StringComparison.Ordinal);
        Assert.Contains("id=\"success-message\"", finalQuestionEditor, StringComparison.Ordinal);
        Assert.Contains("image-clipboard-upload.js", finalQuestionEditor, StringComparison.Ordinal);
        Assert.Contains("Request.Query[\"saved\"]", descriptionEditor, StringComparison.Ordinal);
        Assert.Contains("Message_Saved", descriptionEditor, StringComparison.Ordinal);
        Assert.Contains("image-clipboard-upload.js", descriptionEditor, StringComparison.Ordinal);
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
