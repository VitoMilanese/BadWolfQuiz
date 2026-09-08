namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorWorkspaceBrowserFollowupRegressionTests
{
    [Fact]
    public void Workspace_styles_and_interactions_load_from_head_before_editor_content_is_painted()
    {
        var tagHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "QuizEditorWorkspaceAssetsTagHelper.cs"));

        Assert.Contains("[HtmlTargetElement(\"head\")]", tagHelper, StringComparison.Ordinal);
        Assert.Contains("[HtmlTargetElement(\"body\")]", tagHelper, StringComparison.Ordinal);
        Assert.Contains("context.TagName, \"head\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/css/quiz-editor-workspace.css?v=577.1", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/css/quiz-editor-workspace-fixes.css?v=577.3", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/js/quiz-editor-workspace-interactions.js?v=577.3", tagHelper, StringComparison.Ordinal);
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

        Assert.Contains("padding-bottom: 6px;", css, StringComparison.Ordinal);
        Assert.Contains("overscroll-behavior-x: contain;", css, StringComparison.Ordinal);
        Assert.Contains("overscroll-behavior-y: auto;", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"board\"] .quiz-board-actions,", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace] .editor-actions", css, StringComparison.Ordinal);
        Assert.Contains("bottom: 2px;", css, StringComparison.Ordinal);
        Assert.Contains("color-mix(in srgb, var(--panel) 80%, transparent)", css, StringComparison.Ordinal);
        Assert.Contains("padding-bottom: 4px;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Board_image_previews_delete_icon_and_text_selection_follow_browser_review()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "quiz-editor-workspace-fixes.css"));

        Assert.Contains(".question-editor-preview img", css, StringComparison.Ordinal);
        Assert.Contains("border: 0;", css, StringComparison.Ordinal);
        Assert.Contains("background: transparent;", css, StringComparison.Ordinal);
        Assert.Contains("box-shadow: none;", css, StringComparison.Ordinal);
        Assert.Contains(".js-question-delete::before", css, StringComparison.Ordinal);
        Assert.Contains("background: currentColor;", css, StringComparison.Ordinal);
        Assert.Contains("mask: url(", css, StringComparison.Ordinal);
        Assert.Contains("user-select: none;", css, StringComparison.Ordinal);
        Assert.Contains("user-select: text;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Transient_editor_menus_toggle_closed_and_escape_does_not_leave_the_editor()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "quiz-editor-workspace-interactions.js"));

        Assert.Contains("window.addEventListener(\"keydown\"", script, StringComparison.Ordinal);
        Assert.Contains("event.key !== \"Escape\"", script, StringComparison.Ordinal);
        Assert.Contains(".quiz-editor-context-menu", script, StringComparison.Ordinal);
        Assert.Contains(".content-block-type-menu:not([hidden])", script, StringComparison.Ordinal);
        Assert.Contains("closeTransientEditorMenus()", script, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation();", script, StringComparison.Ordinal);
        Assert.Contains("[data-round-edit-menu]", script, StringComparison.Ordinal);
        Assert.Contains(".content-block-add-button", script, StringComparison.Ordinal);
        Assert.Contains("typeMenu.hidden = true;", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_surfaces_use_live_game_stage_backgrounds_without_the_editor_frame()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "quiz-editor-workspace-fixes.css"));

        Assert.Contains("body[data-quiz-editor-workspace=\"question\"] .question-preview-dialog", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"description\"] .question-preview-dialog", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"final\"] .question-preview-dialog", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace] #question-preview-content", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace] .question-preview-media", css, StringComparison.Ordinal);
        Assert.Contains("BAD WOLF QUIZ", css, StringComparison.Ordinal);
        Assert.Contains("height: min(54vh, 620px) !important;", css, StringComparison.Ordinal);
        Assert.Contains("font-size: clamp(1.64rem, 2.28vw, 2.15rem);", css, StringComparison.Ordinal);
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
