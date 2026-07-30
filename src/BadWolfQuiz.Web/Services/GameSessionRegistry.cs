using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionRegistry
{
    private const int MaxCodeGenerationAttempts = 20;
    private readonly IGameCodeGenerator _gameCodeGenerator;
    private readonly ConcurrentDictionary<GameSessionId, GameSessionRegistration> _sessionsById = new();
    private readonly ConcurrentDictionary<string, GameSessionRegistration> _sessionsByCode =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PlayerAccess> _playerAccessByTokenHash = new();
    private readonly Dictionary<string, PlayerConnection> _playerConnections = new();
    private readonly object _presenceSync = new();

    public GameSessionRegistry(IGameCodeGenerator gameCodeGenerator)
    {
        _gameCodeGenerator = gameCodeGenerator;
    }

    public GameSessionRegistration Create(QuizSnapshot quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        var session = GameSession.Create(quiz);

        for (var attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
        {
            var code = NormalizeCode(_gameCodeGenerator.Create());
            EnsureValidCode(code);

            var registration = new GameSessionRegistration(code, session);

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

    public GameSessionRegistration? Find(string publicCode)
    {
        if (string.IsNullOrWhiteSpace(publicCode))
        {
            return null;
        }

        return _sessionsByCode.GetValueOrDefault(NormalizeCode(publicCode));
    }

    public PlayerJoinResult JoinPlayer(string publicCode, string playerName)
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

            var player = game.Session.AddPlayer(playerName);
            var accessToken = CreatePlayerAccess(game, player);
            return PlayerJoinResult.Succeeded(game, player, accessToken);
        }
    }

    public PlayerConnectionResult? ConnectPlayer(
        string publicCode,
        string accessToken,
        string connectionId,
        bool isVisible)
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

        bool requiresApproval;

        lock (_presenceSync)
        {
            requiresApproval = access.Game.Session.Status != GameSessionStatus.Lobby;
            _playerConnections[connectionId] = new PlayerConnection(
                access,
                isVisible,
                !requiresApproval);
        }

        return new PlayerConnectionResult(access.Game, access.Player, requiresApproval);
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
            return game.Session.JudgeQuestionAnswer(
                sourceQuestionId,
                playerId,
                isCorrect);
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
            return game.Session.ResolveQuestionWithoutCorrectAnswer(
                sourceQuestionId);
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
}
