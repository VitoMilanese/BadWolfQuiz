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
            output.PreElement.AppendHtml(
                "<link rel=\"stylesheet\" href=\"/css/gameplay-review-fixes.css?v=2\" />");
            output.PreElement.AppendHtml(
                "<link rel=\"stylesheet\" href=\"/css/player-regular-gameplay-safe.css?v=3\" />");
            output.PostContent.AppendHtml(
                "<script src=\"/js/anonymous-shared-wager-player.js?v=1\"></script>");
        }

        if (classNames.Contains("host-game-board", StringComparer.Ordinal))
        {
            output.PreElement.AppendHtml(
                "<link rel=\"stylesheet\" href=\"/css/player-regular-gameplay-safe.css?v=3\" />");
            output.PreElement.AppendHtml(
                "<link rel=\"stylesheet\" href=\"/css/player-regular-gameplay-host-followup.css?v=6\" />");
            output.PostContent.AppendHtml(
                "<script src=\"/js/host-gameplay-submit-guard.js?v=4\" data-host-gameplay-submit-guard></script>");
            output.PostContent.AppendHtml(
                "<script src=\"/js/anonymous-shared-wager-host.js?v=1\"></script>");
        }
    }
}
