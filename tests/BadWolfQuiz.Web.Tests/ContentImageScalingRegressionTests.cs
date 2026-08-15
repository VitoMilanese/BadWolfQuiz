using System.Text.RegularExpressions;

namespace BadWolfQuiz.Web.Tests;

public sealed class ContentImageScalingRegressionTests
{
    [Fact]
    public void GameplayImagesGrowWithinExistingMediaBoundsOutsideFourClues()
    {
        var root = FindRepositoryRoot();
        var overrides = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "busy-indicators.css"));
        var siteStyles = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "site.css"));

        AssertCssRuleContains(
            overrides,
            ".game-content-presentation .game-content-blocks:not(.four-clue-grid) > .game-content-block",
            "width: 100%;");
        AssertCssRuleContains(
            overrides,
            ".game-content-presentation .game-content-blocks:not(.four-clue-grid) > .game-content-block > .game-content-image",
            "width: 100%;",
            "height: auto;");
        Assert.Contains("max-height: min(62vh, 680px);", siteStyles, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain;", siteStyles, StringComparison.Ordinal);
        Assert.Contains(".four-clue-grid .game-content-image", siteStyles, StringComparison.Ordinal);
        Assert.Contains("max-height: 24vh;", siteStyles, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorPreviewImagesGrowWithinTheSharedPreviewBounds()
    {
        var root = FindRepositoryRoot();
        var overrides = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "busy-indicators.css"));
        var previewModal = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "Shared", "_QuestionPreviewModal.cshtml"));
        var questionEditor = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "QuestionEditor.cshtml"));
        var finalQuestionEditor = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "FinalQuestionEditor.cshtml"));
        var descriptionPreview = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "description-preview.js"));

        AssertCssRuleContains(
            overrides,
            ".question-preview-modal .question-preview-content:not(.four-clue-grid) .question-preview-image",
            "width: 100%;",
            "height: auto;");
        Assert.Contains(".question-preview-media {", previewModal, StringComparison.Ordinal);
        Assert.Contains("width: min(980px, 100%);", previewModal, StringComparison.Ordinal);
        Assert.Contains("max-height: min(62vh, 680px);", previewModal, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain;", previewModal, StringComparison.Ordinal);
        Assert.Contains("image.className = \"question-preview-image\";", questionEditor, StringComparison.Ordinal);
        Assert.Contains("image.className = \"question-preview-image\";", finalQuestionEditor, StringComparison.Ordinal);
        Assert.Contains("image.className = \"question-preview-image\";", descriptionPreview, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundAndCategoryIntroImagesGrowWithinExistingIntroBounds()
    {
        var root = FindRepositoryRoot();
        var overrides = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "busy-indicators.css"));
        var roundIntro = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "RoundIntro.cshtml"));
        var runningRoundIntro = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "RunningRoundIntro.cshtml"));

        AssertCssRuleContains(
            overrides,
            ".game-intro-page .game-intro-image-block",
            "width: 100%;");
        AssertCssRuleContains(
            overrides,
            ".game-intro-page .game-intro-image",
            "width: min(900px, 100%);",
            "height: auto;");

        foreach (var introMarkup in new[] { roundIntro, runningRoundIntro })
        {
            Assert.Contains("max-width: min(900px, 100%);", introMarkup, StringComparison.Ordinal);
            Assert.Contains("max-height: 52vh;", introMarkup, StringComparison.Ordinal);
            Assert.Contains("object-fit: contain;", introMarkup, StringComparison.Ordinal);
        }
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
