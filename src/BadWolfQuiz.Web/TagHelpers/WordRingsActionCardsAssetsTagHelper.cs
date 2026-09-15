using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "data-word-rings-root")]
public sealed class WordRingsActionCardsAssetsTagHelper : TagHelper
{
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.PreElement.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/word-rings-action-cards-v2.css?v=4\" />" +
            "<link rel=\"stylesheet\" href=\"/css/word-rings-action-cards-patch.css?v=1\" />");
        output.PostElement.AppendHtml(
            "<script src=\"/js/word-rings-action-cards-patch.js?v=1\" defer></script>" +
            "<script src=\"/js/word-rings-action-cards-v2.js?v=4\" defer></script>");
    }
}
