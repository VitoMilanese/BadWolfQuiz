using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class RestartModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    IHubContext<GameHub> gameHub) : PageModel
{
    public IActionResult OnGet(Guid id) =>
        RedirectToPage("Lobby", new { id });

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Status != GameSessionStatus.Running)
        {
            return RedirectToPage("Lobby", new { id });
        }

        game.RestartSession();

        var group = gameHub.Clients.Group(GameHub.GroupName(game.PublicCode));
        await group.SendAsync(
            "GameStatusChanged",
            GameHub.CreateStatusUpdate(game));
        await group.SendAsync(
            "PlayersChanged",
            GameHub.CreatePlayersUpdate(sessionRegistry, game));
        await group.SendAsync(
            "TimerStateChanged",
            GameHub.CreateTimerUpdate(game));
        await group.SendAsync(
            "BuzzerStateChanged",
            GameHub.CreateBuzzerUpdate(game));

        return RedirectToPage("RunningRoundIntro", new { id });
    }
}
