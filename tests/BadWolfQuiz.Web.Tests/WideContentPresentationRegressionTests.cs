using System.Text.RegularExpressions;

namespace BadWolfQuiz.Web.Tests;

public sealed class WideContentPresentationRegressionTests
{
    [Fact]
    public void RoundCategoryAndFinalPresentationsUseAvailableWidth()
    {
        var root = FindRepositoryRoot();
        var styles = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        AssertCssRuleContains(
            styles,
            "main.page-shell:has(> .game-intro-page)",
            "max-width: none;");
        AssertCssRuleContains(
            styles,
            ".game-intro-page .game-intro-group",
            "width: 100%;",
            "max-width: none;");
        AssertCssRuleContains(
            styles,
            ".game-intro-page .game-intro-blocks",
            "width: 100%;",
            "max-width: none;");
        AssertCssRuleContains(
            styles,
            ".game-intro-page .game-intro-text",
            "max-width: none;");
        AssertCssRuleContains(
            styles,
            ".game-intro-page .game-intro-image",
            "width: 100%;",
            "max-width: 100%;");

        AssertCssRuleContains(
            styles,
            ".final-question-host .final-question-panel",
            "width: 100%;",
            "max-width: none;");
        AssertCssRuleContains(
            styles,
            ".final-question-host .final-question-panel .game-content-presentation",
            "width: 100%;",
            "max-width: none;");
        AssertCssRuleContains(
            styles,
            ".final-question-host .final-question-panel .game-content-text",
            "max-width: none;");
        AssertCssRuleContains(
            styles,
            ".final-question-host .final-question-panel .game-content-caption",
            "max-width: none;");
    }

    private static void AssertCssRuleContains(
        string css,
        string selector,
        params string[] declarations)
    {
        var match = Regex.Match(
            css,
            $@"{Regex.Escape(selector)}\s*\{{(?<body>[^}}]*)\}}",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, $"CSS rule was not found: {selector}");
        var body = match.Groups["body"].Value;
        foreach (var declaration in declarations)
        {
            Assert.Contains(declaration, body, StringComparison.Ordinal);
        }
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
