using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Hubs;

public sealed class GameHub(
    BuzzCoordinator buzzCoordinator,
    GameSessionRegistry sessionRegistry) : Hub
{
    public async Task JoinSession(string publicCode)
    {
        var normalizedCode = GameSessionRegistry.NormalizeCode(publicCode);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(normalizedCode));

        var game = sessionRegistry.Find(normalizedCode);

        if (game is not null)
        {
            await Clients.Caller.SendAsync(
                "PlayersChanged",
                CreatePlayersUpdate(sessionRegistry, game));
        }
    }

    public async Task Buzz(string publicCode, int gameQuestionId, int playerId, string playerName)
    {
        var accepted = buzzCoordinator.TryBuzz(gameQuestionId, playerId);

        if (accepted)
        {
            await Clients.Group(GroupName(publicCode)).SendAsync(
                "BuzzAccepted",
                new { gameQuestionId, playerId, playerName });
        }
        else
        {
            await Clients.Caller.SendAsync(
                "BuzzRejected",
                new { gameQuestionId, winnerPlayerId = buzzCoordinator.GetWinner(gameQuestionId) });
        }
    }

    public async Task ResetBuzz(string publicCode, int gameQuestionId)
    {
        buzzCoordinator.Reset(gameQuestionId);
        await Clients.Group(GroupName(publicCode)).SendAsync("BuzzReset", new { gameQuestionId });
    }

    public static object CreatePlayersUpdate(
        GameSessionRegistry sessionRegistry,
        GameSessionRegistration game)
    {
        var players = sessionRegistry.GetPlayers(game);

        return new
        {
            playerCount = players.Count,
            players = players.Select(player => new
            {
                id = player.Id.Value,
                player.Name,
                player.Score
            })
        };
    }

    public static string GroupName(string publicCode)
        => $"game:{GameSessionRegistry.NormalizeCode(publicCode)}";
}
