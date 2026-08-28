using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "class")]
public sealed class AnonymousSharedWagerAssetsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var classes = output.Attributes["class"]?.Value?.ToString() ?? string.Empty;

        if (classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains("player-lobby", StringComparer.Ordinal))
        {
            output.PostContent.AppendHtml(
                "<script src=\"/js/anonymous-shared-wager-player.js?v=1\"></script>");
        }

        if (classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains("host-game-board", StringComparer.Ordinal))
        {
            output.PostContent.AppendHtml(
                "<script src=\"/js/anonymous-shared-wager-host.js?v=1\"></script>");
        }
    }
}
