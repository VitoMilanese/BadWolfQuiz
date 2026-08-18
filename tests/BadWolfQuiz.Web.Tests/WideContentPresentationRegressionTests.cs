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

    [Fact]
    public void FinalQuestionLayoutPrioritizesQuestionContent()
    {
        var root = FindRepositoryRoot();
        var styles = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-admission-menu.css"));
        var lobbyMarkup = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));

        AssertCssRuleContains(
            styles,
            ".final-question-host:is([data-game-status=\"finalwagering\"], [data-game-status=\"finalanswering\"]) .final-question-panel",
            "padding: clamp(10px, 1.25vw, 16px);",
            "gap: clamp(0.5rem, 1vh, 0.8rem);",
            "overflow: hidden;");
        AssertCssRuleContains(
            styles,
            ".final-question-host[data-game-status=\"finalanswering\"] .final-question-panel .question-presentation",
            "flex: 1 1 auto;",
            "min-height: 0;",
            "height: auto;");
        AssertCssRuleContains(
            styles,
            ".final-question-host .final-submission-list",
            "max-width: none;",
            "display: grid;",
            "grid-template-columns: repeat(auto-fit, minmax(min(14rem, 100%), 1fr));",
            "overflow-x: hidden;");
        AssertCssRuleContains(
            styles,
            ".final-question-host[data-game-status=\"finalwagering\"] .final-submission-list",
            "max-height: min(44dvh, 18rem);",
            "overflow-y: auto;");
        AssertCssRuleContains(
            styles,
            ".final-question-host[data-game-status=\"finalanswering\"] .final-question-panel > .final-submission-list",
            "position: absolute;",
            "right: 0;",
            "grid-template-columns: 1fr;",
            "max-height: none;",
            "overflow-y: auto;",
            "transform: translateX(calc(100% - 2.75rem));");
        AssertCssRuleContains(
            styles,
            ".final-question-host[data-game-status=\"finalanswering\"] .final-question-panel > .final-submission-list::before",
            "content: \"👥\";",
            "width: 2.75rem;",
            "background: var(--panel-2);");
        Assert.Contains(
            ".final-question-host[data-game-status=\"finalanswering\"] .final-question-panel > .final-submission-list:hover",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-question-host[data-game-status=\"finalanswering\"] .final-question-panel > .final-submission-list:focus-within",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "@media (hover: hover) and (pointer: fine) and (min-width: 801px)",
            styles,
            StringComparison.Ordinal);
        AssertCssRuleContains(
            styles,
            ".final-question-host .final-submission-list li",
            "grid-template-columns: minmax(0, 1fr) auto auto;",
            "padding: 0.42rem 0.6rem;",
            "line-height: 1.15;");
        AssertCssRuleContains(
            styles,
            ".final-question-host .final-question-panel iframe.game-content-video",
            "width: min(100%, 1400px);");
        AssertCssRuleContains(
            styles,
            ".final-question-host .final-question-panel .game-content-audio-shell",
            "max-width: 960px;");

        Assert.Contains("FinalQuestion_NotSubmitted", lobbyMarkup, StringComparison.Ordinal);
        Assert.Contains("FinalQuestion_Submitted", lobbyMarkup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"SubmitMinimumFinalWager\"", lobbyMarkup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"SubmitEmptyFinalAnswer\"", lobbyMarkup, StringComparison.Ordinal);
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
