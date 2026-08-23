using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class QuizTransferCompletionAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.ViewData.Model is not IndexModel)
        {
            return;
        }

        output.PostContent.AppendHtml(
            "<script src=\"/js/quiz-transfer-completion.js?v=366.2\"></script>" +
            "<script src=\"/js/quiz-transfer-error-dismiss.js?v=366.2\"></script>");
    }
}
