using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
public sealed class AchievementUnlockNotificationAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var path = ViewContext.HttpContext.Request.Path;
        var page = ViewContext.RouteData.Values["page"]?.ToString();
        var isPlayerGame = path.StartsWithSegments(
            "/Player/Lobby",
            StringComparison.OrdinalIgnoreCase);
        var isHostGame = page is
            "/Admin/Games/Lobby" or
            "/Admin/Games/RoundIntro" or
            "/Admin/Games/RunningRoundIntro" or
            "/Admin/Games/FinalQuestionTransition";

        if (!isPlayerGame && !isHostGame)
        {
            return;
        }

        output.PostContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/achievement-unlock-notifications.css?v=3\" />" +
            "<script defer src=\"/js/achievement-unlock-notifications.js?v=3\"></script>");
    }
}
