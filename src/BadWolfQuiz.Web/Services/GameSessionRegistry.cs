using System.Collections.Concurrent;
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
            if (game.Session.Status != GameSessionStatus.Lobby)
            {
                return PlayerJoinResult.Failed(PlayerJoinStatus.GameAlreadyStarted);
            }

            if (game.Session.Players.Any(player =>
                    string.Equals(player.Name, playerName.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return PlayerJoinResult.Failed(PlayerJoinStatus.NameAlreadyUsed);
            }

            var player = game.Session.AddPlayer(playerName);
            return PlayerJoinResult.Succeeded(game, player);
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

    public static string NormalizeCode(string publicCode)
    {
        return publicCode.Trim().ToUpperInvariant();
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
}
