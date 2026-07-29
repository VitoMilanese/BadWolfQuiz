using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Player;

public sealed class LobbyModel(GameSessionRegistry sessionRegistry) : PageModel
{
    public GameSessionRegistration Game { get; private set; } = null!;

    public GamePlayer CurrentPlayer { get; private set; } = null!;

    public IReadOnlyList<GamePlayer> Players { get; private set; } = [];

    public string? AccessToken { get; private set; }

    public IActionResult OnGet(string code, Guid playerId, string? accessToken)
    {
        var game = sessionRegistry.Find(code);

        if (game is null)
        {
            return NotFound();
        }

        var players = sessionRegistry.GetPlayers(game);
        var currentPlayer = players.FirstOrDefault(
            player => player.Id == new GamePlayerId(playerId));

        if (currentPlayer is null)
        {
            return NotFound();
        }

        Game = game;
        CurrentPlayer = currentPlayer;
        Players = players;
        AccessToken = accessToken;
        return Page();
    }
}
