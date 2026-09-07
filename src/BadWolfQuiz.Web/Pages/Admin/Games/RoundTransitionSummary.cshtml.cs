using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class RoundTransitionSummaryModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost) : PageModel
{
    public GameSessionRegistration Game { get; private set; } = null!;

    public IReadOnlyList<RoundLeaderboardEntry> RoundLeaders { get; private set; } = [];

    public IActionResult OnGet(Guid id)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Status != GameSessionStatus.Running ||
            !game.Session.IsCurrentRoundComplete ||
            game.Session.Players.Count == 0)
        {
            return new StatusCodeResult(204);
        }

        Game = game;
        RoundLeaders = game.Session.GetCurrentRoundStandings()
            .Take(3)
            .Select(standing => new RoundLeaderboardEntry(
                standing.Position,
                standing.PlayerId,
                standing.PlayerName,
                standing.Score))
            .ToArray();

        return Page();
    }
}
