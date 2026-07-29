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
                "GameStatusChanged",
                CreateStatusUpdate(game));
            await Clients.Caller.SendAsync(
                "PlayersChanged",
                CreatePlayersUpdate(sessionRegistry, game));
        }
    }

    public async Task JoinPlayerSession(
        string publicCode,
        string accessToken,
        bool isVisible)
    {
        var connection = sessionRegistry.ConnectPlayer(
            publicCode,
            accessToken,
            Context.ConnectionId,
            isVisible);

        if (connection is null)
        {
            await Clients.Caller.SendAsync("PlayerAccessRejected");
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupName(connection.Game.PublicCode));

        if (connection.RequiresApproval)
        {
            await Clients.Caller.SendAsync("RejoinApprovalRequired");
        }

        await Clients.Caller.SendAsync(
            "GameStatusChanged",
            CreateStatusUpdate(connection.Game));
        await BroadcastPlayers(connection.Game);
    }

    public async Task SetPlayerVisibility(bool isVisible)
    {
        var connection = sessionRegistry.SetPlayerVisibility(
            Context.ConnectionId,
            isVisible);

        if (connection is not null)
        {
            await BroadcastPlayers(connection.Game);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connection = sessionRegistry.DisconnectPlayer(Context.ConnectionId);

        if (connection is not null)
        {
            await BroadcastPlayers(connection.Game);
        }

        await base.OnDisconnectedAsync(exception);
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
        var players = sessionRegistry.GetPlayerLobbyEntries(game);

        return new
        {
            playerCount = players.Count,
            players = players.Select(player => new
            {
                id = player.Id.Value,
                player.Name,
                player.Score,
                isActive = game.Session.ActivePlayerId == player.Id,
                presence = player.Presence.ToString().ToLowerInvariant()
            })
        };
    }

    public static object CreateStatusUpdate(GameSessionRegistration game)
        => new { status = game.Session.Status.ToString().ToLowerInvariant() };

    public static string GroupName(string publicCode)
        => $"game:{GameSessionRegistry.NormalizeCode(publicCode)}";

    private Task BroadcastPlayers(GameSessionRegistration game)
    {
        return Clients
            .Group(GroupName(game.PublicCode))
            .SendAsync("PlayersChanged", CreatePlayersUpdate(sessionRegistry, game));
    }
}
