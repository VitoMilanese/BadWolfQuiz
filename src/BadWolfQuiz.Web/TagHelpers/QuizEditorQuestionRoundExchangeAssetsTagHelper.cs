using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class QuizEditorQuestionRoundExchangeAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.ViewData.Model is not EditorModel)
        {
            return;
        }

        output.PostContent.AppendHtml(
            "<script src=\"/js/quiz-editor-question-round-exchange.js?v=269.1\"></script>");
    }
}
