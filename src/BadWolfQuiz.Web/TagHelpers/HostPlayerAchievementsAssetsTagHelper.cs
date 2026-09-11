using BadWolfQuiz.Web.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
public sealed class HostPlayerAchievementsAssetsTagHelper(
    IStringLocalizer<AchievementResource> localizer) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!ViewContext.HttpContext.Request.Path.StartsWithSegments(
                "/Admin/Games/Lobby",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        output.Attributes.SetAttribute(
            "data-host-player-achievements-label",
            localizer["Achievements_Title"].Value);
        output.PostContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/player-achievements.css?v=10\" />" +
            "<script defer src=\"/js/achievement-image-trim.js?v=1\"></script>" +
            "<script defer src=\"/js/host-player-achievements.js?v=6\"></script>");
    }
}

[HtmlTargetElement("button", Attributes = "data-remove-player")]
public sealed class HostPlayerAchievementsButtonTagHelper(
    IStringLocalizer<AchievementResource> localizer) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!ViewContext.HttpContext.Request.Path.StartsWithSegments(
                "/Admin/Games/Lobby",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var playerId = output.Attributes["data-remove-player"]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        var label = localizer["Achievements_Title"].Value;
        output.PreElement.AppendHtml(
            $"<button class=\"button button-secondary icon-button player-action-button player-achievements-host-button\" " +
            $"type=\"button\" data-player-achievements-player-id=\"{System.Net.WebUtility.HtmlEncode(playerId)}\" " +
            $"title=\"{System.Net.WebUtility.HtmlEncode(label)}\" aria-label=\"{System.Net.WebUtility.HtmlEncode(label)}\" " +
            "aria-haspopup=\"dialog\"><span aria-hidden=\"true\">🏆</span></button>");
    }
}
