using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "class")]
[HtmlTargetElement("section", Attributes = "class")]
public sealed class AnonymousSharedWagerAssetsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var classes = output.Attributes["class"]?.Value?.ToString() ?? string.Empty;
        var classNames = classes.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (classNames.Contains("player-lobby", StringComparer.Ordinal))
        {
            output.PreElement.AppendHtml(
                "<link rel=\"stylesheet\" href=\"/css/player-lobby-waiting-room-fixes.css?v=1\" />");
            output.PostContent.AppendHtml(
                "<script src=\"/js/anonymous-shared-wager-player.js?v=1\"></script>");
        }

        if (classNames.Contains("host-game-board", StringComparer.Ordinal))
        {
            output.PostContent.AppendHtml(
                "<script src=\"/js/anonymous-shared-wager-host.js?v=1\"></script>");
        }
    }
}
