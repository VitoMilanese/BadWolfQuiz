using System.Text.RegularExpressions;

namespace BadWolfQuiz.Web.Tests;

public sealed class FinalQuestionWagerSidebarRegressionTests
{
    [Fact]
    public void FinalWageringUsesAlwaysVisibleRightSidebarOnDesktop()
    {
        var root = FindRepositoryRoot();
        var styles = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        Assert.Contains(
            "@media (min-width: 801px)",
            styles,
            StringComparison.Ordinal);

        AssertCssRuleContains(
            styles,
            ".final-question-host[data-game-status=\"finalwagering\"] .final-question-panel",
            "position: relative;",
            "padding-right: clamp(19rem, 25vw, 25rem);");

        AssertCssRuleContains(
            styles,
            ".final-question-host[data-game-status=\"finalwagering\"] .final-question-panel > .final-submission-list",
            "position: absolute;",
            "right: 0.75rem;",
            "bottom: 0.75rem;",
            "width: clamp(18rem, 24vw, 24rem);",
            "max-height: none;",
            "grid-template-columns: 1fr;",
            "overflow-y: auto;",
            "transform: none;");

        Assert.DoesNotContain(
            ".final-question-host[data-game-status=\"finalwagering\"] .final-question-panel > .final-submission-list:hover",
            styles,
            StringComparison.Ordinal);
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
