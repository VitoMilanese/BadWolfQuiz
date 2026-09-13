namespace BadWolfQuiz.Web.Tests;

public sealed class HostMultipleChoiceAnswerWidthRegressionTests
{
    [Fact]
    public void Multiple_choice_answer_reveal_uses_full_gameplay_width_for_host_and_all_player_modes()
    {
        var root = FindRepositoryRoot();
        var reveal = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "_MultipleChoiceRevealBlocks.cshtml"));
        var bootstrap = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-multiple-choice-bootstrap.js"));
        var assets = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "HostMultipleChoiceAssetsTagHelper.cs"));

        Assert.DoesNotContain(
            "width: min(100%, 1100px) !important;",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains(
            ".game-content-blocks.multiple-choice-answer-reveal-grid\n        .game-content-block,",
            reveal.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.Contains("width: 100% !important;", reveal, StringComparison.Ordinal);
        Assert.Contains("max-width: none !important;", reveal, StringComparison.Ordinal);

        Assert.Contains(
            ".answer-presentation > .game-content-blocks.multiple-choice-answer-reveal-grid .game-content-block,",
            bootstrap,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ":not(:has(> .all-player-answer-option-correct))",
            bootstrap,
            StringComparison.Ordinal);
        Assert.Contains(".game-content-text,", bootstrap, StringComparison.Ordinal);
        Assert.Contains(".game-content-caption {", bootstrap, StringComparison.Ordinal);
        Assert.Contains("width: 100% !important;", bootstrap, StringComparison.Ordinal);
        Assert.Contains("max-width: none !important;", bootstrap, StringComparison.Ordinal);
        Assert.Contains(
            "host-multiple-choice-bootstrap.js?v=1.26.7",
            assets,
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
