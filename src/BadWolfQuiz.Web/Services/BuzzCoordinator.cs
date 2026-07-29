using System.Collections.Concurrent;

namespace BadWolfQuiz.Web.Services;

public sealed class BuzzCoordinator
{
    private readonly ConcurrentDictionary<int, int> _winners = new();

    public bool TryBuzz(int gameQuestionId, int playerId)
        => _winners.TryAdd(gameQuestionId, playerId);

    public int? GetWinner(int gameQuestionId)
        => _winners.TryGetValue(gameQuestionId, out var playerId) ? playerId : null;

    public void Reset(int gameQuestionId)
        => _winners.TryRemove(gameQuestionId, out _);
}
