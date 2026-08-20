namespace BadWolfQuiz.Web.Tests;

public sealed class AddRoundBusyIndicatorRegressionTests
{
    [Fact]
    public void Add_round_submit_is_locked_and_uses_global_busy_indicator()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "busy-indicators.js"));
        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Editor.cshtml"));

        Assert.Contains("asp-page-handler=\"AddRound\"", editor, StringComparison.Ordinal);
        Assert.Contains("handler === \"addround\"", script, StringComparison.Ordinal);
        Assert.Contains("lockAddRoundDialogButtons(form)", script, StringComparison.Ordinal);
        Assert.Contains("form.closest(\"dialog\")", script, StringComparison.Ordinal);
        Assert.Contains("dialog.querySelectorAll(\"button\")", script, StringComparison.Ordinal);
        Assert.Contains("button.disabled = true", script, StringComparison.Ordinal);
        Assert.Contains("button.disabled = wasDisabled", script, StringComparison.Ordinal);
        Assert.Contains("form.dataset.busyLocked === \"true\"", script, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation();", script, StringComparison.Ordinal);
        Assert.Contains("show();", script, StringComparison.Ordinal);
        Assert.Contains("window.addEventListener(\"pageshow\", hide)", script, StringComparison.Ordinal);
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
