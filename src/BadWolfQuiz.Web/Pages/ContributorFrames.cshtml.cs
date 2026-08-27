using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages;

public sealed class ContributorFramesModel(
    GameSessionRegistry sessionRegistry,
    AvatarCatalog avatarCatalog,
    IOptions<FooterOptions> footerOptions,
    PremiumHostAccess premiumHostAccess,
    CurrentHost currentHost,
    QuizDbContext db,
    IHubContext<GameHub> gameHub,
    IWebHostEnvironment environment) : PageModel
{
    public IActionResult OnGet(string code)
    {
        var game = sessionRegistry.Find(code);
        if (game is null)
        {
            return NotFound();
        }

        var players = sessionRegistry.GetPlayers(game)
            .Select(player =>
            {
                var canUseFrame =
                    PlayerContributorAccess.IsContributor(
                        footerOptions.Value,
                        player) ||
                    avatarCatalog.CanUseFrame(player.AvatarId) ||
                    (!string.IsNullOrWhiteSpace(player.AvatarFrameAuthorizedHostId) &&
                     premiumHostAccess.IsPremium(player.AvatarFrameAuthorizedHostId));

                return new
                {
                    id = player.Id.Value,
                    enabled = canUseFrame && player.AvatarFrameEnabled,
                    frameId = canUseFrame ? player.AvatarFrameId : null
                };
            })
            .ToArray();

        Response.Headers.CacheControl = "no-store";
        return new JsonResult(new { players });
    }

    public async Task<IActionResult> OnPostHostFrameAsync(
        Guid gameId,
        bool enabled,
        string? frameId,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true ||
            currentHost.Id is not { } hostId)
        {
            return Unauthorized();
        }

        var game = sessionRegistry.FindOwned(new GameSessionId(gameId), hostId);
        if (game is null)
        {
            return NotFound();
        }

        var hostDisplayName = await db.Hosts
            .AsNoTracking()
            .Where(host => host.Id == hostId)
            .Select(host => host.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
        var canUseAvatarFrame = premiumHostAccess.IsPremium(hostId) ||
            avatarCatalog.CanUseFrame(game.Session.Settings.HostAvatarId) ||
            ContributorRecognition.IsContributor(
                footerOptions.Value,
                hostDisplayName);
        if (!canUseAvatarFrame)
        {
            return Forbid();
        }

        if (enabled &&
            !ContributorAvatarFrameCatalog.IsValid(environment, frameId))
        {
            return BadRequest();
        }

        var normalizedFrameId = ContributorAvatarFrameCatalog.Normalize(
            environment,
            frameId);
        var update = new
        {
            enabled = enabled && !string.IsNullOrWhiteSpace(normalizedFrameId),
            frameId = normalizedFrameId
        };

        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "HostContributorFrameChanged",
                update,
                cancellationToken);

        return new JsonResult(new { success = true });
    }
}
