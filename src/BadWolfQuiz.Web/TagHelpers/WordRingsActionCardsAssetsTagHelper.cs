using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "data-word-rings-root")]
public sealed class WordRingsActionCardsAssetsTagHelper : TagHelper
{
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.PreElement.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/word-rings-action-cards.css?v=1\" />");
        output.PostElement.AppendHtml(
            "<script src=\"/js/word-rings-action-cards.js?v=1\" defer></script>");
    }
}
