namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorWorkspaceBrowserFollowupRegressionTests
{
    [Fact]
    public void Workspace_styles_load_from_head_before_editor_content_is_painted()
    {
        var tagHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "QuizEditorWorkspaceAssetsTagHelper.cs"));

        Assert.Contains("[HtmlTargetElement(\"head\")]", tagHelper, StringComparison.Ordinal);
        Assert.Contains("[HtmlTargetElement(\"body\")]", tagHelper, StringComparison.Ordinal);
        Assert.Contains("context.TagName, \"head\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/css/quiz-editor-workspace.css?v=577.1", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/css/quiz-editor-workspace-fixes.css?v=577.2", tagHelper, StringComparison.Ordinal);
        Assert.Contains("context.TagName, \"body\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("data-quiz-editor-workspace", tagHelper, StringComparison.Ordinal);
    }

    [Fact]
    public void Board_vertical_wheel_scroll_chains_to_page_and_action_bars_stay_close_to_footer()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "quiz-editor-workspace-fixes.css"));

        Assert.Contains("padding-bottom: clamp(18px, 2vw, 28px);", css, StringComparison.Ordinal);
        Assert.Contains("overscroll-behavior-x: contain;", css, StringComparison.Ordinal);
        Assert.Contains("overscroll-behavior-y: auto;", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"board\"] .quiz-board-actions,", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace] .editor-actions", css, StringComparison.Ordinal);
        Assert.Contains("bottom: 6px;", css, StringComparison.Ordinal);
        Assert.Contains("padding-bottom: 16px;", css, StringComparison.Ordinal);
    }

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
