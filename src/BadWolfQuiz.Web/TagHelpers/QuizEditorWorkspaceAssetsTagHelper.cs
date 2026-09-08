using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
[HtmlTargetElement("body")]
public sealed class QuizEditorWorkspaceAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var surface = ViewContext.ViewData.Model switch
        {
            EditorModel => "board",
            QuestionEditorModel => "question",
            FinalQuestionEditorModel => "final",
            DescriptionEditorModel => "description",
            _ => null
        };

        if (surface is null)
        {
            return;
        }

        if (string.Equals(context.TagName, "head", StringComparison.OrdinalIgnoreCase))
        {
            output.PostContent.AppendHtml(
                "<link rel=\"stylesheet\" href=\"/css/quiz-editor-workspace.css?v=577.1\" />" +
                "<link rel=\"stylesheet\" href=\"/css/quiz-editor-workspace-fixes.css?v=577.4\" />" +
                "<script src=\"/js/quiz-editor-workspace-interactions.js?v=577.3\"></script>");
            return;
        }

        if (string.Equals(context.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            output.Attributes.SetAttribute("data-quiz-editor-workspace", surface);
        }
    }
}
