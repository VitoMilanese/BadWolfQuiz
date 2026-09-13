using BadWolfQuiz.Web.Pages.Admin.Games;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class AnswerKeyWindowAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var model = ViewContext.ViewData.Model;
        if (model is LobbyModel)
        {
            output.PostContent.AppendHtml(
                "<script src=\"/js/answer-key-window.js?v=1.22.0-281.2\"></script>");
            return;
        }

        if (model is AnswerKeyModel)
        {
            output.PreContent.AppendHtml(
                "<link rel=\"stylesheet\" href=\"/css/answer-key-answer-width-fix.css?v=1\" />");
        }
    }
}
