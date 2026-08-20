namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorQuestionRoundExchangeRegressionTests
{
    [Fact]
    public void Cross_round_question_exchange_uses_busy_indicator_without_page_reload()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "quiz-editor-question-round-exchange.js"));
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "QuizEditorQuestionRoundExchangeAssetsTagHelper.cs"));
        var viewImports = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));
        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Editor.cshtml"));

        Assert.Contains("id=\"exchange-question-dialog\"", editor, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"ExchangeQuestions\"", editor, StringComparison.Ordinal);
        Assert.Contains("form.addEventListener(\"submit\"", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault();", script, StringComparison.Ordinal);
        Assert.Contains("if (submitting)", script, StringComparison.Ordinal);
        Assert.Contains("dialog.querySelectorAll(\"button\")", script, StringComparison.Ordinal);
        Assert.Contains("button.disabled = true", script, StringComparison.Ordinal);
        Assert.Contains("dialog.close();", script, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfBusy?.show();", script, StringComparison.Ordinal);
        Assert.Contains("await fetch(form.action", script, StringComparison.Ordinal);
        Assert.Contains("new DOMParser().parseFromString", script, StringComparison.Ordinal);
        Assert.Contains("sourceSlot.innerHTML = replacementSlot.innerHTML", script, StringComparison.Ordinal);
        Assert.Contains("syncExchangeQuestionRounds", script, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfBusy?.hide();", script, StringComparison.Ordinal);
        Assert.Contains("button.disabled = wasDisabled", script, StringComparison.Ordinal);
        Assert.Contains("dialog.showModal();", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.assign", script, StringComparison.Ordinal);
        Assert.Contains("quiz-editor-question-round-exchange.js", tagHelper, StringComparison.Ordinal);
        Assert.Contains(
            "QuizEditorQuestionRoundExchangeAssetsTagHelper",
            viewImports,
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
