using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionRegistration
{
    private long _persistenceRevision;

    public GameSessionRegistration(
        string publicCode,
        GameSession session,
        string? hostId = null,
        bool isRecovered = false)
    {
        PublicCode = publicCode;
        Session = session;
        HostId = hostId;
        IsRecovered = isRecovered;
        RecoveredPlayerIdsAwaitingReconnect = isRecovered
            ? session.Players.Select(player => player.Id).ToHashSet()
            : [];
    }

    public string PublicCode { get; }

    public GameSession Session { get; }
    public string? HostId { get; }

    public string ClientInstanceId { get; } = Guid.NewGuid().ToString("N");

    public bool IsRecovered { get; }

    internal HashSet<GamePlayerId> RecoveredPlayerIdsAwaitingReconnect { get; }

    internal HashSet<GamePlayerId> UnblockedPlayerIdsAwaitingReconnect { get; } = [];

    public bool AllowsNewPlayers { get; internal set; } = true;

    public long PersistenceRevision => Interlocked.Read(ref _persistenceRevision);

    internal void MarkPersistenceChanged() =>
        Interlocked.Increment(ref _persistenceRevision);

    public BuzzerRaceSnapshot? BuzzerRace { get; internal set; }
}

public sealed record BuzzerRaceSnapshot(
    int SourceQuestionId,
    DateTimeOffset WinnerPressedAt,
    GamePlayerId WinnerPlayerId,
    string WinnerPlayerName,
    IReadOnlyList<BuzzerRaceLatePlayer> LatePlayers);

public sealed record BuzzerRaceLatePlayer(
    GamePlayerId PlayerId,
    string PlayerName,
    int DelayMilliseconds);
