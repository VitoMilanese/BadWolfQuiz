using System.Collections.Concurrent;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Services;

public static class PlayerAdmissionAutomation
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly ConcurrentDictionary<GameSessionId, CancellationTokenSource> Loops = new();

    public static bool IsEnabled(GameSessionRegistration game) =>
        Loops.ContainsKey(game.Session.Id);

    public static bool Toggle(
        GameSessionRegistration game,
        GameSessionRegistry sessionRegistry,
        IHubContext<GameHub> gameHub)
    {
        if (Loops.TryRemove(game.Session.Id, out var existing))
        {
            existing.Cancel();
            existing.Dispose();
            return false;
        }

        var cancellation = new CancellationTokenSource();
        if (!Loops.TryAdd(game.Session.Id, cancellation))
        {
            cancellation.Dispose();
            return IsEnabled(game);
        }

        _ = RunAsync(game, sessionRegistry, gameHub, cancellation);
        return true;
    }

    public static async Task<int> AcceptWaitingPlayersAsync(
        GameSessionRegistration game,
        GameSessionRegistry sessionRegistry,
        IHubContext<GameHub> gameHub,
        CancellationToken cancellationToken = default)
    {
        var waitingPlayerIds = sessionRegistry
            .GetPlayerLobbyEntries(game)
            .Where(player => player.Presence == PlayerPresenceStatus.RejoinPending)
            .Select(player => player.Id)
            .ToArray();

        if (waitingPlayerIds.Length == 0)
        {
            return 0;
        }

        var approvedConnectionIds = new List<string>();
        var approvedPlayers = 0;

        foreach (var playerId in waitingPlayerIds)
        {
            var approval = sessionRegistry.ApprovePlayerRejoin(game.PublicCode, playerId);
            if (approval is null || approval.ConnectionIds.Count == 0)
            {
                continue;
            }

            approvedPlayers++;
            approvedConnectionIds.AddRange(approval.ConnectionIds);
        }

        if (approvedConnectionIds.Count == 0)
        {
            return 0;
        }

        var clients = gameHub.Clients.Clients(approvedConnectionIds);
        await clients.SendAsync("RejoinApproved", cancellationToken);
        await clients.SendAsync(
            "GameStatusChanged",
            GameHub.CreateStatusUpdate(game),
            cancellationToken);

        var buzzerUpdate = GameHub.CreateBuzzerUpdate(game);
        if (buzzerUpdate is not null)
        {
            await clients.SendAsync("BuzzerStateChanged", buzzerUpdate, cancellationToken);
        }

        await clients.SendAsync(
            "TimerStateChanged",
            GameHub.CreateTimerUpdate(game),
            cancellationToken);

        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "PlayersChanged",
                GameHub.CreatePlayersUpdate(sessionRegistry, game),
                cancellationToken);

        return approvedPlayers;
    }

    private static async Task RunAsync(
        GameSessionRegistration game,
        GameSessionRegistry sessionRegistry,
        IHubContext<GameHub> gameHub,
        CancellationTokenSource cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested &&
                   game.Session.Status == GameSessionStatus.Running)
            {
                await AcceptWaitingPlayersAsync(
                    game,
                    sessionRegistry,
                    gameHub,
                    cancellation.Token);
                await Task.Delay(PollInterval, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (Loops.TryGetValue(game.Session.Id, out var current) &&
                ReferenceEquals(current, cancellation))
            {
                Loops.TryRemove(game.Session.Id, out _);
            }

            cancellation.Dispose();
        }
    }
}
