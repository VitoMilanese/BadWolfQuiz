using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;

namespace BadWolfQuiz.Web.Hubs;

public sealed class GameHub(
    GameSessionRegistry sessionRegistry,
    AvatarCatalog avatarCatalog) : Hub
{
    private const int MaxPlayerImageBytes = 160 * 1024;
    private static readonly ConcurrentDictionary<string, HostConnection> HostConnections = new();

    public static bool IsHostConnected(GameSessionRegistration game) =>
        !string.IsNullOrWhiteSpace(game.HostId) &&
        HostConnections.Values.Any(connection =>
            string.Equals(
                connection.PublicCode,
                game.PublicCode,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                connection.HostId,
                game.HostId,
                StringComparison.Ordinal));

    public async Task RegisterHostSession(string publicCode)
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var normalizedCode = GameSessionRegistry.NormalizeCode(publicCode);
        var game = sessionRegistry.Find(normalizedCode);
        var hostId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (game is null ||
            string.IsNullOrWhiteSpace(hostId) ||
            !string.Equals(game.HostId, hostId, StringComparison.Ordinal))
        {
            return;
        }

        HostConnections[Context.ConnectionId] = new HostConnection(
            normalizedCode,
            hostId);
        await Groups.AddToGroupAsync(Context.ConnectionId, HostGroupName(normalizedCode));
        await Clients.Group(GroupName(normalizedCode)).SendAsync("HostWebcamReady");
    }

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

    public Task PollPlayers(string publicCode)
    {
        var game = sessionRegistry.Find(publicCode);
        return game is null
            ? Task.CompletedTask
            : Clients.Caller.SendAsync(
                "PlayersChanged",
                CreatePlayersUpdate(sessionRegistry, game));
    }

    public async Task JoinPlayerSession(
        string publicCode,
        string accessToken,
        bool isVisible,
        string? transitionToken)
    {
        var connection = sessionRegistry.ConnectPlayer(
            publicCode,
            accessToken,
            Context.ConnectionId,
            isVisible,
            transitionToken);

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
        await Clients.Caller.SendAsync(
            "FinalQuestionStateChanged",
            CreateFinalQuestionUpdate(connection.Game, connection.Player));

        await BroadcastPlayers(connection.Game);
    }

    public string? PreparePlayerTransition()
    {
        return sessionRegistry.CreatePlayerTransitionToken(
            Context.ConnectionId);
    }

    public async Task SubmitFinalWager(int amount)
    {
        await ExecuteFinalPlayerAction(() =>
            sessionRegistry.SubmitFinalWager(Context.ConnectionId, amount));
    }

    public async Task SubmitFinalAnswer(string answer)
    {
        await ExecuteFinalPlayerAction(() =>
            sessionRegistry.SubmitFinalAnswer(Context.ConnectionId, answer));
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

    public async Task SetPlayerAvatar(string avatarId)
    {
        if (!avatarCatalog.IsValid(avatarId))
        {
            return;
        }

        var connection = sessionRegistry.SetPlayerAvatar(
            Context.ConnectionId,
            avatarId);

        if (connection is not null)
        {
            await BroadcastPlayers(connection.Game);
        }
    }

    public async Task SetPlayerUploadedImage(string imageDataUrl)
    {
        if (!IsValidPlayerImage(imageDataUrl))
        {
            return;
        }

        var connection = sessionRegistry.SetPlayerUploadedImage(
            Context.ConnectionId,
            imageDataUrl);

        if (connection is not null)
        {
            await BroadcastPlayers(connection.Game);
        }
    }

    public async Task SetPlayerWebcamEnabled(bool isEnabled)
    {
        var connection = sessionRegistry.SetPlayerWebcamEnabled(
            Context.ConnectionId,
            isEnabled);

        if (connection is not null)
        {
            await BroadcastPlayers(connection.Game);
        }
    }

    public async Task SetPlayerWebcamUrl(string webcamUrl)
    {
        if (!IsValidWebcamUrl(webcamUrl))
        {
            return;
        }

        var connection = sessionRegistry.SetPlayerWebcamUrl(
            Context.ConnectionId,
            webcamUrl.Trim());

        if (connection is not null)
        {
            await BroadcastPlayers(connection.Game);
        }
    }

    public Task SendPlayerWebcamOffer(
        string publicCode,
        JsonElement sessionDescription)
    {
        var connection = GetApprovedPlayer(publicCode);
        return connection is null
            ? Task.CompletedTask
            : Clients.Group(HostGroupName(connection.Game.PublicCode)).SendAsync(
                "PlayerWebcamOffer",
                new
                {
                    playerId = connection.Player.Id.Value,
                    playerConnectionId = Context.ConnectionId,
                    sessionDescription
                });
    }

    public Task SendPlayerWebcamIceCandidate(
        string publicCode,
        JsonElement candidate)
    {
        var connection = GetApprovedPlayer(publicCode);
        return connection is null
            ? Task.CompletedTask
            : Clients.Group(HostGroupName(connection.Game.PublicCode)).SendAsync(
                "PlayerWebcamIceCandidate",
                new
                {
                    playerId = connection.Player.Id.Value,
                    playerConnectionId = Context.ConnectionId,
                    candidate
                });
    }

    public Task SendHostWebcamAnswer(
        string playerConnectionId,
        JsonElement sessionDescription)
    {
        return CanRelayToPlayer(playerConnectionId)
            ? Clients.Client(playerConnectionId).SendAsync(
                "HostWebcamAnswer",
                sessionDescription)
            : Task.CompletedTask;
    }

    public Task SendHostWebcamIceCandidate(
        string playerConnectionId,
        JsonElement candidate)
    {
        return CanRelayToPlayer(playerConnectionId)
            ? Clients.Client(playerConnectionId).SendAsync(
                "HostWebcamIceCandidate",
                candidate)
            : Task.CompletedTask;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        HostConnections.TryRemove(Context.ConnectionId, out _);
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

        if (tick.Outcome == QuestionTimerOutcome.ClueRevealed)
        {
            var question = tick.Game.Session.Board.Questions
                .First(item =>
                    item.Status is RuntimeQuestionStatus.Selected or
                        RuntimeQuestionStatus.Active);

            await group.SendAsync(
                "QuestionClueRevealed",
                new
                {
                    sourceQuestionId = question.SourceQuestionId,
                    question.RevealedClueCount,
                    question.CanRevealClue
                });

            return;
        }

        if (tick.Outcome == QuestionTimerOutcome.AnswerExpired &&
            tick.AnswerAttempt is { } attempt)
        {
            var player = tick.Game.Session.Players.Single(
                item => item.Id == attempt.PlayerId);

            sessionRegistry.SetAnswerResultOverlay(
                tick.Game,
                player,
                attempt,
                "timeout");
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
                avatarId = player.Presence is
                    PlayerPresenceStatus.Active or PlayerPresenceStatus.Inactive
                    ? player.AvatarId
                    : null,
                imageDataUrl = (player.Presence is
                    PlayerPresenceStatus.Active or PlayerPresenceStatus.Inactive) &&
                    player.UsesUploadedImage
                        ? player.UploadedImageDataUrl
                        : null,
                webcamEnabled = player.Presence == PlayerPresenceStatus.Active &&
                    player.IsWebcamEnabled,
                webcamUrl = player.Presence is
                    PlayerPresenceStatus.Active or PlayerPresenceStatus.Inactive
                        ? player.WebcamUrl
                        : null,
                isActive = game.Session.ActivePlayerId == player.Id,
                presence = player.Presence.ToString().ToLowerInvariant()
            })
        };
    }

    public static object CreateQuestionAnswerResult(
        GamePlayer player,
        QuestionAnswerAttempt attempt,
        string reason)
    {
        return new
        {
            playerId = player.Id.Value,
            playerName = player.Name,
            scoreDelta = attempt.ScoreDelta,
            reason
        };
    }

    public static object CreateStatusUpdate(GameSessionRegistration game)
        => new { status = game.Session.Status.ToString().ToLowerInvariant() };

    private static bool IsValidPlayerImage(string? imageDataUrl)
    {
        if (string.IsNullOrWhiteSpace(imageDataUrl) ||
            imageDataUrl.Length > 225_000)
        {
            return false;
        }

        var commaIndex = imageDataUrl.IndexOf(',');
        if (commaIndex <= 0)
        {
            return false;
        }

        var prefix = imageDataUrl[..commaIndex];
        if (prefix is not "data:image/jpeg;base64" and
            not "data:image/png;base64" and
            not "data:image/webp;base64")
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(imageDataUrl[(commaIndex + 1)..])
                .Length <= MaxPlayerImageBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidWebcamUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(uri.Host) &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        value!.Length <= 2048;

    public static object CreateFinalQuestionUpdate(
        GameSessionRegistration game,
        GamePlayer player)
    {
        var submission = game.Session.FinalQuestion?.Submissions
            .SingleOrDefault(item => item.PlayerId == player.Id);

        return new
        {
            status = game.Session.Status.ToString().ToLowerInvariant(),
            participates = submission is not null,
            minimumWager = FinalQuestion.MinimumWager,
            maximumWager = submission?.MaximumWager,
            hasSubmittedWager = submission?.Wager is not null,
            hasSubmittedAnswer = submission?.Answer is not null,
            isJudged = submission?.IsCorrect.HasValue ?? false,
            isCorrect = submission?.IsCorrect
        };
    }

    public static object CreateTimerUpdate(GameSessionRegistration game)
    {
        var answerTimer = game.Session.AnswerTimer;
        var buzzerTimer = game.Session.Timer;
        var isAnswerTimerActive = answerTimer.Status is
            GameTimerStatus.Running or GameTimerStatus.Paused;
        var timer = isAnswerTimerActive ? answerTimer : buzzerTimer;
        var isVisible = timer.Status is
            GameTimerStatus.Running or GameTimerStatus.Paused;

        var question = game.Session.Board.Questions.FirstOrDefault(item =>
            item.Status is RuntimeQuestionStatus.Selected or
                RuntimeQuestionStatus.Active);

        return new
        {
            mode = isAnswerTimerActive ? "answer" : "buzzer",
            status = timer.Status.ToString().ToLowerInvariant(),
            remainingMilliseconds = isVisible
                ? Math.Max(0, (int)Math.Ceiling(timer.Remaining.TotalMilliseconds))
                : 0,
            durationMilliseconds = (int)timer.Duration.TotalMilliseconds,
            isVisible,
            sourceQuestionId = question?.SourceQuestionId,
            currentCorrectAnswerValue = question is null
                ? (int?)null
                : game.Session.GetCurrentCorrectAnswerValue(
                    question.SourceQuestionId),
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

    private static string HostGroupName(string publicCode)
        => $"game-host:{GameSessionRegistry.NormalizeCode(publicCode)}";

    private PlayerConnectionResult? GetApprovedPlayer(string publicCode)
    {
        var connection = sessionRegistry.GetPlayerConnection(Context.ConnectionId);
        return connection is not null && string.Equals(
            connection.Game.PublicCode,
            GameSessionRegistry.NormalizeCode(publicCode),
            StringComparison.OrdinalIgnoreCase)
            ? connection
            : null;
    }

    private bool CanRelayToPlayer(string playerConnectionId)
    {
        if (!HostConnections.TryGetValue(Context.ConnectionId, out var hostConnection))
        {
            return false;
        }

        var player = sessionRegistry.GetPlayerConnection(playerConnectionId);
        return player is not null && string.Equals(
            player.Game.PublicCode,
            hostConnection.PublicCode,
            StringComparison.OrdinalIgnoreCase);
    }

    private Task BroadcastPlayers(GameSessionRegistration game)
    {
        return Clients
            .Group(GroupName(game.PublicCode))
            .SendAsync("PlayersChanged", CreatePlayersUpdate(sessionRegistry, game));
    }

    private async Task ExecuteFinalPlayerAction(
        Func<FinalPlayerActionResult?> action)
    {
        FinalPlayerActionResult? result;

        try
        {
            result = action();
        }
        catch (GameRuleViolationException exception)
        {
            await Clients.Caller.SendAsync(
                "FinalQuestionActionRejected",
                new { message = exception.Message });
            return;
        }
        catch (ArgumentException exception)
        {
            await Clients.Caller.SendAsync(
                "FinalQuestionActionRejected",
                new { message = exception.Message });
            return;
        }

        if (result is null)
        {
            await Clients.Caller.SendAsync(
                "FinalQuestionActionRejected",
                new { message = "Player access is not approved." });
            return;
        }

        await Clients.Caller.SendAsync(
            "FinalQuestionStateChanged",
            CreateFinalQuestionUpdate(result.Game, result.Player));
        await Clients
            .Group(GroupName(result.Game.PublicCode))
            .SendAsync("FinalQuestionProgressChanged");
    }

    private sealed record HostConnection(string PublicCode, string HostId);
}
