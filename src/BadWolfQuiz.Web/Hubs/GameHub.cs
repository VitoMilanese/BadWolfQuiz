using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Hubs;

public sealed class GameHub(GameSessionRegistry sessionRegistry) : Hub
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

            var buzzerUpdate = CreateBuzzerUpdate(game);

            if (buzzerUpdate is not null)
            {
                await Clients.Caller.SendAsync("BuzzerStateChanged", buzzerUpdate);
            }

            await Clients.Caller.SendAsync(
                "TimerStateChanged",
                CreateTimerUpdate(game));
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

        var buzzerUpdate = CreateBuzzerUpdate(connection.Game);

        if (buzzerUpdate is not null)
        {
            await Clients.Caller.SendAsync("BuzzerStateChanged", buzzerUpdate);
        }

        await Clients.Caller.SendAsync(
            "TimerStateChanged",
            CreateTimerUpdate(connection.Game));

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

    public async Task PollQuestionTimer(string publicCode)
    {
        var tick = sessionRegistry.ProcessQuestionTimers(publicCode);

        if (tick is null)
        {
            return;
        }

        var group = Clients.Group(GroupName(tick.Game.PublicCode));

        await group.SendAsync(
            "TimerStateChanged",
            CreateTimerUpdate(tick.Game));

        if (tick.Outcome == QuestionTimerOutcome.None)
        {
            return;
        }

        await group.SendAsync(
            "BuzzerStateChanged",
            CreateBuzzerUpdate(tick.Game));
        await group.SendAsync(
            "PlayersChanged",
            CreatePlayersUpdate(sessionRegistry, tick.Game));
    }

    public async Task Buzz(int sourceQuestionId)
    {
        BuzzerClaimResult? claim;

        try
        {
            claim = sessionRegistry.ClaimQuestionBuzzer(
                Context.ConnectionId,
                sourceQuestionId);
        }
        catch (GameRuleViolationException)
        {
            claim = null;
        }

        if (claim is null)
        {
            await Clients.Caller.SendAsync(
                "BuzzRejected",
                new { sourceQuestionId });
            return;
        }

        if (!claim.IsWinner)
        {
            return;
        }

        // Keep the buzzer visually open while the server collects presses
        // that arrive within the near-simultaneous one-second window.
        await Task.Delay(TimeSpan.FromSeconds(1));

        await Clients
            .Group(GroupName(claim.Game.PublicCode))
            .SendAsync("BuzzerStateChanged", CreateBuzzerUpdate(claim.Game));
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

    public static object CreateTimerUpdate(GameSessionRegistration game)
    {
        var answerTimer = game.Session.AnswerTimer;
        var buzzerTimer = game.Session.Timer;
        var isAnswerTimerActive = answerTimer.Status is
            GameTimerStatus.Running or GameTimerStatus.Paused;
        var timer = isAnswerTimerActive ? answerTimer : buzzerTimer;
        var isVisible = timer.Status is
            GameTimerStatus.Running or GameTimerStatus.Paused;

        return new
        {
            mode = isAnswerTimerActive ? "answer" : "buzzer",
            status = timer.Status.ToString().ToLowerInvariant(),
            remainingMilliseconds = isVisible
                ? Math.Max(0, (int)Math.Ceiling(timer.Remaining.TotalMilliseconds))
                : 0,
            durationMilliseconds = (int)timer.Duration.TotalMilliseconds,
            isVisible
        };
    }

    public static object CreateBuzzerUpdate(GameSessionRegistration game)
    {
        var question = game.Session.Board.Questions.FirstOrDefault(item =>
            item.Status is not RuntimeQuestionStatus.Available and
                not RuntimeQuestionStatus.Resolved and
                not RuntimeQuestionStatus.ShowingAnswer);

        if (question is null)
        {
            return new
            {
                sourceQuestionId = (int?)null,
                status = QuestionBuzzerStatus.Closed
                    .ToString()
                    .ToLowerInvariant(),
                answeringPlayerId = (Guid?)null,
                answeringPlayerName = (string?)null,
                ineligiblePlayerIds = Array.Empty<Guid>(),
                buzzerRace = (object?)null
            };
        }

        var answeringPlayer = question.AnsweringPlayerId is { } playerId
            ? game.Session.Players.Single(player => player.Id == playerId)
            : null;

        return new
        {
            sourceQuestionId = question.SourceQuestionId,
            status = question.BuzzerStatus.ToString().ToLowerInvariant(),
            answeringPlayerId = answeringPlayer?.Id.Value,
            answeringPlayerName = answeringPlayer?.Name,
            ineligiblePlayerIds = question.AnswerAttempts
                .Select(attempt => attempt.PlayerId.Value)
                .ToArray(),
            buzzerRace = game.BuzzerRace is { LatePlayers.Count: > 0 } race &&
                race.SourceQuestionId == question.SourceQuestionId
                    ? new
                    {
                        winnerPlayerId = race.WinnerPlayerId.Value,
                        winnerPlayerName = race.WinnerPlayerName,
                        latePlayers = race.LatePlayers.Select(player => new
                        {
                            playerId = player.PlayerId.Value,
                            playerName = player.PlayerName,
                            delayMilliseconds = player.DelayMilliseconds
                        })
                    }
                    : null
        };
    }

    public static string GroupName(string publicCode)
        => $"game:{GameSessionRegistry.NormalizeCode(publicCode)}";

    private Task BroadcastPlayers(GameSessionRegistration game)
    {
        return Clients
            .Group(GroupName(game.PublicCode))
            .SendAsync("PlayersChanged", CreatePlayersUpdate(sessionRegistry, game));
    }
}
