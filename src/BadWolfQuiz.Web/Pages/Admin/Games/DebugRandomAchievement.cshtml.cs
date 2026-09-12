using System.Security.Claims;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

[Authorize]
public sealed class DebugRandomAchievementModel(
    IConfiguration configuration,
    GameSessionRegistry sessionRegistry,
    IDbContextFactory<QuizDbContext> dbFactory,
    IHubContext<GameHub> gameHub,
    IStringLocalizer<AchievementResource> localizer) : PageModel
{
    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostAsync(string? gameCode)
    {
        if (!configuration.GetValue<bool>("DebugMode"))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(gameCode))
        {
            return BadRequest();
        }

        var hostId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var game = sessionRegistry.Find(GameSessionRegistry.NormalizeCode(gameCode));
        if (game is null ||
            string.IsNullOrWhiteSpace(hostId) ||
            !string.Equals(game.HostId, hostId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        var player = game.Session.Players.FirstOrDefault();
        if (player is null)
        {
            return new JsonResult(new
            {
                success = false,
                reason = "no-players"
            });
        }

        await using var db = await dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
        var achievements = new PlayerAchievementService(db);
        var accountId = PlayerAchievementRuntimeState.GetPlayerAccountId(game, player.Id);
        var persisted = await achievements.LoadPersistedUnlocksAsync(
            accountId,
            game.HostId,
            player.Name,
            cancellationToken: HttpContext.RequestAborted);
        var unavailableCodes = persisted
            .Select(item => item.AchievementCode)
            .Concat(PlayerAchievementRuntimeState.GetPendingAchievementCodes(game, player.Id))
            .ToHashSet(StringComparer.Ordinal);
        var candidates = PlayerAchievementService.Catalog
            .Where(definition => !unavailableCodes.Contains(definition.Code))
            .ToList();

        while (candidates.Count > 0)
        {
            var index = Random.Shared.Next(candidates.Count);
            var definition = candidates[index];
            candidates.RemoveAt(index);

            var unlocked = await achievements.UnlockPlayerAsync(
                accountId,
                game.HostId,
                player.Name,
                definition.Code,
                sourceGameSessionId: null,
                cancellationToken: HttpContext.RequestAborted);
            if (!unlocked)
            {
                continue;
            }

            var notification = new AchievementUnlockNotification(
                $"{game.Session.Id.Value:N}:{player.Id.Value:N}:{definition.Code}",
                player.Id.Value,
                player.Name,
                definition.Code,
                localizer[$"{definition.Code}_Name"].Value,
                localizer[$"{definition.Code}_Description"].Value,
                $"/images/achievements/{definition.Code}.png");

            await gameHub.Clients
                .Group(GameHub.GroupName(game.PublicCode))
                .SendAsync(
                    "AchievementUnlocked",
                    notification,
                    HttpContext.RequestAborted);

            return new JsonResult(new
            {
                success = true,
                playerId = player.Id.Value,
                playerName = player.Name,
                achievementCode = definition.Code,
                notification
            });
        }

        return new JsonResult(new
        {
            success = false,
            reason = "all-unlocked"
        });
    }
}
