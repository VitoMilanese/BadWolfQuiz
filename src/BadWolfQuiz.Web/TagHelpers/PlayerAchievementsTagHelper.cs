using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
public sealed class PlayerAchievementAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!ViewContext.HttpContext.Request.Path.StartsWithSegments(
                "/Player/Lobby",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        output.PostContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/player-achievements.css?v=7\" />" +
            "<script defer src=\"/js/player-achievements.js?v=1\"></script>");
    }
}

[HtmlTargetElement("div", Attributes = "data-game-code,data-player-id")]
public sealed class PlayerAchievementsTagHelper(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry,
    IStringLocalizer<AchievementResource> localizer,
    IOptions<FooterOptions> footerOptions) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(
        TagHelperContext context,
        TagHelperOutput output)
    {
        var classValue = output.Attributes["class"]?.Value?.ToString() ?? string.Empty;
        if (!classValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("player-lobby", StringComparer.Ordinal))
        {
            return;
        }

        var code = output.Attributes["data-game-code"]?.Value?.ToString();
        var playerIdText = output.Attributes["data-player-id"]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(code) ||
            !Guid.TryParse(playerIdText, out var playerId))
        {
            return;
        }

        var game = sessionRegistry.Find(code);
        if (game is null)
        {
            return;
        }

        var player = sessionRegistry
            .GetPlayers(game)
            .FirstOrDefault(item => item.Id == new GamePlayerId(playerId));
        if (player is null)
        {
            return;
        }

        var requestAccountId = ViewContext.HttpContext.User
            .FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(requestAccountId))
        {
            PlayerAchievementRuntimeState.LinkPlayerAccount(
                game,
                player.Id,
                requestAccountId);
        }

        var accountId = PlayerAchievementRuntimeState.GetPlayerAccountId(game, player.Id);
        var isContributor = player.HasTemporaryContributorPrivileges ||
            ContributorRecognition.IsContributor(footerOptions.Value, player.OriginalName);

        if (!isContributor && !string.IsNullOrWhiteSpace(accountId))
        {
            var accountDisplayName = await db.Hosts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(host => host.Id == accountId)
                .Select(host => host.DisplayName)
                .SingleOrDefaultAsync(ViewContext.HttpContext.RequestAborted);
            isContributor = ContributorRecognition.IsContributor(
                footerOptions.Value,
                accountDisplayName);
        }

        var achievements = await new PlayerAchievementService(db).LoadForPlayerAsync(
            game.HostId,
            player.Name,
            game.PublicCode,
            accountId,
            isContributor,
            ViewContext.HttpContext.RequestAborted);
        if (achievements.Count == 0)
        {
            return;
        }

        output.Attributes.SetAttribute(
            "data-player-achievements-label",
            localizer["Achievements_PlayerLabel"].Value);
        output.PostContent.AppendHtml(BuildMarkup(achievements));
    }

    private string BuildMarkup(IReadOnlyList<PlayerAchievementProgress> achievements)
    {
        var html = new StringBuilder();
        var unlockedCount = achievements.Count(item => item.IsUnlocked);
        var newlyUnlocked = achievements
            .Where(item => item.IsNewInCurrentGame)
            .ToArray();

        html.Append("<dialog id=\"player-achievements-dialog\" class=\"player-achievements-dialog\" data-player-achievements-dialog aria-labelledby=\"player-achievements-title\">");
        html.Append("<div class=\"player-achievements-dialog-card\">");
        html.Append("<header class=\"dialog-heading player-achievements-dialog-heading\"><h2 id=\"player-achievements-title\">");
        html.Append(Encode(localizer["Achievements_Title"].Value));
        html.Append("</h2><button class=\"dialog-close\" type=\"button\" data-player-achievements-close aria-label=\"");
        html.Append(Encode(localizer["Achievements_Close"].Value));
        html.Append("\">×</button></header>");
        html.Append("<div class=\"player-achievements-dialog-body\">");

        if (newlyUnlocked.Length > 0)
        {
            html.Append("<div class=\"player-achievement-unlocked\" role=\"status\" aria-live=\"polite\">");
            html.Append("<span class=\"player-achievement-unlocked-icon\" aria-hidden=\"true\">🏆</span><div>");
            html.Append("<strong>");
            html.Append(Encode(localizer[
                newlyUnlocked.Length == 1
                    ? "Achievements_NewUnlocked"
                    : "Achievements_NewUnlockedMany"].Value));
            html.Append("</strong><div class=\"player-achievement-unlocked-list\">");
            html.Append(string.Join(
                " · ",
                newlyUnlocked.Select(item => Encode(GetName(item)))));
            html.Append("</div></div></div>");
        }

        html.Append("<section class=\"player-achievements-panel\" aria-describedby=\"player-achievements-subtitle\">");
        html.Append("<div class=\"player-achievements-heading\"><div>");
        html.Append("<span class=\"player-achievements-kicker\">BAD WOLF / MILESTONES</span>");
        html.Append("<p id=\"player-achievements-subtitle\">");
        html.Append(Encode(localizer["Achievements_Subtitle"].Value));
        html.Append("</p></div><strong class=\"player-achievements-count\">");
        html.Append(Encode(localizer[
            "Achievements_UnlockedCount",
            unlockedCount,
            achievements.Count].Value));
        html.Append("</strong></div>");

        html.Append("<div class=\"player-achievements-grid\">");
        foreach (var achievement in achievements)
        {
            var lockedSecret = achievement.IsSecret && !achievement.IsUnlocked;
            var cardClasses = new StringBuilder("player-achievement-card");
            cardClasses.Append(achievement.IsUnlocked ? " is-unlocked" : " is-locked");
            if (achievement.IsNewInCurrentGame)
            {
                cardClasses.Append(" is-new");
            }

            html.Append("<article class=\"");
            html.Append(cardClasses);
            html.Append("\">");
            html.Append("<div class=\"player-achievement-card-top\">");
            if (lockedSecret)
            {
                html.Append("<span class=\"player-achievement-icon\" aria-hidden=\"true\">❔</span>");
            }
            else
            {
                html.Append("<img class=\"player-achievement-image\" src=\"/images/achievements/");
                html.Append(Encode(achievement.Code));
                html.Append(".png\" alt=\"\" aria-hidden=\"true\" loading=\"lazy\" decoding=\"async\" />");
            }
            html.Append("<span class=\"player-achievement-state\" aria-hidden=\"true\">");
            html.Append(achievement.IsUnlocked ? "✓" : "○");
            html.Append("</span></div><strong>");
            html.Append(Encode(lockedSecret
                ? localizer["Achievements_SecretTitle"].Value
                : GetName(achievement)));
            html.Append("</strong><p>");
            html.Append(Encode(lockedSecret
                ? localizer["Achievements_SecretDescription"].Value
                : GetDescription(achievement)));
            html.Append("</p>");

            if (!lockedSecret && !achievement.IsUnlocked)
            {
                var percentage = achievement.Target <= 0
                    ? 0
                    : Math.Clamp(
                        (int)Math.Round(
                            achievement.Progress * 100d / achievement.Target),
                        0,
                        100);
                html.Append("<div class=\"player-achievement-progress\" aria-label=\"");
                html.Append(Encode($"{achievement.Progress} / {achievement.Target}"));
                html.Append("\"><span style=\"width:");
                html.Append(percentage);
                html.Append("%\"></span></div><small>");
                html.Append(achievement.Progress);
                html.Append(" / ");
                html.Append(achievement.Target);
                html.Append("</small>");
            }

            html.Append("</article>");
        }

        html.Append("</div></section></div></div></dialog>");
        return html.ToString();
    }

    private string GetName(PlayerAchievementProgress achievement) =>
        localizer[$"{achievement.Code}_Name"].Value;

    private string GetDescription(PlayerAchievementProgress achievement) =>
        localizer[$"{achievement.Code}_Description"].Value;

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
