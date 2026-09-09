namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorWorkspaceRestyleRegressionTests
{
    [Fact]
    public void Workspace_assets_are_scoped_to_all_quiz_editor_surfaces()
    {
        var tagHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "QuizEditorWorkspaceAssetsTagHelper.cs"));

        Assert.Contains("EditorModel => \"board\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("QuestionEditorModel => \"question\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("FinalQuestionEditorModel => \"final\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("DescriptionEditorModel => \"description\"", tagHelper, StringComparison.Ordinal);
        Assert.Contains("data-quiz-editor-workspace", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/css/quiz-editor-workspace.css?v=577.1", tagHelper, StringComparison.Ordinal);
    }

    [Fact]
    public void Board_restyle_preserves_existing_editing_contracts()
    {
        var page = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Quizzes",
            "Editor.cshtml"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "quiz-editor-workspace.css"));

        Assert.Contains("class=\"quiz-board-form\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"roundTabs\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"categoryColumns\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"question-cell-slot\"", page, StringComparison.Ordinal);
        Assert.Contains("draggable=\"true\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"js-question-delete", page, StringComparison.Ordinal);
        Assert.Contains("data-delete-confirmation=", page, StringComparison.Ordinal);
        Assert.Contains("openRenameRoundDialog()", page, StringComparison.Ordinal);

        Assert.Contains("body[data-quiz-editor-workspace=\"board\"] .quiz-editor-toolbar", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"board\"] .quiz-board-scroll", css, StringComparison.Ordinal);
        Assert.Contains("overflow: auto;", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"board\"] .question-cell", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"board\"] .quiz-board-actions", css, StringComparison.Ordinal);
        Assert.Contains("position: sticky;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Question_final_and_description_editors_share_the_workspace_language_without_replacing_behavior()
    {
        var questionPage = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Quizzes",
            "QuestionEditor.cshtml"));
        var finalPage = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Quizzes",
            "FinalQuestionEditor.cshtml"));
        var descriptionPage = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Quizzes",
            "DescriptionEditor.cshtml"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "quiz-editor-workspace.css"));

        Assert.Contains("data-ajax-question-editor", questionPage, StringComparison.Ordinal);
        Assert.Contains("data-open-question-preview=\"question\"", questionPage, StringComparison.Ordinal);
        Assert.Contains("data-open-question-preview=\"answer\"", questionPage, StringComparison.Ordinal);
        Assert.Contains("data-open-question-preview=\"description\"", finalPage, StringComparison.Ordinal);
        Assert.Contains("data-open-description-preview", descriptionPage, StringComparison.Ordinal);
        Assert.Contains("data-open-description-rename", descriptionPage, StringComparison.Ordinal);

        Assert.Contains("body[data-quiz-editor-workspace=\"question\"] .question-editor", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"final\"] .question-editor", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace=\"description\"] .question-editor", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace] .content-block-card", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace] .content-block-toolbar", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace] .editor-actions", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_dialogs_responsive_layout_and_reduced_motion_are_part_of_the_restyle()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "quiz-editor-workspace.css"));

        Assert.Contains("body[data-quiz-editor-workspace] .app-dialog::backdrop", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace] #question-preview-modal::backdrop", css, StringComparison.Ordinal);
        Assert.Contains("body[data-quiz-editor-workspace] .app-dialog .dialog-card", css, StringComparison.Ordinal);
        Assert.Contains("min-width: 44px;", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px;", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 1100px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 760px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-height: 720px) and (min-width: 761px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css, StringComparison.Ordinal);
        Assert.Contains("transition-duration: 0s !important;", css, StringComparison.Ordinal);
        Assert.Contains("animation-duration: 0s !important;", css, StringComparison.Ordinal);
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
