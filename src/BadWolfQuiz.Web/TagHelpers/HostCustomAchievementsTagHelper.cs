using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
public sealed class HostCustomAchievementAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var path = ViewContext.HttpContext.Request.Path;
        if (!path.StartsWithSegments("/Player/Lobby", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWithSegments("/Admin/Games", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWithSegments("/Achievements", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        output.PostContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/custom-achievements.css?v=1\" />" +
            "<script defer src=\"/js/host-custom-achievements.js?v=1\"></script>");
    }
}

[HtmlTargetElement("div", Attributes = "class")]
public sealed class HostCustomAchievementSelfGridTagHelper(QuizDbContext db) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (!string.Equals(
                ViewContext.HttpContext.Request.Path.Value,
                "/Achievements",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var classes = output.Attributes["class"]?.Value?.ToString() ?? string.Empty;
        if (!classes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains("player-achievements-grid", StringComparer.Ordinal))
        {
            return;
        }

        var accountId = ViewContext.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return;
        }

        var items = await new HostCustomAchievementService(db).LoadForPlayerAsync(
            hostId: null,
            playerName: string.Empty,
            currentGameCode: null,
            accountId: accountId,
            cancellationToken: ViewContext.HttpContext.RequestAborted);
        if (items.Count == 0)
        {
            return;
        }

        var html = new StringBuilder();
        foreach (var item in items)
        {
            var percentage = item.Target <= 0
                ? 0
                : Math.Clamp((int)Math.Round(item.Progress * 100d / item.Target), 0, 100);
            html.Append("<article class=\"player-achievement-card host-custom-achievement-card ");
            html.Append(item.IsUnlocked ? "is-unlocked" : "is-locked");
            html.Append("\" data-achievement-code=\"");
            html.Append(Encode(item.Code));
            html.Append("\"><div class=\"player-achievement-card-top\"><img class=\"player-achievement-image\" src=\"");
            html.Append(Encode(item.ArtworkUrl));
            html.Append("\" alt=\"\" aria-hidden=\"true\" loading=\"lazy\" decoding=\"async\"><span class=\"player-achievement-state\" aria-hidden=\"true\">");
            html.Append(item.IsUnlocked ? "✓" : "○");
            html.Append("</span></div><span class=\"custom-achievement-host-badge\">HOST</span><strong>");
            html.Append(Encode(item.Name));
            html.Append("</strong><p>");
            html.Append(Encode(item.Description));
            html.Append("</p>");
            if (!item.IsUnlocked)
            {
                html.Append("<div class=\"player-achievement-progress\" aria-label=\"");
                html.Append(item.Progress);
                html.Append(" / ");
                html.Append(item.Target);
                html.Append("\"><span style=\"width:");
                html.Append(percentage);
                html.Append("%\"></span></div><small>");
                html.Append(item.Progress);
                html.Append(" / ");
                html.Append(item.Target);
                html.Append("</small>");
            }
            html.Append("</article>");
        }

        output.PostContent.AppendHtml(html.ToString());
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
