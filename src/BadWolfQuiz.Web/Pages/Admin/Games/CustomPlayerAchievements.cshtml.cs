using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

[Authorize]
public sealed class CustomPlayerAchievementsModel(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost) : PageModel
{
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);
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
        var achievements = await new HostCustomAchievementService(db).LoadForPlayerAsync(
            game.HostId,
            player.Name,
            game.PublicCode,
            accountId,
            cancellationToken);

        return new JsonResult(new
        {
            achievements = achievements.Select(item => new
            {
                item.Code,
                item.Name,
                item.Description,
                item.Progress,
                item.Target,
                item.IsUnlocked,
                item.IsNewInCurrentGame,
                item.ArtworkUrl,
                isSecret = false,
                isHostDefined = true
            })
        });
    }
}
