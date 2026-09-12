using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages.Player;

public sealed class ResetAchievementModel(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry) : PageModel
{
    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostAsync(
        string code,
        Guid playerId,
        string? accessToken,
        string achievementCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(accessToken))
        {
            return new JsonResult(new { message = AchievementResetText.Current.Error })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        var validationConnectionId = $"achievement-reset:{Guid.NewGuid():N}";
        var connection = sessionRegistry.ConnectPlayer(
            code,
            accessToken,
            validationConnectionId,
            isVisible: false);
        if (connection is null)
        {
            return new JsonResult(new { message = AchievementResetText.Current.Error })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        try
        {
            if (connection.RequiresApproval ||
                connection.Player.Id != new GamePlayerId(playerId))
            {
                return new JsonResult(new { message = AchievementResetText.Current.Error })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            var accountId = PlayerAchievementRuntimeState.GetPlayerAccountId(
                connection.Game,
                connection.Player.Id);
            var storedGameSessionId = await db.GameSessions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(session => session.PublicCode == connection.Game.PublicCode)
                .Select(session => (int?)session.Id)
                .SingleOrDefaultAsync(cancellationToken);

            var result = await new PlayerAchievementResetService(db).ResetPlayerAsync(
                accountId,
                connection.Game.HostId,
                connection.Player.Name,
                achievementCode,
                storedGameSessionId,
                cancellationToken);
            if (result is null)
            {
                return BadRequest(new { message = AchievementResetText.Current.Error });
            }

            if (!result.Reset)
            {
                return new JsonResult(new { message = AchievementResetText.Current.Error })
                {
                    StatusCode = StatusCodes.Status409Conflict
                };
            }

            return new JsonResult(new
            {
                reset = true,
                achievementCode = result.AchievementCode,
                target = result.Target,
                isSecret = result.IsSecret,
                isHostDefined = result.IsHostDefined
            });
        }
        finally
        {
            sessionRegistry.DisconnectPlayer(validationConnectionId);
        }
    }
}
