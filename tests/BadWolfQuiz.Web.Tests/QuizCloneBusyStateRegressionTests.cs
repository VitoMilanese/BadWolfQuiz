namespace BadWolfQuiz.Web.Tests;

public sealed class QuizCloneBusyStateRegressionTests
{
    [Fact]
    public void Clone_dialog_uses_shared_busy_overlay_and_locks_interactive_controls()
    {
        var source = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js", "quiz-clone-action.js"));

        Assert.Contains("form.dataset.busyLocked = \"true\"", source);
        Assert.Contains("window.BadWolfBusy?.show()", source);
        Assert.Contains("dialog.querySelectorAll(\"button\")", source);
        Assert.Contains("button.disabled = busyState", source);
        Assert.Contains("nameInput.readOnly = busyState", source);
        Assert.Contains("nameInput.setAttribute(\"aria-disabled\", \"true\")", source);
    }

    [Fact]
    public void Clone_dialog_prevents_duplicate_submit_and_cannot_close_while_busy()
    {
        var source = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js", "quiz-clone-action.js"));

        Assert.Contains("if (form.dataset.busyLocked === \"true\")", source);
        Assert.Contains("event.stopImmediatePropagation()", source);
        Assert.Contains("if (form.dataset.busyLocked !== \"true\")", source);
        Assert.Contains("dialog.addEventListener(\"cancel\"", source);
        Assert.Contains("window.addEventListener(\"pageshow\", () => setBusyState(false))", source);
    }

    [Fact]
    public void Clone_busy_state_keeps_post_fields_successful_controls()
    {
        var source = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js", "quiz-clone-action.js"));

        Assert.Contains("nameInput.readOnly = busyState", source);
        Assert.DoesNotContain("nameInput.disabled = busyState", source);
        Assert.DoesNotContain("quizId.disabled", source);
        Assert.DoesNotContain("token.disabled", source);
    }

    private static string FindRepoFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find repository file: {Path.Combine(segments)}");
    }
}
