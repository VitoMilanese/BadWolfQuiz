using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

[Authorize]
public sealed class PlayerAdmissionModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    IHubContext<GameHub> gameHub,
    IStringLocalizer<PlayerAdmissionResource> localizer) : PageModel
{
    public IActionResult OnGet(Guid id)
    {
        var game = FindOwnedGame(id);
        if (game is null)
        {
            return NotFound();
        }

        var waitingCount = sessionRegistry
            .GetPlayerLobbyEntries(game)
            .Count(player => player.Presence == PlayerPresenceStatus.RejoinPending);

        return new JsonResult(new
        {
            waitingCount,
            automaticallyAcceptNewPlayers = PlayerAdmissionAutomation.IsEnabled(game),
            game.AllowsNewPlayers,
            labels = new
            {
                acceptAllWaiting = localizer["AcceptAllWaiting"].Value,
                automaticAcceptance = localizer["AutomaticAcceptance"].Value,
                allowNewConnections = localizer["AllowNewConnections"].Value,
                denyNewConnections = localizer["DenyNewConnections"].Value,
                enabled = localizer["Enabled"].Value.ToUpper(),
                disabled = localizer["Disabled"].Value.ToUpper()
            }
        });
    }

    public async Task<IActionResult> OnPostAcceptAllWaitingAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = FindOwnedGame(id);
        if (game is null)
        {
            return NotFound();
        }

        var accepted = await PlayerAdmissionAutomation.AcceptWaitingPlayersAsync(
            game,
            sessionRegistry,
            gameHub,
            cancellationToken);

        return new JsonResult(new { success = true, accepted });
    }

    public IActionResult OnPostToggleAutomaticAcceptance(Guid id)
    {
        var game = FindOwnedGame(id);
        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Status != GameSessionStatus.Running)
        {
            return new JsonResult(new { success = false }) { StatusCode = 409 };
        }

        var enabled = PlayerAdmissionAutomation.Toggle(
            game,
            sessionRegistry,
            gameHub);

        return new JsonResult(new { success = true, enabled });
    }

    private GameSessionRegistration? FindOwnedGame(Guid id) =>
        sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);
}
