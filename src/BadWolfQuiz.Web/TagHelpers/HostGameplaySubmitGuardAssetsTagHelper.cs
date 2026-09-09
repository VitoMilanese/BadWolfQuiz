using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class HostGameplaySubmitGuardAssetsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.PreContent.AppendHtml(
            "<script src=\"/js/host-gameplay-submit-guard.js?v=5\" data-host-gameplay-submit-guard></script>");
    }
}
