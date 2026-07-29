using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class LobbyModel(GameSessionRegistry sessionRegistry) : PageModel
{
    public GameSessionRegistration Game { get; private set; } = null!;

    public IReadOnlyList<GamePlayer> Players { get; private set; } = [];

    public IActionResult OnGet(Guid id)
    {
        var game = sessionRegistry.Find(new GameSessionId(id));

        if (game is null)
        {
            return NotFound();
        }

        Game = game;
        Players = sessionRegistry.GetPlayers(game);
        return Page();
    }
}
