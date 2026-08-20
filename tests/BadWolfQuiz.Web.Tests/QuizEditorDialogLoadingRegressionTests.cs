namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorDialogLoadingRegressionTests
{
    [Fact]
    public void Copy_and_exchange_dialogs_show_local_busy_state_while_destinations_are_prepared()
    {
        var root = FindRepositoryRoot();
        var loadingScript = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "quiz-editor-dialog-loading.js"));
        var copyScript = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "question-copy-action.js"));
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "QuizEditorQuestionRoundExchangeAssetsTagHelper.cs"));
        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Editor.cshtml"));

        Assert.Contains("dialog.dataset.questionCopyDialog = \"true\"", copyScript, StringComparison.Ordinal);
        Assert.Contains("setStatus(labels.loading)", copyScript, StringComparison.Ordinal);
        Assert.Contains(
            "dialog[data-question-copy-dialog] .alert:not(.alert-error):not([hidden])",
            loadingScript,
            StringComparison.Ordinal);
        Assert.Contains("quiz-editor-dialog-loading", loadingScript, StringComparison.Ordinal);
        Assert.Contains("dataset.dialogDestinationLoading", loadingScript, StringComparison.Ordinal);
        Assert.Contains("aria-busy", loadingScript, StringComparison.Ordinal);
        Assert.Contains("runAfterPaint", loadingScript, StringComparison.Ordinal);
        Assert.Contains("window.openExchangeCategoryDialog", loadingScript, StringComparison.Ordinal);
        Assert.Contains("window.openExchangeQuestionDialog", loadingScript, StringComparison.Ordinal);
        Assert.Contains("exchangeCategoryRounds.filter", loadingScript, StringComparison.Ordinal);
        Assert.Contains("exchangeQuestionRounds.filter", loadingScript, StringComparison.Ordinal);
        Assert.Contains("refreshExchangeTargetCategories()", loadingScript, StringComparison.Ordinal);
        Assert.Contains("refreshExchangeTargetQuestions()", loadingScript, StringComparison.Ordinal);
        Assert.Contains("control.disabled = true", loadingScript, StringComparison.Ordinal);
        Assert.Contains("quiz-editor-dialog-loading.js?v=271.1", tagHelper, StringComparison.Ordinal);
        Assert.Contains("id=\"exchange-category-dialog\"", editor, StringComparison.Ordinal);
        Assert.Contains("id=\"exchange-question-dialog\"", editor, StringComparison.Ordinal);
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
