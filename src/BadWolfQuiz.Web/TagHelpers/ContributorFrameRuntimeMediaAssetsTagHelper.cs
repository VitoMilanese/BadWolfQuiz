using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
[HtmlTargetElement("body")]
public sealed class ContributorFrameRuntimeMediaAssetsTagHelper : TagHelper
{
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.Equals(output.TagName, "head", StringComparison.OrdinalIgnoreCase))
        {
            output.PostContent.AppendHtml(
                "<link rel=\"stylesheet\" href=\"/css/contributor-frame-runtime-media.css?v=1\" />");
            return;
        }

        if (string.Equals(output.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            output.PostContent.AppendHtml(
                "<script src=\"/js/contributor-frame-runtime-media.js?v=1\" defer></script>");
        }
    }
}
