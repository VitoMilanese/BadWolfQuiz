namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorWorkspacePolishRegressionTests
{
    [Fact]
    public void Workspace_loads_the_final_polish_stylesheet()
    {
        var tagHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "QuizEditorWorkspaceAssetsTagHelper.cs"));

        Assert.Contains(
            "/css/quiz-editor-workspace-polish.css?v=577.5",
            tagHelper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Question_delete_is_a_red_button_with_a_centered_normal_color_icon()
    {
        var css = ReadPolishCss();

        Assert.Contains(".question-card-actions.has-question-copy .js-question-delete", css, StringComparison.Ordinal);
        Assert.Contains("background: #b42336 !important;", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--text) !important;", css, StringComparison.Ordinal);
        Assert.Contains("display: inline-flex !important;", css, StringComparison.Ordinal);
        Assert.Contains("align-items: center;", css, StringComparison.Ordinal);
        Assert.Contains("justify-content: center;", css, StringComparison.Ordinal);
        Assert.Contains("width: 17px;", css, StringComparison.Ordinal);
        Assert.Contains("height: 19px;", css, StringComparison.Ordinal);
        Assert.Contains("background-color: currentColor;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Final_editor_does_not_repeat_description_question_or_answer_headings()
    {
        var css = ReadPolishCss();

        Assert.Contains("body[data-quiz-editor-workspace=\"question\"] .question-editor > h2,", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"final\"] .question-editor > h2", css, StringComparison.Ordinal);
        Assert.Contains("display: none;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_control_reserves_its_space_during_reload()
    {
        var css = ReadPolishCss();

        Assert.Contains(":not(:has(+ [data-editor-reset]))", css, StringComparison.Ordinal);
        Assert.Contains("margin-right: 48px;", css, StringComparison.Ordinal);
        Assert.Contains("content: \"↻\";", css, StringComparison.Ordinal);
        Assert.Contains("left: calc(100% + 10px);", css, StringComparison.Ordinal);
        Assert.Contains("width: 38px;", css, StringComparison.Ordinal);
        Assert.Contains("height: 38px;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Regular_question_and_answer_preview_use_a_full_gameplay_stage()
    {
        var css = ReadPolishCss();

        Assert.Contains("body[data-quiz-editor-workspace=\"question\"] .question-preview-dialog", css, StringComparison.Ordinal);
        Assert.Contains("repeating-linear-gradient(", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"question\"] .question-preview-screen", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-rows: minmax(0, 1fr);", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"question\"] .question-preview-title", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"question\"] .question-preview-content", css, StringComparison.Ordinal);
        Assert.Contains("font-size: clamp(2rem, 4.5vw, 4.8rem);", css, StringComparison.Ordinal);
        Assert.Contains("font-size: clamp(1.26rem, 3.15vw, 2.8rem);", css, StringComparison.Ordinal);
        Assert.Contains("max-height: min(68vh, 760px);", css, StringComparison.Ordinal);
    }

    private static string ReadPolishCss() => File.ReadAllText(FindWebFile(
        "wwwroot",
        "css",
        "quiz-editor-workspace-polish.css"));

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
