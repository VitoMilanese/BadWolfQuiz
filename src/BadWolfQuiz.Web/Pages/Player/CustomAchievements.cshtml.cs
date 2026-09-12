using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Player;

public sealed class CustomAchievementsModel(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry) : PageModel
{
    public async Task<IActionResult> OnGetAsync(
        string code,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.Find(code);
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
        var achievements = await new HostCustomAchievementVisibilityService(db).LoadForGameAsync(
            game.HostId,
            player.Name,
            game.PublicCode,
            accountId,
            cancellationToken);

        return new JsonResult(new
        {
            achievements = achievements.Select(ToDto)
        });
    }

    internal static object ToDto(HostCustomAchievementProgress item) => new
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
    };
}
