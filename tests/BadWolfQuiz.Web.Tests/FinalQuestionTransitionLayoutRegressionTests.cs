namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionTransitionLayoutRegressionTests
{
    [Fact]
    public void Transition_fits_available_viewport_without_page_shell_overflow()
    {
        var root = FindRepositoryRoot();
        var transition = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "FinalQuestionTransition.cshtml"));

        Assert.Contains(
            "body.gameplay-layout:has(.final-question-transition) > .page-shell",
            transition,
            StringComparison.Ordinal);
        Assert.Contains(
            "height: calc(100dvh - var(--topbar-height, 60px));",
            transition,
            StringComparison.Ordinal);
        Assert.Contains("padding: 0;", transition, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden;", transition, StringComparison.Ordinal);
        Assert.Contains("position: fixed;", transition, StringComparison.Ordinal);
        Assert.Contains(
            "inset: var(--topbar-height, 60px) 0 0;",
            transition,
            StringComparison.Ordinal);
        Assert.Contains("width: auto;", transition, StringComparison.Ordinal);
        Assert.Contains("height: auto;", transition, StringComparison.Ordinal);
        Assert.Contains("box-sizing: border-box;", transition, StringComparison.Ordinal);
        Assert.DoesNotContain("width: 100vw;", transition, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "calc(50% - 50vw)",
            transition,
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
