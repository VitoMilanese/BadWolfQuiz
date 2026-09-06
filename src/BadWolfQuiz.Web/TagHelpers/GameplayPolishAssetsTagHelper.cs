using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "data-game-intro-page")]
[HtmlTargetElement("div", Attributes = "data-game-code,data-player-id,data-final-status")]
[HtmlTargetElement("div", Attributes = "data-host-gameplay-view")]
public sealed class GameplayPolishAssetsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.PreContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/gameplay-polish.css?v=2\" />" +
            "<script src=\"/js/gameplay-polish.js?v=2\"></script>");
    }
}
