using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class PlayerAchievementsModel(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    IStringLocalizer<AchievementResource> localizer,
    IOptions<FooterOptions> footerOptions) : PageModel
{
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        var player = sessionRegistry
            .GetPlayers(game)
            .FirstOrDefault(item => item.Id == new GamePlayerId(playerId));
        if (player is null)
        {
            return NotFound();
        }

        var accountId = PlayerAchievementRuntimeState.GetPlayerAccountId(game, player.Id);
        var isContributor = player.HasTemporaryContributorPrivileges ||
            ContributorRecognition.IsContributor(
                footerOptions.Value,
                player.OriginalName);

        if (!isContributor && !string.IsNullOrWhiteSpace(accountId))
        {
            var accountDisplayName = await db.Hosts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(host => host.Id == accountId)
                .Select(host => host.DisplayName)
                .SingleOrDefaultAsync(cancellationToken);
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
            cancellationToken);

        var unlockedCount = achievements.Count(item => item.IsUnlocked);
        return new JsonResult(new
        {
            playerName = player.Name,
            title = localizer["Achievements_Title"].Value,
            subtitle = localizer["Achievements_Subtitle"].Value,
            closeLabel = localizer["Achievements_Close"].Value,
            unlockedCountText = localizer[
                "Achievements_UnlockedCount",
                unlockedCount,
                achievements.Count].Value,
            secretTitle = localizer["Achievements_SecretTitle"].Value,
            secretDescription = localizer["Achievements_SecretDescription"].Value,
            achievements = achievements.Select(item => new
            {
                item.Code,
                item.Icon,
                item.IsUnlocked,
                item.IsSecret,
                item.IsNewInCurrentGame,
                item.Progress,
                item.Target,
                name = localizer[$"{item.Code}_Name"].Value,
                description = localizer[$"{item.Code}_Description"].Value
            })
        });
    }
}
