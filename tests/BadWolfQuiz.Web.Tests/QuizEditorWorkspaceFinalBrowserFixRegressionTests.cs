namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorWorkspaceFinalBrowserFixRegressionTests
{
    [Fact]
    public void Workspace_loads_the_final_browser_fix_stylesheet()
    {
        var tagHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "QuizEditorWorkspaceAssetsTagHelper.cs"));

        Assert.Contains(
            "/css/quiz-editor-workspace-browser-fixes.css?v=577.7",
            tagHelper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Question_card_action_buttons_are_vertically_balanced()
    {
        var css = ReadBrowserFixCss();

        Assert.Contains(
            "body[data-quiz-editor-workspace=\"board\"] .question-card-actions.has-question-copy",
            css,
            StringComparison.Ordinal);
        Assert.Contains("top: 0;", css, StringComparison.Ordinal);
        Assert.Contains("bottom: 0;", css, StringComparison.Ordinal);
        Assert.Contains("gap: 6px;", css, StringComparison.Ordinal);
        Assert.Contains("justify-content: center;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Question_and_final_preview_images_keep_their_own_box_and_can_upscale()
    {
        var css = ReadBrowserFixCss();

        Assert.Contains(
            "#question-preview-content:not(.four-clue-grid):not(.all-player-answer-grid)",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            "body[data-quiz-editor-workspace=\"question\"]",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            "body[data-quiz-editor-workspace=\"final\"]",
            css,
            StringComparison.Ordinal);
        Assert.Contains("width: auto !important;", css, StringComparison.Ordinal);
        Assert.Contains("height: min(58vh, 640px) !important;", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 100% !important;", css, StringComparison.Ordinal);
        Assert.Contains("max-height: min(58vh, 640px) !important;", css, StringComparison.Ordinal);
        Assert.Contains("box-shadow: none !important;", css, StringComparison.Ordinal);
        Assert.Contains("outline: 0 !important;", css, StringComparison.Ordinal);
        Assert.Contains(
            ".content-block-container-layout .question-preview-image",
            css,
            StringComparison.Ordinal);
        Assert.Contains("height: min(50vh, 520px) !important;", css, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "height: auto !important;\n    max-width: 100% !important;\n    max-height: min(62vh, 680px) !important;",
            css,
            StringComparison.Ordinal);
    }

    private static string ReadBrowserFixCss() => File.ReadAllText(FindWebFile(
        "wwwroot",
        "css",
        "quiz-editor-workspace-browser-fixes.css"));

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[]
            {
                directory.FullName,
                "src",
                "BadWolfQuiz.Web"
            }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
