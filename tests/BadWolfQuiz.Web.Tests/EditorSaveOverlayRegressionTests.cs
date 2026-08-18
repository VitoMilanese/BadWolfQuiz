namespace BadWolfQuiz.Web.Tests;

public sealed class EditorSaveOverlayRegressionTests
{
    [Fact]
    public void Editor_save_results_use_shared_top_overlay_across_quiz_editors()
    {
        var root = FindRepositoryRoot();
        var overlayScript = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "editor-save-overlay.js"));
        var gameplayBootstrap = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));
        var imageClipboardScript = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "image-clipboard-upload.js"));
        var quizEditor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Editor.cshtml"));
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
        var descriptionEditorModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "DescriptionEditor.cshtml.cs"));

        Assert.Contains("editor-save-overlay.js", gameplayBootstrap, StringComparison.Ordinal);
        Assert.Contains(
            "form.quiz-board-form, form.question-editor",
            gameplayBootstrap,
            StringComparison.Ordinal);

        Assert.Contains("editorSaveOverlayDurationMs = 1500", overlayScript, StringComparison.Ordinal);
        Assert.Contains(
            "form.quiz-board-form, form.question-editor",
            overlayScript,
            StringComparison.Ordinal);
        Assert.Contains("body .editor-save-overlay", overlayScript, StringComparison.Ordinal);
        Assert.Contains(
            "top: calc(var(--topbar-height, 60px) + 16px + env(safe-area-inset-top)) !important",
            overlayScript,
            StringComparison.Ordinal);
        Assert.Contains("bottom: auto !important", overlayScript, StringComparison.Ordinal);
        Assert.Contains("pointer-events: none", overlayScript, StringComparison.Ordinal);
        Assert.Contains("font-size: 1.08rem", overlayScript, StringComparison.Ordinal);
        Assert.Contains("font-weight: 600", overlayScript, StringComparison.Ordinal);
        Assert.Contains("background: rgba(63, 185, 80, 0.34)", overlayScript, StringComparison.Ordinal);
        Assert.Contains("background: rgba(239, 35, 60, 0.34)", overlayScript, StringComparison.Ordinal);
        Assert.Contains("[data-editor-save-status]", overlayScript, StringComparison.Ordinal);
        Assert.Contains("[data-quiz-save-status]", overlayScript, StringComparison.Ordinal);
        Assert.Contains("[data-question-save-status]", overlayScript, StringComparison.Ordinal);
        Assert.Contains("MutationObserver", overlayScript, StringComparison.Ordinal);

        Assert.DoesNotContain("editorSaveOverlayDurationMs", imageClipboardScript, StringComparison.Ordinal);
        Assert.DoesNotContain(".editor-save-overlay", imageClipboardScript, StringComparison.Ordinal);

        Assert.Contains("data-quiz-save-status", quizEditor, StringComparison.Ordinal);
        Assert.Contains("data-question-save-status", questionEditor, StringComparison.Ordinal);
        Assert.Contains("id=\"success-message\"", finalQuestionEditor, StringComparison.Ordinal);
        Assert.Contains("bool.TryParse(Request.Query[\"saved\"].ToString()", descriptionEditor, StringComparison.Ordinal);
        Assert.Contains("data-editor-save-status", descriptionEditor, StringComparison.Ordinal);
        Assert.Contains("Message_Saved", descriptionEditor, StringComparison.Ordinal);
        Assert.Contains("Input.CategoryId", descriptionEditor, StringComparison.Ordinal);
        Assert.Contains("saved = true", descriptionEditorModel, StringComparison.Ordinal);
        Assert.Contains("categoryId = Input.CategoryId", descriptionEditorModel, StringComparison.Ordinal);
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
