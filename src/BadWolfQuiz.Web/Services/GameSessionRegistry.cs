using System.Collections.Concurrent;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionRegistry
{
    private readonly ConcurrentDictionary<GameSessionId, GameSession> _sessions = new();

    public GameSession Create(QuizSnapshot quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        var session = GameSession.Create(quiz);

        if (!_sessions.TryAdd(session.Id, session))
        {
            throw new InvalidOperationException(
                $"A game session with identifier {session.Id} already exists.");
        }

        return session;
    }

    public GameSession? Find(GameSessionId id)
    {
        return _sessions.GetValueOrDefault(id);
    }
}
