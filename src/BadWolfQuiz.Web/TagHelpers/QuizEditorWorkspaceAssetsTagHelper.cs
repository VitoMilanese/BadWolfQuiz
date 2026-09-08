using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

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

        output.Attributes.SetAttribute("data-quiz-editor-workspace", surface);
        output.PostContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/quiz-editor-workspace.css?v=577.1\" />");
    }
}
