namespace BadWolfQuiz.Web.Tests;

public sealed class EditorSaveShortcutRegressionTests
{
    [Fact]
    public void Quiz_editors_use_keyboard_layout_independent_save_shortcut()
    {
        var root = FindRepositoryRoot();
        var viewImports = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "EditorSaveShortcutAssetsTagHelper.cs"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "editor-save-shortcut.js"));

        Assert.Contains(
            "EditorSaveShortcutAssetsTagHelper, BadWolfQuiz.Web",
            viewImports,
            StringComparison.Ordinal);

        Assert.Contains("EditorModel", tagHelper, StringComparison.Ordinal);
        Assert.Contains("QuestionEditorModel", tagHelper, StringComparison.Ordinal);
        Assert.Contains("FinalQuestionEditorModel", tagHelper, StringComparison.Ordinal);
        Assert.Contains("DescriptionEditorModel", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/js/editor-save-shortcut.js?v=1", tagHelper, StringComparison.Ordinal);

        Assert.Contains("event.code === \"KeyS\"", script, StringComparison.Ordinal);
        Assert.Contains("event.ctrlKey || event.metaKey", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault();", script, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation();", script, StringComparison.Ordinal);
        Assert.Contains("{ capture: true }", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelector(\"dialog[open]\")", script, StringComparison.Ordinal);
        Assert.Contains("form.quiz-board-form", script, StringComparison.Ordinal);
        Assert.Contains("button[data-ajax-save-round]", script, StringComparison.Ordinal);
        Assert.Contains("form[data-ajax-question-editor]", script, StringComparison.Ordinal);
        Assert.Contains("form.description-editor", script, StringComparison.Ordinal);
        Assert.Contains("form.question-editor", script, StringComparison.Ordinal);
        Assert.Contains("target.form.requestSubmit(target.submitter);", script, StringComparison.Ordinal);
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
