namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorQuestionDragDropRegressionTests
{
    [Fact]
    public void Question_drag_drop_updates_board_without_page_reload()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "quiz-editor-question-drag-drop.js"));
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "QuizEditorQuestionDragDropAssetsTagHelper.cs"));

        Assert.Contains(
            "document.addEventListener(\"drop\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation()", script, StringComparison.Ordinal);
        Assert.Contains("?handler=ExchangeQuestions", script, StringComparison.Ordinal);
        Assert.Contains(
            "\"X-Requested-With\": \"XMLHttpRequest\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains("swapQuestionSlots(sourceSlot, targetSlot)", script, StringComparison.Ordinal);
        Assert.Contains("updateQuestionTitle(sourceSlot, targetTitle)", script, StringComparison.Ordinal);
        Assert.Contains("updateQuestionTitle(targetSlot, sourceTitle)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload", script, StringComparison.Ordinal);

        Assert.Contains(
            "ViewContext.ViewData.Model is not EditorModel",
            tagHelper,
            StringComparison.Ordinal);
        Assert.Contains("quiz-editor-question-drag-drop.js", tagHelper, StringComparison.Ordinal);
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
