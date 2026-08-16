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

        var previousStatus = game.Session.Status;
        if (previousStatus is not (
            GameSessionStatus.Running or
            GameSessionStatus.FinalWagering or
            GameSessionStatus.FinalAnswering or
            GameSessionStatus.FinalJudging))
        {
            return RedirectToPage("Lobby", new { id });
        }

        var wasFinalQuestion = previousStatus is
            GameSessionStatus.FinalWagering or
            GameSessionStatus.FinalAnswering or
            GameSessionStatus.FinalJudging;

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

        if (wasFinalQuestion)
        {
            await group.SendAsync("FinalQuestionProgressChanged");
        }

        return RedirectToPage("RunningRoundIntro", new { id });
    }
}
