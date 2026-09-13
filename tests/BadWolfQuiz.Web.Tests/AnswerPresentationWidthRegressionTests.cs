namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerPresentationWidthRegressionTests
{
    [Fact]
    public void Host_answer_and_answer_preview_text_use_the_full_gameplay_stage()
    {
        var root = FindRepositoryRoot();
        var helper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "AnonymousSharedWagerAssetsTagHelper.cs"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "answer-stage-full-width.css"));

        Assert.Contains(
            "/css/answer-stage-full-width.css?v=1",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            ".current-question-summary:not(.wager-mode)",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            ".question-review-preview",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            ":is(.game-content-text, .game-content-caption)",
            css,
            StringComparison.Ordinal);
        Assert.Contains("width: 100% !important;", css, StringComparison.Ordinal);
        Assert.Contains("max-width: none !important;", css, StringComparison.Ordinal);
        Assert.Contains("margin-inline: 0 !important;", css, StringComparison.Ordinal);
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
