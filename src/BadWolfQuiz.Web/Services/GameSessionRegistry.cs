using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionRegistry
{
    private const int MaxCodeGenerationAttempts = 20;
    private static readonly TimeSpan BuzzerRaceWindow = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PlayerTransitionTokenLifetime =
        TimeSpan.FromSeconds(30);
    private readonly IGameCodeGenerator _gameCodeGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<GameSessionId, GameSessionRegistration> _sessionsById = new();
    private readonly ConcurrentDictionary<string, GameSessionRegistration> _sessionsByCode =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PlayerAccess> _playerAccessByTokenHash = new();
    private readonly ConcurrentDictionary<string, PlayerTransitionAccess>
        _playerTransitionAccessByTokenHash = new();
    private readonly Dictionary<string, PlayerConnection> _playerConnections = new();
    private readonly object _presenceSync = new();

    public GameSessionRegistry(
        IGameCodeGenerator gameCodeGenerator,
        TimeProvider? timeProvider = null)
    {
        _gameCodeGenerator = gameCodeGenerator;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public GameSessionRegistration Create(QuizSnapshot quiz)
    {
        return Create(quiz, GameSessionSettings.Default);
    }

    public GameSessionRegistration Create(
        QuizSnapshot quiz,
        GameSessionSettings settings,
        string? hostId = null)
    {
        ArgumentNullException.ThrowIfNull(quiz);
        ArgumentNullException.ThrowIfNull(settings);

        var session = GameSession.Create(quiz, settings);

        for (var attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
        {
            var code = NormalizeCode(_gameCodeGenerator.Create());
            EnsureValidCode(code);

            var registration = new GameSessionRegistration(code, session, hostId);

            if (!_sessionsByCode.TryAdd(code, registration))
            {
                continue;
            }

            if (_sessionsById.TryAdd(session.Id, registration))
            {
                return registration;
            }

            _sessionsByCode.TryRemove(code, out _);
            throw new InvalidOperationException(
                $"A game session with identifier {session.Id} already exists.");
        }

        throw new InvalidOperationException("Could not generate a unique public game code.");
    }

    public GameSessionRegistration? Find(GameSessionId id)
    {
        return _sessionsById.GetValueOrDefault(id);
    }

    public GameSessionRegistration? FindOwned(GameSessionId id, string hostId)
    {
        var game = Find(id);
        return game is not null && string.Equals(game.HostId, hostId, StringComparison.Ordinal)
            ? game
            : null;
    }

    public GameSessionRegistration? Find(string publicCode)
    {
        if (string.IsNullOrWhiteSpace(publicCode))
        {
            return null;
        }

        return _sessionsByCode.GetValueOrDefault(NormalizeCode(publicCode));
    }

    public PlayerJoinResult JoinPlayer(
        string publicCode,
        string playerName,
        string? avatarId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

        var game = Find(publicCode);

        if (game is null)
        {
            return PlayerJoinResult.Failed(PlayerJoinStatus.GameNotFound);
        }

        lock (game)
        {
            if (game.Session.Players.Any(player =>
                    string.Equals(player.Name, playerName.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return PlayerJoinResult.Failed(PlayerJoinStatus.NameAlreadyUsed);
            }

            if (game.Session.Status != GameSessionStatus.Lobby &&
                !game.AllowsNewPlayers)
            {
                return PlayerJoinResult.Failed(PlayerJoinStatus.GameAlreadyStarted);
            }

            var player = game.Session.AddPlayer(playerName);
            if (!string.IsNullOrWhiteSpace(avatarId))
            {
                player.SetAvatar(avatarId);
            }
            var accessToken = CreatePlayerAccess(game, player);
            return PlayerJoinResult.Succeeded(game, player, accessToken);
        }
    }

    public bool ToggleNewPlayerJoining(string publicCode)
    {
        var game = Find(publicCode)
            ?? throw new GameRuleViolationException("The game was not found.");

        lock (game)
        {
            if (game.Session.Status != GameSessionStatus.Running)
            {
                throw new GameRuleViolationException(
                    "Player joining can only be changed while the game is running.");
            }

            game.AllowsNewPlayers = !game.AllowsNewPlayers;
            return game.AllowsNewPlayers;
        }
    }

    public PlayerRemoval? RemovePlayer(string publicCode, GamePlayerId playerId)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        GamePlayer player;

        lock (game)
        {
            player = game.Session.RemovePlayer(playerId);
        }

        string[] connectionIds;

        lock (_presenceSync)
        {
            connectionIds = _playerConnections
                .Where(item =>
                    item.Value.Access.Game == game &&
                    item.Value.Access.Player.Id == playerId)
                .Select(item => item.Key)
                .ToArray();

            foreach (var connectionId in connectionIds)
            {
                _playerConnections.Remove(connectionId);
            }
        }

        foreach (var access in _playerAccessByTokenHash.Where(item =>
                     item.Value.Game == game && item.Value.Player.Id == playerId).ToArray())
        {
            _playerAccessByTokenHash.TryRemove(access.Key, out _);
        }

        foreach (var transition in _playerTransitionAccessByTokenHash.Where(item =>
                     item.Value.Access.Game == game &&
                     item.Value.Access.Player.Id == playerId).ToArray())
        {
            _playerTransitionAccessByTokenHash.TryRemove(transition.Key, out _);
        }

        return new PlayerRemoval(game, player, connectionIds);
    }

    public PlayerConnectionResult? ConnectPlayer(
        string publicCode,
        string accessToken,
        string connectionId,
        bool isVisible,
        string? transitionToken = null)
    {
        if (string.IsNullOrWhiteSpace(accessToken) ||
            !_playerAccessByTokenHash.TryGetValue(HashToken(accessToken), out var access) ||
            !string.Equals(
                access.Game.PublicCode,
                NormalizeCode(publicCode),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hasValidTransition = ConsumePlayerTransitionToken(
            transitionToken,
            access);
        bool requiresApproval;

        lock (_presenceSync)
        {
            var hasApprovedConnection = _playerConnections.Values.Any(connection =>
                connection.Access == access &&
                connection.IsApproved);
            requiresApproval =
                access.Game.Session.Status != GameSessionStatus.Lobby &&
                !hasApprovedConnection &&
                !hasValidTransition;
            _playerConnections[connectionId] = new PlayerConnection(
                access,
                isVisible,
                !requiresApproval);
        }

        return new PlayerConnectionResult(access.Game, access.Player, requiresApproval);
    }

    public string? CreatePlayerTransitionToken(string connectionId)
    {
        PlayerConnection connection;

        lock (_presenceSync)
        {
            if (!_playerConnections.TryGetValue(
                    connectionId,
                    out var currentConnection) ||
                !currentConnection.IsApproved)
            {
                return null;
            }

            connection = currentConnection;
        }

        while (true)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var transition = new PlayerTransitionAccess(
                connection.Access,
                _timeProvider.GetUtcNow() + PlayerTransitionTokenLifetime);

            if (_playerTransitionAccessByTokenHash.TryAdd(
                    HashToken(token),
                    transition))
            {
                return token;
            }
        }
    }

    public PlayerConnectionResult? SetPlayerVisibility(string connectionId, bool isVisible)
    {
        lock (_presenceSync)
        {
            if (!_playerConnections.TryGetValue(connectionId, out var connection))
            {
                return null;
            }

            _playerConnections[connectionId] = connection with { IsVisible = isVisible };
            return new PlayerConnectionResult(
                connection.Access.Game,
                connection.Access.Player,
                !connection.IsApproved);
        }
    }

    public PlayerConnectionResult? SetPlayerAvatar(string connectionId, string avatarId)
    {
        PlayerConnection connection;

        lock (_presenceSync)
        {
            if (!_playerConnections.TryGetValue(connectionId, out connection))
            {
                return null;
            }
        }

        lock (connection.Access.Game)
        {
            connection.Access.Player.SetAvatar(avatarId);
        }

        return new PlayerConnectionResult(
            connection.Access.Game,
            connection.Access.Player,
            !connection.IsApproved);
    }

    public PlayerConnectionResult? DisconnectPlayer(string connectionId)
    {
        lock (_presenceSync)
        {
            if (!_playerConnections.Remove(connectionId, out var connection))
            {
                return null;
            }

            return new PlayerConnectionResult(
                connection.Access.Game,
                connection.Access.Player,
                !connection.IsApproved);
        }
    }

    public GameSessionRegistration? UpdateSettings(
        string publicCode,
        GameSessionSettings settings)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            game.Session.UpdateSettings(settings);
            return game;
        }
    }

    public GameSessionRegistration? StartGame(string publicCode)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            game.Session.Start();
            return game;
        }
    }

    public QuizRoundSnapshot? AdvanceToNextRound(string publicCode)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            game.BuzzerRace = null;
            return game.Session.AdvanceToNextRound();
        }
    }

    public FinalQuestion? StartFinalQuestion(string publicCode)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.StartFinalQuestion();
        }
    }

    public FinalPlayerActionResult? SubmitFinalWager(
        string connectionId,
        int amount)
    {
        return ExecuteApprovedPlayerAction(
            connectionId,
            (game, player) => game.Session.SubmitFinalWager(player.Id, amount));
    }

    public GameSessionRegistration? LockFinalWagers(string publicCode)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            game.Session.LockFinalWagers();
            return game;
        }
    }

    public FinalPlayerActionResult? SubmitFinalAnswer(
        string connectionId,
        string answer)
    {
        return ExecuteApprovedPlayerAction(
            connectionId,
            (game, player) => game.Session.SubmitFinalAnswer(player.Id, answer));
    }

    public GameSessionRegistration? LockFinalAnswers(string publicCode)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            game.Session.LockFinalAnswers();
            return game;
        }
    }

    public FinalPlayerSubmission? JudgeFinalAnswer(
        string publicCode,
        GamePlayerId playerId,
        bool isCorrect)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.JudgeFinalAnswer(playerId, isCorrect);
        }
    }

    public RuntimeQuestion? SelectQuestion(
        string publicCode,
        int sourceQuestionId)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.SelectQuestion(sourceQuestionId);
        }
    }

    public RuntimeQuestion? SubmitQuestionWager(
        string publicCode,
        int sourceQuestionId,
        int amount)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.SubmitQuestionWager(
                sourceQuestionId,
                amount);
        }
    }

    public GameTimer? StartWagerAnswerTimer(
        string publicCode,
        int sourceQuestionId)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.StartWagerAnswerTimer(sourceQuestionId);
        }
    }

    public GameTimer? PauseQuestionTimer(string publicCode)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.PauseQuestionTimer();
        }
    }

    public GameTimer? ResumeQuestionTimer(string publicCode)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.ResumeQuestionTimer();
        }
    }

    public QuestionTimerTickResult? ProcessQuestionTimers(string publicCode)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            var outcome = game.Session.ProcessQuestionTimers();

            if (outcome != QuestionTimerOutcome.None)
            {
                game.BuzzerRace = null;
            }

            return new QuestionTimerTickResult(game, outcome);
        }
    }

    public RuntimeQuestion? ActivateQuestionBuzzer(
        string publicCode,
        int sourceQuestionId)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            var question = game.Session.ActivateQuestionBuzzer(sourceQuestionId);
            game.BuzzerRace = null;
            return question;
        }
    }

    public BuzzerClaimResult? ClaimQuestionBuzzer(
        string connectionId,
        int sourceQuestionId)
    {
        PlayerConnection connection;

        lock (_presenceSync)
        {
            if (!_playerConnections.TryGetValue(
                    connectionId,
                    out var currentConnection) ||
                !currentConnection.IsApproved)
            {
                return null;
            }

            connection = currentConnection;
        }

        lock (connection.Access.Game)
        {
            var game = connection.Access.Game;
            var player = connection.Access.Player;
            var question = game.Session.Board.Questions.SingleOrDefault(
                item => item.SourceQuestionId == sourceQuestionId);

            if (question is null)
            {
                return null;
            }

            var pressedAt = _timeProvider.GetUtcNow();

            if (question.BuzzerStatus == QuestionBuzzerStatus.Open)
            {
                question = game.Session.ClaimQuestionBuzzer(
                    sourceQuestionId,
                    player.Id);
                game.BuzzerRace = new BuzzerRaceSnapshot(
                    sourceQuestionId,
                    pressedAt,
                    player.Id,
                    player.Name,
                    []);

                return new BuzzerClaimResult(game, question, player, true);
            }

            var race = game.BuzzerRace;
            var delay = race is null
                ? TimeSpan.MaxValue
                : pressedAt - race.WinnerPressedAt;

            if (question.BuzzerStatus != QuestionBuzzerStatus.Claimed ||
                race is null ||
                race.SourceQuestionId != sourceQuestionId ||
                player.Id == race.WinnerPlayerId ||
                race.LatePlayers.Any(item => item.PlayerId == player.Id) ||
                delay < TimeSpan.Zero ||
                delay > BuzzerRaceWindow)
            {
                return null;
            }

            var latePlayer = new BuzzerRaceLatePlayer(
                player.Id,
                player.Name,
                checked((int)Math.Round(delay.TotalMilliseconds)));

            game.BuzzerRace = race with
            {
                LatePlayers = [.. race.LatePlayers, latePlayer]
            };

            return new BuzzerClaimResult(game, question, player, false);
        }
    }

    public QuestionAnswerAttempt? JudgeQuestionAnswer(
        string publicCode,
        int sourceQuestionId,
        GamePlayerId playerId,
        bool isCorrect)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            var attempt = game.Session.JudgeQuestionAnswer(
                sourceQuestionId,
                playerId,
                isCorrect);
            game.BuzzerRace = null;
            return attempt;
        }
    }

    public QuestionAnswerAttempt? AddQuestionAnswerHistoryEntry(
        string publicCode,
        int sourceQuestionId,
        GamePlayerId playerId,
        bool isCorrect,
        int value)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.AddQuestionAnswerHistoryEntry(
                sourceQuestionId,
                playerId,
                isCorrect,
                value);
        }
    }

    public QuestionAnswerAttempt? UpdateQuestionAnswerHistoryEntry(
        string publicCode,
        int sourceQuestionId,
        Guid attemptId,
        GamePlayerId playerId,
        bool isCorrect,
        int value)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.UpdateQuestionAnswerHistoryEntry(
                sourceQuestionId,
                attemptId,
                playerId,
                isCorrect,
                value);
        }
    }

    public QuestionAnswerAttempt? RemoveQuestionAnswerHistoryEntry(
        string publicCode,
        int sourceQuestionId,
        Guid attemptId)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.RemoveQuestionAnswerHistoryEntry(
                sourceQuestionId,
                attemptId);
        }
    }

    public RuntimeQuestion? ResolveQuestionWithoutCorrectAnswer(
        string publicCode,
        int sourceQuestionId)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            var question = game.Session.ResolveQuestionWithoutCorrectAnswer(
                sourceQuestionId);
            game.BuzzerRace = null;
            return question;
        }
    }

    public GamePlayer? AdjustPlayerScore(
        string publicCode,
        GamePlayerId playerId,
        int points)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.AdjustPlayerScore(playerId, points);
        }
    }

    public RuntimeQuestion? CloseQuestionAnswer(
        string publicCode,
        int sourceQuestionId)
    {
        var game = Find(publicCode);
        return game?.Session.CloseQuestionAnswer(sourceQuestionId);
    }

    public GamePlayer? SetActivePlayer(
        string publicCode,
        GamePlayerId playerId)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.SetActivePlayer(playerId);
        }
    }

    public GamePlayer? SelectRandomActivePlayer(string publicCode)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (game)
        {
            return game.Session.SelectRandomActivePlayer();
        }
    }

    public PlayerRejoinApproval? ApprovePlayerRejoin(
        string publicCode,
        GamePlayerId playerId)
    {
        var game = Find(publicCode);

        if (game is null)
        {
            return null;
        }

        lock (_presenceSync)
        {
            var connectionIds = _playerConnections
                .Where(item =>
                    item.Value.Access.Game == game &&
                    item.Value.Access.Player.Id == playerId &&
                    !item.Value.IsApproved)
                .Select(item => item.Key)
                .ToArray();

            foreach (var connectionId in connectionIds)
            {
                var connection = _playerConnections[connectionId];
                _playerConnections[connectionId] = connection with { IsApproved = true };
            }

            return new PlayerRejoinApproval(game, connectionIds);
        }
    }

    public IReadOnlyList<GamePlayer> GetPlayers(GameSessionRegistration game)
    {
        ArgumentNullException.ThrowIfNull(game);

        lock (game)
        {
            return game.Session.Players.ToArray();
        }
    }

    public IReadOnlyList<PlayerLobbyEntry> GetPlayerLobbyEntries(GameSessionRegistration game)
    {
        var players = GetPlayers(game);

        lock (_presenceSync)
        {
            return players
                .Select(player => new PlayerLobbyEntry(
                    player.Id,
                    player.Name,
                    player.Score,
                    player.AvatarId,
                    GetPresence(player.Id)))
                .ToArray();
        }
    }

    public static string NormalizeCode(string publicCode)
    {
        return publicCode.Trim().ToUpperInvariant();
    }

    private string CreatePlayerAccess(GameSessionRegistration game, GamePlayer player)
    {
        while (true)
        {
            var accessToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var access = new PlayerAccess(game, player);

            if (_playerAccessByTokenHash.TryAdd(HashToken(accessToken), access))
            {
                return accessToken;
            }
        }
    }

    private FinalPlayerActionResult? ExecuteApprovedPlayerAction(
        string connectionId,
        Func<GameSessionRegistration, GamePlayer, FinalPlayerSubmission> action)
    {
        PlayerConnection connection;

        lock (_presenceSync)
        {
            if (!_playerConnections.TryGetValue(
                    connectionId,
                    out var currentConnection) ||
                !currentConnection.IsApproved)
            {
                return null;
            }

            connection = currentConnection;
        }

        lock (connection.Access.Game)
        {
            var submission = action(
                connection.Access.Game,
                connection.Access.Player);

            return new FinalPlayerActionResult(
                connection.Access.Game,
                connection.Access.Player,
                submission);
        }
    }

    private bool ConsumePlayerTransitionToken(
        string? transitionToken,
        PlayerAccess access)
    {
        if (string.IsNullOrWhiteSpace(transitionToken) ||
            !_playerTransitionAccessByTokenHash.TryRemove(
                HashToken(transitionToken),
                out var transition))
        {
            return false;
        }

        return transition.Access == access &&
            transition.ExpiresAtUtc >= _timeProvider.GetUtcNow();
    }

    private PlayerPresenceStatus GetPresence(GamePlayerId playerId)
    {
        var connections = _playerConnections.Values
            .Where(connection => connection.Access.Player.Id == playerId)
            .ToArray();

        if (connections.Length == 0)
        {
            return PlayerPresenceStatus.Disconnected;
        }

        if (connections.All(connection => !connection.IsApproved))
        {
            return PlayerPresenceStatus.RejoinPending;
        }

        return connections.Any(connection => connection.IsApproved && connection.IsVisible)
            ? PlayerPresenceStatus.Active
            : PlayerPresenceStatus.Inactive;
    }

    private static string HashToken(string accessToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));
    }

    private static void EnsureValidCode(string code)
    {
        if (code.Length != GameCodeGenerator.CodeLength ||
            code.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new InvalidOperationException(
                $"The game code generator returned an invalid code: '{code}'.");
        }
    }

    private sealed record PlayerAccess(
        GameSessionRegistration Game,
        GamePlayer Player);

    private sealed record PlayerConnection(
        PlayerAccess Access,
        bool IsVisible,
        bool IsApproved);

    private sealed record PlayerTransitionAccess(
        PlayerAccess Access,
        DateTimeOffset ExpiresAtUtc);
}


public sealed record BuzzerClaimResult(
    GameSessionRegistration Game,
    RuntimeQuestion Question,
    GamePlayer Player,
    bool IsWinner);


public sealed record QuestionTimerTickResult(
    GameSessionRegistration Game,
    QuestionTimerOutcome Outcome);


public sealed record FinalPlayerActionResult(
    GameSessionRegistration Game,
    GamePlayer Player,
    FinalPlayerSubmission Submission);
