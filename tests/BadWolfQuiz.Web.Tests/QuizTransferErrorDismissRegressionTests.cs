namespace BadWolfQuiz.Web.Tests;

public sealed class QuizTransferErrorDismissRegressionTests
{
    [Fact]
    public void QuizListLoadsCacheBustedErrorDismissAsset()
    {
        var root = FindRepositoryRoot();
        var tagHelper = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "TagHelpers",
            "QuizTransferCompletionAssetsTagHelper.cs"));

        Assert.Contains(
            "/js/quiz-transfer-completion.js?v=366.2",
            tagHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/quiz-transfer-error-dismiss.js?v=366.2",
            tagHelper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorDismissDoesNotDependOnTransitionEventToRemoveMessage()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "quiz-transfer-error-dismiss.js"));

        Assert.Contains(
            ".message.message-error[role=\"alert\"]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "message.setAttribute(\"data-auto-dismiss\", \"\")",
            script,
            StringComparison.Ordinal);
        Assert.Contains("dismissDelayMilliseconds = 4000", script, StringComparison.Ordinal);
        Assert.Contains("message.classList.add(\"message-hidden\")", script, StringComparison.Ordinal);
        Assert.Contains("message.addEventListener(\"transitionend\"", script, StringComparison.Ordinal);
        Assert.Contains("window.setTimeout(removeMessage, removalFallbackMilliseconds)", script, StringComparison.Ordinal);
        Assert.Contains("message.remove()", script, StringComparison.Ordinal);
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
