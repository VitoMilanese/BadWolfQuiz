using BadWolfQuiz.Web.Pages.Admin.Games;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class HostNavigationGuardAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var model = ViewContext.ViewData.Model;
        if (model is not LobbyModel && model is not AnswerHistoryModel)
        {
            return;
        }

        if (model is AnswerHistoryModel)
        {
            output.PreContent.AppendHtml(
                "<link rel=\"stylesheet\" href=\"/css/gameplay-review-fixes.css?v=1\" />");
        }

        output.PostContent.AppendHtml(
            "<script src=\"/js/host-navigation-action-guard.js?v=1.22.10\"></script>");
    }
}
